using MessageProtocol.CodeGenerator.Generate;
using MessageProtocol.CodeGenerator.Metadata;
using MessageProtocol.CodeGenerator.Reference;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace MessageProtocol.CodeGenerator
{
    /// <summary>
    /// 메시지 속성이 붙은 partial 타입을 찾아 Serialize/Deserialize/MessageId 와
    /// ModuleInitializer 자동 등록 코드를 생성하는 incremental source generator.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public class MessageCodeGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var standalone = CreateAttributeProvider(context, MetadataNames.StandaloneMessageAttribute);
            var groupRoot = CreateAttributeProvider(context, MetadataNames.GroupRootMessageAttribute);
            var groupElement = CreateAttributeProvider(context, MetadataNames.GroupElementMessageAttribute);
            var nonId = CreateAttributeProvider(context, MetadataNames.NonIdMessageAttribute);
            var generic = CreateAttributeProvider(context, MetadataNames.GenericMessageAttribute);

            var candidates = standalone.Collect()
                .Combine(groupRoot.Collect())
                .Combine(groupElement.Collect())
                .Combine(nonId.Collect())
                .Combine(generic.Collect())
                .Select(static (sources, _) =>
                {
                    var ((((standaloneTypes, groupRootTypes), groupElementTypes), nonIdTypes), genericTypes) = sources;
                    return standaloneTypes
                        .Concat(groupRootTypes)
                        .Concat(groupElementTypes)
                        .Concat(nonIdTypes)
                        .Concat(genericTypes)
                        .Distinct(NamedTypeSymbolComparer.Instance)
                        .ToImmutableArray();
                });

            var compilationAndCandidates = context.CompilationProvider.Combine(candidates);

            context.RegisterSourceOutput(compilationAndCandidates, static (spc, source) =>
            {
                var (compilation, types) = source;
                var attributeReferences = new AttributeReferences(compilation);
                foreach (var typeSymbol in types)
                {
                    Generate(typeSymbol, compilation, spc, attributeReferences);
                }
            });
        }

        static IncrementalValuesProvider<INamedTypeSymbol> CreateAttributeProvider(
            IncrementalGeneratorInitializationContext context,
            string metadataName)
        {
            // 속성별 SyntaxProvider + Collect: 타입 선언만 걸러 증분 파이프라인을 유지한다.
            return context.SyntaxProvider.ForAttributeWithMetadataName(
                metadataName,
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol);
        }

        internal static void Generate(
            INamedTypeSymbol typeSymbol,
            Compilation compilation,
            SourceProductionContext context,
            AttributeReferences? cachedReferences = null)
        {
            var location = typeSymbol.Locations.FirstOrDefault() ?? Location.None;
            var attributeReferences = cachedReferences ?? new AttributeReferences(compilation);

            if (!IsPartial(typeSymbol))
            {
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.MustBePartial, location, typeSymbol.Name));
                return;
            }

            if (typeSymbol.ContainingType != null && !IsNestedContainingTypesPartial(typeSymbol))
            {
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.NestedContainingTypesMustBePartial, location, typeSymbol.Name));
                return;
            }

            if (!TypeMetadataValidator.TryValidateMessageIdRange(typeSymbol, attributeReferences, out string invalidAttributeName, out string invalidAttributeValue))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MessageAttributeValueOutOfRange,
                    location,
                    typeSymbol.Name,
                    invalidAttributeName,
                    invalidAttributeValue));
                return;
            }

            var typeMeta = new TypeMetadata(typeSymbol, attributeReferences);

            if (!TryValidateGenericConstructions(typeMeta, context, location))
            {
                return;
            }

            if (TryReportDuplicateMessageAttributes(typeMeta, context, location))
            {
                return;
            }

            if (!ValidateRootHierarchy(typeSymbol, typeMeta, attributeReferences, context, location))
            {
                return;
            }

            // abstract 그룹 루트는 상속 전용이라 코드를 생성하지 않는다.
            if (typeMeta.IsGroupRootMessage && typeSymbol.IsAbstract)
            {
                return;
            }

            bool hasCollectionsMarshal = compilation.GetTypeByMetadataName("System.Runtime.InteropServices.CollectionsMarshal") != null;
            if (!MessageSerializeCodeEmitter.TryEmit(typeMeta, attributeReferences, hasCollectionsMarshal, out string? serializeCode, out var unsupportedMembers))
            {
                foreach (var unsupported in unsupportedMembers)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.UnsupportedMemberType,
                        unsupported.Location,
                        unsupported.TypeName,
                        unsupported.MemberOrTypeName));
                }
                return;
            }

            context.AddSource($"{GetGeneratedFileName(typeMeta.Symbol)}.g.cs", SourceText.From(serializeCode!, Encoding.UTF8));
        }

        static bool HasMessageAttribute(INamedTypeSymbol typeSymbol, AttributeReferences attributeReferences)
        {
            return typeSymbol.ContainAttribute(attributeReferences.NonIdMessageAttributeType)
                || typeSymbol.ContainAttribute(attributeReferences.StandaloneMessageAttributeType)
                || typeSymbol.ContainAttribute(attributeReferences.GroupRootMessageAttributeType)
                || typeSymbol.ContainAttribute(attributeReferences.GroupElementMessageAttributeType);
        }

        static bool IsPartial(INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.DeclaringSyntaxReferences
                .Select(static reference => reference.GetSyntax())
                .Any(static syntax => syntax is TypeDeclarationSyntax declarationSyntax
                    && declarationSyntax.Modifiers.Any(static modifier => modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)));
        }

        static bool IsNestedContainingTypesPartial(INamedTypeSymbol typeSymbol)
        {
            var containingType = typeSymbol.ContainingType;
            while (containingType != null)
            {
                if (!IsPartial(containingType))
                {
                    return false;
                }

                containingType = containingType.ContainingType;
            }

            return true;
        }

        static string GetGeneratedFileName(INamedTypeSymbol typeSymbol)
        {
            // 힌트 이름은 네임스페이스 + 중첩 + 제네릭 차수를 포함해 유일해야 한다.
            // 단순 이름만 쓰면 다른 네임스페이스의 동명 타입이 충돌해 AddSource 가 예외를 던지고,
            // 해당 컴파일의 전체 생성 소스가 유실된다.
            var typeNames = new Stack<string>();
            for (var current = typeSymbol; current != null; current = current.ContainingType)
            {
                typeNames.Push(current.MetadataName);
            }

            string typeName = string.Join("+", typeNames); // 중첩 구분자: 네임스페이스 점과 혼동 방지(메타데이터 관례 +)
            string prefix = typeSymbol.ContainingNamespace == null || typeSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : typeSymbol.ContainingNamespace.ToDisplayString() + ".";

            return SanitizeHintName(prefix + typeName);
        }

        static string SanitizeHintName(string name)
        {
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                bool allowed = char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '-' || c == '(' || c == ')' || c == '`';
                sb.Append(allowed ? c : '_');
            }
            return sb.ToString();
        }

        /// <summary>
        /// 한 타입에 메시지 속성이 2개 이상이면 MSGPROT007 경고 후 생성을 건너뛴다.
        /// 중복 속성은 헤더 플래그를 OR 로 합쳐 런타임 등록·디스패치와 어긋나기 때문이다.
        /// </summary>
        static bool TryReportDuplicateMessageAttributes(TypeMetadata typeMeta, SourceProductionContext context, Location location)
        {
            var names = new List<string>(4);
            if (typeMeta.IsNonIdMessage) names.Add("NonIdMessage");
            if (typeMeta.IsStandaloneMessage) names.Add("StandaloneMessage");
            if (typeMeta.IsGroupRootMessage) names.Add("GroupRootMessage");
            if (typeMeta.IsGroupElementMessage) names.Add("GroupElementMessage");

            if (names.Count < 2)
            {
                return false;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DuplicateMessageAttributes,
                location,
                typeMeta.Symbol.Name,
                string.Join(", ", names)));
            return true;
        }

        /// <summary>
        /// GenericMessage 구성 선언 검증 (MSGPROT008/009).
        /// </summary>
        static bool TryValidateGenericConstructions(TypeMetadata typeMeta, SourceProductionContext context, Location location)
        {
            if (typeMeta.GenericConstructions.Length == 0)
            {
                return true;
            }

            if (!typeMeta.Symbol.IsGenericType)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.InvalidGenericMessageDeclaration,
                    location,
                    typeMeta.Symbol.Name,
                    "the type is not generic"));
                return false;
            }

            if (!typeMeta.IsStandaloneMessage)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.GenericMessageRequiresStandalone,
                    location,
                    typeMeta.Symbol.Name));
                return false;
            }

            var seenClassIds = new HashSet<uint>();
            foreach (var construction in typeMeta.GenericConstructions)
            {
                if (construction.ClassId == 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.InvalidGenericMessageDeclaration,
                        location,
                        typeMeta.Symbol.Name,
                        "a construction is missing 'ClassId' (must be 1 .. 16777215)"));
                    return false;
                }

                if (!seenClassIds.Add(construction.ClassId))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.InvalidGenericMessageDeclaration,
                        location,
                        typeMeta.Symbol.Name,
                        $"ClassId {construction.ClassId} is declared more than once"));
                    return false;
                }

                if (construction.TypeArguments.Length != typeMeta.Symbol.TypeParameters.Length)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.InvalidGenericMessageDeclaration,
                        location,
                        typeMeta.Symbol.Name,
                        $"construction declares {construction.TypeArguments.Length} type argument(s) but the type has {typeMeta.Symbol.TypeParameters.Length} type parameter(s)"));
                    return false;
                }
            }

            return true;
        }

        static bool ValidateRootHierarchy(INamedTypeSymbol typeSymbol, TypeMetadata typeMeta, AttributeReferences attributeReferences)
        {
            // 요소 메시지는 상속 계층에 루트가 있어야 한다.
            if (typeMeta.IsGroupElementMessage)
            {
                bool hasRoot = false;
                var current = typeMeta;
                while (current != null)
                {
                    if (current.IsGroupRootMessage)
                    {
                        hasRoot = true;
                        break;
                    }
                    current = current.BaseTypeMetadata;
                }

                if (!hasRoot)
                {
                    return false;
                }
            }

            // 루트 메시지의 조상이 루트일 수 없다.
            if (typeMeta.IsGroupRootMessage)
            {
                var baseType = typeSymbol.BaseType;
                while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
                {
                    if (baseType.FindAttribute(attributeReferences.GroupRootMessageAttributeType) != null)
                    {
                        return false;
                    }
                    baseType = baseType.BaseType;
                }
            }

            return true;
        }

        static bool ValidateRootHierarchy(
            INamedTypeSymbol typeSymbol,
            TypeMetadata typeMeta,
            AttributeReferences attributeReferences,
            SourceProductionContext context,
            Location location)
        {
            if (ValidateRootHierarchy(typeSymbol, typeMeta, attributeReferences))
            {
                return true;
            }

            if (typeMeta.IsGroupElementMessage)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ElementMessageMustHaveRoot,
                    location,
                    typeSymbol.Name));
                return false;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.RootMessageCannotHaveRootParent,
                location,
                typeSymbol.Name));
            return false;
        }

        sealed class NamedTypeSymbolComparer : IEqualityComparer<INamedTypeSymbol>
        {
            public static readonly NamedTypeSymbolComparer Instance = new();

            public bool Equals(INamedTypeSymbol? x, INamedTypeSymbol? y)
            {
                return SymbolEqualityComparer.Default.Equals(x, y);
            }

            public int GetHashCode(INamedTypeSymbol obj)
            {
                return SymbolEqualityComparer.Default.GetHashCode(obj);
            }
        }
    }
}

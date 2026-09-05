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
                // 컴파일 전체 구성 선언을 먼저 훑어 중복(모듈 로드 크래시 원인)을 컴파일 진단으로 승격한다.
                var conflicts = CollectConstructionConflicts(types, attributeReferences);
                // 캐리어 등록 클래스 이름 유일성 상태 — 동일 접미사 충돌 시 구분자 부여.
                var usedCarrierSuffixes = new HashSet<string>();
                foreach (var typeSymbol in types)
                {
                    Generate(typeSymbol, compilation, spc, conflicts, usedCarrierSuffixes, attributeReferences);
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
            ConstructionConflicts conflicts,
            HashSet<string> usedCarrierSuffixes,
            AttributeReferences? cachedReferences = null)
        {
            var location = typeSymbol.Locations.FirstOrDefault() ?? Location.None;
            var attributeReferences = cachedReferences ?? new AttributeReferences(compilation);

            // 구성 선언 처리: [GenericMessage(typeof(구성), ClassId)] 가 붙은 타입은 선언부·캐리어 구분 없이 등록 클래스를 출력한다.
            var constructionEntries = ParseConstructionEntries(typeSymbol, attributeReferences);
            if (constructionEntries.Count > 0)
            {
                if (ValidateConstructionEntries(typeSymbol, constructionEntries, attributeReferences, conflicts, context, location))
                {
                    EmitConstructionRegistration(typeSymbol, constructionEntries, context, usedCarrierSuffixes);
                }
            }

            // 메시지 속성이 없는 순수 캐리어는 여기서 끝.
            if (!HasMessageAttribute(typeSymbol, attributeReferences))
            {
                return;
            }

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

            // 메시지 타입은 매개변수 없는 생성자로 인스턴스를 만들 수 있어야 한다 (추상 클래스·포지셔널 레코드 거부).
            if (!IsConstructibleMessageType(typeSymbol))
            {
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UnconstructibleMessageType, location, typeSymbol.Name));
                return;
            }

            bool hasCollectionsMarshal = compilation.GetTypeByMetadataName("System.Runtime.InteropServices.CollectionsMarshal") != null;
            if (!MessageSerializeCodeEmitter.TryEmit(typeMeta, attributeReferences, hasCollectionsMarshal, out string? serializeCode, out var unsupportedMembers))
            {
                foreach (var unsupported in unsupportedMembers)
                {
                    var descriptor = unsupported.Kind == UnsupportedMemberKind.NotAssignable
                        ? DiagnosticDescriptors.NotAssignableMember
                        : DiagnosticDescriptors.UnsupportedMemberType;
                    context.ReportDiagnostic(Diagnostic.Create(
                        descriptor,
                        unsupported.Location,
                        unsupported.TypeName,
                        unsupported.MemberOrTypeName));
                }
                return;
            }

            context.AddSource($"{GetGeneratedFileName(typeMeta.Symbol)}.g.cs", SourceText.From(serializeCode!, Encoding.UTF8));
        }

        /// <summary>매개변수 없는 생성자로 만들 수 있는 구체 타입인지 확인한다. 생성 partial 은 타입 내부라 비공개 생성자도 호출 가능하다.</summary>
        static bool IsConstructibleMessageType(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol.IsAbstract)
            {
                return false;
            }

            if (typeSymbol.TypeKind == TypeKind.Struct)
            {
                return true;
            }

            return typeSymbol.InstanceConstructors.Any(constructor => constructor.Parameters.Length == 0);
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
        /// 타입에 붙은 [GenericMessage(typeof(구성), ClassId)] 선언을 파싱한다. 잘못된 속성 인수는 Construction 이 null.
        /// </summary>
        static List<(INamedTypeSymbol? Construction, uint ClassId)> ParseConstructionEntries(
            INamedTypeSymbol typeSymbol,
            AttributeReferences attributeReferences)
        {
            var entries = new List<(INamedTypeSymbol?, uint)>();
            if (attributeReferences.GenericMessageAttributeType == null)
            {
                return entries;
            }

            foreach (var attribute in typeSymbol.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeReferences.GenericMessageAttributeType))
                {
                    continue;
                }

                INamedTypeSymbol? construction = attribute.ConstructorArguments.Length > 0
                    && attribute.ConstructorArguments[0].Kind == TypedConstantKind.Type
                    && attribute.ConstructorArguments[0].Value is INamedTypeSymbol named
                        ? named
                        : null;

                uint classId = 0;
                foreach (var namedArgument in attribute.NamedArguments)
                {
                    if (namedArgument.Key == "ClassId"
                        && namedArgument.Value.Kind == TypedConstantKind.Primitive
                        && namedArgument.Value.Value is uint parsed)
                    {
                        classId = parsed;
                    }
                }

                entries.Add((construction, classId));
            }

            return entries;
        }

        /// <summary>
        /// 컴파일 전체에서 같은 구성 중복 선언·(선언, ClassId) 충돌을 찾아낸다.
        /// 방치하면 모듈 로드 시 등록 충돌로 크래시하므로 컴파일 진단으로 승격한다.
        /// </summary>
        static ConstructionConflicts CollectConstructionConflicts(
            ImmutableArray<INamedTypeSymbol> types,
            AttributeReferences attributeReferences)
        {
            var constructionCounts = new Dictionary<INamedTypeSymbol, int>(SymbolEqualityComparer.Default);
            var idCounts = new Dictionary<(INamedTypeSymbol Declaration, uint ClassId), int>(DeclarationClassIdComparer.Instance);

            foreach (var type in types)
            {
                foreach (var (construction, classId) in ParseConstructionEntries(type, attributeReferences))
                {
                    if (construction == null)
                    {
                        continue;
                    }

                    constructionCounts[construction] = constructionCounts.TryGetValue(construction, out int c) ? c + 1 : 1;
                    var idKey = (construction.OriginalDefinition, classId);
                    idCounts[idKey] = idCounts.TryGetValue(idKey, out int n) ? n + 1 : 1;
                }
            }

            var conflicts = new ConstructionConflicts();
            foreach (var pair in constructionCounts)
            {
                if (pair.Value > 1)
                {
                    conflicts.DuplicateConstructions.Add(pair.Key);
                }
            }
            foreach (var pair in idCounts)
            {
                if (pair.Value > 1)
                {
                    conflicts.CollidedIds.Add(pair.Key);
                }
            }
            return conflicts;
        }

        static bool ValidateConstructionEntries(
            INamedTypeSymbol host,
            List<(INamedTypeSymbol? Construction, uint ClassId)> entries,
            AttributeReferences attributeReferences,
            ConstructionConflicts conflicts,
            SourceProductionContext context,
            Location location)
        {
            var seenClassIds = new HashSet<uint>();
            var seenConstructions = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var (construction, classId) in entries)
            {
                if (construction == null)
                {
                    ReportInvalidConstruction(context, location, host, "GenericMessage requires a closed generic construction type");
                    return false;
                }

                if (classId == 0)
                {
                    ReportInvalidConstruction(context, location, host, $"construction '{construction.ToDisplayString()}' is missing 'ClassId' (must be 1 .. {TypeMetadata.MaxMessageAttributeValue})");
                    return false;
                }

                // ClassId 는 MessageId 와 같은 24비트 와이어 슬롯(`GenericIdHeaderSize` 의 뒤 3바이트)에 담긴다.
                // 상한 초과는 와이어에 잘리므로 런타임 등록이 거부하는데, 그 등록은 **모듈 이니셜라이저** 안에서 돌아
                // ArgumentOutOfRangeException 이 TypeInitializationException(어셈블리 로드 실패)으로 번진다.
                // 런타임 크래시 대신 컴파일 진단으로 승격한다 (Known-Issues KI-27).
                if (classId > TypeMetadata.MaxMessageAttributeValue)
                {
                    ReportInvalidConstruction(context, location, host, $"ClassId {classId} is out of range for construction '{construction.ToDisplayString()}' (must be 1 .. {TypeMetadata.MaxMessageAttributeValue})");
                    return false;
                }

                if (!seenClassIds.Add(classId))
                {
                    ReportInvalidConstruction(context, location, host, $"ClassId {classId} is declared more than once");
                    return false;
                }

                if (!seenConstructions.Add(construction))
                {
                    ReportInvalidConstruction(context, location, host, $"construction '{construction.ToDisplayString()}' is declared more than once");
                    return false;
                }

                if (construction.IsUnboundGenericType)
                {
                    ReportInvalidConstruction(context, location, host, $"'{construction.ToDisplayString()}' is an unbound generic type; declare a closed construction like typeof({construction.Name}<...>)");
                    return false;
                }

                var declaration = construction.OriginalDefinition;
                if (!construction.IsGenericType
                    || !declaration.IsGenericType
                    || !declaration.ContainAttribute(attributeReferences.StandaloneMessageAttributeType))
                {
                    ReportInvalidConstruction(context, location, host, $"'{construction.ToDisplayString()}' is not a construction of a generic message declaration ('[StandaloneMessage]' required)");
                    return false;
                }

                if (conflicts.IsConflicting(construction, classId))
                {
                    ReportInvalidConstruction(context, location, host, $"construction '{construction.ToDisplayString()}' (or its ClassId) is declared more than once in this compilation");
                    return false;
                }
            }

            return true;
        }

        static void ReportInvalidConstruction(
            SourceProductionContext context,
            Location location,
            INamedTypeSymbol host,
            string reason)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InvalidGenericMessageDeclaration,
                location,
                host.Name,
                reason));
        }

        static void EmitConstructionRegistration(
            INamedTypeSymbol host,
            List<(INamedTypeSymbol? Construction, uint ClassId)> entries,
            SourceProductionContext context,
            HashSet<string> usedCarrierSuffixes)
        {
            string suffix = SymbolNaming.MakeUniqueSuffix(host, usedCarrierSuffixes);
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine("using MessageProtocol.Serialize;");
            sb.AppendLine();
            sb.AppendLine($"internal static class __GenericConstructionRegistration_{suffix}");
            sb.AppendLine("{");
            sb.AppendLine("    [ModuleInitializer]");
            sb.AppendLine("    internal static void Initialize()");
            sb.AppendLine("    {");
            foreach (var (construction, classId) in entries)
            {
                string constructionName = construction!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                sb.AppendLine($"        MessageSerializer.RegisterGenericConstruction<{constructionName}>({classId});");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");

            context.AddSource(
                SanitizeHintName($"__GenericConstructionRegistration_{suffix}"),
                SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        /// <summary>컴파일 전체 구성 선언 중복 상태.</summary>
        internal sealed class ConstructionConflicts
        {
            public HashSet<INamedTypeSymbol> DuplicateConstructions { get; } = new(SymbolEqualityComparer.Default);
            public HashSet<(INamedTypeSymbol Declaration, uint ClassId)> CollidedIds { get; } = new(DeclarationClassIdComparer.Instance);

            public bool IsConflicting(INamedTypeSymbol construction, uint classId)
            {
                return DuplicateConstructions.Contains(construction)
                    || CollidedIds.Contains((construction.OriginalDefinition, classId));
            }
        }

        sealed class DeclarationClassIdComparer : IEqualityComparer<(INamedTypeSymbol Declaration, uint ClassId)>
        {
            public static readonly DeclarationClassIdComparer Instance = new();

            public bool Equals((INamedTypeSymbol Declaration, uint ClassId) x, (INamedTypeSymbol Declaration, uint ClassId) y)
            {
                return SymbolEqualityComparer.Default.Equals(x.Declaration, y.Declaration) && x.ClassId == y.ClassId;
            }

            public int GetHashCode((INamedTypeSymbol Declaration, uint ClassId) obj)
            {
                unchecked
                {
                    return (SymbolEqualityComparer.Default.GetHashCode(obj.Declaration) * 397) ^ (int)obj.ClassId;
                }
            }
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

using MessageProtocol.CodeGenerator.Reference;
using MessageProtocol.CodeGenerator.Metadata;
using MessageProtocol.CodeGenerator.Generate;
using MessageProtocol.CodeGenerator;
using Microsoft.CodeAnalysis;
using System.Linq;
using System;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using System.Collections.Generic;
using System.IO;

namespace MessageProtocol.CodeGenerator.Generate
{
    [Generator(LanguageNames.CSharp)]
    public class MessageCodeGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(
                context.CompilationProvider,
                static (spc, compilation) =>
                {
                    GenerateFromCompilation(compilation, spc);
                });
        }

        static void GenerateFromCompilation(Compilation compilation, SourceProductionContext context)
        {
            var attributeReferences = new AttributeReferences(compilation);
            var targetSymbols = EnumerateAllNamedTypes(compilation.Assembly.GlobalNamespace)
                .Where(typeSymbol => HasMessageAttribute(typeSymbol, attributeReferences))
                .Distinct(NamedTypeSymbolComparer.Instance);

            foreach (var typeSymbol in targetSymbols)
            {
                Generate(typeSymbol, compilation, context, attributeReferences);
            }
        }

        internal static void Generate(INamedTypeSymbol typeSymbol, Compilation compilation, SourceProductionContext context, AttributeReferences? cachedReferences = null)
        {
            var location = typeSymbol.Locations.FirstOrDefault() ?? Location.None;
            var attributeReferences = cachedReferences ?? new AttributeReferences(compilation);

            // verify is partial
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

            // Root 계층 구조 검증
            if (!ValidateRootHierarchy(typeSymbol, typeMeta, attributeReferences, context, location))
            {
                return;
            }

            // Root 메시지가 abstract이면 코드 생성을 건너뛰기 (abstract Root는 상속용이므로)
            if (typeMeta.IsGroupRootMessage && typeSymbol.IsAbstract)
            {
                return;
            }

            // Step2 : Generate Code
            string serializeCode = MessageSerializeCodeEmitter.Emit(typeMeta, attributeReferences);

            // Step3 : Debug - Save generated code to file
#pragma warning disable RS1035 // Do not use APIs banned for analyzers
            try
            {
                var generatedFileName = GetGeneratedFileName(typeMeta.Symbol);
                var debugFilePath = Path.Combine("C:\\Debug\\", $"{generatedFileName}.g.debug.cs");
                if(Directory.Exists(@"C:\Debug"))
                    File.WriteAllText(debugFilePath, serializeCode);
            }
            catch
            {
                // 디버그 파일 생성 실패는 무시
            }
#pragma warning restore RS1035

            // Step4 : Output Code
            context.AddSource($"{GetGeneratedFileName(typeMeta.Symbol)}.g.cs", SourceText.From(serializeCode, Encoding.UTF8));
        }

        public static bool TryGenerateMessageSource(
            INamedTypeSymbol typeSymbol,
            Compilation compilation,
            out string? serializeCode)
        {
            return TryGenerateMessageSource(typeSymbol, compilation, out serializeCode, out _);
        }

        public static bool TryGenerateMessageSource(
            INamedTypeSymbol typeSymbol,
            Compilation compilation,
            out string? serializeCode,
            out string? error)
        {
            serializeCode = null;
            error = null;

            if (!IsPartial(typeSymbol))
            {
                error = $"Type '{typeSymbol.ToDisplayString()}' must be partial.";
                return false;
            }

            if (typeSymbol.ContainingType != null && !IsNestedContainingTypesPartial(typeSymbol))
            {
                error = $"All containing types of '{typeSymbol.ToDisplayString()}' must be partial.";
                return false;
            }

            var attributeReferences = new AttributeReferences(compilation);
            if (!HasMessageAttribute(typeSymbol, attributeReferences))
            {
                error = $"Type '{typeSymbol.ToDisplayString()}' has no message attribute.";
                return false;
            }

            if (!TypeMetadataValidator.TryValidateMessageIdRange(
                    typeSymbol,
                    attributeReferences,
                    out string invalidAttributeName,
                    out string invalidAttributeValue))
            {
                error = $"Message attribute value is out of range. Attribute='{invalidAttributeName}', Value='{invalidAttributeValue}'.";
                return false;
            }

            var typeMeta = new TypeMetadata(typeSymbol, attributeReferences);
            if (!ValidateRootHierarchy(typeSymbol, typeMeta, attributeReferences))
            {
                error = $"Type '{typeSymbol.ToDisplayString()}' has invalid root/group hierarchy.";
                return false;
            }

            if (typeMeta.IsGroupRootMessage && typeSymbol.IsAbstract)
            {
                error = $"Abstract group root type '{typeSymbol.ToDisplayString()}' does not emit serialization source.";
                return false;
            }

            serializeCode = MessageSerializeCodeEmitter.Emit(typeMeta, attributeReferences);
            if (string.IsNullOrWhiteSpace(serializeCode))
            {
                error = $"Generated source is empty for type '{typeSymbol.ToDisplayString()}'.";
                return false;
            }
            
#pragma warning disable RS1035 // Do not use APIs banned for analyzers
            try
            {
                var generatedFileName = GetGeneratedFileName(typeMeta.Symbol);
                var debugFilePath = Path.Combine("C:\\Debug\\", $"{generatedFileName}.g.debug.cs");
                if(Directory.Exists(@"C:\Debug"))
                    File.WriteAllText(debugFilePath, serializeCode);
            }
            catch
            {
                // 디버그 파일 생성 실패는 무시
            }
#pragma warning restore RS1035
            
            return true;
        }

        static bool HasMessageAttribute(INamedTypeSymbol typeSymbol, AttributeReferences attributeReferences)
        {
            return typeSymbol.ContainAttribute(attributeReferences.NonIdMessageAttributeType)
                || typeSymbol.ContainAttribute(attributeReferences.StandaloneMessageAttributeType)
                || typeSymbol.ContainAttribute(attributeReferences.GroupRootMessageAttributeType)
                || typeSymbol.ContainAttribute(attributeReferences.GroupElementMessageAttributeType);
        }

        static IEnumerable<INamedTypeSymbol> EnumerateAllNamedTypes(INamespaceSymbol namespaceSymbol)
        {
            foreach (var member in namespaceSymbol.GetMembers())
            {
                if (member is INamespaceSymbol childNamespace)
                {
                    foreach (var nestedType in EnumerateAllNamedTypes(childNamespace))
                    {
                        yield return nestedType;
                    }
                }
                else if (member is INamedTypeSymbol namedType)
                {
                    foreach (var nestedType in EnumerateTypeAndNested(namedType))
                    {
                        yield return nestedType;
                    }
                }
            }
        }

        static IEnumerable<INamedTypeSymbol> EnumerateTypeAndNested(INamedTypeSymbol typeSymbol)
        {
            yield return typeSymbol;
            foreach (var nestedType in typeSymbol.GetTypeMembers())
            {
                foreach (var deepNested in EnumerateTypeAndNested(nestedType))
                {
                    yield return deepNested;
                }
            }
        }

        static bool IsPartial(INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.DeclaringSyntaxReferences
                .Select(static reference => reference.GetSyntax())
                .Any(static syntax => syntax is Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax declarationSyntax
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
            if (typeSymbol.ContainingType == null)
            {
                return typeSymbol.Name;
            }

            var typeNames = new System.Collections.Generic.Stack<string>();
            for (var current = typeSymbol; current != null; current = current.ContainingType)
            {
                typeNames.Push(current.Name);
            }

            return string.Join("_", typeNames);
        }

        static bool ValidateRootHierarchy(INamedTypeSymbol typeSymbol, TypeMetadata typeMeta, AttributeReferences attributeReferences)
        {
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

            if (typeMeta.IsGroupRootMessage)
            {
                var baseType = typeSymbol.BaseType;
                while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
                {
                    var parentRootAttribute = baseType.FindAttribute(attributeReferences.GroupRootMessageAttributeType);
                    if (parentRootAttribute != null)
                    {
                        return false;
                    }
                    baseType = baseType.BaseType;
                }
            }

            return true;
        }

        static bool ValidateRootHierarchy(INamedTypeSymbol typeSymbol, TypeMetadata typeMeta, AttributeReferences attributeReferences, SourceProductionContext context, Location location)
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

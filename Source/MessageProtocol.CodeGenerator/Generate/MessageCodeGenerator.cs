using MessageProtocol.CodeGenerator.Reference;
using MessageProtocol.CodeGenerator.Metadata;
using MessageProtocol.CodeGenerator.Generate;
using MessageProtocol.CodeGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System;
using System.IO;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace MessageProtocol.CodeGenerator.Generate
{
    [Generator(LanguageNames.CSharp)]
    public class MessageCodeGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var classesWithMessage = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    fullyQualifiedMetadataName: "MessageProtocol.MessageAttribute",
                    predicate: static (node, token) =>
                    {
                        return (node is ClassDeclarationSyntax
                                     or StructDeclarationSyntax
                                     or RecordDeclarationSyntax
                                     or InterfaceDeclarationSyntax);
                    },
                    transform: static (context, token) =>
                    {
                        return (TypeDeclarationSyntax)context.TargetNode;
                    }).Where(static result => result != null);

            var classesWithMessageGroupRoot = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    fullyQualifiedMetadataName: "MessageProtocol.MessageGroupRootAttribute",
                    predicate: static (node, token) =>
                    {
                        return (node is ClassDeclarationSyntax
                                     or StructDeclarationSyntax
                                     or RecordDeclarationSyntax
                                     or InterfaceDeclarationSyntax);
                    },
                    transform: static (context, token) =>
                    {
                        return (TypeDeclarationSyntax)context.TargetNode;
                    }).Where(static result => result != null);

            var classesWithMessageGroupElement = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    fullyQualifiedMetadataName: "MessageProtocol.MessageGroupElementAttribute",
                    predicate: static (node, token) =>
                    {
                        return (node is ClassDeclarationSyntax
                                     or StructDeclarationSyntax
                                     or RecordDeclarationSyntax
                                     or InterfaceDeclarationSyntax);
                    },
                    transform: static (context, token) =>
                    {
                        return (TypeDeclarationSyntax)context.TargetNode;
                    }).Where(static result => result != null);

            var classesWithMessageStandalone = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    fullyQualifiedMetadataName: "MessageProtocol.MessageStandaloneAttribute",
                    predicate: static (node, token) =>
                    {
                        return (node is ClassDeclarationSyntax
                                     or StructDeclarationSyntax
                                     or RecordDeclarationSyntax
                                     or InterfaceDeclarationSyntax);
                    },
                    transform: static (context, token) =>
                    {
                        return (TypeDeclarationSyntax)context.TargetNode;
                    }).Where(static result => result != null);

            var compilation = context.CompilationProvider;

            {
                var source = classesWithMessage.Combine(compilation);
                context.RegisterSourceOutput(
                    source,
                    static (context, source) =>
                    {
                        var (typeDeclaration, compilation) = source;
                        Generate(typeDeclaration, compilation, context);
                    });
            }

            {
                var source = classesWithMessageGroupRoot.Combine(compilation);
                context.RegisterSourceOutput(
                    source,
                    static (context, source) =>
                    {
                        var (typeDeclaration, compilation) = source;
                        Generate(typeDeclaration, compilation, context);
                    });
            }

            {
                var source = classesWithMessageGroupElement.Combine(compilation);
                context.RegisterSourceOutput(
                    source,
                    static (context, source) =>
                    {
                        var (typeDeclaration, compilation) = source;
                        Generate(typeDeclaration, compilation, context);
                    });
            }

            {
                var source = classesWithMessageStandalone.Combine(compilation);
                context.RegisterSourceOutput(
                    source,
                    static (context, source) =>
                    {
                        var (typeDeclaration, compilation) = source;
                        Generate(typeDeclaration, compilation, context);
                    });
            }
        }

        internal static void Generate(TypeDeclarationSyntax syntax, Compilation compilation, SourceProductionContext context)
        {
            var semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);

            var typeSymbol = semanticModel.GetDeclaredSymbol(syntax) as INamedTypeSymbol;
            if (typeSymbol == null)
            {
                return;
            }

            // verify is partial
            if (!IsPartial(syntax))
            {
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.MustBePartial, syntax.Identifier.GetLocation(), typeSymbol.Name));
                return;
            }

            if (IsNested(syntax) && !IsNestedContainingTypesPartial(syntax))
            {
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.NestedContainingTypesMustBePartial, syntax.Identifier.GetLocation(), typeSymbol.Name));
                return;
            }

            var attributeReferences = new AttributeReferences(compilation);
            if (!TypeMetadataValidator.TryValidateMessageIdRange(typeSymbol, attributeReferences, out string invalidAttributeName, out string invalidAttributeValue))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MessageAttributeValueOutOfRange,
                    syntax.Identifier.GetLocation(),
                    typeSymbol.Name,
                    invalidAttributeName,
                    invalidAttributeValue));
                return;
            }

            var typeMeta = new TypeMetadata(typeSymbol, attributeReferences);

            // Root 계층 구조 검증
            if (!ValidateRootHierarchy(typeSymbol, typeMeta, attributeReferences, syntax, context))
            {
                return;
            }

            // Root 메시지가 abstract이면 코드 생성을 건너뛰기 (abstract Root는 상속용이므로)
            if (typeMeta.IsGroupedRootMessage && typeSymbol.IsAbstract)
            {
                return;
            }

            // Step2 : Generate Code
            var serializeCodeEmitter = new MessageSerializeCodeEmitter(typeMeta);
            string serializeCode = serializeCodeEmitter.Emit();

            // Step3 : Debug - Save generated code to file
#pragma warning disable RS1035 // Do not use APIs banned for analyzers
            try
            {
                var debugFilePath = Path.Combine("C:\\Debug\\", $"{typeMeta.Symbol.Name}.g.debug.cs");
                if(File.Exists(debugFilePath))
                    File.WriteAllText(debugFilePath, serializeCode);
            }
            catch
            {
                // 디버그 파일 생성 실패는 무시
            }
#pragma warning restore RS1035

            // Step4 : Output Code
            context.AddSource($"{typeMeta.Symbol.Name}.g.cs", SourceText.From(serializeCode, Encoding.UTF8));
        }

        static bool IsPartial(TypeDeclarationSyntax typeDeclaration)
        {
            return typeDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
        }

        static bool IsNestedContainingTypesPartial(TypeDeclarationSyntax typeDeclaration)
        {
            if (typeDeclaration.Parent is TypeDeclarationSyntax parentTypeDeclaration)
            {
                if (!IsPartial(parentTypeDeclaration))
                    return false;

                return IsNestedContainingTypesPartial(parentTypeDeclaration);
            }
            else
            {
                return true;
            }
        }

        static bool IsNested(TypeDeclarationSyntax typeDeclaration)
        {
            return typeDeclaration.Parent is TypeDeclarationSyntax;
        }

        static bool ValidateRootHierarchy(INamedTypeSymbol typeSymbol, TypeMetadata typeMeta, AttributeReferences attributeReferences, TypeDeclarationSyntax syntax, SourceProductionContext context)
        {
            // Element 메시지인데 Root가 없으면 에러
            // MessageRootId가 0이어도 Root 메시지가 있을 수 있으므로, BaseTypeMetadata에서 확인
            if (typeMeta.IsGroupedElementMessage)
            {
                bool hasRoot = false;
                var current = typeMeta;
                while (current != null)
                {
                    if (current.IsGroupedRootMessage)
                    {
                        hasRoot = true;
                        break;
                    }
                    current = current.BaseTypeMetadata;
                }
                
                if (!hasRoot)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ElementMessageMustHaveRoot,
                        syntax.Identifier.GetLocation(),
                        typeSymbol.Name));
                    return false;
                }
            }

            // Root 메시지인데 부모에 Root가 있으면 에러
            if (typeMeta.IsGroupedRootMessage)
            {
                var baseType = typeSymbol.BaseType;
                while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
                {
                    var parentRootAttribute = baseType.FindAttribute(attributeReferences.MessageGroupRootAttributeType);
                    if (parentRootAttribute != null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.RootMessageCannotHaveRootParent,
                            syntax.Identifier.GetLocation(),
                            typeSymbol.Name));
                        return false;
                    }
                    baseType = baseType.BaseType;
                }
            }

            return true;
        }
    }
}

using MessageProtocol.CodeGenerator.Graph;
using MessageProtocol.CodeGenerator.Metadata;
using MessageProtocol.CodeGenerator.Reference;
using Microsoft.CodeAnalysis;
using System.Text;

namespace MessageProtocol.CodeGenerator.Generate
{
    internal static partial class MessageSerializeCodeEmitter
    {
        /// <summary>partial 타입 선언 + 상속·인터페이스 + 정적 멤버 배치.</summary>
        internal static class Define
        {
            public static string Emit(TypeMetadata typeMeta, SerializationGraph serializationGraph, AttributeReferences attributeReferences, EmitState state)
            {
                var sb = new StringBuilder();
                string indent = GetTypeIndent(typeMeta);
                string declarationIndent = GetNamespaceIndent(typeMeta);

                sb.AppendLine();

                // 컨테이닝 타입 partial 래퍼를 먼저 연다.
                foreach (var containingType in typeMeta.ContainingTypes)
                {
                    sb.AppendLine($"{declarationIndent}partial {containingType.DeclarationKeyword} {containingType.Name}{containingType.TypeParameters}{containingType.Constraints}");
                    sb.AppendLine($"{declarationIndent}{{");
                    declarationIndent += "    ";
                }

            string baseAndInterfaces = GetBaseAndInterfaces(typeMeta, attributeReferences);
            string staticHidingModifier = GetStaticHidingModifier(typeMeta);
            sb.AppendLine($"{declarationIndent}public partial {typeMeta.DeclarationKeyword} {typeMeta.DeclarationName}{baseAndInterfaces}");
            sb.AppendLine($"{declarationIndent}{{");
            sb.AppendLine($"{declarationIndent}    public {staticHidingModifier}static uint MessageId => {typeMeta.GetMessageId()};");
            if (typeMeta.CanUseModuleInitializer)
            {
                sb.AppendLine($"{declarationIndent}    {Method.EmitOnModuleInitialize(typeMeta, indent + "     ")}");
                sb.AppendLine($"{declarationIndent}");
            }
            sb.AppendLine($"{declarationIndent}    {Method.EmitSerialize(typeMeta, indent + "    ", serializationGraph)}");
                sb.AppendLine($"{declarationIndent}");
                sb.AppendLine($"{declarationIndent}    {Method.EmitDeserialize(typeMeta, indent + "    ", serializationGraph)}");
                sb.AppendLine($"{declarationIndent}");
                sb.AppendLine($"{declarationIndent}    {Method.EmitHelperMethods(indent + "    ", serializationGraph, state)}");
                sb.AppendLine($"{declarationIndent}}}");

            for (int i = typeMeta.ContainingTypes.Length - 1; i >= 0; i--)
            {
                declarationIndent = declarationIndent.Substring(0, declarationIndent.Length - 4);
                sb.AppendLine($"{declarationIndent}}}");
            }

            return sb.ToString();
        }

            static string GetNamespaceIndent(TypeMetadata typeMeta)
            {
                string namespaceName = typeMeta.Symbol.ContainingNamespace.ToDisplayString();
                bool hasNamespace = !string.IsNullOrEmpty(namespaceName) && namespaceName != "<global namespace>";
                return hasNamespace ? "    " : "";
            }

            static string GetTypeIndent(TypeMetadata typeMeta)
            {
                return GetNamespaceIndent(typeMeta) + new string(' ', typeMeta.ContainingTypes.Length * 4);
            }

            static string GetBaseAndInterfaces(TypeMetadata typeMeta, AttributeReferences attributeReferences)
            {
                var parts = new List<string>();

                var baseType = typeMeta.Symbol.BaseType;
                bool canHaveBaseType = typeMeta.DeclarationKind == TypeDeclarationKind.Class ||
                                       typeMeta.DeclarationKind == TypeDeclarationKind.RecordClass;
                if (canHaveBaseType &&
                    baseType != null &&
                    baseType.SpecialType != SpecialType.System_Object &&
                    baseType.SpecialType != SpecialType.System_ValueType)
                {
                    parts.Add(baseType.ToDisplayString());
                }

                // Group / Standalone 은 MessageId 를 프로토콜 식별자로 쓰므로 IHasIdMessageSerializable.
                bool hasIdInProtocol = typeMeta.IsGroupMessage || typeMeta.IsStandaloneMessage;
                parts.Add(hasIdInProtocol
                    ? $"IHasIdMessageSerializable<{typeMeta.DeclarationName}>"
                    : $"IMessageSerializable<{typeMeta.DeclarationName}>");

                // 원본 선언의 인터페이스는 생성하는 직렬화 인터페이스와 중복만 제거하고 유지.
                foreach (var interfaceType in typeMeta.Symbol.Interfaces)
                {
                    if (IsGeneratedSerializationInterface(interfaceType, attributeReferences.MessageSerializableInterfaceType, typeMeta.Symbol) ||
                        IsGeneratedSerializationInterface(interfaceType, attributeReferences.HasIdMessageSerializableInterfaceType, typeMeta.Symbol))
                    {
                        continue;
                    }

                    parts.Add(interfaceType.ToDisplayString());
                }

                if (parts.Count == 0)
                {
                    return "";
                }

                return " : " + string.Join(", ", parts);
            }

            static string GetStaticHidingModifier(TypeMetadata typeMeta)
            {
                var baseType = typeMeta.BaseTypeMetadata;
                if (baseType == null)
                {
                    return string.Empty;
                }

                return baseType.IsNonIdMessage || baseType.IsStandaloneMessage || baseType.IsGroupMessage
                    ? "new "
                    : string.Empty;
            }

            static bool IsGeneratedSerializationInterface(
                INamedTypeSymbol interfaceType,
                INamedTypeSymbol? expectedDefinition,
                INamedTypeSymbol messageType)
            {
                return expectedDefinition != null &&
                    interfaceType.IsGenericType &&
                    interfaceType.TypeArguments.Length == 1 &&
                    SymbolEqualityComparer.Default.Equals(interfaceType.OriginalDefinition, expectedDefinition) &&
                    SymbolEqualityComparer.Default.Equals(interfaceType.TypeArguments[0], messageType);
            }
        }
    }
}

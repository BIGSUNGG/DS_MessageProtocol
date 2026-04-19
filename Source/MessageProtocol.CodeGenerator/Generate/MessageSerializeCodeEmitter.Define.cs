using MessageProtocol.CodeGenerator.Metadata;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;

namespace MessageProtocol.CodeGenerator.Generate
{
    internal sealed partial class MessageSerializeCodeEmitter
    {
        // Class: 클래스 선언 및 상속
        internal static class Define
        {
            public static string Emit(TypeMetadata typeMeta)
            {
                StringBuilder sb = new StringBuilder();
                string indent = GetTypeIndent(typeMeta);
                string declarationIndent = GetNamespaceIndent(typeMeta);

                sb.AppendLine();
                
                // 상위 클래스 먼저 선언
                foreach (var containingType in typeMeta.ContainingTypes)
                {
                    sb.AppendLine($"{declarationIndent}partial {containingType.DeclarationKeyword} {containingType.Name}{containingType.TypeParameters}{containingType.Constraints}");
                    sb.AppendLine($"{declarationIndent}{{");
                    declarationIndent += "    ";
                }
                
                // 메시지 클래스 정의
                string baseAndInterfaces = GetBaseAndInterfaces(typeMeta);
                sb.AppendLine($"{declarationIndent}public partial {typeMeta.DeclarationKeyword} {typeMeta.Symbol.Name}{baseAndInterfaces}");
                sb.AppendLine($"{declarationIndent}{{");
                sb.AppendLine($"{declarationIndent}    public static uint MessageId => {typeMeta.GetMessageId()};");
                sb.AppendLine($"{declarationIndent}    {Method.EmitOnModuleInitialize(typeMeta, indent + "     ")}");
                sb.AppendLine($"{declarationIndent}");
                sb.AppendLine($"{declarationIndent}    {Method.EmitSerialize(typeMeta, indent + "    ")}");
                sb.AppendLine($"{declarationIndent}");
                sb.AppendLine($"{declarationIndent}    {Method.EmitDeserialize(typeMeta, indent + "    ")}");
                sb.AppendLine($"{declarationIndent}}}");
                
                // 괄호 닫기
                for (int i = typeMeta.ContainingTypes.Length - 1; i >= 0; i--)
                {
                    declarationIndent = declarationIndent.Substring(0, declarationIndent.Length - 4);
                    sb.AppendLine($"{declarationIndent}}}");
                }

                return sb.ToString();
            }

            private static string GetNamespaceIndent(TypeMetadata typeMeta)
            {
                string namespaceName = typeMeta.Symbol.ContainingNamespace.ToDisplayString();
                bool hasNamespace = !string.IsNullOrEmpty(namespaceName) && namespaceName != "<global namespace>";
                return hasNamespace ? "    " : "";
            }

            private static string GetTypeIndent(TypeMetadata typeMeta)
            {
                return GetNamespaceIndent(typeMeta) + new string(' ', typeMeta.ContainingTypes.Length * 4);
            }

            private static string GetBaseAndInterfaces(TypeMetadata typeMeta)
            {
                var parts = new List<string>();
                
                // 기본 클래스가 있고 Object가 아니면 추가
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
                
                // 인터페이스 추가 (using 문에 이미 포함되어 있으므로 네임스페이스 없이)
                // Group / Standalone 은 MessageId를 프로토콜 식별자로 쓰므로 IHasIdMessageSerializable
                bool hasIdInProtocol = typeMeta.IsGroupMessage || typeMeta.IsStandaloneMessage;
                parts.Add(hasIdInProtocol
                    ? $"IHasIdMessageSerializable<{typeMeta.Symbol.Name}>"
                    : $"IMessageSerializable<{typeMeta.Symbol.Name}>");
                
                // 기존에 구현된 인터페이스들도 추가 (원본 클래스 선언에 있는 인터페이스들)
                foreach (var interfaceType in typeMeta.Symbol.Interfaces)
                {
                    // 생성기가 이미 추가한 직렬화 인터페이스는 제외
                    bool isSameMessageSerializable = interfaceType.Name == "IMessageSerializable" &&
                                               interfaceType.IsGenericType &&
                                               interfaceType.TypeArguments.Length == 1 &&
                                               interfaceType.TypeArguments[0].Equals(typeMeta.Symbol, SymbolEqualityComparer.Default);
                    bool isSameHasIdMessageSerializable = interfaceType.Name == "IHasIdMessageSerializable" &&
                                               interfaceType.IsGenericType &&
                                               interfaceType.TypeArguments.Length == 1 &&
                                               interfaceType.TypeArguments[0].Equals(typeMeta.Symbol, SymbolEqualityComparer.Default);
                    
                    if (!isSameMessageSerializable && !isSameHasIdMessageSerializable)
                    {
                        parts.Add(interfaceType.ToDisplayString());
                    }
                }
                
                if (parts.Count == 0)
                {
                    return "";
                }
                
                return " : " + string.Join(", ", parts);
            }
        }
    }
}


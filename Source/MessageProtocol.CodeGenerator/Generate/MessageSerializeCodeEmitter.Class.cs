using MessageProtocol.CodeGenerator.Metadata;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;

namespace MessageProtocol.CodeGenerator.Generate
{
    internal sealed partial class MessageSerializeCodeEmitter
    {
        // Class: 클래스 선언 및 상속
        internal static class Class
        {
            public static string Emit(TypeMetadata typeMeta)
            {
                StringBuilder sb = new StringBuilder();
                string indent = GetTypeIndent(typeMeta);
                string declarationIndent = GetNamespaceIndent(typeMeta);

                sb.AppendLine();

                foreach (var containingType in typeMeta.ContainingTypes)
                {
                    sb.AppendLine($"{declarationIndent}partial {containingType.DeclarationKeyword} {containingType.Name}{containingType.TypeParameters}{containingType.Constraints}");
                    sb.AppendLine($"{declarationIndent}{{");
                    declarationIndent += "    ";
                }

                string baseAndInterfaces = GetBaseAndInterfaces(typeMeta);
                sb.AppendLine($"{declarationIndent}public partial class {typeMeta.Symbol.Name}{baseAndInterfaces}");
                sb.AppendLine($"{declarationIndent}{{");
                sb.AppendLine($"{declarationIndent}    public static uint MessageId => {typeMeta.GetMessageId()};");
                sb.AppendLine($"{declarationIndent}    {Method.EmitOnModuleInitialize(typeMeta, indent + "     ")}");
                sb.AppendLine($"{declarationIndent}");
                sb.AppendLine($"{declarationIndent}    {Method.EmitSerialize(typeMeta, indent + "    ")}");
                sb.AppendLine($"{declarationIndent}");
                sb.AppendLine($"{declarationIndent}    {Method.EmitDeserialize(typeMeta, indent + "    ")}");
                sb.AppendLine($"{declarationIndent}}}");

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
                if (baseType != null && baseType.SpecialType != SpecialType.System_Object)
                {
                    parts.Add(baseType.ToDisplayString());
                }
                
                // 인터페이스 추가 (using 문에 이미 포함되어 있으므로 네임스페이스 없이)
                parts.Add($"IMessageSerializable<{typeMeta.Symbol.Name}>");
                
                // 기존에 구현된 인터페이스들도 추가 (원본 클래스 선언에 있는 인터페이스들)
                foreach (var interfaceType in typeMeta.Symbol.Interfaces)
                {
                    // IMessageSerializable은 이미 추가했으므로 제외
                    bool isMessageSerializable = interfaceType.Name == "IMessageSerializable" && 
                                               interfaceType.IsGenericType &&
                                               interfaceType.TypeArguments.Length == 1 &&
                                               interfaceType.TypeArguments[0].Equals(typeMeta.Symbol, SymbolEqualityComparer.Default);
                    
                    if (!isMessageSerializable)
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


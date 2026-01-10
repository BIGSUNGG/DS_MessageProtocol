using MessageProtocol.CodeGenerator.Metadata;
using System.Text;

namespace MessageProtocol.CodeGenerator.Generate
{
    internal sealed partial class MessageSerializeCodeEmitter
    {
        // Header: 네임스페이스와 using 추가
        internal static class Header
        {
            public static string Emit(TypeMetadata typeMeta, out bool hasNamespace)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("using System;");
                sb.AppendLine("using System.IO;");
                sb.AppendLine("using System.Collections.Generic;");
                sb.AppendLine("using System.Runtime.CompilerServices;");
                sb.AppendLine("using MessageProtocol.Serialize;");
                sb.AppendLine();

                // 네임스페이스 처리
                string namespaceName = typeMeta.Symbol.ContainingNamespace.ToDisplayString();
                hasNamespace = !string.IsNullOrEmpty(namespaceName) && namespaceName != "<global namespace>";
                if (hasNamespace)
                {
                    sb.AppendLine($"namespace {namespaceName}");
                    sb.AppendLine("{");
                }

                return sb.ToString();
            }

            public static string EmitCloseNamespace()
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine("}");
                return sb.ToString();
            }
        }
    }
}


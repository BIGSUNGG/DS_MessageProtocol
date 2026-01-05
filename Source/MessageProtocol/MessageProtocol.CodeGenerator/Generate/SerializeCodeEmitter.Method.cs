using MessageProtocol.CodeGenerator.Metadata;
using System.Text;

namespace MessageProtocol.CodeGenerator.Generate
{
    internal sealed partial class SerializeCodeEmitter
    {
        // Method: 직렬화, 역직렬화 함수 추가
        internal static class Method
        {
            public static string EmitSerialize(TypeMetadata typeMeta, string indent)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($@"public static byte[] Serialize({typeMeta.Symbol.Name} message)");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    using (var ms = new MemoryStream())");
                sb.AppendLine($@"{indent}    using (var writer = new BinaryWriter(ms))");
                sb.AppendLine($@"{indent}    {{");
                
                // Header 작성 (RootId와 ElementId를 ushort로)
                ushort header = ushort.MaxValue;
                if (typeMeta.IsGroupedMessage)
                    header = (ushort)((typeMeta.MessageRootId << 8) | (typeMeta.MessageElementId & 0xFF));
                
                sb.AppendLine($@"{indent}        writer.Write((ushort){header});");
                
                // 각 멤버 직렬화
                foreach (var member in typeMeta.Members)
                {
                    string memberCode = Member.EmitSerialize(member, indent + "        ");
                    sb.Append(memberCode);
                }
                
                sb.AppendLine($@"{indent}        return ms.ToArray();");
                sb.AppendLine($@"{indent}    }}");
                sb.AppendLine($@"{indent}}}");
                
                return sb.ToString();
            }

            public static string EmitDeserialize(TypeMetadata typeMeta, string indent)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($@"public static {typeMeta.Symbol.Name} Deserialize(byte[] data)");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    using (var ms = new MemoryStream(data))");
                sb.AppendLine($@"{indent}    using (var reader = new BinaryReader(ms))");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        ushort header = reader.ReadUInt16();");
                sb.AppendLine();
                sb.AppendLine($@"{indent}        var result = new {typeMeta.Symbol.Name}();");
                sb.AppendLine();
                
                // 각 멤버 역직렬화
                foreach (var member in typeMeta.Members)
                {
                    string memberCode = Member.EmitDeserialize(member, indent + "        ");
                    sb.Append(memberCode);
                }
                
                sb.AppendLine($@"{indent}        return result;");
                sb.AppendLine($@"{indent}    }}");
                sb.AppendLine($@"{indent}}}");
                
                return sb.ToString();
            }
        }
    }
}


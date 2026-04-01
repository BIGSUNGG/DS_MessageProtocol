using MessageProtocol.CodeGenerator.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MessageProtocol.CodeGenerator.Generate
{
    internal sealed partial class MessageSerializeCodeEmitter
    {
        // Method: 직렬화, 역직렬화 함수 추가
        internal static class Method
        {
            public static string EmitOnModuleInitialize(TypeMetadata typeMeta, string indent)
            {
                StringBuilder sb = new StringBuilder();
                if(typeMeta.IsMessage == false)
                    sb.AppendLine($@"
{indent}[ModuleInitializer]
{indent}internal static void Initialize()
{indent}{{
{indent}    MessageSerializer.RegisterType(typeof({typeMeta.Symbol.Name}));
{indent}}}");

                return sb.ToString();
            }

            static IEnumerable<MemberMetadata> GetMembers(TypeMetadata typeMeta)
            {
                var memberDict = new Dictionary<string, MemberMetadata>();
                
                // 부모 타입의 멤버부터 수집 (부모 -> 자식 순서)
                if (typeMeta.BaseTypeMetadata != null)
                {
                    foreach (var member in GetMembers(typeMeta.BaseTypeMetadata))
                    {
                        memberDict[member.Name] = member;
                    }
                }
                
                // 현재 타입의 멤버 추가 (같은 이름이면 덮어씀 - 자식이 부모를 override)
                foreach (var member in typeMeta.Members)
                {
                    memberDict[member.Name] = member;
                }
                
                return memberDict.Values;
            }

            public static string EmitSerialize(TypeMetadata typeMeta, string indent)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($@"public static byte[] Serialize({typeMeta.Symbol.Name} message)");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    using (var ms = new MemoryStream())");
                sb.AppendLine($@"{indent}    using (var writer = new BinaryWriter(ms))");
                sb.AppendLine($@"{indent}    {{");
                
                // id 구성: 앞 8비트(standaloneId) + 그 다음 8비트(groupRootId) + 그 다음 16비트(elementId)
                uint id = typeMeta.GetMessageId();

                sb.AppendLine($@"{indent}        uint id = {id};");
                sb.AppendLine($@"{indent}        byte messageFlag = (byte)(id >> 24);");
                sb.AppendLine($@"{indent}        writer.Write(messageFlag);");
                sb.AppendLine($@"{indent}        if ((messageFlag & 0x01) == 0)");
                sb.AppendLine($@"{indent}        {{");
                sb.AppendLine($@"{indent}            writer.Write((byte)(id >> 16));");
                sb.AppendLine($@"{indent}            writer.Write((byte)(id >> 8));");
                sb.AppendLine($@"{indent}            writer.Write((byte)id);");
                sb.AppendLine($@"{indent}        }}");
                
                // 각 멤버 직렬화
                foreach (var member in GetMembers(typeMeta))
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
                sb.AppendLine($@"{indent}        byte messageFlag = reader.ReadByte();");
                sb.AppendLine($@"{indent}        uint id = (uint)messageFlag << 24;");
                sb.AppendLine($@"{indent}        if ((messageFlag & 0x01) == 0)");
                sb.AppendLine($@"{indent}        {{");
                sb.AppendLine($@"{indent}            id |= (uint)reader.ReadByte() << 16;");
                sb.AppendLine($@"{indent}            id |= (uint)reader.ReadByte() << 8;");
                sb.AppendLine($@"{indent}            id |= reader.ReadByte();");
                sb.AppendLine($@"{indent}        }}");
                sb.AppendLine();
                sb.AppendLine($@"{indent}        var result = new {typeMeta.Symbol.Name}();");
                sb.AppendLine();
                
                // 각 멤버 역직렬화
                foreach (var member in GetMembers(typeMeta))
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


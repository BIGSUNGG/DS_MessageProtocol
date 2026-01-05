using MessageProtocol.CodeGenerator.Metadata;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MessageProtocol.CodeGenerator.Generate
{
    internal sealed class SerializeCodeEmitter
    {
        TypeMetadata _typeMeta;

        public SerializeCodeEmitter(TypeMetadata typeMeta)
        {
            _typeMeta = typeMeta;
        }

        public string Emit()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.IO;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using MessageProtocol.Serialize;");
            sb.AppendLine();

            // 네임스페이스 처리
            string namespaceName = _typeMeta.Symbol.ContainingNamespace.ToDisplayString();
            bool hasNamespace = !string.IsNullOrEmpty(namespaceName) && namespaceName != "<global namespace>";
            string indent = hasNamespace ? "    " : "";

            if (hasNamespace)
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
            }

            // 기본 클래스와 인터페이스 선언 생성
            string baseAndInterfaces = GetBaseAndInterfaces();

            sb.Append($@"{indent}public partial class {_typeMeta.Symbol.Name}{baseAndInterfaces}
{indent}{{
{indent}    public static byte[] Serialize({_typeMeta.Symbol.Name} message)
{indent}    {{
{indent}        using (var ms = new MemoryStream())
{indent}        using (var writer = new BinaryWriter(ms))
{indent}        {{
{indent}            {EmitSerializeMethod().Replace("\n", "\n" + indent + "            ").TrimEnd()}
{indent}            return ms.ToArray();
{indent}        }}
{indent}    }}

{indent}    public static {_typeMeta.Symbol.Name} Deserialize(byte[] data)
{indent}    {{
{indent}        using (var ms = new MemoryStream(data))
{indent}        using (var reader = new BinaryReader(ms))
{indent}        {{
{indent}            {EmitDeerializeMethod().Replace("\n", "\n" + indent + "            ").TrimEnd()}
{indent}        }}
{indent}    }}
{indent}}}");

            if (hasNamespace)
            {
                sb.AppendLine();
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        private string EmitSerializeMethod()
        {
            StringBuilder sb = new StringBuilder();

            // Header 작성 (RootId와 ElementId를 ushort로)
            ushort header = (ushort)((_typeMeta.MessageRootId << 8) | (_typeMeta.MessageElementId & 0xFF));
            sb.AppendLine($"            writer.Write((ushort){header});");

            // 각 멤버 직렬화
            foreach (var member in _typeMeta.Members)
            {
                sb.AppendLine(EmitMemberSerialize(member));
            }

            return sb.ToString();
        }

        private string EmitDeerializeMethod()
        {
            StringBuilder sb = new StringBuilder();

            // Header 읽기
            sb.AppendLine("            ushort header = reader.ReadUInt16();");
            sb.AppendLine();

            // 객체 생성
            sb.AppendLine($"            var result = new {_typeMeta.Symbol.Name}();");
            sb.AppendLine();

            // 각 멤버 역직렬화
            foreach (var member in _typeMeta.Members)
            {
                sb.AppendLine(EmitMemberDeserialize(member));
            }

            sb.AppendLine("            return result;");

            return sb.ToString();
        }

        private string EmitMemberSerialize(MemberMetadata member)
        {
            string memberAccess = $"message.{member.Name}";
            string typeName = member.Type.ToDisplayString();

            // 기본 타입 처리
            if (member.Type.SpecialType == SpecialType.System_Boolean)
                return $"            writer.Write({memberAccess});";
            if (member.Type.SpecialType == SpecialType.System_Byte)
                return $"            writer.Write({memberAccess});";
            if (member.Type.SpecialType == SpecialType.System_SByte)
                return $"            writer.Write({memberAccess});";
            if (member.Type.SpecialType == SpecialType.System_Int16)
                return $"            writer.Write({memberAccess});";
            if (member.Type.SpecialType == SpecialType.System_UInt16)
                return $"            writer.Write({memberAccess});";
            if (member.Type.SpecialType == SpecialType.System_Int32)
                return $"            writer.Write({memberAccess});";
            if (member.Type.SpecialType == SpecialType.System_UInt32)
                return $"            writer.Write({memberAccess});";
            if (member.Type.SpecialType == SpecialType.System_Int64)
                return $"            writer.Write({memberAccess});";
            if (member.Type.SpecialType == SpecialType.System_UInt64)
                return $"            writer.Write({memberAccess});";
            if (member.Type.SpecialType == SpecialType.System_Single)
                return $"            writer.Write({memberAccess});";
            if (member.Type.SpecialType == SpecialType.System_Double)
                return $"            writer.Write({memberAccess});";
            if (member.Type.SpecialType == SpecialType.System_String)
            {
                return $@"            writer.Write({memberAccess} ?? string.Empty);";
            }

            // 배열 처리
            if (member.Type is IArrayTypeSymbol arrayType)
            {
                string elementType = arrayType.ElementType.ToDisplayString();
                return $@"            if ({memberAccess} == null)
            {{
                writer.Write(0);
            }}
            else
            {{
                writer.Write({memberAccess}.Length);
                foreach (var item in {memberAccess})
                {{
                    writer.Write(item);
                }}
            }}";
            }

            // List 처리
            if (member.Type is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                string genericTypeName = namedType.ConstructedFrom.ToDisplayString();
                if (genericTypeName.StartsWith("System.Collections.Generic.List<") ||
                    genericTypeName.StartsWith("System.Collections.Generic.IList<"))
                {
                    string elementType = namedType.TypeArguments[0].ToDisplayString();
                    return $@"            if ({memberAccess} == null)
            {{
                writer.Write(0);
            }}
            else
            {{
                writer.Write({memberAccess}.Count);
                foreach (var item in {memberAccess})
                {{
                    writer.Write(item);
                }}
            }}";
                }
            }

            // 지원하지 않는 타입
            return $"            // TODO: Serialize {member.Name} ({typeName})";
        }

        private string EmitMemberDeserialize(MemberMetadata member)
        {
            string memberAccess = $"result.{member.Name}";
            string typeName = member.Type.ToDisplayString();

            // 기본 타입 처리
            if (member.Type.SpecialType == SpecialType.System_Boolean)
                return $"            {memberAccess} = reader.ReadBoolean();";
            if (member.Type.SpecialType == SpecialType.System_Byte)
                return $"            {memberAccess} = reader.ReadByte();";
            if (member.Type.SpecialType == SpecialType.System_SByte)
                return $"            {memberAccess} = reader.ReadSByte();";
            if (member.Type.SpecialType == SpecialType.System_Int16)
                return $"            {memberAccess} = reader.ReadInt16();";
            if (member.Type.SpecialType == SpecialType.System_UInt16)
                return $"            {memberAccess} = reader.ReadUInt16();";
            if (member.Type.SpecialType == SpecialType.System_Int32)
                return $"            {memberAccess} = reader.ReadInt32();";
            if (member.Type.SpecialType == SpecialType.System_UInt32)
                return $"            {memberAccess} = reader.ReadUInt32();";
            if (member.Type.SpecialType == SpecialType.System_Int64)
                return $"            {memberAccess} = reader.ReadInt64();";
            if (member.Type.SpecialType == SpecialType.System_UInt64)
                return $"            {memberAccess} = reader.ReadUInt64();";
            if (member.Type.SpecialType == SpecialType.System_Single)
                return $"            {memberAccess} = reader.ReadSingle();";
            if (member.Type.SpecialType == SpecialType.System_Double)
                return $"            {memberAccess} = reader.ReadDouble();";
            if (member.Type.SpecialType == SpecialType.System_String)
            {
                return $"            {memberAccess} = reader.ReadString();";
            }

            // 배열 처리
            if (member.Type is IArrayTypeSymbol arrayType)
            {
                string elementType = arrayType.ElementType.ToDisplayString();
                return $@"            int {member.Name}_length = reader.ReadInt32();
            {memberAccess} = new {elementType}[{member.Name}_length];
            for (int i = 0; i < {member.Name}_length; i++)
            {{
                {memberAccess}[i] = reader.Read{GetReadMethodName(arrayType.ElementType)}();
            }}";
            }

            // List 처리
            if (member.Type is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                string genericTypeName = namedType.ConstructedFrom.ToDisplayString();
                if (genericTypeName.StartsWith("System.Collections.Generic.List<") ||
                    genericTypeName.StartsWith("System.Collections.Generic.IList<"))
                {
                    string elementType = namedType.TypeArguments[0].ToDisplayString();
                    return $@"            int {member.Name}_count = reader.ReadInt32();
            {memberAccess} = new List<{elementType}>({member.Name}_count);
            for (int i = 0; i < {member.Name}_count; i++)
            {{
                {memberAccess}.Add(reader.Read{GetReadMethodName(namedType.TypeArguments[0])}());
            }}";
                }
            }

            // 지원하지 않는 타입
            return $"            // TODO: Deserialize {member.Name} ({typeName})";
        }

        private string GetReadMethodName(ITypeSymbol type)
        {
            if (type.SpecialType == SpecialType.System_Boolean) return "Boolean";
            if (type.SpecialType == SpecialType.System_Byte) return "Byte";
            if (type.SpecialType == SpecialType.System_SByte) return "SByte";
            if (type.SpecialType == SpecialType.System_Int16) return "Int16";
            if (type.SpecialType == SpecialType.System_UInt16) return "UInt16";
            if (type.SpecialType == SpecialType.System_Int32) return "Int32";
            if (type.SpecialType == SpecialType.System_UInt32) return "UInt32";
            if (type.SpecialType == SpecialType.System_Int64) return "Int64";
            if (type.SpecialType == SpecialType.System_UInt64) return "UInt64";
            if (type.SpecialType == SpecialType.System_Single) return "Single";
            if (type.SpecialType == SpecialType.System_Double) return "Double";
            if (type.SpecialType == SpecialType.System_String) return "String";
            // TODO: 다른 타입 지원 추가
            return "Object"; // 기본값
        }

        private string GetBaseAndInterfaces()
        {
            var parts = new List<string>();
            
            // 기본 클래스가 있고 Object가 아니면 추가
            var baseType = _typeMeta.Symbol.BaseType;
            if (baseType != null && baseType.SpecialType != SpecialType.System_Object)
            {
                parts.Add(baseType.ToDisplayString());
            }
            
            // 인터페이스 추가 (using 문에 이미 포함되어 있으므로 네임스페이스 없이)
            parts.Add($"IMessageSerializable<{_typeMeta.Symbol.Name}>");
            
            // 기존에 구현된 인터페이스들도 추가 (원본 클래스 선언에 있는 인터페이스들)
            foreach (var interfaceType in _typeMeta.Symbol.Interfaces)
            {
                // IMessageSerializable은 이미 추가했으므로 제외
                bool isMessageSerializable = interfaceType.Name == "IMessageSerializable" && 
                                           interfaceType.IsGenericType &&
                                           interfaceType.TypeArguments.Length == 1 &&
                                           interfaceType.TypeArguments[0].Equals(_typeMeta.Symbol, SymbolEqualityComparer.Default);
                
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

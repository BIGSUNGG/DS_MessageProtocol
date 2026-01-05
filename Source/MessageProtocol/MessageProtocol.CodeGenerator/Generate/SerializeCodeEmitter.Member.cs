using MessageProtocol.CodeGenerator.Metadata;
using Microsoft.CodeAnalysis;

namespace MessageProtocol.CodeGenerator.Generate
{
    internal sealed partial class SerializeCodeEmitter
    {
        // Member: 개별 멤버 변수 직렬화, 역직렬화 코드 추가
        internal static class Member
        {
            public static string EmitSerialize(MemberMetadata member, string indent)
            {
                string memberAccess = $"message.{member.Name}";
                string typeName = member.Type.ToDisplayString();

                // 기본 타입 처리
                if (member.Type.SpecialType == SpecialType.System_Boolean)
                    return $"{indent}writer.Write({memberAccess});\n";
                if (member.Type.SpecialType == SpecialType.System_Byte)
                    return $"{indent}writer.Write({memberAccess});\n";
                if (member.Type.SpecialType == SpecialType.System_SByte)
                    return $"{indent}writer.Write({memberAccess});\n";
                if (member.Type.SpecialType == SpecialType.System_Int16)
                    return $"{indent}writer.Write({memberAccess});\n";
                if (member.Type.SpecialType == SpecialType.System_UInt16)
                    return $"{indent}writer.Write({memberAccess});\n";
                if (member.Type.SpecialType == SpecialType.System_Int32)
                    return $"{indent}writer.Write({memberAccess});\n";
                if (member.Type.SpecialType == SpecialType.System_UInt32)
                    return $"{indent}writer.Write({memberAccess});\n";
                if (member.Type.SpecialType == SpecialType.System_Int64)
                    return $"{indent}writer.Write({memberAccess});\n";
                if (member.Type.SpecialType == SpecialType.System_UInt64)
                    return $"{indent}writer.Write({memberAccess});\n";
                if (member.Type.SpecialType == SpecialType.System_Single)
                    return $"{indent}writer.Write({memberAccess});\n";
                if (member.Type.SpecialType == SpecialType.System_Double)
                    return $"{indent}writer.Write({memberAccess});\n";
                if (member.Type.SpecialType == SpecialType.System_String)
                {
                    return $"{indent}writer.Write({memberAccess} ?? string.Empty);\n";
                }

                // 배열 처리
                if (member.Type is IArrayTypeSymbol arrayType)
                {
                    string elementType = arrayType.ElementType.ToDisplayString();
                    return $@"{indent}if ({memberAccess} == null)
{indent}{{
{indent}    writer.Write(0);
{indent}}}
{indent}else
{indent}{{
{indent}    writer.Write({memberAccess}.Length);
{indent}    foreach (var item in {memberAccess})
{indent}    {{
{indent}        writer.Write(item);
{indent}    }}
{indent}}}
";
                }

                // List 처리
                if (member.Type is INamedTypeSymbol namedType && namedType.IsGenericType)
                {
                    string genericTypeName = namedType.ConstructedFrom.ToDisplayString();
                    if (genericTypeName.StartsWith("System.Collections.Generic.List<") ||
                        genericTypeName.StartsWith("System.Collections.Generic.IList<"))
                    {
                        string elementType = namedType.TypeArguments[0].ToDisplayString();
                        return $@"{indent}if ({memberAccess} == null)
{indent}{{
{indent}    writer.Write(0);
{indent}}}
{indent}else
{indent}{{
{indent}    writer.Write({memberAccess}.Count);
{indent}    foreach (var item in {memberAccess})
{indent}    {{
{indent}        writer.Write(item);
{indent}    }}
{indent}}}
";
                    }
                }

                // 지원하지 않는 타입
                return $"{indent}// TODO: Serialize {member.Name} ({typeName})\n";
            }

            public static string EmitDeserialize(MemberMetadata member, string indent)
            {
                string memberAccess = $"result.{member.Name}";
                string typeName = member.Type.ToDisplayString();

                // 기본 타입 처리
                if (member.Type.SpecialType == SpecialType.System_Boolean)
                    return $"{indent}{memberAccess} = reader.ReadBoolean();\n";
                if (member.Type.SpecialType == SpecialType.System_Byte)
                    return $"{indent}{memberAccess} = reader.ReadByte();\n";
                if (member.Type.SpecialType == SpecialType.System_SByte)
                    return $"{indent}{memberAccess} = reader.ReadSByte();\n";
                if (member.Type.SpecialType == SpecialType.System_Int16)
                    return $"{indent}{memberAccess} = reader.ReadInt16();\n";
                if (member.Type.SpecialType == SpecialType.System_UInt16)
                    return $"{indent}{memberAccess} = reader.ReadUInt16();\n";
                if (member.Type.SpecialType == SpecialType.System_Int32)
                    return $"{indent}{memberAccess} = reader.ReadInt32();\n";
                if (member.Type.SpecialType == SpecialType.System_UInt32)
                    return $"{indent}{memberAccess} = reader.ReadUInt32();\n";
                if (member.Type.SpecialType == SpecialType.System_Int64)
                    return $"{indent}{memberAccess} = reader.ReadInt64();\n";
                if (member.Type.SpecialType == SpecialType.System_UInt64)
                    return $"{indent}{memberAccess} = reader.ReadUInt64();\n";
                if (member.Type.SpecialType == SpecialType.System_Single)
                    return $"{indent}{memberAccess} = reader.ReadSingle();\n";
                if (member.Type.SpecialType == SpecialType.System_Double)
                    return $"{indent}{memberAccess} = reader.ReadDouble();\n";
                if (member.Type.SpecialType == SpecialType.System_String)
                {
                    return $"{indent}{memberAccess} = reader.ReadString();\n";
                }

                // 배열 처리
                if (member.Type is IArrayTypeSymbol arrayType)
                {
                    string elementType = arrayType.ElementType.ToDisplayString();
                    return $@"{indent}int {member.Name}_length = reader.ReadInt32();
{indent}{memberAccess} = new {elementType}[{member.Name}_length];
{indent}for (int i = 0; i < {member.Name}_length; i++)
{indent}{{
{indent}    {memberAccess}[i] = reader.Read{GetReadMethodName(arrayType.ElementType)}();
{indent}}}
";
                }

                // List 처리
                if (member.Type is INamedTypeSymbol namedType && namedType.IsGenericType)
                {
                    string genericTypeName = namedType.ConstructedFrom.ToDisplayString();
                    if (genericTypeName.StartsWith("System.Collections.Generic.List<") ||
                        genericTypeName.StartsWith("System.Collections.Generic.IList<"))
                    {
                        string elementType = namedType.TypeArguments[0].ToDisplayString();
                        return $@"{indent}int {member.Name}_count = reader.ReadInt32();
{indent}{memberAccess} = new List<{elementType}>({member.Name}_count);
{indent}for (int i = 0; i < {member.Name}_count; i++)
{indent}{{
{indent}    {memberAccess}.Add(reader.Read{GetReadMethodName(namedType.TypeArguments[0])}());
{indent}}}
";
                    }
                }

                // 지원하지 않는 타입
                return $"{indent}// TODO: Deserialize {member.Name} ({typeName})\n";
            }

            private static string GetReadMethodName(ITypeSymbol type)
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
        }
    }
}


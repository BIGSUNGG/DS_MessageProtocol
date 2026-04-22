using MessageProtocol.CodeGenerator.Metadata;
using MessageProtocol.CodeGenerator.Graph;
using Microsoft.CodeAnalysis;

namespace MessageProtocol.CodeGenerator.Generate
{
    internal static partial class MessageSerializeCodeEmitter
    {
        // Member: 개별 멤버 변수 직렬화, 역직렬화 코드 추가
        internal static class Member
        {
            public static string EmitSerialize(
                MemberMetadata member,
                string instanceExpression,
                string indent,
                SerializationGraph serializationGraph)
            {
                string memberAccess = $"{instanceExpression}.{member.Name}";
                return EmitSerializeValue(member.Type, memberAccess, indent, serializationGraph);
            }

            public static string EmitDeserialize(
                MemberMetadata member,
                string instanceExpression,
                string indent,
                SerializationGraph serializationGraph)
            {
                string memberAccess = $"{instanceExpression}.{member.Name}";
                return EmitDeserializeValue(member.Type, memberAccess, indent, serializationGraph);
            }

            static string EmitSerializeValue(
                ITypeSymbol typeSymbol,
                string valueExpression,
                string indent,
                SerializationGraph serializationGraph)
            {
                string typeName = GetTypeDisplayName(typeSymbol);

                if (serializationGraph.IsMessageType(typeSymbol))
                {
                    if (typeSymbol.IsReferenceType)
                    {
                        return $@"{indent}if ({valueExpression} == null)
{indent}{{
{indent}    writer.Write(-1);
{indent}}}
{indent}else
{indent}{{
{indent}    var nestedBytes = MessageSerializer.Serialize({valueExpression});
{indent}    writer.Write(nestedBytes.Length);
{indent}    writer.Write(nestedBytes);
{indent}}}
";
                    }

                    return $@"{indent}{{
{indent}    var nestedBytes = MessageSerializer.Serialize({valueExpression});
{indent}    writer.Write(nestedBytes.Length);
{indent}    writer.Write(nestedBytes);
{indent}}}
";
                }

                if (TryEmitPrimitiveWrite(typeSymbol, valueExpression, indent, out string primitiveWrite))
                {
                    return primitiveWrite;
                }

                if (typeSymbol is IArrayTypeSymbol arrayType)
                {
                    return $@"{indent}if ({valueExpression} == null)
{indent}{{
{indent}    writer.Write(0);
{indent}}}
{indent}else
{indent}{{
{indent}    writer.Write({valueExpression}.Length);
{indent}    foreach (var item in {valueExpression})
{indent}    {{
{EmitSerializeValue(arrayType.ElementType, "item", indent + "        ", serializationGraph)}{indent}    }}
{indent}}}
";
                }

                if (SerializationGraph.TryGetCollectionElementType(typeSymbol, out var collectionElementType)
                    && typeSymbol is INamedTypeSymbol collectionType
                    && collectionType.IsGenericType)
                {
                    return $@"{indent}if ({valueExpression} == null)
{indent}{{
{indent}    writer.Write(0);
{indent}}}
{indent}else
{indent}{{
{indent}    writer.Write({valueExpression}.Count);
{indent}    foreach (var item in {valueExpression})
{indent}    {{
{EmitSerializeValue(collectionElementType, "item", indent + "        ", serializationGraph)}{indent}    }}
{indent}}}
";
                }

                if (serializationGraph.TryGetSerializableObjectType(typeSymbol, out var typeModel))
                {
                    string helperCall = typeModel.IsReferenceType
                        ? $"__MessageProtocolWriteSizedReference(writer, {valueExpression}, context, {typeModel.WritePayloadMethodName});"
                        : $"__MessageProtocolWriteSizedValue(writer, {valueExpression}, context, {typeModel.WritePayloadMethodName});";
                    return $"{indent}{helperCall}\n";
                }

                return $"{indent}// TODO: Serialize value ({typeName})\n";
            }

            static string EmitDeserializeValue(
                ITypeSymbol typeSymbol,
                string targetExpression,
                string indent,
                SerializationGraph serializationGraph)
            {
                string typeName = GetTypeDisplayName(typeSymbol);

                if (serializationGraph.IsMessageType(typeSymbol))
                {
                    if (typeSymbol.IsReferenceType)
                    {
                        return $@"{indent}int nestedLength = reader.ReadInt32();
{indent}if (nestedLength < 0)
{indent}{{
{indent}    {targetExpression} = null;
{indent}}}
{indent}else
{indent}{{
{indent}    byte[] nestedBytes = reader.ReadBytes(nestedLength);
{indent}    {targetExpression} = MessageSerializer.Deserialize<{typeName}>(nestedBytes);
{indent}}}
";
                    }

                    return $@"{indent}int nestedLength = reader.ReadInt32();
{indent}if (nestedLength < 0)
{indent}{{
{indent}    throw new InvalidDataException(""Value type message payload cannot be null."");
{indent}}}
{indent}else
{indent}{{
{indent}    byte[] nestedBytes = reader.ReadBytes(nestedLength);
{indent}    {targetExpression} = MessageSerializer.Deserialize<{typeName}>(nestedBytes);
{indent}}}
";
                }

                if (TryEmitPrimitiveRead(typeSymbol, targetExpression, indent, out string primitiveRead))
                {
                    return primitiveRead;
                }

                if (typeSymbol is IArrayTypeSymbol arrayType)
                {
                    string elementTypeName = GetTypeDisplayName(arrayType.ElementType);
                    return $@"{indent}{{
{indent}    int length = reader.ReadInt32();
{indent}    {targetExpression} = new {elementTypeName}[length];
{indent}    for (int i = 0; i < length; i++)
{indent}    {{
{EmitDeserializeValue(arrayType.ElementType, $"{targetExpression}[i]", indent + "        ", serializationGraph)}{indent}    }}
{indent}}}
";
                }

                if (SerializationGraph.TryGetCollectionElementType(typeSymbol, out var collectionElementType)
                    && typeSymbol is INamedTypeSymbol)
                {
                    string elementTypeName = GetTypeDisplayName(collectionElementType);
                    return $@"{indent}{{
{indent}    int count = reader.ReadInt32();
{indent}    {targetExpression} = new List<{elementTypeName}>(count);
{indent}    for (int i = 0; i < count; i++)
{indent}    {{
{indent}        {elementTypeName} item = default({elementTypeName});
{EmitDeserializeValue(collectionElementType, "item", indent + "        ", serializationGraph)}{indent}        {targetExpression}.Add(item);
{indent}    }}
{indent}}}
";
                }

                if (serializationGraph.TryGetSerializableObjectType(typeSymbol, out var typeModel))
                {
                    string helperCall = typeModel.IsReferenceType
                        ? $"__MessageProtocolReadSizedReference(reader, context, {typeModel.CreateInstanceMethodName}, {typeModel.PopulatePayloadMethodName})"
                        : $"__MessageProtocolReadSizedValue(reader, context, {typeModel.ReadPayloadMethodName})";
                    return $"{indent}{targetExpression} = {helperCall};\n";
                }

                return $"{indent}// TODO: Deserialize value ({typeName})\n";
            }

            static bool TryEmitPrimitiveWrite(
                ITypeSymbol typeSymbol,
                string valueExpression,
                string indent,
                out string code)
            {
                if (typeSymbol.TypeKind == TypeKind.Enum && typeSymbol is INamedTypeSymbol enumType)
                {
                    var underlyingType = enumType.EnumUnderlyingType;
                    if (underlyingType != null)
                    {
                        string underlyingTypeName = GetTypeDisplayName(underlyingType);
                        code = $"{indent}writer.Write(({underlyingTypeName}){valueExpression});\n";
                        return true;
                    }
                }

                switch (typeSymbol.SpecialType)
                {
                    case SpecialType.System_Boolean:
                    case SpecialType.System_Byte:
                    case SpecialType.System_SByte:
                    case SpecialType.System_Int16:
                    case SpecialType.System_UInt16:
                    case SpecialType.System_Int32:
                    case SpecialType.System_UInt32:
                    case SpecialType.System_Int64:
                    case SpecialType.System_UInt64:
                    case SpecialType.System_Single:
                    case SpecialType.System_Double:
                    case SpecialType.System_Decimal:
                    case SpecialType.System_Char:
                        code = $"{indent}writer.Write({valueExpression});\n";
                        return true;
                    case SpecialType.System_String:
                        code = $"{indent}writer.Write({valueExpression} ?? string.Empty);\n";
                        return true;
                    default:
                        code = string.Empty;
                        return false;
                }
            }

            static bool TryEmitPrimitiveRead(
                ITypeSymbol typeSymbol,
                string targetExpression,
                string indent,
                out string code)
            {
                if (typeSymbol.TypeKind == TypeKind.Enum && typeSymbol is INamedTypeSymbol enumType)
                {
                    var underlyingType = enumType.EnumUnderlyingType;
                    if (underlyingType != null &&
                        TryGetPrimitiveReadExpression(underlyingType, out string enumReadExpression))
                    {
                        code = $"{indent}{targetExpression} = ({GetTypeDisplayName(typeSymbol)})({enumReadExpression});\n";
                        return true;
                    }
                }

                if (TryGetPrimitiveReadExpression(typeSymbol, out string readExpression))
                {
                    code = $"{indent}{targetExpression} = {readExpression};\n";
                    return true;
                }

                code = string.Empty;
                return false;
            }

            static bool TryGetPrimitiveReadExpression(ITypeSymbol typeSymbol, out string readExpression)
            {
                switch (typeSymbol.SpecialType)
                {
                    case SpecialType.System_Boolean:
                        readExpression = "reader.ReadBoolean()";
                        return true;
                    case SpecialType.System_Byte:
                        readExpression = "reader.ReadByte()";
                        return true;
                    case SpecialType.System_SByte:
                        readExpression = "reader.ReadSByte()";
                        return true;
                    case SpecialType.System_Int16:
                        readExpression = "reader.ReadInt16()";
                        return true;
                    case SpecialType.System_UInt16:
                        readExpression = "reader.ReadUInt16()";
                        return true;
                    case SpecialType.System_Int32:
                        readExpression = "reader.ReadInt32()";
                        return true;
                    case SpecialType.System_UInt32:
                        readExpression = "reader.ReadUInt32()";
                        return true;
                    case SpecialType.System_Int64:
                        readExpression = "reader.ReadInt64()";
                        return true;
                    case SpecialType.System_UInt64:
                        readExpression = "reader.ReadUInt64()";
                        return true;
                    case SpecialType.System_Single:
                        readExpression = "reader.ReadSingle()";
                        return true;
                    case SpecialType.System_Double:
                        readExpression = "reader.ReadDouble()";
                        return true;
                    case SpecialType.System_Decimal:
                        readExpression = "reader.ReadDecimal()";
                        return true;
                    case SpecialType.System_Char:
                        readExpression = "reader.ReadChar()";
                        return true;
                    case SpecialType.System_String:
                        readExpression = "reader.ReadString()";
                        return true;
                    default:
                        readExpression = string.Empty;
                        return false;
                }
            }
        }
    }
}


using MessageProtocol.CodeGenerator.Metadata;
using MessageProtocol.CodeGenerator.Graph;
using Microsoft.CodeAnalysis;
using System.Text;
using System.Threading;

namespace MessageProtocol.CodeGenerator.Generate
{
    internal static partial class MessageSerializeCodeEmitter
    {
        // Member: 개별 멤버 변수 직렬화, 역직렬화 코드 추가
        internal static class Member
        {
            static int _uniqueIdCounter;

            static int NextUniqueId() => Interlocked.Increment(ref _uniqueIdCounter);

            public static string EmitSerialize(
                MemberMetadata member,
                string instanceExpression,
                string indent,
                SerializationGraph graph)
            {
                string memberAccess = $"{instanceExpression}.{member.Name}";
                return EmitSerializeValue(member.Type, memberAccess, indent, graph);
            }

            public static string EmitDeserialize(
                MemberMetadata member,
                string instanceExpression,
                string indent,
                SerializationGraph graph)
            {
                string memberAccess = $"{instanceExpression}.{member.Name}";
                return EmitDeserializeValue(member.Type, memberAccess, indent, graph);
            }

            static string EmitSerializeValue(
                ITypeSymbol typeSymbol,
                string valueExpression,
                string indent,
                SerializationGraph graph)
            {
                // 1) primitive / string / enum 먼저 (fast path)
                if (TryEmitPrimitiveWrite(typeSymbol, valueExpression, indent, out string primitiveWrite))
                {
                    return primitiveWrite;
                }

                // 2) 배열
                if (typeSymbol is IArrayTypeSymbol arrayType)
                {
                    return EmitArrayWrite(arrayType, valueExpression, indent, graph);
                }

                // 3) List<T> / IList<T>
                if (SerializationGraph.TryGetCollectionElementType(typeSymbol, out var collectionElementType)
                    && typeSymbol is INamedTypeSymbol listType
                    && listType.IsGenericType)
                {
                    return EmitListWrite(collectionElementType, valueExpression, indent, graph);
                }

                // 4) graph 내부 타입 (메시지든 plain POCO 든 공통 처리)
                if (graph.TryGetSerializableObjectType(typeSymbol, out var inGraphModel))
                {
                    return EmitInGraphMessageWrite(inGraphModel, valueExpression, indent);
                }

                // 5) 메시지 타입인데 graph 에 없음 (외부 타입 등) - 정적 Serialize 호출
                if (graph.IsMessageType(typeSymbol))
                {
                    return EmitOutOfGraphMessageWrite(typeSymbol, valueExpression, indent);
                }

                string typeName = GetTypeDisplayName(typeSymbol);
                return $"{indent}// TODO: Serialize value ({typeName})\n";
            }

            static string EmitDeserializeValue(
                ITypeSymbol typeSymbol,
                string targetExpression,
                string indent,
                SerializationGraph graph)
            {
                if (TryEmitPrimitiveRead(typeSymbol, targetExpression, indent, out string primitiveRead))
                {
                    return primitiveRead;
                }

                if (typeSymbol is IArrayTypeSymbol arrayType)
                {
                    return EmitArrayRead(arrayType, targetExpression, indent, graph);
                }

                if (SerializationGraph.TryGetCollectionElementType(typeSymbol, out var collectionElementType)
                    && typeSymbol is INamedTypeSymbol listType
                    && listType.IsGenericType)
                {
                    return EmitListRead(collectionElementType, targetExpression, indent, graph);
                }

                if (graph.TryGetSerializableObjectType(typeSymbol, out var inGraphModel))
                {
                    return EmitInGraphMessageRead(inGraphModel, targetExpression, indent);
                }

                if (graph.IsMessageType(typeSymbol))
                {
                    return EmitOutOfGraphMessageRead(typeSymbol, targetExpression, indent);
                }

                string typeName = GetTypeDisplayName(typeSymbol);
                return $"{indent}// TODO: Deserialize value ({typeName})\n";
            }

            // ------- In-graph message -------

            static string EmitInGraphMessageWrite(SerializableTypeModel model, string valueExpression, string indent)
            {
                if (!model.IsReferenceType)
                {
                    return $"{indent}{model.WritePayloadMethodName}(ref writer, {valueExpression}, ref context);\n";
                }

                int uid = NextUniqueId();
                return $@"{indent}if ({valueExpression} is null)
{indent}{{
{indent}    writer.WriteByte((byte)MessageSerializer.ReferenceKind.Null);
{indent}}}
{indent}else if (context.TryGetObjectId({valueExpression}, out int __backId{uid}))
{indent}{{
{indent}    writer.WriteByte((byte)MessageSerializer.ReferenceKind.BackReference);
{indent}    writer.WriteInt32(__backId{uid});
{indent}}}
{indent}else
{indent}{{
{indent}    context.RegisterObject({valueExpression});
{indent}    writer.WriteByte((byte)MessageSerializer.ReferenceKind.NewObject);
{indent}    {model.WritePayloadMethodName}(ref writer, {valueExpression}, ref context);
{indent}}}
";
            }

            static string EmitInGraphMessageRead(SerializableTypeModel model, string targetExpression, string indent)
            {
                if (!model.IsReferenceType)
                {
                    return $"{indent}{targetExpression} = {model.ReadPayloadMethodName}(ref reader, ref context);\n";
                }

                int uid = NextUniqueId();
                return $@"{indent}{{
{indent}    byte __refKind{uid} = reader.ReadByte();
{indent}    if (__refKind{uid} == (byte)MessageSerializer.ReferenceKind.Null)
{indent}    {{
{indent}        {targetExpression} = null;
{indent}    }}
{indent}    else if (__refKind{uid} == (byte)MessageSerializer.ReferenceKind.BackReference)
{indent}    {{
{indent}        int __objId{uid} = reader.ReadInt32();
{indent}        {targetExpression} = ({model.TypeName})context.GetObject(__objId{uid});
{indent}    }}
{indent}    else
{indent}    {{
{indent}        var __tmp{uid} = {model.CreateInstanceMethodName}();
{indent}        context.RegisterNewObject(__tmp{uid});
{indent}        {model.PopulatePayloadMethodName}(ref reader, __tmp{uid}, ref context);
{indent}        {targetExpression} = __tmp{uid};
{indent}    }}
{indent}}}
";
            }

            // ------- Out-of-graph message (defer to type.Serialize) -------

            static string EmitOutOfGraphMessageWrite(ITypeSymbol typeSymbol, string valueExpression, string indent)
            {
                string typeName = GetTypeDisplayName(typeSymbol);
                if (typeSymbol.IsReferenceType)
                {
                    return $@"{indent}if ({valueExpression} is null)
{indent}{{
{indent}    writer.WriteByte((byte)MessageSerializer.ReferenceKind.Null);
{indent}}}
{indent}else
{indent}{{
{indent}    writer.WriteByte((byte)MessageSerializer.ReferenceKind.NewObject);
{indent}    {typeName}.Serialize({valueExpression}, ref writer);
{indent}}}
";
                }

                return $"{indent}{typeName}.Serialize({valueExpression}, ref writer);\n";
            }

            static string EmitOutOfGraphMessageRead(ITypeSymbol typeSymbol, string targetExpression, string indent)
            {
                string typeName = GetTypeDisplayName(typeSymbol);
                int uid = NextUniqueId();
                if (typeSymbol.IsReferenceType)
                {
                    return $@"{indent}{{
{indent}    byte __nk{uid} = reader.ReadByte();
{indent}    if (__nk{uid} == (byte)MessageSerializer.ReferenceKind.Null)
{indent}    {{
{indent}        {targetExpression} = null;
{indent}    }}
{indent}    else
{indent}    {{
{indent}        {targetExpression} = {typeName}.Deserialize(ref reader);
{indent}    }}
{indent}}}
";
                }

                return $"{indent}{targetExpression} = {typeName}.Deserialize(ref reader);\n";
            }

            // ------- Array -------

            static string EmitArrayWrite(IArrayTypeSymbol arrayType, string valueExpression, string indent, SerializationGraph graph)
            {
                var elementType = arrayType.ElementType;
                string elementTypeName = GetTypeDisplayName(elementType);
                int uid = NextUniqueId();

                if (IsBulkCopyable(elementType))
                {
                    return $@"{indent}if ({valueExpression} is null)
{indent}{{
{indent}    writer.WriteInt32(-1);
{indent}}}
{indent}else
{indent}{{
{indent}    writer.WriteInt32({valueExpression}.Length);
{indent}    if ({valueExpression}.Length > 0)
{indent}    {{
{indent}        writer.WriteBytes(System.Runtime.InteropServices.MemoryMarshal.AsBytes<{elementTypeName}>({valueExpression}.AsSpan()));
{indent}    }}
{indent}}}
";
                }

                var itemName = $"__item{uid}";
                return $@"{indent}if ({valueExpression} is null)
{indent}{{
{indent}    writer.WriteInt32(-1);
{indent}}}
{indent}else
{indent}{{
{indent}    writer.WriteInt32({valueExpression}.Length);
{indent}    for (int __i{uid} = 0; __i{uid} < {valueExpression}.Length; __i{uid}++)
{indent}    {{
{indent}        var {itemName} = {valueExpression}[__i{uid}];
{EmitSerializeValue(elementType, itemName, indent + "        ", graph)}{indent}    }}
{indent}}}
";
            }

            static string EmitArrayRead(IArrayTypeSymbol arrayType, string targetExpression, string indent, SerializationGraph graph)
            {
                var elementType = arrayType.ElementType;
                string elementTypeName = GetTypeDisplayName(elementType);
                int uid = NextUniqueId();

                if (IsBulkCopyable(elementType))
                {
                    int size = GetBulkElementSize(elementType);
                    return $@"{indent}{{
{indent}    int __len{uid} = reader.ReadInt32();
{indent}    if (__len{uid} < 0)
{indent}    {{
{indent}        {targetExpression} = null;
{indent}    }}
{indent}    else
{indent}    {{
{indent}        var __arr{uid} = new {elementTypeName}[__len{uid}];
{indent}        if (__len{uid} > 0)
{indent}        {{
{indent}            reader.ReadBytes(__len{uid} * {size}).CopyTo(System.Runtime.InteropServices.MemoryMarshal.AsBytes<{elementTypeName}>(__arr{uid}.AsSpan()));
{indent}        }}
{indent}        {targetExpression} = __arr{uid};
{indent}    }}
{indent}}}
";
                }

                var itemName = $"__item{uid}";
                return $@"{indent}{{
{indent}    int __len{uid} = reader.ReadInt32();
{indent}    if (__len{uid} < 0)
{indent}    {{
{indent}        {targetExpression} = null;
{indent}    }}
{indent}    else
{indent}    {{
{indent}        var __arr{uid} = new {elementTypeName}[__len{uid}];
{indent}        for (int __i{uid} = 0; __i{uid} < __len{uid}; __i{uid}++)
{indent}        {{
{indent}            {elementTypeName} {itemName} = default({elementTypeName});
{EmitDeserializeValue(elementType, itemName, indent + "            ", graph)}{indent}            __arr{uid}[__i{uid}] = {itemName};
{indent}        }}
{indent}        {targetExpression} = __arr{uid};
{indent}    }}
{indent}}}
";
            }

            // ------- List<T> -------

            static string EmitListWrite(ITypeSymbol elementType, string valueExpression, string indent, SerializationGraph graph)
            {
                string elementTypeName = GetTypeDisplayName(elementType);
                int uid = NextUniqueId();

                if (IsBulkCopyable(elementType))
                {
                    return $@"{indent}if ({valueExpression} is null)
{indent}{{
{indent}    writer.WriteInt32(-1);
{indent}}}
{indent}else
{indent}{{
{indent}    writer.WriteInt32({valueExpression}.Count);
{indent}    if ({valueExpression}.Count > 0)
{indent}    {{
{indent}        writer.WriteBytes(System.Runtime.InteropServices.MemoryMarshal.AsBytes(System.Runtime.InteropServices.CollectionsMarshal.AsSpan({valueExpression})));
{indent}    }}
{indent}}}
";
                }

                var itemName = $"__item{uid}";
                return $@"{indent}if ({valueExpression} is null)
{indent}{{
{indent}    writer.WriteInt32(-1);
{indent}}}
{indent}else
{indent}{{
{indent}    var __span{uid} = System.Runtime.InteropServices.CollectionsMarshal.AsSpan({valueExpression});
{indent}    writer.WriteInt32(__span{uid}.Length);
{indent}    for (int __i{uid} = 0; __i{uid} < __span{uid}.Length; __i{uid}++)
{indent}    {{
{indent}        var {itemName} = __span{uid}[__i{uid}];
{EmitSerializeValue(elementType, itemName, indent + "        ", graph)}{indent}    }}
{indent}}}
";
            }

            static string EmitListRead(ITypeSymbol elementType, string targetExpression, string indent, SerializationGraph graph)
            {
                string elementTypeName = GetTypeDisplayName(elementType);
                int uid = NextUniqueId();

                if (IsBulkCopyable(elementType))
                {
                    int size = GetBulkElementSize(elementType);
                    return $@"{indent}{{
{indent}    int __c{uid} = reader.ReadInt32();
{indent}    if (__c{uid} < 0)
{indent}    {{
{indent}        {targetExpression} = null;
{indent}    }}
{indent}    else
{indent}    {{
{indent}        var __list{uid} = new System.Collections.Generic.List<{elementTypeName}>(__c{uid});
{indent}        if (__c{uid} > 0)
{indent}        {{
{indent}            System.Runtime.InteropServices.CollectionsMarshal.SetCount(__list{uid}, __c{uid});
{indent}            reader.ReadBytes(__c{uid} * {size}).CopyTo(System.Runtime.InteropServices.MemoryMarshal.AsBytes(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(__list{uid})));
{indent}        }}
{indent}        {targetExpression} = __list{uid};
{indent}    }}
{indent}}}
";
                }

                var itemName = $"__item{uid}";
                return $@"{indent}{{
{indent}    int __c{uid} = reader.ReadInt32();
{indent}    if (__c{uid} < 0)
{indent}    {{
{indent}        {targetExpression} = null;
{indent}    }}
{indent}    else
{indent}    {{
{indent}        var __list{uid} = new System.Collections.Generic.List<{elementTypeName}>(__c{uid});
{indent}        for (int __i{uid} = 0; __i{uid} < __c{uid}; __i{uid}++)
{indent}        {{
{indent}            {elementTypeName} {itemName} = default({elementTypeName});
{EmitDeserializeValue(elementType, itemName, indent + "            ", graph)}{indent}            __list{uid}.Add({itemName});
{indent}        }}
{indent}        {targetExpression} = __list{uid};
{indent}    }}
{indent}}}
";
            }

            // ------- Primitive -------

            static bool TryEmitPrimitiveWrite(ITypeSymbol typeSymbol, string valueExpression, string indent, out string code)
            {
                if (typeSymbol.TypeKind == TypeKind.Enum && typeSymbol is INamedTypeSymbol enumType)
                {
                    var underlying = enumType.EnumUnderlyingType;
                    if (underlying != null && TryGetPrimitiveWriteCall(underlying, $"({GetTypeDisplayName(underlying)}){valueExpression}", out string call))
                    {
                        code = $"{indent}{call};\n";
                        return true;
                    }
                }

                if (TryGetPrimitiveWriteCall(typeSymbol, valueExpression, out string writeCall))
                {
                    code = $"{indent}{writeCall};\n";
                    return true;
                }

                code = string.Empty;
                return false;
            }

            static bool TryGetPrimitiveWriteCall(ITypeSymbol typeSymbol, string expression, out string call)
            {
                switch (typeSymbol.SpecialType)
                {
                    case SpecialType.System_Boolean: call = $"writer.WriteBoolean({expression})"; return true;
                    case SpecialType.System_Byte: call = $"writer.WriteByte({expression})"; return true;
                    case SpecialType.System_SByte: call = $"writer.WriteSByte({expression})"; return true;
                    case SpecialType.System_Int16: call = $"writer.WriteInt16({expression})"; return true;
                    case SpecialType.System_UInt16: call = $"writer.WriteUInt16({expression})"; return true;
                    case SpecialType.System_Int32: call = $"writer.WriteInt32({expression})"; return true;
                    case SpecialType.System_UInt32: call = $"writer.WriteUInt32({expression})"; return true;
                    case SpecialType.System_Int64: call = $"writer.WriteInt64({expression})"; return true;
                    case SpecialType.System_UInt64: call = $"writer.WriteUInt64({expression})"; return true;
                    case SpecialType.System_Single: call = $"writer.WriteSingle({expression})"; return true;
                    case SpecialType.System_Double: call = $"writer.WriteDouble({expression})"; return true;
                    case SpecialType.System_Decimal: call = $"writer.WriteDecimal({expression})"; return true;
                    case SpecialType.System_Char: call = $"writer.WriteChar({expression})"; return true;
                    case SpecialType.System_String: call = $"writer.WriteString({expression})"; return true;
                    default: call = string.Empty; return false;
                }
            }

            static bool TryEmitPrimitiveRead(ITypeSymbol typeSymbol, string targetExpression, string indent, out string code)
            {
                if (typeSymbol.TypeKind == TypeKind.Enum && typeSymbol is INamedTypeSymbol enumType)
                {
                    var underlying = enumType.EnumUnderlyingType;
                    if (underlying != null && TryGetPrimitiveReadExpression(underlying, out string underlyingRead))
                    {
                        code = $"{indent}{targetExpression} = ({GetTypeDisplayName(typeSymbol)})({underlyingRead});\n";
                        return true;
                    }
                }

                if (TryGetPrimitiveReadExpression(typeSymbol, out string readExpr))
                {
                    if (typeSymbol.SpecialType == SpecialType.System_String)
                    {
                        code = $"{indent}{targetExpression} = {readExpr};\n";
                        return true;
                    }
                    code = $"{indent}{targetExpression} = {readExpr};\n";
                    return true;
                }

                code = string.Empty;
                return false;
            }

            static bool TryGetPrimitiveReadExpression(ITypeSymbol typeSymbol, out string expression)
            {
                switch (typeSymbol.SpecialType)
                {
                    case SpecialType.System_Boolean: expression = "reader.ReadBoolean()"; return true;
                    case SpecialType.System_Byte: expression = "reader.ReadByte()"; return true;
                    case SpecialType.System_SByte: expression = "reader.ReadSByte()"; return true;
                    case SpecialType.System_Int16: expression = "reader.ReadInt16()"; return true;
                    case SpecialType.System_UInt16: expression = "reader.ReadUInt16()"; return true;
                    case SpecialType.System_Int32: expression = "reader.ReadInt32()"; return true;
                    case SpecialType.System_UInt32: expression = "reader.ReadUInt32()"; return true;
                    case SpecialType.System_Int64: expression = "reader.ReadInt64()"; return true;
                    case SpecialType.System_UInt64: expression = "reader.ReadUInt64()"; return true;
                    case SpecialType.System_Single: expression = "reader.ReadSingle()"; return true;
                    case SpecialType.System_Double: expression = "reader.ReadDouble()"; return true;
                    case SpecialType.System_Decimal: expression = "reader.ReadDecimal()"; return true;
                    case SpecialType.System_Char: expression = "reader.ReadChar()"; return true;
                    case SpecialType.System_String: expression = "reader.ReadString()"; return true;
                    default: expression = string.Empty; return false;
                }
            }

            static bool IsBulkCopyable(ITypeSymbol typeSymbol)
            {
                if (typeSymbol.TypeKind == TypeKind.Enum && typeSymbol is INamedTypeSymbol enumType)
                {
                    var underlying = enumType.EnumUnderlyingType;
                    return underlying != null && GetBulkElementSize(underlying) > 0;
                }
                return GetBulkElementSize(typeSymbol) > 0;
            }

            static int GetBulkElementSize(ITypeSymbol typeSymbol)
            {
                if (typeSymbol.TypeKind == TypeKind.Enum && typeSymbol is INamedTypeSymbol enumType && enumType.EnumUnderlyingType != null)
                {
                    return GetBulkElementSize(enumType.EnumUnderlyingType);
                }

                switch (typeSymbol.SpecialType)
                {
                    case SpecialType.System_Byte:
                    case SpecialType.System_SByte:
                        return 1;
                    case SpecialType.System_Int16:
                    case SpecialType.System_UInt16:
                    case SpecialType.System_Char:
                        return 2;
                    case SpecialType.System_Int32:
                    case SpecialType.System_UInt32:
                    case SpecialType.System_Single:
                        return 4;
                    case SpecialType.System_Int64:
                    case SpecialType.System_UInt64:
                    case SpecialType.System_Double:
                        return 8;
                    default:
                        return -1;
                }
            }
        }
    }
}

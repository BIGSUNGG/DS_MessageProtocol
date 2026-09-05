using MessageProtocol.CodeGenerator.Graph;
using MessageProtocol.CodeGenerator.Metadata;
using Microsoft.CodeAnalysis;
using System.Threading;

namespace MessageProtocol.CodeGenerator.Generate
{
    internal static partial class MessageSerializeCodeEmitter
    {
        /// <summary>멤버 단위 직렬화·역직렬화 코드 이미터.</summary>
        internal static class Member
        {
            static int _uniqueIdCounter;

            static int NextUniqueId() => Interlocked.Increment(ref _uniqueIdCounter);

            public static string EmitSerialize(
                MemberMetadata member,
                string instanceExpression,
                string indent,
                SerializationGraph graph,
                EmitState state)
            {
                string memberAccess = $"{instanceExpression}.{member.Name}";
                Location location = member.Symbol.Locations.FirstOrDefault() ?? Location.None;
                return EmitSerializeValue(member.Type, memberAccess, indent, graph, state, location, member.Name);
            }

            public static string EmitDeserialize(
                MemberMetadata member,
                string instanceExpression,
                string indent,
                SerializationGraph graph,
                EmitState state,
                bool isRootType)
            {
                string memberAccess = $"{instanceExpression}.{member.Name}";
                Location location = member.Symbol.Locations.FirstOrDefault() ?? Location.None;

                if (!IsDeserializableMember(member, isRootType))
                {
                    state.ReportNotAssignable(location, GetTypeDisplayName(member.Type), member.Name);
                    return string.Empty;
                }

                return EmitDeserializeValue(member.Type, memberAccess, indent, graph, state, location, member.Name);
            }

            /// <summary>
            /// 생성 코드는 멤버마다 `result.멤버 = …` 로 대입한다 — 읽기 전용·초기화 전용·읽기전용 필드는 채울 수 없다.
            /// 루트 타입은 자기 partial 안이라 모든 접근 수준 허용, 중첩 페이로드는 루트 클래스에서 접근 가능한 internal 이상만 허용.
            /// </summary>
            static bool IsDeserializableMember(MemberMetadata member, bool isRootType)
            {
                if (member.Symbol is IFieldSymbol field)
                {
                    if (field.IsConst || field.IsReadOnly)
                    {
                        return false;
                    }

                    return isRootType || IsAtLeastInternal(field.DeclaredAccessibility);
                }

                if (member.Symbol is IPropertySymbol property)
                {
                    var setter = property.SetMethod;
                    if (setter == null || setter.IsInitOnly)
                    {
                        return false;
                    }

                    return isRootType || IsAtLeastInternal(setter.DeclaredAccessibility);
                }

                return false;
            }

            static bool IsAtLeastInternal(Accessibility accessibility)
            {
                return accessibility == Accessibility.Public || accessibility == Accessibility.Internal;
            }

            static string EmitSerializeValue(
                ITypeSymbol typeSymbol,
                string valueExpression,
                string indent,
                SerializationGraph graph,
                EmitState state,
                Location diagnosticLocation,
                string memberDisplayName)
            {
                // 1) primitive / string / enum fast path
                if (TryEmitPrimitiveWrite(typeSymbol, valueExpression, indent, out string primitiveWrite))
                {
                    return primitiveWrite;
                }

                // 1.5) 타입 매개변수: 런타임 메시지 디스패치 (T 에는 등록된 메시지 타입만 올 수 있다).
                if (typeSymbol is ITypeParameterSymbol)
                {
                    return EmitRuntimeDispatchWrite(valueExpression, indent);
                }

                // 2) 배열 (1차원만 지원)
                if (typeSymbol is IArrayTypeSymbol arrayType)
                {
                    if (arrayType.Rank != 1)
                    {
                        return ReportUnsupported(typeSymbol, state, diagnosticLocation, memberDisplayName);
                    }

                    return EmitArrayWrite(arrayType, valueExpression, indent, graph, state, diagnosticLocation, memberDisplayName);
                }

                // 3) List<T> / IList<T>
                if (SerializationGraph.TryGetCollectionElementType(typeSymbol, out var collectionElementType)
                    && typeSymbol is INamedTypeSymbol listType
                    && listType.IsGenericType)
                {
                    return EmitListWrite(typeSymbol, collectionElementType, valueExpression, indent, graph, state, diagnosticLocation, memberDisplayName);
                }

                // 4) 그래프 내부 타입 (메시지·중첩 객체 공통)
                if (graph.TryGetSerializableObjectType(typeSymbol, out var inGraphModel))
                {
                    return EmitInGraphMessageWrite(inGraphModel, valueExpression, indent);
                }

                // 5) 메시지 타입인데 그래프 밖 (다른 어셈블리 등) — 정적 Serialize 위임.
                //    단, 추상 메시지 타입(abstract [GroupRootMessage] 등)은 생성기가 정적 Serialize/Deserialize 를
                //    방출하지 않으므로(MSGPROT010 계열 — 인스턴스 생성 불가) 위임 코드가 소비자 빌드를 CS0117 로 깨뜨린다.
                //    대신 런타임 메시지 디스패치로 *구체* 요소를 헤더째 쓴다 — 파생 멤버 유실 없이 다형성이 복원된다.
                if (graph.IsMessageType(typeSymbol))
                {
                    return typeSymbol.IsAbstract
                        ? EmitRuntimeDispatchWrite(valueExpression, indent)
                        : EmitOutOfGraphMessageWrite(typeSymbol, valueExpression, indent);
                }

                return ReportUnsupported(typeSymbol, state, diagnosticLocation, memberDisplayName);
            }

            static string EmitDeserializeValue(
                ITypeSymbol typeSymbol,
                string targetExpression,
                string indent,
                SerializationGraph graph,
                EmitState state,
                Location diagnosticLocation,
                string memberDisplayName)
            {
                if (TryEmitPrimitiveRead(typeSymbol, targetExpression, indent, out string primitiveRead))
                {
                    return primitiveRead;
                }

                if (typeSymbol is ITypeParameterSymbol)
                {
                    return EmitRuntimeDispatchRead(typeSymbol, targetExpression, indent);
                }

                if (typeSymbol is IArrayTypeSymbol arrayType)
                {
                    if (arrayType.Rank != 1)
                    {
                        return ReportUnsupported(typeSymbol, state, diagnosticLocation, memberDisplayName);
                    }

                    return EmitArrayRead(arrayType, targetExpression, indent, graph, state, diagnosticLocation, memberDisplayName);
                }

                if (SerializationGraph.TryGetCollectionElementType(typeSymbol, out var collectionElementType)
                    && typeSymbol is INamedTypeSymbol listType
                    && listType.IsGenericType)
                {
                    return EmitListRead(collectionElementType, targetExpression, indent, graph, state, diagnosticLocation, memberDisplayName);
                }

                if (graph.TryGetSerializableObjectType(typeSymbol, out var inGraphModel))
                {
                    return EmitInGraphMessageRead(inGraphModel, targetExpression, indent);
                }

                // 추상 메시지 타입은 생성 정적 Deserialize 가 없어 위임이 CS0117 을 낸다 — 쓰기 경로와 같은 이유로
                // 런타임 디스패치로 읽고 선언 타입(추상 루트)으로 캐스트한다 (실체 인스턴스는 등록된 구체 요소).
                if (graph.IsMessageType(typeSymbol))
                {
                    return typeSymbol.IsAbstract
                        ? EmitRuntimeDispatchRead(typeSymbol, targetExpression, indent)
                        : EmitOutOfGraphMessageRead(typeSymbol, targetExpression, indent);
                }

                return ReportUnsupported(typeSymbol, state, diagnosticLocation, memberDisplayName);
            }

            static string ReportUnsupported(
                ITypeSymbol typeSymbol,
                EmitState state,
                Location diagnosticLocation,
                string memberDisplayName)
            {
                state.ReportUnsupported(diagnosticLocation, GetTypeDisplayName(typeSymbol), memberDisplayName);
                return string.Empty;
            }

            // ------- 그래프 내부 객체 (참조 추적) -------

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
{indent}    writer.EnterNestedObject();
{indent}    {model.WritePayloadMethodName}(ref writer, {valueExpression}, ref context);
{indent}    writer.LeaveNestedObject();
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
{indent}        reader.EnterNestedObject();
{indent}        var __tmp{uid} = {model.CreateInstanceMethodName}();
{indent}        context.RegisterNewObject(__tmp{uid});
{indent}        {model.PopulatePayloadMethodName}(ref reader, __tmp{uid}, ref context);
{indent}        reader.LeaveNestedObject();
{indent}        {targetExpression} = __tmp{uid};
{indent}    }}
{indent}}}
";
            }

            // ------- 그래프 밖 메시지 (정적 Serialize/Deserialize 위임) -------

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
{indent}    writer.EnterNestedObject();
{indent}    {typeName}.Serialize({valueExpression}, ref writer);
{indent}    writer.LeaveNestedObject();
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
{indent}        reader.EnterNestedObject();
{indent}        {targetExpression} = {typeName}.Deserialize(ref reader);
{indent}        reader.LeaveNestedObject();
{indent}    }}
{indent}}}
";
                }

                return $"{indent}{targetExpression} = {typeName}.Deserialize(ref reader);\n";
            }

            // ------- 런타임 메시지 디스패치 (타입 매개변수·추상 메시지 멤버) -------

            /// <summary>
            /// 런타임 타입 디스패치 쓰기: 전체 메시지(헤더 포함)를 <c>SerializeToWriter</c> 로 쓴다.
            /// 타입 매개변수 멤버와 추상 메시지 타입 멤버가 공유하며, 백레퍼런스 추적은 하지 않는다.
            /// </summary>
            static string EmitRuntimeDispatchWrite(string valueExpression, string indent)
            {
                return $@"{indent}if ({valueExpression} is null)
{indent}{{
{indent}    writer.WriteByte((byte)MessageSerializer.ReferenceKind.Null);
{indent}}}
{indent}else
{indent}{{
{indent}    writer.WriteByte((byte)MessageSerializer.ReferenceKind.NewObject);
{indent}    MessageSerializer.SerializeToWriter({valueExpression}, ref writer);
{indent}}}
";
            }

            /// <summary>런타임 타입 디스패치 읽기: 헤더의 MessageId 로 등록된 구체 타입을 복원하고 선언 타입으로 캐스트한다.</summary>
            static string EmitRuntimeDispatchRead(ITypeSymbol typeSymbol, string targetExpression, string indent)
            {
                int uid = NextUniqueId();
                return $@"{indent}{{
{indent}    byte __pk{uid} = reader.ReadByte();
{indent}    if (__pk{uid} == (byte)MessageSerializer.ReferenceKind.Null)
{indent}    {{
{indent}        {targetExpression} = default;
{indent}    }}
{indent}    else
{indent}    {{
{indent}        {targetExpression} = ({GetTypeDisplayName(typeSymbol)})MessageSerializer.DeserializeFromReader(ref reader);
{indent}    }}
{indent}}}
";
            }

            // ------- 배열 -------
            //
            // 컬렉션 쓰기는 멤버 표현식을 **딱 한 번** 평가해 로컬로 스냅샷한다(`__arr`/`__coll`/`__list` → `__span`/`__count`).
            // null 판정도 스냅샷 로컬로 한다. 두 가지 이유 (Known-Issues KI-26):
            //  ① 일관성 — 길이 접두와 요소를 서로 다른 평가에서 가져오면 프레임이 스스로 모순된다.
            //     계산형 프로퍼티(`public IList<int> Codes => Build();`)에서는 길이가 다른 컬렉션에서 나오고,
            //     두 번째 평가가 null 을 돌려주면 else 분기 안에서 NRE 가 난다(TOCTOU).
            //  ② 비용 — 이전 코드는 `Count`(길이 접두) + `Count`(루프 조건, N+1회) + 인덱서(멤버 접근 N회)로
            //     게터가 2N+2회 돌았다. `CollectionsMarshal` 경로가 이미 스팬으로 스냅샷하던 것과 같은 규약으로 맞춘다
            //     (특히 `CollectionsMarshal` 이 없는 Unity/netstandard2.1 의 `List<T>`·`IList<T>` 에서 효과).

            static string EmitArrayWrite(
                IArrayTypeSymbol arrayType,
                string valueExpression,
                string indent,
                SerializationGraph graph,
                EmitState state,
                Location diagnosticLocation,
                string memberDisplayName)
            {
                var elementType = arrayType.ElementType;
                string elementTypeName = GetTypeDisplayName(elementType);
                int uid = NextUniqueId();

                if (IsBulkCopyable(elementType))
                {
                    return $@"{indent}var __arr{uid} = {valueExpression};
{indent}if (__arr{uid} is null)
{indent}{{
{indent}    writer.WriteInt32(-1);
{indent}}}
{indent}else
{indent}{{
{indent}    writer.WriteInt32(__arr{uid}.Length);
{indent}    if (__arr{uid}.Length > 0)
{indent}    {{
{indent}        writer.WriteBytes(System.Runtime.InteropServices.MemoryMarshal.AsBytes<{elementTypeName}>(__arr{uid}.AsSpan()));
{indent}    }}
{indent}}}
";
                }

                var itemName = $"__item{uid}";
                return $@"{indent}var __arr{uid} = {valueExpression};
{indent}if (__arr{uid} is null)
{indent}{{
{indent}    writer.WriteInt32(-1);
{indent}}}
{indent}else
{indent}{{
{indent}    int __count{uid} = __arr{uid}.Length;
{indent}    writer.WriteInt32(__count{uid});
{indent}    for (int __i{uid} = 0; __i{uid} < __count{uid}; __i{uid}++)
{indent}    {{
{indent}        var {itemName} = __arr{uid}[__i{uid}];
{EmitSerializeValue(elementType, itemName, indent + "        ", graph, state, diagnosticLocation, memberDisplayName)}{indent}    }}
{indent}}}
";
            }

            static string EmitArrayRead(
                IArrayTypeSymbol arrayType,
                string targetExpression,
                string indent,
                SerializationGraph graph,
                EmitState state,
                Location diagnosticLocation,
                string memberDisplayName)
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
{indent}        if ((long)__len{uid} * {size} > reader.Remaining) throw new System.IO.EndOfStreamException(""Collection length prefix exceeds the remaining buffer."");
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
{indent}        if (__len{uid} > reader.Remaining) throw new System.IO.EndOfStreamException(""Collection length prefix exceeds the remaining buffer."");
{indent}        var __arr{uid} = new {elementTypeName}[__len{uid}];
{indent}        for (int __i{uid} = 0; __i{uid} < __len{uid}; __i{uid}++)
{indent}        {{
{indent}            {elementTypeName} {itemName} = default({elementTypeName});
{EmitDeserializeValue(elementType, itemName, indent + "            ", graph, state, diagnosticLocation, memberDisplayName)}{indent}            __arr{uid}[__i{uid}] = {itemName};
{indent}        }}
{indent}        {targetExpression} = __arr{uid};
{indent}    }}
{indent}}}
";
            }

            // ------- List<T> / IList<T> -------

            /// <summary>
            /// CollectionsMarshal 고속 경로는 선언 타입이 정확히 List&lt;T&gt; 일 때만 사용한다
            /// (IList&lt;T&gt; 멤버는 인덱서 루프).
            /// </summary>
            static bool UseCollectionsMarshal(ITypeSymbol containerType, EmitState state)
            {
                return state.HasCollectionsMarshal
                    && containerType is INamedTypeSymbol namedContainer
                    && namedContainer.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>";
            }

            static string EmitListWrite(
                ITypeSymbol containerType,
                ITypeSymbol elementType,
                string valueExpression,
                string indent,
                SerializationGraph graph,
                EmitState state,
                Location diagnosticLocation,
                string memberDisplayName)
            {
                int uid = NextUniqueId();
                bool useCollectionsMarshal = UseCollectionsMarshal(containerType, state);

                if (IsBulkCopyable(elementType))
                {
                    if (useCollectionsMarshal)
                    {
                        return $@"{indent}var __list{uid} = {valueExpression};
{indent}if (__list{uid} is null)
{indent}{{
{indent}    writer.WriteInt32(-1);
{indent}}}
{indent}else
{indent}{{
{indent}    var __span{uid} = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(__list{uid});
{indent}    writer.WriteInt32(__span{uid}.Length);
{indent}    if (__span{uid}.Length > 0)
{indent}    {{
{indent}        writer.WriteBytes(System.Runtime.InteropServices.MemoryMarshal.AsBytes(__span{uid}));
{indent}    }}
{indent}}}
";
                    }

                    var bulkItemName = $"__item{uid}";
                    return $@"{indent}var __coll{uid} = {valueExpression};
{indent}if (__coll{uid} is null)
{indent}{{
{indent}    writer.WriteInt32(-1);
{indent}}}
{indent}else
{indent}{{
{indent}    int __count{uid} = __coll{uid}.Count;
{indent}    writer.WriteInt32(__count{uid});
{indent}    for (int __i{uid} = 0; __i{uid} < __count{uid}; __i{uid}++)
{indent}    {{
{indent}        var {bulkItemName} = __coll{uid}[__i{uid}];
{EmitSerializeValue(elementType, bulkItemName, indent + "        ", graph, state, diagnosticLocation, memberDisplayName)}{indent}    }}
{indent}}}
";
                }

                var itemName = $"__item{uid}";
                if (useCollectionsMarshal)
                {
                    return $@"{indent}var __list{uid} = {valueExpression};
{indent}if (__list{uid} is null)
{indent}{{
{indent}    writer.WriteInt32(-1);
{indent}}}
{indent}else
{indent}{{
{indent}    var __span{uid} = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(__list{uid});
{indent}    writer.WriteInt32(__span{uid}.Length);
{indent}    for (int __i{uid} = 0; __i{uid} < __span{uid}.Length; __i{uid}++)
{indent}    {{
{indent}        var {itemName} = __span{uid}[__i{uid}];
{EmitSerializeValue(elementType, itemName, indent + "        ", graph, state, diagnosticLocation, memberDisplayName)}{indent}    }}
{indent}}}
";
                }

                return $@"{indent}var __coll{uid} = {valueExpression};
{indent}if (__coll{uid} is null)
{indent}{{
{indent}    writer.WriteInt32(-1);
{indent}}}
{indent}else
{indent}{{
{indent}    int __count{uid} = __coll{uid}.Count;
{indent}    writer.WriteInt32(__count{uid});
{indent}    for (int __i{uid} = 0; __i{uid} < __count{uid}; __i{uid}++)
{indent}    {{
{indent}        var {itemName} = __coll{uid}[__i{uid}];
{EmitSerializeValue(elementType, itemName, indent + "        ", graph, state, diagnosticLocation, memberDisplayName)}{indent}    }}
{indent}}}
";
            }

            static string EmitListRead(
                ITypeSymbol elementType,
                string targetExpression,
                string indent,
                SerializationGraph graph,
                EmitState state,
                Location diagnosticLocation,
                string memberDisplayName)
            {
                string elementTypeName = GetTypeDisplayName(elementType);
                int uid = NextUniqueId();

                if (IsBulkCopyable(elementType))
                {
                    int size = GetBulkElementSize(elementType);
                    if (state.HasCollectionsMarshal)
                    {
                        return $@"{indent}{{
{indent}    int __c{uid} = reader.ReadInt32();
{indent}    if (__c{uid} < 0)
{indent}    {{
{indent}        {targetExpression} = null;
{indent}    }}
{indent}    else
{indent}    {{
{indent}        if ((long)__c{uid} * {size} > reader.Remaining) throw new System.IO.EndOfStreamException(""Collection length prefix exceeds the remaining buffer."");
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

                    var bulkItemName = $"__item{uid}";
                    return $@"{indent}{{
{indent}    int __c{uid} = reader.ReadInt32();
{indent}    if (__c{uid} < 0)
{indent}    {{
{indent}        {targetExpression} = null;
{indent}    }}
{indent}    else
{indent}    {{
{indent}        if ((long)__c{uid} * {size} > reader.Remaining) throw new System.IO.EndOfStreamException(""Collection length prefix exceeds the remaining buffer."");
{indent}        var __list{uid} = new System.Collections.Generic.List<{elementTypeName}>(__c{uid});
{indent}        for (int __i{uid} = 0; __i{uid} < __c{uid}; __i{uid}++)
{indent}        {{
{indent}            {elementTypeName} {bulkItemName} = default({elementTypeName});
{EmitDeserializeValue(elementType, bulkItemName, indent + "            ", graph, state, diagnosticLocation, memberDisplayName)}{indent}            __list{uid}.Add({bulkItemName});
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
{indent}        if (__c{uid} > reader.Remaining) throw new System.IO.EndOfStreamException(""Collection length prefix exceeds the remaining buffer."");
{indent}        var __list{uid} = new System.Collections.Generic.List<{elementTypeName}>(__c{uid});
{indent}        for (int __i{uid} = 0; __i{uid} < __c{uid}; __i{uid}++)
{indent}        {{
{indent}            {elementTypeName} {itemName} = default({elementTypeName});
{EmitDeserializeValue(elementType, itemName, indent + "            ", graph, state, diagnosticLocation, memberDisplayName)}{indent}            __list{uid}.Add({itemName});
{indent}        }}
{indent}        {targetExpression} = __list{uid};
{indent}    }}
{indent}}}
";
            }

            // ------- 프리미티브 / enum / string -------

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

            /// <summary>고정 wire size 프리미티브(및 enum). EnsureCapacity 일괄 합산에 사용.</summary>
            public static bool TryGetFixedPrimitiveWireSize(ITypeSymbol typeSymbol, out int size)
            {
                if (typeSymbol.TypeKind == TypeKind.Enum && typeSymbol is INamedTypeSymbol enumType)
                {
                    var underlying = enumType.EnumUnderlyingType;
                    if (underlying != null)
                    {
                        return TryGetFixedPrimitiveWireSize(underlying, out size);
                    }
                    size = 0;
                    return false;
                }

                switch (typeSymbol.SpecialType)
                {
                    case SpecialType.System_Boolean:
                    case SpecialType.System_Byte:
                    case SpecialType.System_SByte:
                        size = 1;
                        return true;
                    case SpecialType.System_Int16:
                    case SpecialType.System_UInt16:
                    case SpecialType.System_Char:
                        size = 2;
                        return true;
                    case SpecialType.System_Int32:
                    case SpecialType.System_UInt32:
                    case SpecialType.System_Single:
                        size = 4;
                        return true;
                    case SpecialType.System_Int64:
                    case SpecialType.System_UInt64:
                    case SpecialType.System_Double:
                        size = 8;
                        return true;
                    case SpecialType.System_Decimal:
                        size = 16;
                        return true;
                    default:
                        size = 0;
                        return false;
                }
            }

            /// <summary>메모리 블록 복사 대상 요소 타입인지 여부 (불리언·문자열·가변 형식 제외).</summary>
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

using MessageProtocol.CodeGenerator.Graph;
using MessageProtocol.CodeGenerator.Metadata;
using Microsoft.CodeAnalysis;

namespace MessageProtocol.CodeGenerator.Generate
{
    internal static partial class MessageSerializeCodeEmitter
    {
        /// <summary>멤버 단위 직렬화·역직렬화 코드 이미터.</summary>
        internal static class Member
        {
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
                    return EmitRuntimeDispatchWrite(valueExpression, indent, state);
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
                    return EmitInGraphMessageWrite(inGraphModel, valueExpression, indent, state);
                }

                // 5) 메시지 타입인데 그래프 밖 (다른 어셈블리 등) — 정적 Serialize 위임.
                //    단, 추상 메시지 타입(abstract [Message(MessageKind.Parent)] 등)은 생성기가 정적 Serialize/Deserialize 를
                //    방출하지 않으므로(MSGPROT010 계열 — 인스턴스 생성 불가) 위임 코드가 소비자 빌드를 CS0117 로 깨뜨린다.
                //    대신 런타임 메시지 디스패치로 *구체* 요소를 헤더째 쓴다 — 파생 멤버 유실 없이 다형성이 복원된다.
                if (graph.IsMessageType(typeSymbol))
                {
                    return typeSymbol.IsAbstract
                        ? EmitRuntimeDispatchWrite(valueExpression, indent, state)
                        : EmitOutOfGraphMessageWrite(typeSymbol, valueExpression, indent, state);
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
                    return EmitRuntimeDispatchRead(typeSymbol, targetExpression, indent, state);
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
                    return EmitInGraphMessageRead(inGraphModel, targetExpression, indent, state);
                }

                // 추상 메시지 타입은 생성 정적 Deserialize 가 없어 위임이 CS0117 을 낸다 — 쓰기 경로와 같은 이유로
                // 런타임 디스패치로 읽고 선언 타입(추상 루트)으로 캐스트한다 (실체 인스턴스는 등록된 구체 요소).
                if (graph.IsMessageType(typeSymbol))
                {
                    return typeSymbol.IsAbstract
                        ? EmitRuntimeDispatchRead(typeSymbol, targetExpression, indent, state)
                        : EmitOutOfGraphMessageRead(typeSymbol, targetExpression, indent, state);
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
            //
            // 참조 추적 3경로(그래프 내부·그래프 밖 위임·런타임 디스패치)의 쓰기·판독 골격은 같은
            // Null/BackReference/NewObject 와이어 프로토콜을 공유한다. 골격과 안내 메시지(KI-34 백레퍼런스
            // 불일치·KI-36 알수없는 태그)는 아래 두 헬퍼가 단일 사실원으로 뿜고, 경로별 차이(등록 순서·
            // null 표현·프레임 호출문·태그 로컬 이름)만 호출부가 전달한다 — 6곳 수작업 복제는 이미 문장
            // 순서 표류(in-graph 는 RegisterObject 먼저, 나머지는 태그 먼저)를 보였다(2026-09-08 구조
            // 감사 FINDING 1). 생성 바이트는 기존과 동일하다(골든 비교로 검증).

            static string EmitTrackedReferenceWrite(string valueExpression, int uid, string indent, string newObjectBody)
            {
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
{newObjectBody}{indent}}}
";
            }

            static string EmitTrackedReferenceRead(string tagLocalName, string typeName, string targetExpression, int uid, string indent, string nullAssignment, string newObjectBody)
            {
                return $@"{indent}{{
{indent}    byte {tagLocalName}{uid} = reader.ReadByte();
{indent}    if ({tagLocalName}{uid} == (byte)MessageSerializer.ReferenceKind.Null)
{indent}    {{
{indent}        {nullAssignment}
{indent}    }}
{indent}    else if ({tagLocalName}{uid} == (byte)MessageSerializer.ReferenceKind.BackReference)
{indent}    {{
{indent}        int __objId{uid} = reader.ReadInt32();
{indent}        var __back{uid} = context.GetObject(__objId{uid});
{indent}        if (!(__back{uid} is {typeName}))
{indent}        {{
{indent}            throw new System.IO.InvalidDataException($""Back-reference {{__objId{uid}}} resolved to '{{__back{uid}.GetType().FullName}}' but member '{targetExpression}' requires '{{typeof({typeName}).FullName}}'. The same instance was first recorded through a member with a less derived static type, so only its base members were written; declare the member as the concrete type or make the base abstract so the concrete element is dispatched at runtime (Known-Issues KI-34)."");
{indent}        }}
{indent}        {targetExpression} = ({typeName})__back{uid};
{indent}    }}
{indent}    else if ({tagLocalName}{uid} != (byte)MessageSerializer.ReferenceKind.NewObject)
{indent}    {{
{indent}        throw new System.IO.InvalidDataException($""Unknown reference kind {{{tagLocalName}{uid}}}; expected Null(0), NewObject(1) or BackReference(2). The payload is corrupt or from an incompatible protocol version."");
{indent}    }}
{indent}    else
{indent}    {{
{newObjectBody}{indent}    }}
{indent}}}
";
            }

            static string EmitInGraphMessageWrite(SerializableTypeModel model, string valueExpression, string indent, EmitState state)
            {
                if (!model.IsReferenceType)
                {
                    return $"{indent}{model.WritePayloadMethodName}(ref writer, {valueExpression}, ref context);\n";
                }

                int uid = state.NextUniqueId();
                string newObjectBody = $@"{indent}    context.RegisterObject({valueExpression});
{indent}    writer.WriteByte((byte)MessageSerializer.ReferenceKind.NewObject);
{indent}    writer.EnterNestedObject();
{indent}    {model.WritePayloadMethodName}(ref writer, {valueExpression}, ref context);
{indent}    writer.LeaveNestedObject();
";
                return EmitTrackedReferenceWrite(valueExpression, uid, indent, newObjectBody);
            }

            static string EmitInGraphMessageRead(SerializableTypeModel model, string targetExpression, string indent, EmitState state)
            {
                if (!model.IsReferenceType)
                {
                    return $"{indent}{targetExpression} = {model.ReadPayloadMethodName}(ref reader, ref context);\n";
                }

                int uid = state.NextUniqueId();
                string newObjectBody = $@"{indent}        reader.EnterNestedObject();
{indent}        var __tmp{uid} = {model.CreateInstanceMethodName}();
{indent}        context.RegisterNewObject(__tmp{uid});
{indent}        {model.PopulatePayloadMethodName}(ref reader, __tmp{uid}, ref context);
{indent}        reader.LeaveNestedObject();
{indent}        {targetExpression} = __tmp{uid};
";
                return EmitTrackedReferenceRead("__refKind", model.TypeName, targetExpression, uid, indent, $"{targetExpression} = null;", newObjectBody);
            }

            // ------- 그래프 밖 메시지 (정적 Serialize/Deserialize 위임) -------

            static string EmitOutOfGraphMessageWrite(ITypeSymbol typeSymbol, string valueExpression, string indent, EmitState state)
            {
                string typeName = GetTypeDisplayName(typeSymbol);
                if (typeSymbol.IsReferenceType)
                {
                    int uid = state.NextUniqueId();
                    string newObjectBody = $@"{indent}    writer.WriteByte((byte)MessageSerializer.ReferenceKind.NewObject);
{indent}    context.RegisterObject({valueExpression});
{indent}    writer.EnterNestedObject();
{indent}    {typeName}.Serialize({valueExpression}, ref writer);
{indent}    writer.LeaveNestedObject();
";
                    return EmitTrackedReferenceWrite(valueExpression, uid, indent, newObjectBody);
                }

                return $"{indent}{typeName}.Serialize({valueExpression}, ref writer);\n";
            }

            static string EmitOutOfGraphMessageRead(ITypeSymbol typeSymbol, string targetExpression, string indent, EmitState state)
            {
                string typeName = GetTypeDisplayName(typeSymbol);
                int uid = state.NextUniqueId();
                if (typeSymbol.IsReferenceType)
                {
                    string newObjectBody = $@"{indent}        reader.EnterNestedObject();
{indent}        {targetExpression} = {typeName}.Deserialize(ref reader);
{indent}        reader.LeaveNestedObject();
{indent}        context.RegisterNewObject({targetExpression}!);
";
                    return EmitTrackedReferenceRead("__nk", typeName, targetExpression, uid, indent, $"{targetExpression} = null;", newObjectBody);
                }

                return $"{indent}{targetExpression} = {typeName}.Deserialize(ref reader);\n";
            }

            // ------- 런타임 메시지 디스패치 (타입 매개변수·추상 메시지 멤버) -------

            /// <summary>
            /// 런타임 타입 디스패치 쓰기: 전체 메시지(헤더 포함)을 <c>SerializeToWriter</c> 로 쓴다.
            /// 타입 매개변수 멤버와 추상 메시지 타입 멤버가 공유한다. 호출측 SerializeContext 의
            /// 오브젝트 id 추적을 그대로 쓴다 — 같은 인스턴스가 두 번 등장하면 두 번째부터 백레퍼런스로
            /// 기록되어 참조 동일성이 복원된다(감사 원장 MEDIUM, 2026-09-05 패스 · KI-9). 프레임 내부는
            /// 여전히 자체 컨텍스트를 쓰므로 프레임 경계를 넘는 공유는 별개 인스턴스로 남는다.
            /// </summary>
            static string EmitRuntimeDispatchWrite(string valueExpression, string indent, EmitState state)
            {
                int uid = state.NextUniqueId();
                string newObjectBody = $@"{indent}    writer.WriteByte((byte)MessageSerializer.ReferenceKind.NewObject);
{indent}    context.RegisterObject({valueExpression});
{indent}    MessageSerializer.SerializeToWriter({valueExpression}, ref writer);
";
                return EmitTrackedReferenceWrite(valueExpression, uid, indent, newObjectBody);
            }

            /// <summary>
            /// 런타임 타입 디스패치 읽기: 헤더의 MessageId 로 등록된 구체 타입을 복원하고 선언 타입으로 캐스트한다.
            /// 쓰기와 대칭으로 백레퍼런스를 역참조하고, 복원된 인스턴스를 호출측 컨텍스트에 등록한다 —
            /// 쓰기는 프레임 앞에서·읽기는 프레임 뒤에서 등록하지만 그 사이 외부 컨텍스트 등록은 없으므로
            /// id 순서는 양측이 일치한다(KI-9 해소).
            /// </summary>
            static string EmitRuntimeDispatchRead(ITypeSymbol typeSymbol, string targetExpression, string indent, EmitState state)
            {
                int uid = state.NextUniqueId();
                string typeName = GetTypeDisplayName(typeSymbol);
                // 디스패치 복원 객체를 선언 타입으로 블라인드 캐스트하지 않는다(KI-41, 2026-09-08 퍼저 발견):
                // 불신 헤더가 다른 등록 타입으로 라우팅하면 InvalidCastException 이 원인 없이 터졌다 —
                // 백레퍼런스 분기(KI-34)와 같은 계열의 안내 검사로 교정한다.
                string newObjectBody = $@"{indent}        var __dispatched{uid} = MessageSerializer.DeserializeFromReader(ref reader);
{indent}        if (!(__dispatched{uid} is {typeName}))
{indent}        {{
{indent}            throw new System.IO.InvalidDataException($""Dispatched wire element resolved to '{{__dispatched{uid}.GetType().FullName}}' but member '{targetExpression}' requires '{{typeof({typeName}).FullName}}'. The payload is corrupt or from an incompatible peer."");
{indent}        }}
{indent}        {targetExpression} = ({typeName})__dispatched{uid};
{indent}        context.RegisterNewObject({targetExpression}!);
";
                return EmitTrackedReferenceRead("__pk", typeName, targetExpression, uid, indent, $"{targetExpression} = default;", newObjectBody);
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
                int uid = state.NextUniqueId();

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
                int uid = state.NextUniqueId();

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
                int uid = state.NextUniqueId();
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
                int uid = state.NextUniqueId();

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

            /// <summary>
            /// 원시형 단일 사실원 표 — 읽기 식·쓰기 호출 포맷·고정 wire 크기·벌크 복사 크기를 한 곳에 둔다.
            /// 과거 4개의 독립 스위치(읽기·쓰기·고정 크기·벌크 크기)는 프리미티브 하나 고칠 때 4곳을
            /// 맞춰 고쳐야 했고, 하나라도 어긋나면 읽기·쓰기가 조용히 불일치하는 와이어 표류 버그 클래스였다
            /// (2026-09-08 구조 감사 FINDING 2). 문자열은 가변 길이라 고정·벌크 모두 -1, 불리언(패킹 불가)과
            /// decimal(20바이트 표현 가능 — 런타임 16바이트 GetBits 고정과 달라 안전하지 않음)은 고정 크기만 있다.
            /// </summary>
            static readonly System.Collections.Generic.Dictionary<SpecialType, (string Read, string WriteFormat, int FixedSize, int BulkSize)> PrimitiveWireTable =
                new System.Collections.Generic.Dictionary<SpecialType, (string, string, int, int)>
            {
                [SpecialType.System_Boolean]  = ("reader.ReadBoolean()",  "writer.WriteBoolean({0})",  1, -1),
                [SpecialType.System_Byte]     = ("reader.ReadByte()",     "writer.WriteByte({0})",     1,  1),
                [SpecialType.System_SByte]    = ("reader.ReadSByte()",    "writer.WriteSByte({0})",    1,  1),
                [SpecialType.System_Int16]    = ("reader.ReadInt16()",    "writer.WriteInt16({0})",    2,  2),
                [SpecialType.System_UInt16]   = ("reader.ReadUInt16()",   "writer.WriteUInt16({0})",   2,  2),
                [SpecialType.System_Char]     = ("reader.ReadChar()",     "writer.WriteChar({0})",     2,  2),
                [SpecialType.System_Int32]    = ("reader.ReadInt32()",    "writer.WriteInt32({0})",    4,  4),
                [SpecialType.System_UInt32]   = ("reader.ReadUInt32()",   "writer.WriteUInt32({0})",   4,  4),
                [SpecialType.System_Single]   = ("reader.ReadSingle()",   "writer.WriteSingle({0})",   4,  4),
                [SpecialType.System_Int64]    = ("reader.ReadInt64()",    "writer.WriteInt64({0})",    8,  8),
                [SpecialType.System_UInt64]   = ("reader.ReadUInt64()",   "writer.WriteUInt64({0})",   8,  8),
                [SpecialType.System_Double]   = ("reader.ReadDouble()",   "writer.WriteDouble({0})",   8,  8),
                [SpecialType.System_Decimal]  = ("reader.ReadDecimal()",  "writer.WriteDecimal({0})", 16, -1),
                [SpecialType.System_String]   = ("reader.ReadString()",   "writer.WriteString({0})",  -1, -1),
            };

            static bool TryGetPrimitiveWriteCall(ITypeSymbol typeSymbol, string expression, out string call)
            {
                if (PrimitiveWireTable.TryGetValue(typeSymbol.SpecialType, out var info))
                {
                    call = string.Format(info.WriteFormat, expression);
                    return true;
                }
                call = string.Empty;
                return false;
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
                if (PrimitiveWireTable.TryGetValue(typeSymbol.SpecialType, out var info))
                {
                    expression = info.Read;
                    return true;
                }
                expression = string.Empty;
                return false;
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

                if (PrimitiveWireTable.TryGetValue(typeSymbol.SpecialType, out var info) && info.FixedSize > 0)
                {
                    size = info.FixedSize;
                    return true;
                }
                size = 0;
                return false;
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

                return PrimitiveWireTable.TryGetValue(typeSymbol.SpecialType, out var info) ? info.BulkSize : -1;
            }
        }
    }
}

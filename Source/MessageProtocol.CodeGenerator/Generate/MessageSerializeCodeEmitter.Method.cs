using MessageProtocol.CodeGenerator.Graph;
using MessageProtocol.CodeGenerator.Metadata;
using MessageProtocol;
using Microsoft.CodeAnalysis;
using System.Text;

namespace MessageProtocol.CodeGenerator.Generate
{
    internal static partial class MessageSerializeCodeEmitter
    {
        /// <summary>Serialize / Deserialize / ModuleInitializer / 그래프 헬퍼 메서드 이미터.</summary>
        internal static class Method
        {
            public static string EmitOnModuleInitialize(TypeMetadata typeMeta, string indent, IAssemblySymbol? consumerAssembly)
            {
                // Initialize 는 internal 이라 가릴 대상이 기본적으로 같은 어셈블리에만 존재한다 — 어셈블리 밖 베이스에서는
                // new 를 붙이지 않되, 베이스 어셈블리가 InternalsVisibleTo 로 접근을 여는 경우는 제외한다.
                string staticHidingModifier = GetStaticHidingModifier(typeMeta, isModuleInitializer: true, consumerAssembly);
                string typeName = typeMeta.Symbol.Name;
                bool hasId = typeMeta.IsStandaloneMessage || typeMeta.IsGroupMessage;

                var sb = new StringBuilder();
                sb.AppendLine($@"
{indent}[ModuleInitializer]
{indent}internal {staticHidingModifier}static void Initialize()
{indent}{{");
                if (hasId)
                {
                    // 델리게이트·MessageId 직접 전달 → SerializerCache 리플렉션 생략.
                    sb.AppendLine($@"{indent}    MessageSerializer.RegisterHasIdMessage<{typeName}>({typeName}.Serialize, {typeName}.Deserialize, {typeName}.MessageId);");
                }
                else
                {
                    sb.AppendLine($@"{indent}    MessageSerializer.RegisterNonIdMessage<{typeName}>({typeName}.Serialize, {typeName}.Deserialize);");
                }
                sb.AppendLine($@"{indent}}}");
                return sb.ToString();
            }

            /// <summary>
            /// 와이어 MessageId 4바이트(빅엔디언) 분해 — EmitSerialize 가 바이크하는 값과 EmitDeserialize 가
            /// 검증하는 값이 같은 분해에서 나와야 한다. 두 곳이 따로 분해하면 id 레이아웃 변경 시 한쪽만 고쳐
            /// 쓰는 바이트를 읽는 쪽이 거부하는 자기부정 버그가 생긴다(2026-09-08 구조 감사 FINDING 5).
            /// </summary>
            static (byte Header, byte B1, byte B2, byte B3) DecomposeWireId(uint messageId)
            {
                return ((byte)(messageId >> 24), (byte)(messageId >> 16), (byte)(messageId >> 8), (byte)messageId);
            }

            public static string EmitSerialize(TypeMetadata typeMeta, string indent, SerializationGraph graph)
            {
                var rootModel = graph.RootType;
                uint id = typeMeta.GetMessageId();
                var (headerByte, idB1, idB2, idB3) = DecomposeWireId(id);
                bool hasEmbeddedId = typeMeta.IsStandaloneMessage || typeMeta.IsGroupMessage;

                var sb = new StringBuilder();

                // Hot path: writer 기반
                sb.AppendLine($@"public static void Serialize({typeMeta.DeclarationName} message, ref MessageBufferWriter writer)");
                sb.AppendLine($@"{indent}{{");
                if (rootModel.IsReferenceType)
                {
                    sb.AppendLine($@"{indent}    if (message is null) throw new ArgumentNullException(nameof(message));");
                }
                sb.AppendLine($@"{indent}    writer.WriteByte(0x{headerByte:X2});");
                if (hasEmbeddedId)
                {
                    sb.AppendLine($@"{indent}    writer.WriteByte(0x{idB1:X2});");
                    sb.AppendLine($@"{indent}    writer.WriteByte(0x{idB2:X2});");
                    sb.AppendLine($@"{indent}    writer.WriteByte(0x{idB3:X2});");
                }
                if (typeMeta.IsGenericWireMessage)
                {
                    // 헤더 뒤 구성 클래스 ID 3바이트 — 클래스 ID는 런타임 레지스트리에서 조회 (구성 미선언 시 예외).
                    sb.AppendLine($@"{indent}    uint __classId = MessageSerializer.GetGenericClassId<{typeMeta.DeclarationName}>();");
                    sb.AppendLine($@"{indent}    if (__classId == 0) throw new InvalidOperationException(""This generic construction is not registered for serialization; declare it with [GenericMessage(typeof({typeMeta.DeclarationName}), ClassId = n)] on the declaration or any carrier type, or call MessageSerializer.RegisterGenericConstruction at startup."");");
                    sb.AppendLine($@"{indent}    writer.WriteByte((byte)(__classId >> 16));");
                    sb.AppendLine($@"{indent}    writer.WriteByte((byte)(__classId >> 8));");
                    sb.AppendLine($@"{indent}    writer.WriteByte((byte)__classId);");
                }
                sb.AppendLine($@"{indent}    var __context = default(MessageSerializer.SerializeContext);");
                if (rootModel.IsReferenceType)
                {
                    sb.AppendLine($@"{indent}    __context.RegisterObject(message);");
                }
                sb.AppendLine($@"{indent}    {rootModel.WritePayloadMethodName}(ref writer, message, ref __context);");
                sb.AppendLine($@"{indent}}}");
                sb.AppendLine();

                // Compat: byte[] 반환
                sb.AppendLine($@"{indent}public static byte[] Serialize({typeMeta.DeclarationName} message)");
                sb.AppendLine($@"{indent}{{");
                if (rootModel.IsReferenceType)
                {
                    sb.AppendLine($@"{indent}    if (message is null) throw new ArgumentNullException(nameof(message));");
                }
                sb.AppendLine($@"{indent}    var __writer = MessageBufferWriter.Create();");
                sb.AppendLine($@"{indent}    try");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        Serialize(message, ref __writer);");
                sb.AppendLine($@"{indent}        return __writer.ToArray();");
                sb.AppendLine($@"{indent}    }}");
                sb.AppendLine($@"{indent}    finally");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        __writer.Dispose();");
                sb.AppendLine($@"{indent}    }}");
                sb.AppendLine($@"{indent}}}");

                return sb.ToString();
            }

            public static string EmitDeserialize(TypeMetadata typeMeta, string indent, SerializationGraph graph)
            {
                var rootModel = graph.RootType;
                string staticHidingModifier = GetStaticHidingModifier(typeMeta);

                // 검증 상수 — EmitSerialize 가 바이크하는 것과 같은 MessageId 4바이트(빅엔디언).
                // 다른 타입의 바이트를 먹이면 페이로드를 조용히 재해석하던 결함(KI-5)을 프레임 진입에서 차단한다.
                uint expectedId = typeMeta.GetMessageId();
                var (expectedHeader, expectedB1, expectedB2, expectedB3) = DecomposeWireId(expectedId);
                string typeName = typeMeta.DeclarationName;

                var sb = new StringBuilder();

                // Hot path: reader 기반
                sb.AppendLine($@"public {staticHidingModifier}static {typeMeta.DeclarationName} Deserialize(ref MessageBufferReader reader)");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    byte __headerByte = reader.ReadByte();");
                // 헤더 규칙은 공용 단일 사실원을 호출한다 — 인라인 비트 재구현이 와이어 규칙과 어긋나는 것을
                // 구조적으로 불가능하게 만든다(감사 원장 LOW, 2026-09-08). 분기는 **프레임 바이트(런타임) 기준**으로
                // 판정한다 — 불신 바이트가 NonId 플래그를 주장하면 4바이트 읽기를 건너뛰고 아래 비교에서 거부된다.
                sb.AppendLine($@"{indent}    if (MessageProtocol.MessageWireFormat.HasEmbeddedMessageId(__headerByte))");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        byte __idB1 = reader.ReadByte();");
                sb.AppendLine($@"{indent}        byte __idB2 = reader.ReadByte();");
                sb.AppendLine($@"{indent}        byte __idB3 = reader.ReadByte();");
                sb.AppendLine($@"{indent}        if (__headerByte != 0x{expectedHeader:X2} || __idB1 != 0x{expectedB1:X2} || __idB2 != 0x{expectedB2:X2} || __idB3 != 0x{expectedB3:X2})");
                sb.AppendLine($@"{indent}        {{");
                sb.AppendLine($@"{indent}            throw new System.IO.InvalidDataException($""Wire header {{__headerByte:X2}} {{__idB1:X2}} {{__idB2:X2}} {{__idB3:X2}} does not match {typeName} (expected MessageId 0x{expectedId:X8}); the bytes belong to a different message type or are corrupt."");");
                sb.AppendLine($@"{indent}        }}");
                sb.AppendLine($@"{indent}    }}");
                sb.AppendLine($@"{indent}    else if (__headerByte != 0x{expectedHeader:X2})");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        throw new System.IO.InvalidDataException($""Wire header {{__headerByte:X2}} does not match {typeName} (expected 0x{expectedHeader:X2}); the bytes belong to a different message type or are corrupt."");");
                sb.AppendLine($@"{indent}    }}");
                if (typeMeta.IsGenericWireMessage)
                {
                    // 구성 클래스 ID 3바이트 소비 (라우팅이 이미 사용).
                    sb.AppendLine($@"{indent}    reader.ReadByte();");
                    sb.AppendLine($@"{indent}    reader.ReadByte();");
                    sb.AppendLine($@"{indent}    reader.ReadByte();");
                }
                sb.AppendLine($@"{indent}    var __context = default(MessageSerializer.DeserializeContext);");
                if (rootModel.IsReferenceType)
                {
                    sb.AppendLine($@"{indent}    var result = {rootModel.CreateInstanceMethodName}();");
                    sb.AppendLine($@"{indent}    __context.RegisterNewObject(result);");
                    sb.AppendLine($@"{indent}    {rootModel.PopulatePayloadMethodName}(ref reader, result, ref __context);");
                    sb.AppendLine($@"{indent}    return result;");
                }
                else
                {
                    sb.AppendLine($@"{indent}    return {rootModel.ReadPayloadMethodName}(ref reader, ref __context);");
                }
                sb.AppendLine($@"{indent}}}");
                sb.AppendLine();

                // Compat: byte[] 입력
                sb.AppendLine($@"{indent}public {staticHidingModifier}static {typeMeta.DeclarationName} Deserialize(byte[] data)");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    if (data is null) throw new ArgumentNullException(nameof(data));");
                sb.AppendLine($@"{indent}    var __reader = new MessageBufferReader(data);");
                sb.AppendLine($@"{indent}    return Deserialize(ref __reader);");
                sb.AppendLine($@"{indent}}}");

                return sb.ToString();
            }

            public static string EmitHelperMethods(string indent, SerializationGraph graph, EmitState state)
            {
                var sb = new StringBuilder();
                sb.Append(EmitTypeMethods(graph.RootType, indent, graph, state));

                foreach (var typeModel in graph.ReachableTypes)
                {
                    if (ReferenceEquals(typeModel, graph.RootType))
                    {
                        continue;
                    }

                    sb.AppendLine();
                    sb.Append(EmitTypeMethods(typeModel, indent, graph, state));
                }

                return sb.ToString();
            }

            static string EmitTypeMethods(SerializableTypeModel typeModel, string indent, SerializationGraph graph, EmitState state)
            {
                bool isRootType = ReferenceEquals(typeModel, graph.RootType);
                return typeModel.IsReferenceType
                    ? EmitReferenceTypeMethods(typeModel, indent, graph, state, isRootType)
                    : EmitValueTypeMethods(typeModel, indent, graph, state, isRootType);
            }

            static string EmitReferenceTypeMethods(SerializableTypeModel typeModel, string indent, SerializationGraph graph, EmitState state, bool isRootType)
            {
                var sb = new StringBuilder();

                sb.AppendLine($@"private static {typeModel.TypeName} {typeModel.CreateInstanceMethodName}()");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    return new {typeModel.TypeName}();");
                sb.AppendLine($@"{indent}}}");
                sb.AppendLine();

                sb.AppendLine($@"{indent}private static void {typeModel.WritePayloadMethodName}(ref MessageBufferWriter writer, {typeModel.TypeName} message, ref MessageSerializer.SerializeContext context)");
                sb.AppendLine($@"{indent}{{");
                AppendWritePayloadBody(sb, typeModel, indent + "    ", graph, state);
                sb.AppendLine($@"{indent}}}");
                sb.AppendLine();

                sb.AppendLine($@"{indent}private static void {typeModel.PopulatePayloadMethodName}(ref MessageBufferReader reader, {typeModel.TypeName} result, ref MessageSerializer.DeserializeContext context)");
                sb.AppendLine($@"{indent}{{");
                foreach (var member in TypeMetadata.GetWireMembers(typeModel.Metadata))
                {
                    sb.Append(Member.EmitDeserialize(member, "result", indent + "    ", graph, state, isRootType));
                }
                sb.AppendLine($@"{indent}}}");

                return sb.ToString();
            }

            static string EmitValueTypeMethods(SerializableTypeModel typeModel, string indent, SerializationGraph graph, EmitState state, bool isRootType)
            {
                var sb = new StringBuilder();

                sb.AppendLine($@"private static void {typeModel.WritePayloadMethodName}(ref MessageBufferWriter writer, {typeModel.TypeName} message, ref MessageSerializer.SerializeContext context)");
                sb.AppendLine($@"{indent}{{");
                AppendWritePayloadBody(sb, typeModel, indent + "    ", graph, state);
                sb.AppendLine($@"{indent}}}");
                sb.AppendLine();

                sb.AppendLine($@"{indent}private static {typeModel.TypeName} {typeModel.ReadPayloadMethodName}(ref MessageBufferReader reader, ref MessageSerializer.DeserializeContext context)");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    var result = default({typeModel.TypeName});");
                foreach (var member in TypeMetadata.GetWireMembers(typeModel.Metadata))
                {
                    sb.Append(Member.EmitDeserialize(member, "result", indent + "    ", graph, state, isRootType));
                }
                sb.AppendLine($@"{indent}    return result;");
                sb.AppendLine($@"{indent}}}");

                return sb.ToString();
            }

            /// <summary>고정 크기 프리미티브 구간 합산으로 EnsureCapacity 1회 호출 후 멤버를 순서대로 쓴다.</summary>
            static void AppendWritePayloadBody(
                StringBuilder sb,
                SerializableTypeModel typeModel,
                string indent,
                SerializationGraph graph,
                EmitState state)
            {
                int fixedSize = 0;
                foreach (var member in TypeMetadata.GetWireMembers(typeModel.Metadata))
                {
                    if (Member.TryGetFixedPrimitiveWireSize(member.Type, out int size))
                    {
                        fixedSize += size;
                    }
                }

                if (fixedSize > 0)
                {
                    sb.AppendLine($@"{indent}writer.EnsureCapacity({fixedSize});");
                }

                foreach (var member in TypeMetadata.GetWireMembers(typeModel.Metadata))
                {
                    sb.Append(Member.EmitSerialize(member, "message", indent, graph, state));
                }
            }
        }
    }
}

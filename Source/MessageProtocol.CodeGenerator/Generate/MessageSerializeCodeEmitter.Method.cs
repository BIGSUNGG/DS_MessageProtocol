using MessageProtocol.CodeGenerator.Graph;
using MessageProtocol.CodeGenerator.Metadata;
using MessageProtocol;
using System.Text;

namespace MessageProtocol.CodeGenerator.Generate
{
    internal static partial class MessageSerializeCodeEmitter
    {
        /// <summary>Serialize / Deserialize / ModuleInitializer / 그래프 헬퍼 메서드 이미터.</summary>
        internal static class Method
        {
            public static string EmitOnModuleInitialize(TypeMetadata typeMeta, string indent)
            {
                string staticHidingModifier = GetStaticHidingModifier(typeMeta);
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

            public static string EmitSerialize(TypeMetadata typeMeta, string indent, SerializationGraph graph)
            {
                var rootModel = graph.RootType;
                uint id = typeMeta.GetMessageId();
                byte headerByte = (byte)(id >> 24);
                byte idB1 = (byte)(id >> 16);
                byte idB2 = (byte)(id >> 8);
                byte idB3 = (byte)id;
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
                if (typeMeta.IsGenericMessageDeclaration)
                {
                    // 헤더 뒤 구성 클래스 ID 3바이트 (미등록 구성은 직렬화 불가).
                    sb.AppendLine($@"{indent}    if (__GenericClassId == 0) throw new InvalidOperationException(""This generic construction is not registered for serialization; declare it with [GenericMessage(..., ClassId = n)]."");");
                    sb.AppendLine($@"{indent}    writer.WriteByte((byte)(__GenericClassId >> 16));");
                    sb.AppendLine($@"{indent}    writer.WriteByte((byte)(__GenericClassId >> 8));");
                    sb.AppendLine($@"{indent}    writer.WriteByte((byte)__GenericClassId);");
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

                var sb = new StringBuilder();

                // Hot path: reader 기반
                sb.AppendLine($@"public {staticHidingModifier}static {typeMeta.DeclarationName} Deserialize(ref MessageBufferReader reader)");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    byte __headerByte = reader.ReadByte();");
                sb.AppendLine($@"{indent}    if ((__headerByte & {((byte)MessageFlag.NonIdMessage) << 4}) == 0)");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        reader.ReadByte();");
                sb.AppendLine($@"{indent}        reader.ReadByte();");
                sb.AppendLine($@"{indent}        reader.ReadByte();");
                sb.AppendLine($@"{indent}    }}");
                if (typeMeta.IsGenericMessageDeclaration)
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
                return typeModel.IsReferenceType
                    ? EmitReferenceTypeMethods(typeModel, indent, graph, state)
                    : EmitValueTypeMethods(typeModel, indent, graph, state);
            }

            static string EmitReferenceTypeMethods(SerializableTypeModel typeModel, string indent, SerializationGraph graph, EmitState state)
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
                foreach (var member in GetAllMembers(typeModel.Metadata))
                {
                    sb.Append(Member.EmitDeserialize(member, "result", indent + "    ", graph, state));
                }
                sb.AppendLine($@"{indent}}}");

                return sb.ToString();
            }

            static string EmitValueTypeMethods(SerializableTypeModel typeModel, string indent, SerializationGraph graph, EmitState state)
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
                foreach (var member in GetAllMembers(typeModel.Metadata))
                {
                    sb.Append(Member.EmitDeserialize(member, "result", indent + "    ", graph, state));
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
                foreach (var member in GetAllMembers(typeModel.Metadata))
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

                foreach (var member in GetAllMembers(typeModel.Metadata))
                {
                    sb.Append(Member.EmitSerialize(member, "message", indent, graph, state));
                }
            }

            static string GetStaticHidingModifier(TypeMetadata typeMeta)
            {
                var baseType = typeMeta.BaseTypeMetadata;
                if (baseType == null)
                {
                    return string.Empty;
                }

                return baseType.IsNonIdMessage || baseType.IsStandaloneMessage || baseType.IsGroupMessage
                    ? "new "
                    : string.Empty;
            }
        }
    }
}

using MessageProtocol.CodeGenerator.Metadata;
using MessageProtocol.CodeGenerator.Graph;
using System.Text;

namespace MessageProtocol.CodeGenerator.Generate
{
    internal static partial class MessageSerializeCodeEmitter
    {
        // Method: 직렬화, 역직렬화 함수 추가
        internal static class Method
        {
            public static string EmitOnModuleInitialize(TypeMetadata typeMeta, string indent)
            {
                string staticHidingModifier = GetStaticHidingModifier(typeMeta);
                string registerMethod = (typeMeta.IsStandaloneMessage || typeMeta.IsGroupMessage)
                    ? "RegisterHasIdMessage"
                    : "RegisterNonIdMessage";

                var sb = new StringBuilder();
                sb.AppendLine($@"
{indent}[ModuleInitializer]
{indent}internal {staticHidingModifier}static void Initialize()
{indent}{{
{indent}    MessageSerializer.{registerMethod}<{typeMeta.Symbol.Name}>();
{indent}}}");
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
                sb.AppendLine($@"public static void Serialize({typeMeta.Symbol.Name} message, ref MessageBufferWriter writer)");
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
                sb.AppendLine($@"{indent}    var __context = default(MessageSerializer.SerializeContext);");
                if (rootModel.IsReferenceType)
                {
                    sb.AppendLine($@"{indent}    __context.RegisterObject(message);");
                }
                sb.AppendLine($@"{indent}    {rootModel.WritePayloadMethodName}(ref writer, message, ref __context);");
                sb.AppendLine($@"{indent}}}");
                sb.AppendLine();

                // Compat: byte[] 반환
                sb.AppendLine($@"{indent}public static byte[] Serialize({typeMeta.Symbol.Name} message)");
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
                sb.AppendLine($@"public {staticHidingModifier}static {typeMeta.Symbol.Name} Deserialize(ref MessageBufferReader reader)");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    byte __headerByte = reader.ReadByte();");
                sb.AppendLine($@"{indent}    if ((__headerByte & {((byte)MessageFlag.NonIdMessage) << 4}) == 0)");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        reader.ReadByte();");
                sb.AppendLine($@"{indent}        reader.ReadByte();");
                sb.AppendLine($@"{indent}        reader.ReadByte();");
                sb.AppendLine($@"{indent}    }}");
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
                sb.AppendLine($@"{indent}public {staticHidingModifier}static {typeMeta.Symbol.Name} Deserialize(byte[] data)");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    if (data is null) throw new ArgumentNullException(nameof(data));");
                sb.AppendLine($@"{indent}    var __reader = new MessageBufferReader(data);");
                sb.AppendLine($@"{indent}    return Deserialize(ref __reader);");
                sb.AppendLine($@"{indent}}}");

                return sb.ToString();
            }

            public static string EmitHelperMethods(string indent, SerializationGraph graph)
            {
                var sb = new StringBuilder();
                sb.Append(EmitTypeMethods(graph.RootType, indent, graph));

                foreach (var typeModel in graph.ReachableTypes)
                {
                    if (ReferenceEquals(typeModel, graph.RootType))
                    {
                        continue;
                    }

                    sb.AppendLine();
                    sb.Append(EmitTypeMethods(typeModel, indent, graph));
                }

                return sb.ToString();
            }

            static string EmitTypeMethods(SerializableTypeModel typeModel, string indent, SerializationGraph graph)
            {
                return typeModel.IsReferenceType
                    ? EmitReferenceTypeMethods(typeModel, indent, graph)
                    : EmitValueTypeMethods(typeModel, indent, graph);
            }

            static string EmitReferenceTypeMethods(SerializableTypeModel typeModel, string indent, SerializationGraph graph)
            {
                var sb = new StringBuilder();

                // CreateInstance
                sb.AppendLine($@"private static {typeModel.TypeName} {typeModel.CreateInstanceMethodName}()");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    return new {typeModel.TypeName}();");
                sb.AppendLine($@"{indent}}}");
                sb.AppendLine();

                // WritePayload
                sb.AppendLine($@"{indent}private static void {typeModel.WritePayloadMethodName}(ref MessageBufferWriter writer, {typeModel.TypeName} message, ref MessageSerializer.SerializeContext context)");
                sb.AppendLine($@"{indent}{{");
                foreach (var member in GetAllMembers(typeModel.Metadata))
                {
                    sb.Append(Member.EmitSerialize(member, "message", indent + "    ", graph));
                }
                sb.AppendLine($@"{indent}}}");
                sb.AppendLine();

                // PopulatePayload
                sb.AppendLine($@"{indent}private static void {typeModel.PopulatePayloadMethodName}(ref MessageBufferReader reader, {typeModel.TypeName} result, ref MessageSerializer.DeserializeContext context)");
                sb.AppendLine($@"{indent}{{");
                foreach (var member in GetAllMembers(typeModel.Metadata))
                {
                    sb.Append(Member.EmitDeserialize(member, "result", indent + "    ", graph));
                }
                sb.AppendLine($@"{indent}}}");

                return sb.ToString();
            }

            static string EmitValueTypeMethods(SerializableTypeModel typeModel, string indent, SerializationGraph graph)
            {
                var sb = new StringBuilder();

                // WritePayload
                sb.AppendLine($@"private static void {typeModel.WritePayloadMethodName}(ref MessageBufferWriter writer, {typeModel.TypeName} message, ref MessageSerializer.SerializeContext context)");
                sb.AppendLine($@"{indent}{{");
                foreach (var member in GetAllMembers(typeModel.Metadata))
                {
                    sb.Append(Member.EmitSerialize(member, "message", indent + "    ", graph));
                }
                sb.AppendLine($@"{indent}}}");
                sb.AppendLine();

                // ReadPayload (returns new instance)
                sb.AppendLine($@"{indent}private static {typeModel.TypeName} {typeModel.ReadPayloadMethodName}(ref MessageBufferReader reader, ref MessageSerializer.DeserializeContext context)");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    var result = default({typeModel.TypeName});");
                foreach (var member in GetAllMembers(typeModel.Metadata))
                {
                    sb.Append(Member.EmitDeserialize(member, "result", indent + "    ", graph));
                }
                sb.AppendLine($@"{indent}    return result;");
                sb.AppendLine($@"{indent}}}");

                return sb.ToString();
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

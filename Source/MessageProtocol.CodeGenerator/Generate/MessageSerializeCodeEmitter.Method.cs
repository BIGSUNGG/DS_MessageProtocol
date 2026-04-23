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
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($@"
{indent}[ModuleInitializer]
{indent}internal {staticHidingModifier}static void Initialize()
{indent}{{
{indent}    MessageSerializer.RegisterType(typeof({typeMeta.Symbol.Name}));
{indent}}}");

                return sb.ToString();
            }

            public static string EmitSerialize(TypeMetadata typeMeta, string indent, SerializationGraph serializationGraph)
            {
                var rootModel = serializationGraph.RootType;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($@"public static byte[] Serialize({typeMeta.Symbol.Name} message)");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    using (var ms = new MemoryStream({MessageWireFormat.DefaultStreamCapacity}))");
                sb.AppendLine($@"{indent}    using (var writer = new BinaryWriter(ms))");
                sb.AppendLine($@"{indent}    {{");
                
                // id 구성: 앞 8비트(standaloneId) + 그 다음 8비트(groupRootId) + 그 다음 16비트(elementId)
                uint id = typeMeta.GetMessageId();

                sb.AppendLine($@"{indent}        uint id = {id};");
                sb.AppendLine($@"{indent}        byte headerByte = (byte)(id >> 24);");
                sb.AppendLine($@"{indent}        writer.Write(headerByte);");
                sb.AppendLine($@"{indent}        if ((headerByte & {((byte)MessageFlag.NonIdMessage) << 4}) == 0)");
                sb.AppendLine($@"{indent}        {{");
                sb.AppendLine($@"{indent}            writer.Write((byte)(id >> 16));");
                sb.AppendLine($@"{indent}            writer.Write((byte)(id >> 8));");
                sb.AppendLine($@"{indent}            writer.Write((byte)id);");
                sb.AppendLine($@"{indent}        }}");
                sb.AppendLine($@"{indent}        var context = new MessageSerializer.SerializeContext();");
                if (rootModel.IsReferenceType)
                {
                    sb.AppendLine($@"{indent}        context.RegisterObject(message);");
                }
                sb.AppendLine($@"{indent}        {rootModel.WritePayloadMethodName}(writer, message, context);");
                sb.AppendLine($@"{indent}        return ms.ToArray();");
                sb.AppendLine($@"{indent}    }}");
                sb.AppendLine($@"{indent}}}");

                return sb.ToString();
            }

            public static string EmitDeserialize(TypeMetadata typeMeta, string indent, SerializationGraph serializationGraph)
            {
                var rootModel = serializationGraph.RootType;
                string staticHidingModifier = GetStaticHidingModifier(typeMeta);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($@"public {staticHidingModifier}static {typeMeta.Symbol.Name} Deserialize(byte[] data)");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    using (var ms = new MemoryStream(data))");
                sb.AppendLine($@"{indent}    using (var reader = new BinaryReader(ms))");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        byte headerByte = reader.ReadByte();");
                sb.AppendLine($@"{indent}        uint id = (uint)headerByte << 24;");
                sb.AppendLine($@"{indent}        if ((headerByte & {((byte)MessageFlag.NonIdMessage) << 4}) == 0)");
                sb.AppendLine($@"{indent}        {{");
                sb.AppendLine($@"{indent}            id |= (uint)reader.ReadByte() << 16;");
                sb.AppendLine($@"{indent}            id |= (uint)reader.ReadByte() << 8;");
                sb.AppendLine($@"{indent}            id |= reader.ReadByte();");
                sb.AppendLine($@"{indent}        }}");
                sb.AppendLine($@"{indent}        var context = new MessageSerializer.DeserializeContext();");
                if (rootModel.IsReferenceType)
                {
                    sb.AppendLine($@"{indent}        var result = {rootModel.CreateInstanceMethodName}();");
                    sb.AppendLine($@"{indent}        context.RegisterObject(1, result);");
                    sb.AppendLine($@"{indent}        {rootModel.PopulatePayloadMethodName}(reader, result, context);");
                    sb.AppendLine($@"{indent}        return result;");
                }
                else
                {
                    sb.AppendLine($@"{indent}        return {rootModel.ReadPayloadMethodName}(reader, context);");
                }
                sb.AppendLine($@"{indent}    }}");
                sb.AppendLine($@"{indent}}}");

                return sb.ToString();
            }

            public static string EmitHelperMethods(string indent, SerializationGraph serializationGraph)
            {
                var sb = new StringBuilder();
                sb.Append(EmitTypeMethods(serializationGraph.RootType, indent, serializationGraph));

                foreach (var typeModel in serializationGraph.ReachableTypes)
                {
                    if (ReferenceEquals(typeModel, serializationGraph.RootType))
                    {
                        continue;
                    }

                    sb.AppendLine();
                    sb.Append(EmitTypeMethods(typeModel, indent, serializationGraph));
                }

                return sb.ToString();
            }

            static string EmitTypeMethods(
                SerializableTypeModel typeModel,
                string indent,
                SerializationGraph serializationGraph)
            {
                return typeModel.IsReferenceType
                    ? EmitReferenceTypeMethods(typeModel, indent, serializationGraph)
                    : EmitValueTypeMethods(typeModel, indent, serializationGraph);
            }

            static string EmitReferenceTypeMethods(
                SerializableTypeModel typeModel,
                string indent,
                SerializationGraph serializationGraph)
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine($@"private static {typeModel.TypeName} {typeModel.CreateInstanceMethodName}()");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    return new {typeModel.TypeName}();");
                sb.AppendLine($@"{indent}}}");
                sb.AppendLine();
                sb.AppendLine($@"private static void {typeModel.WritePayloadMethodName}(BinaryWriter writer, {typeModel.TypeName} message, MessageSerializer.SerializeContext context)");
                sb.AppendLine($@"{indent}{{");
                foreach (var member in GetAllMembers(typeModel.Metadata))
                {
                    sb.Append(Member.EmitSerialize(member, "message", indent + "    ", serializationGraph));
                }
                sb.AppendLine($@"{indent}}}");
                sb.AppendLine();
                sb.AppendLine($@"private static void {typeModel.PopulatePayloadMethodName}(BinaryReader reader, {typeModel.TypeName} result, MessageSerializer.DeserializeContext context)");
                sb.AppendLine($@"{indent}{{");
                foreach (var member in GetAllMembers(typeModel.Metadata))
                {
                    sb.Append(Member.EmitDeserialize(member, "result", indent + "    ", serializationGraph));
                }
                sb.AppendLine($@"{indent}}}");

                return sb.ToString();
            }

            static string EmitValueTypeMethods(
                SerializableTypeModel typeModel,
                string indent,
                SerializationGraph serializationGraph)
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine($@"private static void {typeModel.WritePayloadMethodName}(BinaryWriter writer, {typeModel.TypeName} message, MessageSerializer.SerializeContext context)");
                sb.AppendLine($@"{indent}{{");
                foreach (var member in GetAllMembers(typeModel.Metadata))
                {
                    sb.Append(Member.EmitSerialize(member, "message", indent + "    ", serializationGraph));
                }
                sb.AppendLine($@"{indent}}}");
                sb.AppendLine();
                sb.AppendLine($@"private static {typeModel.TypeName} {typeModel.ReadPayloadMethodName}(BinaryReader reader, MessageSerializer.DeserializeContext context)");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    var result = new {typeModel.TypeName}();");
                foreach (var member in GetAllMembers(typeModel.Metadata))
                {
                    sb.Append(Member.EmitDeserialize(member, "result", indent + "    ", serializationGraph));
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


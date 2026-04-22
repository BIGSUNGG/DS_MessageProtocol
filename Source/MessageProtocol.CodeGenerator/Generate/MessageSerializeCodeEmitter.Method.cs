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
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($@"
{indent}[ModuleInitializer]
{indent}internal static void Initialize()
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
                sb.AppendLine($@"{indent}    using (var ms = new MemoryStream())");
                sb.AppendLine($@"{indent}    using (var writer = new BinaryWriter(ms))");
                sb.AppendLine($@"{indent}    {{");
                
                // id 구성: 앞 8비트(standaloneId) + 그 다음 8비트(groupRootId) + 그 다음 16비트(elementId)
                uint id = typeMeta.GetMessageId();

                sb.AppendLine($@"{indent}        uint id = {id};");
                sb.AppendLine($@"{indent}        byte messageFlag = (byte)(id >> 24);");
                sb.AppendLine($@"{indent}        writer.Write(messageFlag);");
                sb.AppendLine($@"{indent}        if ((messageFlag & 0x10) == 0)");
                sb.AppendLine($@"{indent}        {{");
                sb.AppendLine($@"{indent}            writer.Write((byte)(id >> 16));");
                sb.AppendLine($@"{indent}            writer.Write((byte)(id >> 8));");
                sb.AppendLine($@"{indent}            writer.Write((byte)id);");
                sb.AppendLine($@"{indent}        }}");
                sb.AppendLine($@"{indent}        var context = new __SerializeContext();");
                sb.AppendLine($@"{indent}        {rootModel.WritePayloadMethodName}(writer, message, context);");
                sb.AppendLine($@"{indent}        return ms.ToArray();");
                sb.AppendLine($@"{indent}    }}");
                sb.AppendLine($@"{indent}}}");

                return sb.ToString();
            }

            public static string EmitDeserialize(TypeMetadata typeMeta, string indent, SerializationGraph serializationGraph)
            {
                var rootModel = serializationGraph.RootType;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($@"public static {typeMeta.Symbol.Name} Deserialize(byte[] data)");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    using (var ms = new MemoryStream(data))");
                sb.AppendLine($@"{indent}    using (var reader = new BinaryReader(ms))");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        byte messageFlag = reader.ReadByte();");
                sb.AppendLine($@"{indent}        uint id = (uint)messageFlag << 24;");
                sb.AppendLine($@"{indent}        if ((messageFlag & 0x10) == 0)");
                sb.AppendLine($@"{indent}        {{");
                sb.AppendLine($@"{indent}            id |= (uint)reader.ReadByte() << 16;");
                sb.AppendLine($@"{indent}            id |= (uint)reader.ReadByte() << 8;");
                sb.AppendLine($@"{indent}            id |= reader.ReadByte();");
                sb.AppendLine($@"{indent}        }}");
                sb.AppendLine($@"{indent}        var context = new __DeserializeContext();");
                if (rootModel.IsReferenceType)
                {
                    sb.AppendLine($@"{indent}        var result = {rootModel.CreateInstanceMethodName}();");
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
                sb.Append(EmitSharedContextTypes(indent));
                sb.AppendLine();
                sb.Append(EmitSharedHelperMethods(indent));
                sb.AppendLine();
                sb.Append(EmitTypeMethods(serializationGraph.RootType, indent, serializationGraph));

                foreach (var typeModel in serializationGraph.ReachableTypes)
                {
                    sb.AppendLine();
                    sb.Append(EmitTypeMethods(typeModel, indent, serializationGraph));
                }

                return sb.ToString();
            }

            static string EmitSharedContextTypes(string indent)
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine($@"private enum __ReferenceKind : byte");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    BackReference = 0,");
                sb.AppendLine($@"{indent}    NewObject = 1,");
                sb.AppendLine($@"{indent}}}");
                sb.AppendLine();
                sb.AppendLine($@"private sealed class __ReferenceComparer : IEqualityComparer<object>");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    public static readonly __ReferenceComparer Instance = new __ReferenceComparer();");
                sb.AppendLine();
                sb.AppendLine($@"{indent}    bool IEqualityComparer<object>.Equals(object x, object y)");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        return ReferenceEquals(x, y);");
                sb.AppendLine($@"{indent}    }}");
                sb.AppendLine();
                sb.AppendLine($@"{indent}    int IEqualityComparer<object>.GetHashCode(object obj)");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        return RuntimeHelpers.GetHashCode(obj);");
                sb.AppendLine($@"{indent}    }}");
                sb.AppendLine($@"{indent}}}");
                sb.AppendLine();
                sb.AppendLine($@"private sealed class __SerializeContext");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    readonly Dictionary<object, int> _objectIds = new Dictionary<object, int>(__ReferenceComparer.Instance);");
                sb.AppendLine($@"{indent}    int _nextObjectId = 1;");
                sb.AppendLine();
                sb.AppendLine($@"{indent}    public bool TryGetObjectId(object value, out int objectId)");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        return _objectIds.TryGetValue(value, out objectId);");
                sb.AppendLine($@"{indent}    }}");
                sb.AppendLine();
                sb.AppendLine($@"{indent}    public int RegisterObject(object value)");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        int objectId = _nextObjectId++;");
                sb.AppendLine($@"{indent}        _objectIds[value] = objectId;");
                sb.AppendLine($@"{indent}        return objectId;");
                sb.AppendLine($@"{indent}    }}");
                sb.AppendLine($@"{indent}}}");
                sb.AppendLine();
                sb.AppendLine($@"private sealed class __DeserializeContext");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    readonly Dictionary<int, object> _objects = new Dictionary<int, object>();");
                sb.AppendLine();
                sb.AppendLine($@"{indent}    public void RegisterObject(int objectId, object value)");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        _objects[objectId] = value;");
                sb.AppendLine($@"{indent}    }}");
                sb.AppendLine();
                sb.AppendLine($@"{indent}    public object GetObject(int objectId)");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        return _objects[objectId];");
                sb.AppendLine($@"{indent}    }}");
                sb.AppendLine($@"{indent}}}");

                return sb.ToString();
            }

            static string EmitSharedHelperMethods(string indent)
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine($@"private static void __WriteSizedReference<T>(");
                sb.AppendLine($@"{indent}BinaryWriter writer,");
                sb.AppendLine($@"{indent}T value,");
                sb.AppendLine($@"{indent}__SerializeContext context,");
                sb.AppendLine($@"{indent}Action<BinaryWriter, T, __SerializeContext> writePayload)");
                sb.AppendLine($@"{indent}where T : class");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    if (value == null)");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        writer.Write(-1);");
                sb.AppendLine($@"{indent}        return;");
                sb.AppendLine($@"{indent}    }}");
                sb.AppendLine();
                sb.AppendLine($@"{indent}    using (var ms = new MemoryStream())");
                sb.AppendLine($@"{indent}    using (var nestedWriter = new BinaryWriter(ms))");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        if (context.TryGetObjectId(value, out int objectId))");
                sb.AppendLine($@"{indent}        {{");
                sb.AppendLine($@"{indent}            nestedWriter.Write((byte)__ReferenceKind.BackReference);");
                sb.AppendLine($@"{indent}            nestedWriter.Write(objectId);");
                sb.AppendLine($@"{indent}        }}");
                sb.AppendLine($@"{indent}        else");
                sb.AppendLine($@"{indent}        {{");
                sb.AppendLine($@"{indent}            objectId = context.RegisterObject(value);");
                sb.AppendLine($@"{indent}            nestedWriter.Write((byte)__ReferenceKind.NewObject);");
                sb.AppendLine($@"{indent}            nestedWriter.Write(objectId);");
                sb.AppendLine($@"{indent}            writePayload(nestedWriter, value, context);");
                sb.AppendLine($@"{indent}        }}");
                sb.AppendLine();
                sb.AppendLine($@"{indent}        writer.Write((int)ms.Length);");
                sb.AppendLine($@"{indent}        writer.Write(ms.ToArray());");
                sb.AppendLine($@"{indent}    }}");
                sb.AppendLine($@"{indent}}}");
                sb.AppendLine();
                sb.AppendLine($@"private static T __ReadSizedReference<T>(");
                sb.AppendLine($@"{indent}BinaryReader reader,");
                sb.AppendLine($@"{indent}__DeserializeContext context,");
                sb.AppendLine($@"{indent}Func<T> createValue,");
                sb.AppendLine($@"{indent}Action<BinaryReader, T, __DeserializeContext> populatePayload)");
                sb.AppendLine($@"{indent}where T : class");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    int size = reader.ReadInt32();");
                sb.AppendLine($@"{indent}    if (size < 0)");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        return null;");
                sb.AppendLine($@"{indent}    }}");
                sb.AppendLine();
                sb.AppendLine($@"{indent}    byte[] bytes = reader.ReadBytes(size);");
                sb.AppendLine($@"{indent}    using (var ms = new MemoryStream(bytes))");
                sb.AppendLine($@"{indent}    using (var nestedReader = new BinaryReader(ms))");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        var referenceKind = (__ReferenceKind)nestedReader.ReadByte();");
                sb.AppendLine($@"{indent}        int objectId = nestedReader.ReadInt32();");
                sb.AppendLine($@"{indent}        if (referenceKind == __ReferenceKind.BackReference)");
                sb.AppendLine($@"{indent}        {{");
                sb.AppendLine($@"{indent}            return (T)context.GetObject(objectId);");
                sb.AppendLine($@"{indent}        }}");
                sb.AppendLine();
                sb.AppendLine($@"{indent}        if (referenceKind != __ReferenceKind.NewObject)");
                sb.AppendLine($@"{indent}        {{");
                sb.AppendLine($@"{indent}            throw new InvalidDataException(""Invalid reference kind."");");
                sb.AppendLine($@"{indent}        }}");
                sb.AppendLine();
                sb.AppendLine($@"{indent}        T value = createValue();");
                sb.AppendLine($@"{indent}        context.RegisterObject(objectId, value);");
                sb.AppendLine($@"{indent}        populatePayload(nestedReader, value, context);");
                sb.AppendLine($@"{indent}        return value;");
                sb.AppendLine($@"{indent}    }}");
                sb.AppendLine($@"{indent}}}");
                sb.AppendLine();
                sb.AppendLine($@"private static void __WriteSizedValue<T>(");
                sb.AppendLine($@"{indent}BinaryWriter writer,");
                sb.AppendLine($@"{indent}T value,");
                sb.AppendLine($@"{indent}__SerializeContext context,");
                sb.AppendLine($@"{indent}Action<BinaryWriter, T, __SerializeContext> writePayload)");
                sb.AppendLine($@"{indent}where T : struct");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    using (var ms = new MemoryStream())");
                sb.AppendLine($@"{indent}    using (var nestedWriter = new BinaryWriter(ms))");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        writePayload(nestedWriter, value, context);");
                sb.AppendLine($@"{indent}        writer.Write((int)ms.Length);");
                sb.AppendLine($@"{indent}        writer.Write(ms.ToArray());");
                sb.AppendLine($@"{indent}    }}");
                sb.AppendLine($@"{indent}}}");
                sb.AppendLine();
                sb.AppendLine($@"private static T __ReadSizedValue<T>(");
                sb.AppendLine($@"{indent}BinaryReader reader,");
                sb.AppendLine($@"{indent}__DeserializeContext context,");
                sb.AppendLine($@"{indent}Func<BinaryReader, __DeserializeContext, T> readPayload)");
                sb.AppendLine($@"{indent}where T : struct");
                sb.AppendLine($@"{indent}{{");
                sb.AppendLine($@"{indent}    int size = reader.ReadInt32();");
                sb.AppendLine($@"{indent}    if (size < 0)");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        throw new InvalidDataException(""Value type payload cannot be null."");");
                sb.AppendLine($@"{indent}    }}");
                sb.AppendLine();
                sb.AppendLine($@"{indent}    byte[] bytes = reader.ReadBytes(size);");
                sb.AppendLine($@"{indent}    using (var ms = new MemoryStream(bytes))");
                sb.AppendLine($@"{indent}    using (var nestedReader = new BinaryReader(ms))");
                sb.AppendLine($@"{indent}    {{");
                sb.AppendLine($@"{indent}        return readPayload(nestedReader, context);");
                sb.AppendLine($@"{indent}    }}");
                sb.AppendLine($@"{indent}}}");

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
                sb.AppendLine($@"private static void {typeModel.WritePayloadMethodName}(BinaryWriter writer, {typeModel.TypeName} message, __SerializeContext context)");
                sb.AppendLine($@"{indent}{{");
                foreach (var member in GetAllMembers(typeModel.Metadata))
                {
                    sb.Append(Member.EmitSerialize(member, "message", indent + "    ", serializationGraph));
                }
                sb.AppendLine($@"{indent}}}");
                sb.AppendLine();
                sb.AppendLine($@"private static void {typeModel.PopulatePayloadMethodName}(BinaryReader reader, {typeModel.TypeName} result, __DeserializeContext context)");
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

                sb.AppendLine($@"private static void {typeModel.WritePayloadMethodName}(BinaryWriter writer, {typeModel.TypeName} message, __SerializeContext context)");
                sb.AppendLine($@"{indent}{{");
                foreach (var member in GetAllMembers(typeModel.Metadata))
                {
                    sb.Append(Member.EmitSerialize(member, "message", indent + "    ", serializationGraph));
                }
                sb.AppendLine($@"{indent}}}");
                sb.AppendLine();
                sb.AppendLine($@"private static {typeModel.TypeName} {typeModel.ReadPayloadMethodName}(BinaryReader reader, __DeserializeContext context)");
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
        }
    }
}


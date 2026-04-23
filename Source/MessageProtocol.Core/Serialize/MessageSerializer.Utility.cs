using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace MessageProtocol.Serialize
{
    public static partial class MessageSerializer
    {
        enum ReferenceKind : byte
        {
            BackReference = 0,
            NewObject = 1,
        }

        sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            bool IEqualityComparer<object>.Equals(object? x, object? y)
            {
                return ReferenceEquals(x, y);
            }

            int IEqualityComparer<object>.GetHashCode(object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }

        public sealed class SerializeContext
        {
            readonly Dictionary<object, int> _objectIds = new Dictionary<object, int>(ReferenceComparer.Instance);
            int _nextObjectId = 1;

            public bool TryGetObjectId(object value, out int objectId)
            {
                return _objectIds.TryGetValue(value, out objectId);
            }

            public int RegisterObject(object value)
            {
                int objectId = _nextObjectId++;
                _objectIds[value] = objectId;
                return objectId;
            }
        }

        public sealed class DeserializeContext
        {
            readonly Dictionary<int, object> _objects = new Dictionary<int, object>();

            public void RegisterObject(int objectId, object value)
            {
                _objects[objectId] = value;
            }

            public object GetObject(int objectId)
            {
                return _objects[objectId];
            }
        }

        public static void WriteSizedReference<T>(
            BinaryWriter writer,
            T? value,
            SerializeContext context,
            Action<BinaryWriter, T, SerializeContext> writePayload)
            where T : class
        {
            if (value == null)
            {
                writer.Write(-1);
                return;
            }

            using (var ms = new MemoryStream())
            using (var nestedWriter = new BinaryWriter(ms))
            {
                if (context.TryGetObjectId(value, out int objectId))
                {
                    nestedWriter.Write((byte)ReferenceKind.BackReference);
                    nestedWriter.Write(objectId);
                }
                else
                {
                    objectId = context.RegisterObject(value);
                    nestedWriter.Write((byte)ReferenceKind.NewObject);
                    nestedWriter.Write(objectId);
                    writePayload(nestedWriter, value, context);
                }

                writer.Write((int)ms.Length);
                writer.Write(ms.ToArray());
            }
        }

        public static T ReadSizedReference<T>(
            BinaryReader reader,
            DeserializeContext context,
            Func<T> createValue,
            Action<BinaryReader, T, DeserializeContext> populatePayload)
            where T : class
        {
            int size = reader.ReadInt32();
            if (size < 0)
            {
                return null!;
            }

            byte[] bytes = reader.ReadBytes(size);
            using (var ms = new MemoryStream(bytes))
            using (var nestedReader = new BinaryReader(ms))
            {
                var referenceKind = (ReferenceKind)nestedReader.ReadByte();
                int objectId = nestedReader.ReadInt32();
                if (referenceKind == ReferenceKind.BackReference)
                {
                    return (T)context.GetObject(objectId);
                }

                if (referenceKind != ReferenceKind.NewObject)
                {
                    throw new InvalidDataException("Invalid reference kind.");
                }

                T value = createValue();
                context.RegisterObject(objectId, value);
                populatePayload(nestedReader, value, context);
                return value;
            }
        }

        public static void WriteSizedValue<T>(
            BinaryWriter writer,
            T value,
            SerializeContext context,
            Action<BinaryWriter, T, SerializeContext> writePayload)
            where T : struct
        {
            using (var ms = new MemoryStream())
            using (var nestedWriter = new BinaryWriter(ms))
            {
                writePayload(nestedWriter, value, context);
                writer.Write((int)ms.Length);
                writer.Write(ms.ToArray());
            }
        }

        public static T ReadSizedValue<T>(
            BinaryReader reader,
            DeserializeContext context,
            Func<BinaryReader, DeserializeContext, T> readPayload)
            where T : struct
        {
            int size = reader.ReadInt32();
            if (size < 0)
            {
                throw new InvalidDataException("Value type payload cannot be null.");
            }

            byte[] bytes = reader.ReadBytes(size);
            using (var ms = new MemoryStream(bytes))
            using (var nestedReader = new BinaryReader(ms))
            {
                return readPayload(nestedReader, context);
            }
        }
    }
}

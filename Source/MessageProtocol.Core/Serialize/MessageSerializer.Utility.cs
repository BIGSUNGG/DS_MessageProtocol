using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using MessageProtocol;

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
                writer.Write(MessageWireFormat.NullSizedPayloadLength);
                return;
            }

            long lengthPosition = BeginSizedPayload(writer);
            if (context.TryGetObjectId(value, out int objectId))
            {
                writer.Write((byte)ReferenceKind.BackReference);
                writer.Write(objectId);
            }
            else
            {
                objectId = context.RegisterObject(value);
                writer.Write((byte)ReferenceKind.NewObject);
                writer.Write(objectId);
                writePayload(writer, value, context);
            }

            CompleteSizedPayload(writer, lengthPosition);
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

            long payloadEndPosition = GetPayloadEndPosition(reader, size);
            var referenceKind = (ReferenceKind)reader.ReadByte();
            int objectId = reader.ReadInt32();
            if (referenceKind == ReferenceKind.BackReference)
            {
                T value = (T)context.GetObject(objectId);
                VerifyPayloadConsumed(reader, payloadEndPosition);
                return value;
            }

            if (referenceKind != ReferenceKind.NewObject)
            {
                throw new InvalidDataException("Invalid reference kind.");
            }

            T createdValue = createValue();
            context.RegisterObject(objectId, createdValue);
            populatePayload(reader, createdValue, context);
            VerifyPayloadConsumed(reader, payloadEndPosition);
            return createdValue;
        }

        public static void WriteSizedValue<T>(
            BinaryWriter writer,
            T value,
            SerializeContext context,
            Action<BinaryWriter, T, SerializeContext> writePayload)
            where T : struct
        {
            long lengthPosition = BeginSizedPayload(writer);
            writePayload(writer, value, context);
            CompleteSizedPayload(writer, lengthPosition);
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

            long payloadEndPosition = GetPayloadEndPosition(reader, size);
            T value = readPayload(reader, context);
            VerifyPayloadConsumed(reader, payloadEndPosition);
            return value;
        }

        static long BeginSizedPayload(BinaryWriter writer)
        {
            EnsureSeekable(writer.BaseStream, "Sized payloads require a seekable stream.");
            long lengthPosition = writer.BaseStream.Position;
            writer.Write(0);
            return lengthPosition;
        }

        static void CompleteSizedPayload(BinaryWriter writer, long lengthPosition)
        {
            Stream stream = writer.BaseStream;
            long payloadEndPosition = stream.Position;
            long payloadStartPosition = lengthPosition + sizeof(int);
            int payloadLength = checked((int)(payloadEndPosition - payloadStartPosition));

            stream.Position = lengthPosition;
            writer.Write(payloadLength);
            stream.Position = payloadEndPosition;
        }

        static long GetPayloadEndPosition(BinaryReader reader, int size)
        {
            Stream stream = reader.BaseStream;
            EnsureSeekable(stream, "Sized payloads require a seekable stream.");

            long payloadEndPosition = checked(stream.Position + size);
            if (payloadEndPosition > stream.Length)
            {
                throw new EndOfStreamException("Sized payload exceeds the available data.");
            }

            return payloadEndPosition;
        }

        static void VerifyPayloadConsumed(BinaryReader reader, long payloadEndPosition)
        {
            long currentPosition = reader.BaseStream.Position;
            if (currentPosition != payloadEndPosition)
            {
                throw new InvalidDataException(
                    $"Sized payload was not fully consumed. Expected end position {payloadEndPosition}, actual {currentPosition}.");
            }
        }

        static void EnsureSeekable(Stream stream, string errorMessage)
        {
            if (!stream.CanSeek)
            {
                throw new NotSupportedException(errorMessage);
            }
        }
    }
}

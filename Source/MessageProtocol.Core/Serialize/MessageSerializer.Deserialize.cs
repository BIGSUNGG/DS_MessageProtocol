using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MessageProtocol.Serialize
{
    public static partial class MessageSerializer
    {
        /// <summary>MessageId 로 dispatch 되는 reader 델리게이트.</summary>
        public delegate object BufferReaderFunc(ref MessageBufferReader reader);

        static readonly ConcurrentDictionary<uint, BufferReaderFunc> _readerDispatch = new();

        /// <summary>제네릭 구성 디스패치: (MessageId, ClassId) → reader. 키는 두 값을 24비트씩 합성한 ulong.</summary>
        static readonly ConcurrentDictionary<ulong, BufferReaderFunc> _genericReaderDispatch = new();

        /// <summary>(MessageId, ClassId) 등록 소유 타입. 구성 간 충돌 검출용.</summary>
        static readonly ConcurrentDictionary<ulong, Type> _registeredGenericIds = new();

        internal static ulong GenericDispatchKey(uint messageId, uint classId)
        {
            return ((ulong)messageId << 24) | (classId & MessageWireFormat.MessageIdValueMask);
        }

        /// <summary>제네릭 hot path 역직렬화 (딕셔너리 조회·박싱 없음).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Deserialize<T>(ref MessageBufferReader reader) where T : IMessageSerializable<T>
        {
            var deserialize = SerializerCache<T>.Deserialize;
            if (deserialize is null) ThrowMissingDeserialize<T>();
            return deserialize!(ref reader);
        }

        /// <summary>제네릭 경로: ReadOnlySpan 에서 역직렬화.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Deserialize<T>(ReadOnlySpan<byte> data) where T : IMessageSerializable<T>
        {
            if (data.Length == 0) throw new ArgumentException("Message data is empty.", nameof(data));
            var deserialize = SerializerCache<T>.Deserialize;
            if (deserialize is null) ThrowMissingDeserialize<T>();
            var reader = new MessageBufferReader(data);
            return deserialize!(ref reader);
        }

        /// <summary>제네릭 경로: ReadOnlyMemory 에서 역직렬화.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Deserialize<T>(ReadOnlyMemory<byte> data) where T : IMessageSerializable<T>
        {
            return Deserialize<T>(data.Span);
        }

        /// <summary>제네릭 경로: byte[] 호환 경로.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Deserialize<T>(byte[] data) where T : IMessageSerializable<T>
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0) throw new ArgumentException("Message data is empty.", nameof(data));
            var deserializeBytes = SerializerCache<T>.DeserializeBytes;
            if (deserializeBytes is null) ThrowMissingDeserialize<T>();
            return deserializeBytes!(data);
        }

        static void ThrowMissingDeserialize<T>()
        {
            throw new InvalidOperationException(
                $"Type '{typeof(T).FullName}' has no deserialize method. " +
                $"Ensure the type is generated via MessageProtocol.CodeGenerator or defines " +
                $"'public static {typeof(T).Name} Deserialize(ref MessageBufferReader)' and " +
                $"'public static {typeof(T).Name} Deserialize(byte[])'.");
        }

        /// <summary>object dispatch 역직렬화: 헤더 MessageId 로 등록된 타입에 라우팅한다 (Standalone/Group 만).</summary>
        public static object Deserialize(byte[] data)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            return Deserialize(new ReadOnlySpan<byte>(data));
        }

        /// <summary>object dispatch 역직렬화: ReadOnlyMemory 입력.</summary>
        public static object Deserialize(ReadOnlyMemory<byte> data) => Deserialize(data.Span);

        /// <summary>object dispatch 역직렬화: ReadOnlySpan 입력. 제네릭 메시지는 (MessageId, ClassId) 로 구성에 라우팅한다.</summary>
        public static object Deserialize(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) throw new ArgumentException("Message data is empty.", nameof(data));

            byte header = data[0];
            var flags = MessageWireFormat.GetFlags(header);
            bool generic = MessageWireFormat.IsGenericMessage(header);
            if (!generic && (flags & MessageFlag.StandaloneOrGroup) == 0)
            {
                throw new InvalidCastException("Message is not a standalone or group message.");
            }

            uint messageId = ReadMessageIdFromHeader(data);

            if (generic)
            {
                if (data.Length < MessageWireFormat.GenericIdHeaderSize)
                {
                    throw new ArgumentException($"Message data is too short to read the {MessageWireFormat.GenericIdHeaderSize}-byte generic header.");
                }

                uint classId = (uint)data[4] << 16 | (uint)data[5] << 8 | data[6];
                if (!_genericReaderDispatch.TryGetValue(GenericDispatchKey(messageId, classId), out var genericInvoker))
                {
                    throw new KeyNotFoundException($"Generic message type with ID {messageId} and ClassId {classId} is not registered.");
                }

                var genericReader = new MessageBufferReader(data);
                return genericInvoker(ref genericReader);
            }

            if (!_readerDispatch.TryGetValue(messageId, out var invoker))
            {
                throw new KeyNotFoundException($"Message type with ID {messageId} is not registered.");
            }

            var reader = new MessageBufferReader(data);
            return invoker(ref reader);
        }

        /// <summary>중첩 object dispatch: 현재 reader 위치의 헤더로 등록된 타입에 라우팅한다. 제네릭 헤더는 (MessageId, ClassId) 라우팅.</summary>
        public static object DeserializeFromReader(ref MessageBufferReader reader)
        {
            var unread = reader.UnreadSpan;
            if (unread.Length == 0) throw new ArgumentException("Reader has no data to deserialize.");

            uint messageId = ReadMessageIdFromHeader(unread);

            if (MessageWireFormat.IsGenericMessage(unread[0]))
            {
                if (unread.Length < MessageWireFormat.GenericIdHeaderSize)
                {
                    throw new ArgumentException($"Reader data is too short to read the {MessageWireFormat.GenericIdHeaderSize}-byte generic header.");
                }

                uint classId = (uint)unread[4] << 16 | (uint)unread[5] << 8 | unread[6];
                if (!_genericReaderDispatch.TryGetValue(GenericDispatchKey(messageId, classId), out var genericInvoker))
                {
                    throw new KeyNotFoundException($"Generic message type with ID {messageId} and ClassId {classId} is not registered.");
                }
                return genericInvoker(ref reader);
            }

            if (!_readerDispatch.TryGetValue(messageId, out var invoker))
            {
                throw new KeyNotFoundException($"Message type with ID {messageId} is not registered.");
            }
            return invoker(ref reader);
        }

        static uint ReadMessageIdFromHeader(ReadOnlySpan<byte> data)
        {
            byte header = data[0];
            uint messageId = (uint)header << 24;
            if (!MessageWireFormat.HasEmbeddedMessageId(header))
            {
                return messageId;
            }
            if (data.Length < MessageWireFormat.IdHeaderSize)
            {
                throw new ArgumentException($"Message data is too short to read the {MessageWireFormat.IdHeaderSize}-byte message id.");
            }
            messageId |= (uint)data[1] << 16;
            messageId |= (uint)data[2] << 8;
            messageId |= data[3];
            return messageId;
        }

        internal static void RegisterReaderInvoker(uint messageId, BufferReaderFunc invoker)
        {
            if (!_readerDispatch.TryAdd(messageId, invoker))
            {
                throw new InvalidOperationException($"Message id {messageId} already registered for deserialization.");
            }
        }

        internal static bool TryRemoveReaderInvoker(uint messageId)
        {
            return _readerDispatch.TryRemove(messageId, out _);
        }

        internal static void RegisterGenericReaderInvoker(uint messageId, uint classId, Type type, BufferReaderFunc invoker)
        {
            ulong key = GenericDispatchKey(messageId, classId);
            var existing = _registeredGenericIds.GetOrAdd(key, type);
            if (!ReferenceEquals(existing, type))
            {
                throw new InvalidOperationException(
                    $"Generic construction with MessageId {messageId} and ClassId {classId} is already registered by '{existing.FullName}'.");
            }

            if (!_genericReaderDispatch.TryAdd(key, invoker))
            {
                _registeredGenericIds.TryRemove(key, out _);
                throw new InvalidOperationException(
                    $"Generic construction with MessageId {messageId} and ClassId {classId} is already registered for deserialization.");
            }
        }

        internal static bool TryRemoveGenericReaderInvoker(uint messageId, uint classId)
        {
            ulong key = GenericDispatchKey(messageId, classId);
            _registeredGenericIds.TryRemove(key, out _);
            return _genericReaderDispatch.TryRemove(key, out _);
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MessageProtocol;

namespace MessageProtocol.Serialize
{
    public static partial class MessageSerializer
    {
        /// <summary>
        /// 메시지 id 로 dispatch 되는 reader 델리게이트.
        /// </summary>
        public delegate object BufferReaderFunc(ref MessageBufferReader reader);

        static readonly ConcurrentDictionary<uint, BufferReaderFunc> _readerDispatch = new();

        /// <summary>
        /// 제네릭 hot path. 버퍼 reader 에서 T 를 역직렬화합니다.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Deserialize<T>(ref MessageBufferReader reader) where T : IMessageSerializable<T>
        {
            return T.Deserialize(ref reader);
        }

        /// <summary>
        /// 제네릭 API: ReadOnlySpan 에서 역직렬화.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Deserialize<T>(ReadOnlySpan<byte> data) where T : IMessageSerializable<T>
        {
            if (data.Length == 0) throw new ArgumentException("Message data is empty.", nameof(data));
            var reader = new MessageBufferReader(data);
            return T.Deserialize(ref reader);
        }

        /// <summary>
        /// 제네릭 API: ReadOnlyMemory 에서 역직렬화.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Deserialize<T>(ReadOnlyMemory<byte> data) where T : IMessageSerializable<T>
        {
            return Deserialize<T>(data.Span);
        }

        /// <summary>
        /// 제네릭 API: byte[] 호환 경로.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Deserialize<T>(byte[] data) where T : IMessageSerializable<T>
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0) throw new ArgumentException("Message data is empty.", nameof(data));
            return T.Deserialize(data);
        }

        /// <summary>
        /// object dispatch API: 헤더의 messageId 로 dispatch.
        /// </summary>
        public static object Deserialize(byte[] data)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            return Deserialize(new ReadOnlySpan<byte>(data));
        }

        public static object Deserialize(ReadOnlyMemory<byte> data) => Deserialize(data.Span);

        public static object Deserialize(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) throw new ArgumentException("Message data is empty.", nameof(data));

            byte header = data[0];
            var flags = MessageWireFormat.GetFlags(header);
            if ((flags & MessageFlag.StandaloneOrGroup) == 0)
            {
                throw new InvalidCastException("Message is not a standalone or group message.");
            }

            uint messageId = ReadMessageIdFromHeader(data);
            if (!_readerDispatch.TryGetValue(messageId, out var invoker))
            {
                throw new KeyNotFoundException($"Message type with ID {messageId} is not registered.");
            }

            var reader = new MessageBufferReader(data);
            return invoker(ref reader);
        }

        /// <summary>
        /// 중첩된 object dispatch 에서 사용. 현재 reader 위치의 헤더를 보고 등록된 타입으로 dispatch 합니다.
        /// </summary>
        public static object DeserializeFromReader(ref MessageBufferReader reader)
        {
            var unread = reader.UnreadSpan;
            if (unread.Length == 0) throw new ArgumentException("Reader has no data to deserialize.");

            uint messageId = ReadMessageIdFromHeader(unread);
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
    }
}

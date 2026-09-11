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

        /// <summary>
        /// 제네릭 경로(전체 소비 검사): 프레임 전체를 정확히 소비해야 성공한다. 남은 바이트가 있으면
        /// <see cref="System.IO.InvalidDataException"/> — 피어가 이 타입과 다른 멤버 레이아웃으로 쓴 프레임
        /// (스키마 표류 — ADR-0006 레이아웃 동결 위반, 예: 필드 제거)을 조용한 데이터 유실 대신 크게 실패시킨다.
        /// 기본 <see cref="Deserialize{T}(ReadOnlySpan{byte})"/> 은 뒤에 붙은 여유 바이트를 허용한다.
        /// </summary>
        public static T DeserializeExact<T>(ReadOnlySpan<byte> data) where T : IMessageSerializable<T>
        {
            if (data.Length == 0) throw new ArgumentException("Message data is empty.", nameof(data));
            var deserialize = SerializerCache<T>.Deserialize;
            if (deserialize is null) ThrowMissingDeserialize<T>();
            var reader = new MessageBufferReader(data);
            var result = deserialize!(ref reader);
            if (reader.Position != data.Length)
            {
                ThrowTrailingBytes(reader.Position, data.Length);
            }
            return result;
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
            return DeserializeCore(data, out _);
        }

        /// <summary>
        /// object dispatch 역직렬화(전체 소비 검사): 프레임 전체를 정확히 소비해야 성공한다.
        /// 남은 바이트가 있으면 <see cref="System.IO.InvalidDataException"/> — 피어가 이 타입과 다른 멤버 레이아웃으로
        /// 쓴 프레임(스키마 표류 — ADR-0006 레이아웃 동결 위반)을 조용한 데이터 유실 대신 크게 실패시킨다.
        /// 기본 <see cref="Deserialize(ReadOnlySpan{byte})"/> 은 전송 계층 프레이밍 여유 등으로 뒤에 붙은 바이트를 허용한다.
        /// </summary>
        public static object DeserializeExact(ReadOnlySpan<byte> data)
        {
            var result = DeserializeCore(data, out int consumed);
            if (consumed != data.Length)
            {
                ThrowTrailingBytes(consumed, data.Length);
            }
            return result;
        }

        /// <summary>라우팅 공통 본체 — 소비한 바이트 수를 반환한다(전체 소비 검사용).</summary>
        static object DeserializeCore(ReadOnlySpan<byte> data, out int consumed)
        {
            if (data.Length == 0) throw new ArgumentException("Message data is empty.", nameof(data));

            byte header = data[0];
            var flags = MessageWireFormat.GetFlags(header);
            bool generic = MessageWireFormat.IsGenericMessage(header);
            if (!generic && (flags & MessageFlag.IdMessage) == 0)
            {
                // 와이어 내용 불법(플래그 비트)은 InvalidDataException 이다 — 캐스트가 일어난 적이 없으므로
                // InvalidCastException 은 유형부터 오해를 주었고 신뢰 경계 퍼저의 깨끗한 거부 목록에도
                // 잡히지 않았다(2026-09-08 퍼저 발견, KI-41 괘련).
                throw new System.IO.InvalidDataException("Message is not a standalone or group message; the header flag bits are invalid.");
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
                var genericValue = genericInvoker(ref genericReader);
                consumed = genericReader.Position;
                return genericValue;
            }

            if (!_readerDispatch.TryGetValue(messageId, out var invoker))
            {
                throw new KeyNotFoundException($"Message type with ID {messageId} is not registered.");
            }

            var reader = new MessageBufferReader(data);
            var value = invoker(ref reader);
            consumed = reader.Position;
            return value;
        }

        /// <summary>전체 소비 검사 실패 — 와이어 내용 불법으로 보고한다(경계·인자 오류와 구분).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowTrailingBytes(int consumed, int total)
        {
            throw new System.IO.InvalidDataException(
                $"Deserialization consumed {consumed} of {total} bytes; {total - consumed} trailing byte(s) remain. " +
                $"The frame was written with a different member layout than this type (schema drift) or contains extra data. " +
                $"Per ADR-0006, published message layouts are frozen — introduce a new MessageId type for layout changes.");
        }

        /// <summary>중첩 object dispatch: 현재 reader 위치의 헤더로 등록된 타입에 라우팅한다. 제네릭 헤더는 (MessageId, ClassId) 라우팅.</summary>
        /// <remarks>
        /// 중첩 객체 한 수준으로 계산된다(<see cref="MessageBufferReader.EnterNestedObject"/>) — 타입 매개변수 멤버·외부 호출자의
        /// 재귀가 reader 깊이 카운터에 연결되어 작은 적대 프레임의 무한 재귀(스택 오버플로)를 막는다 (Known-Issues KI-14).
        /// </remarks>
        public static object DeserializeFromReader(ref MessageBufferReader reader)
        {
            var unread = reader.UnreadSpan;
            if (unread.Length == 0) throw new ArgumentException("Reader has no data to deserialize.");

            uint messageId = ReadMessageIdFromHeader(unread);
            BufferReaderFunc? invoker;

            if (MessageWireFormat.IsGenericMessage(unread[0]))
            {
                if (unread.Length < MessageWireFormat.GenericIdHeaderSize)
                {
                    throw new ArgumentException($"Reader data is too short to read the {MessageWireFormat.GenericIdHeaderSize}-byte generic header.");
                }

                uint classId = (uint)unread[4] << 16 | (uint)unread[5] << 8 | unread[6];
                if (!_genericReaderDispatch.TryGetValue(GenericDispatchKey(messageId, classId), out invoker))
                {
                    throw new KeyNotFoundException($"Generic message type with ID {messageId} and ClassId {classId} is not registered.");
                }
            }
            else if (!_readerDispatch.TryGetValue(messageId, out invoker))
            {
                throw new KeyNotFoundException($"Message type with ID {messageId} is not registered.");
            }

            // 공개 경유 지점이라 수동 구현이 예외 후 같은 reader 를 계속 쓸 수 있다 — finally 로 짝을 맞춘다.
            reader.EnterNestedObject();
            try
            {
                return invoker!(ref reader);
            }
            finally
            {
                reader.LeaveNestedObject();
            }
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
            // 등록은 owner→dispatch 순서로 발행하므로 제거(롤백)는 역순 dispatch→owner 로 — 순서가 같으면
            // 디스패치가 아직 살아있는 찰나에 owner 가 사라져, 같은 키의 재등록이 owner 를 선점하고 롤백이
            // 새 등록의 디스패치를 지우는 창이 열린다(KI-38 감사 FINDING 3, 등록 실패 경로에서만 도달).
            bool removed = _genericReaderDispatch.TryRemove(key, out _);
            _registeredGenericIds.TryRemove(key, out _);
            return removed;
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace MessageProtocol.Serialize
{
    public static partial class MessageSerializer
    {
        /// <summary>object 경로에서 타입별 직렬화 델리게이트를 찾는 디스패치 테이블.</summary>
        static readonly ConcurrentDictionary<Type, BufferWriterAction> _writerDispatch = new();

        /// <summary>object 경로 직렬화 델리게이트.</summary>
        public delegate void BufferWriterAction(object message, ref MessageBufferWriter writer);

        /// <summary>
        /// 제네릭 hot path. 정적 캐시 델리게이트 1회 호출만으로 직렬화한다 (딕셔너리 조회·박싱 없음).
        /// 선언 타입 기준이며 파생 타입 다형성이 필요하면 <see cref="Serialize(object)"/> 를 쓴다.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Serialize<T>(T message, ref MessageBufferWriter writer) where T : IMessageSerializable<T>
        {
            if (message is null) throw new ArgumentNullException(nameof(message));
            var serialize = SerializerCache<T>.Serialize;
            if (serialize is null) ThrowMissingSerialize<T>();
            serialize!(message, ref writer);
        }

        /// <summary>제네릭 경로: 호환용 byte[] 반환.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Serialize<T>(T message) where T : IMessageSerializable<T>
        {
            if (message is null) throw new ArgumentNullException(nameof(message));
            var serializeBytes = SerializerCache<T>.SerializeBytes;
            if (serializeBytes is null) ThrowMissingSerialize<T>();
            return serializeBytes!(message);
        }

        /// <summary>제네릭 경로: ArrayPool 기반 <see cref="PooledBuffer"/> 반환. 호출자가 Dispose 해야 한다.</summary>
        public static PooledBuffer SerializePooled<T>(T message) where T : IMessageSerializable<T>
        {
            if (message is null) throw new ArgumentNullException(nameof(message));
            var serialize = SerializerCache<T>.Serialize;
            if (serialize is null) ThrowMissingSerialize<T>();
            var writer = MessageBufferWriter.Create();
            try
            {
                serialize!(message, ref writer);
                return writer.ToPooledBuffer();
            }
            catch
            {
                writer.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 캐시에 직렬화 델리게이트가 없을 때 보고한다. <see cref="SerializerCache{T}"/> cctor 가 던지는 대신 여기서 보고하므로
        /// CLR 이 초기화 실패를 타입별로 영구 캐싱하지 않고, 이후 델리게이트 등록으로 복구할 수 있다 (Known-Issues KI-11).
        /// </summary>
        static void ThrowMissingSerialize<T>()
        {
            throw new InvalidOperationException(
                $"Type '{typeof(T).FullName}' has no serialize method. " +
                $"Ensure the type is generated via MessageProtocol.CodeGenerator or defines " +
                $"'public static void Serialize({typeof(T).Name}, ref MessageBufferWriter)' and " +
                $"'public static byte[] Serialize({typeof(T).Name})', and that it is registered " +
                $"(MessageSerializer.RegisterNonIdMessage / RegisterHasIdMessage) before first use.");
        }

        /// <summary>object dispatch 경로: 런타임 타입으로 직렬화 (다형성). 호환용 byte[] 반환.</summary>
        public static byte[] Serialize(object message)
        {
            if (message is null) throw new ArgumentNullException(nameof(message));
            var writer = MessageBufferWriter.Create();
            try
            {
                var invoker = GetWriterInvoker(message.GetType());
                invoker(message, ref writer);
                return writer.ToArray();
            }
            finally
            {
                writer.Dispose();
            }
        }

        /// <summary>object dispatch 경로: <see cref="PooledBuffer"/> 반환.</summary>
        public static PooledBuffer SerializePooled(object message)
        {
            if (message is null) throw new ArgumentNullException(nameof(message));
            var writer = MessageBufferWriter.Create();
            try
            {
                var invoker = GetWriterInvoker(message.GetType());
                invoker(message, ref writer);
                return writer.ToPooledBuffer();
            }
            catch
            {
                writer.Dispose();
                throw;
            }
        }

        /// <summary>object dispatch: 지정 writer 에 직접 기록한다 (중첩 메시지 용도).</summary>
        /// <remarks>
        /// 중첩 객체 한 수준으로 계산된다(<see cref="MessageBufferWriter.EnterNestedObject"/>) — 타입 매개변수·추상 메시지 멤버와
        /// 수동 구현의 재귀가 writer 깊이 카운터에 연결되어, 백레퍼런스가 추적되지 않는 디스패치 경로의 순환 그래프가
        /// 재귀 스택을 소진시키는 것(catch 불가 스택 오버플로)을 막는다 (Known-Issues KI-25).
        /// </remarks>
        public static void SerializeToWriter(object message, ref MessageBufferWriter writer)
        {
            if (message is null) throw new ArgumentNullException(nameof(message));
            var invoker = GetWriterInvoker(message.GetType());

            // 공개 경유 지점이라 호출자가 예외 후 같은 writer 를 계속 쓸 수 있다 — finally 로 짝을 맞춘다.
            writer.EnterNestedObject();
            try
            {
                invoker(message, ref writer);
            }
            finally
            {
                writer.LeaveNestedObject();
            }
        }

        static readonly object _lazyRegisterLock = new();

        static BufferWriterAction GetWriterInvoker(Type messageType)
        {
            if (_writerDispatch.TryGetValue(messageType, out var invoker))
            {
                return invoker;
            }

            // ModuleInitializer 가 돌지 않은 수동 구현 타입의 지연 등록.
            lock (_lazyRegisterLock)
            {
                if (_writerDispatch.TryGetValue(messageType, out invoker))
                {
                    return invoker;
                }

                try
                {
                    RegisterType(messageType);
                }
                catch (InvalidOperationException) when (_writerDispatch.ContainsKey(messageType))
                {
                    // 다른 스레드와의 경쟁으로 이미 등록됨.
                }
            }

            if (_writerDispatch.TryGetValue(messageType, out invoker))
            {
                return invoker;
            }

            throw new InvalidOperationException(
                $"Type '{messageType.FullName}' is not registered for serialization. " +
                $"Ensure the type is generated via MessageProtocol.CodeGenerator and referenced so its ModuleInitializer runs, " +
                $"or call MessageSerializer.RegisterType(typeof({messageType.Name})) manually.");
        }

        internal static void RegisterWriterInvoker(Type type, BufferWriterAction invoker)
        {
            if (!_writerDispatch.TryAdd(type, invoker))
            {
                throw new InvalidOperationException($"Type '{type.FullName}' already registered for serialization.");
            }
        }

        internal static bool TryRemoveWriterInvoker(Type type)
        {
            return _writerDispatch.TryRemove(type, out _);
        }
    }
}

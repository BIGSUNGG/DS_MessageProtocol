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
            SerializerCache<T>.Serialize(message, ref writer);
        }

        /// <summary>제네릭 경로: 호환용 byte[] 반환.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Serialize<T>(T message) where T : IMessageSerializable<T>
        {
            if (message is null) throw new ArgumentNullException(nameof(message));
            return SerializerCache<T>.SerializeBytes(message);
        }

        /// <summary>제네릭 경로: ArrayPool 기반 <see cref="PooledBuffer"/> 반환. 호출자가 Dispose 해야 한다.</summary>
        public static PooledBuffer SerializePooled<T>(T message) where T : IMessageSerializable<T>
        {
            if (message is null) throw new ArgumentNullException(nameof(message));
            var writer = MessageBufferWriter.Create();
            try
            {
                SerializerCache<T>.Serialize(message, ref writer);
                return writer.ToPooledBuffer();
            }
            catch
            {
                writer.Dispose();
                throw;
            }
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializeToWriter(object message, ref MessageBufferWriter writer)
        {
            if (message is null) throw new ArgumentNullException(nameof(message));
            var invoker = GetWriterInvoker(message.GetType());
            invoker(message, ref writer);
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

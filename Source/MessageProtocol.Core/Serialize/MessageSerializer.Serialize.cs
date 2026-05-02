using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace MessageProtocol.Serialize
{
    public static partial class MessageSerializer
    {
        /// <summary>
        /// 버퍼 writer 에 전체 메시지를 쓰는 타입별 델리게이트 등록 테이블. object 경로에서만 사용됩니다.
        /// </summary>
        static readonly ConcurrentDictionary<Type, BufferWriterAction> _writerDispatch = new();

        public delegate void BufferWriterAction(object message, ref MessageBufferWriter writer);

        /// <summary>
        /// 제네릭 hot path. <see cref="SerializerCache{T}.Serialize"/> 정적 필드 1회 읽기 + 델리게이트 1회 호출만으로
        /// 직렬화하므로 ConcurrentDictionary lookup 이 없고 구조체 박싱도 발생하지 않습니다.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Serialize<T>(T message, ref MessageBufferWriter writer) where T : IMessageSerializable<T>
        {
            if (message is null) throw new ArgumentNullException(nameof(message));
            SerializerCache<T>.Serialize(message, ref writer);
        }

        /// <summary>
        /// 제네릭 API: PooledBuffer 반환 (ArrayPool 사용). 호출자가 Dispose 로 반드시 반환해야 합니다.
        /// </summary>
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

        /// <summary>
        /// 제네릭 API: 호환용 byte[] 반환. T 가 ID 메시지이면 런타임 타입 기반 dispatch 로 라우팅하여
        /// 다형성(파생 타입을 베이스 타입 매개변수로 전달하는 경우)을 보존합니다.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Serialize<T>(T message) where T : IMessageSerializable<T>
        {
            if (message is null) throw new ArgumentNullException(nameof(message));

            if (message is IHasIdMessageSerializable<T>)
                return Serialize((object)message);

            return SerializerCache<T>.SerializeBytes(message);
        }

        /// <summary>
        /// object dispatch API: 호환용 byte[] 반환. 타입별 writer 델리게이트로 dispatch 합니다.
        /// </summary>
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

        /// <summary>
        /// object dispatch API: PooledBuffer 반환.
        /// </summary>
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

        /// <summary>
        /// object dispatch: 지정된 버퍼 writer 에 직접 기록합니다. 중첩 메시지 용도.
        /// </summary>
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

            // Lazy registration for manually-implemented types not auto-registered via ModuleInitializer.
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
                    // 다른 스레드와의 경쟁 또는 중복 등록 - writer 가 이미 있으면 무시.
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

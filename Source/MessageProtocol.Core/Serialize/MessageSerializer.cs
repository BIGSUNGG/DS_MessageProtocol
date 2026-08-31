using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MessageProtocol.Serialize
{
    /// <summary>
    /// 메시지 등록·직렬화·역직렬화 정적 진입점.
    /// 생성 코드는 <c>[ModuleInitializer]</c> 에서 델리게이트·MessageId 를 직접 넘겨 등록한다.
    /// </summary>
    public static partial class MessageSerializer
    {
        static readonly ConcurrentDictionary<Type, byte> _registeredTypes = new();
        static readonly ConcurrentDictionary<uint, Type> _registeredMessageIds = new();

        /// <summary>
        /// ID 메시지 등록 fast path. <see cref="SerializerCache{T}"/> 를 리플렉션 없이 채운다.
        /// </summary>
        public static void RegisterHasIdMessage<T>(
            TypedSerializeRefAction<T> serialize,
            TypedDeserializeRefFunc<T> deserialize,
            uint messageId,
            Func<T, byte[]>? serializeBytes = null,
            Func<byte[], T>? deserializeBytes = null)
            where T : IHasIdMessageSerializable<T>
        {
            if (serialize is null) throw new ArgumentNullException(nameof(serialize));
            if (deserialize is null) throw new ArgumentNullException(nameof(deserialize));

            serializeBytes ??= CreateSerializeBytesWrapper(serialize);
            deserializeBytes ??= CreateDeserializeBytesWrapper(deserialize);

            PrefillSerializerCache(serialize, deserialize, serializeBytes, deserializeBytes, messageId, hasId: true);

            RegisterCore(typeof(T), messageId, hasId: true,
                writer: static (object m, ref MessageBufferWriter w) => SerializerCache<T>.Serialize((T)m, ref w),
                reader: static (ref MessageBufferReader r) => (object)SerializerCache<T>.Deserialize!(ref r)!);
        }

        /// <summary>ID 메시지 등록 리플렉션 경로. 수동 구현 타입이거나 델리게이트를 넘기지 않을 때 사용한다.</summary>
        public static void RegisterHasIdMessage<T>() where T : IHasIdMessageSerializable<T>
        {
            if (!SerializerCache<T>.HasId)
            {
                throw new InvalidOperationException(
                    $"Type '{typeof(T).FullName}' is registered as a HasId message but exposes no 'public static uint MessageId' property.");
            }

            uint messageId = SerializerCache<T>.MessageId;
            RegisterCore(typeof(T), messageId, hasId: true,
                writer: static (object m, ref MessageBufferWriter w) => SerializerCache<T>.Serialize((T)m, ref w),
                reader: SerializerCache<T>.Deserialize is null
                    ? null
                    : static (ref MessageBufferReader r) => (object)SerializerCache<T>.Deserialize!(ref r)!);
        }

        /// <summary>NonId 메시지 등록 fast path.</summary>
        public static void RegisterNonIdMessage<T>(
            TypedSerializeRefAction<T> serialize,
            TypedDeserializeRefFunc<T>? deserialize = null,
            Func<T, byte[]>? serializeBytes = null,
            Func<byte[], T>? deserializeBytes = null)
            where T : IMessageSerializable<T>
        {
            if (serialize is null) throw new ArgumentNullException(nameof(serialize));

            serializeBytes ??= CreateSerializeBytesWrapper(serialize);
            if (deserialize != null)
            {
                deserializeBytes ??= CreateDeserializeBytesWrapper(deserialize);
            }

            PrefillSerializerCache(serialize, deserialize, serializeBytes, deserializeBytes, messageId: 0u, hasId: false);

            RegisterCore(typeof(T), 0u, hasId: false,
                writer: static (object m, ref MessageBufferWriter w) => SerializerCache<T>.Serialize((T)m, ref w),
                reader: null);
        }

        /// <summary>NonId 메시지 등록 리플렉션 경로.</summary>
        public static void RegisterNonIdMessage<T>() where T : IMessageSerializable<T>
        {
            RegisterCore(typeof(T), 0u, hasId: false,
                writer: static (object m, ref MessageBufferWriter w) => SerializerCache<T>.Serialize((T)m, ref w),
                reader: null);
        }

        /// <summary>
        /// 리플렉션 기반 등록. 타입이 <see cref="IMessageSerializable{T}"/> 또는
        /// <see cref="IHasIdMessageSerializable{T}"/> 를 구현해야 한다.
        /// </summary>
        public static void RegisterType(Type type)
        {
            if (type is null) throw new ArgumentNullException(nameof(type));
            if (type.IsGenericTypeDefinition)
            {
                throw new ArgumentException($"Open generic type '{type.FullName}' cannot be registered.", nameof(type));
            }

            var iHasId = type.GetInterfaces().FirstOrDefault(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHasIdMessageSerializable<>));

            var iMessage = type.GetInterfaces().FirstOrDefault(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageSerializable<>));

            if (iHasId == null && iMessage == null)
            {
                throw new InvalidOperationException(
                    $"Type '{type.FullName}' does not implement 'IMessageSerializable<{type.Name}>'. " +
                    $"This usually means the source generator (MessageProtocol.CodeGenerator) did not generate the required partial class implementation.");
            }

            string methodName = iHasId != null ? nameof(RegisterHasIdMessage) : nameof(RegisterNonIdMessage);
            // 매개변수 없는 오버로드만 선택 (델리게이트 오버로드와 구분).
            var generic = typeof(MessageSerializer)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == methodName
                            && m.IsGenericMethodDefinition
                            && m.GetParameters().Length == 0)
                .MakeGenericMethod(type);
            try
            {
                generic.Invoke(null, null);
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                throw tie.InnerException;
            }
        }

        static Func<T, byte[]> CreateSerializeBytesWrapper<T>(TypedSerializeRefAction<T> serialize)
        {
            return message =>
            {
                var writer = MessageBufferWriter.Create();
                try
                {
                    serialize(message, ref writer);
                    return writer.ToArray();
                }
                finally
                {
                    writer.Dispose();
                }
            };
        }

        static Func<byte[], T> CreateDeserializeBytesWrapper<T>(TypedDeserializeRefFunc<T> deserialize)
        {
            return data =>
            {
                var reader = new MessageBufferReader(data);
                return deserialize(ref reader);
            };
        }

        /// <summary>
        /// Prefill 홀더에 델리게이트를 심은 뒤 <see cref="SerializerCache{T}"/> cctor 를 돌려 리플렉션을 건너뛴다.
        /// 홀더가 별도 타입이라 설정 중에는 캐시 cctor 가 트리거되지 않는다.
        /// </summary>
        static void PrefillSerializerCache<T>(
            TypedSerializeRefAction<T> serialize,
            TypedDeserializeRefFunc<T>? deserialize,
            Func<T, byte[]> serializeBytes,
            Func<byte[], T>? deserializeBytes,
            uint messageId,
            bool hasId)
        {
            SerializerCachePrefill<T>.Serialize = serialize;
            SerializerCachePrefill<T>.Deserialize = deserialize;
            SerializerCachePrefill<T>.SerializeBytes = serializeBytes;
            SerializerCachePrefill<T>.DeserializeBytes = deserializeBytes;
            SerializerCachePrefill<T>.MessageId = messageId;
            SerializerCachePrefill<T>.HasId = hasId;
            SerializerCachePrefill<T>.IsSet = true;

            RuntimeHelpers.RunClassConstructor(typeof(SerializerCache<T>).TypeHandle);
        }

        static void RegisterCore(Type type, uint messageId, bool hasId, BufferWriterAction writer, BufferReaderFunc? reader)
        {
            if (!_registeredTypes.TryAdd(type, 0))
            {
                throw new InvalidOperationException($"Message type '{type.FullName}' is already registered.");
            }

            bool writerRegistered = false;
            bool messageIdRegistered = false;
            bool readerRegistered = false;
            try
            {
                RegisterWriterInvoker(type, writer);
                writerRegistered = true;

                if (hasId)
                {
                    byte headerByte = (byte)(messageId >> 24);
                    if (MessageWireFormat.HasEmbeddedMessageId(headerByte))
                    {
                        var existing = _registeredMessageIds.GetOrAdd(messageId, type);
                        if (!ReferenceEquals(existing, type))
                        {
                            throw new InvalidOperationException(
                                $"Message type with ID {messageId} is already registered by '{existing.FullName}'.");
                        }
                        messageIdRegistered = true;

                        if (reader != null)
                        {
                            RegisterReaderInvoker(messageId, reader);
                            readerRegistered = true;
                        }
                    }
                }
            }
            catch
            {
                _registeredTypes.TryRemove(type, out _);
                if (writerRegistered) TryRemoveWriterInvoker(type);
                if (readerRegistered) TryRemoveReaderInvoker(messageId);
                if (messageIdRegistered) _registeredMessageIds.TryRemove(messageId, out _);
                throw;
            }
        }
    }
}

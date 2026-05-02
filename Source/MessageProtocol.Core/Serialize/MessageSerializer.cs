using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MessageProtocol;

namespace MessageProtocol.Serialize
{
    public static partial class MessageSerializer
    {
        static readonly ConcurrentDictionary<Type, byte> _registeredTypes = new();
        static readonly ConcurrentDictionary<uint, Type> _registeredMessageIds = new();

        static MessageSerializer()
        {
        }

        /// <summary>
        /// 생성기가 ModuleInitializer 에서 호출하는 fast path. 첫 호출 시 <see cref="SerializerCache{T}"/>
        /// 정적 생성자가 리플렉션으로 typed 델리게이트를 한 번 캐싱하므로 이후 핫 경로는 lookup 이 없습니다.
        /// </summary>
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

        /// <summary>
        /// 생성기가 ModuleInitializer 에서 호출하는 fast path. NonId 메시지용.
        /// </summary>
        public static void RegisterNonIdMessage<T>() where T : IMessageSerializable<T>
        {
            RegisterCore(typeof(T), 0u, hasId: false,
                writer: static (object m, ref MessageBufferWriter w) => SerializerCache<T>.Serialize((T)m, ref w),
                reader: null);
        }

        /// <summary>
        /// 리플렉션 기반 호환 API. 수동 구현 타입이나 동적으로 등록해야 하는 경우에만 사용하세요.
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
            var generic = typeof(MessageSerializer)
                .GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)!
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
                if (messageIdRegistered) _registeredMessageIds.TryRemove(new KeyValuePair<uint, Type>(messageId, type));
                throw;
            }
        }
    }
}

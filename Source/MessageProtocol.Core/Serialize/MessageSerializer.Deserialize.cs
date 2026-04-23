using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using MessageProtocol;

namespace MessageProtocol.Serialize
{
    public static partial class MessageSerializer
    {
        /// <summary>
        /// Key : Message Id
        /// Value : Deserialize 메서드를 호출하는 객체
        /// </summary>
        static readonly ConcurrentDictionary<uint, NonGenericDeserializeInvoker> _deserializeCache = new();

        public static T Deserialize<T>(byte[] data) where T : IMessageSerializable<T>
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0)
                throw new ArgumentException("Message data is empty.", nameof(data));

            byte header = data[0];
            var flags = MessageWireFormat.GetFlags(header);
            if ((flags & MessageFlag.StandaloneOrGroup) != 0)
                return (T)Deserialize(data);

            return T.Deserialize(data);
        }

        public static object Deserialize(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0)
                throw new ArgumentException("Message data is empty.", nameof(data));

            byte header = data[0];
            var flags = MessageWireFormat.GetFlags(header);
            if ((flags & MessageFlag.StandaloneOrGroup) == 0)
                throw new InvalidCastException("Message is not a standalone or group message.");

            uint messageId = ReadMessageId(data);
            if (!_deserializeCache.TryGetValue(messageId, out var invoker))
                throw new KeyNotFoundException($"Message type with ID {messageId} is not registered.");

            return invoker.Deserialize(data);
        }

        static uint ReadMessageId(byte[] data)
        {
            if (data.Length == 0)
                throw new ArgumentException("Message data is empty.", nameof(data));

            byte header = data[0];
            uint messageId = (uint)header << 24;

            // 상위 니블: 플래그. NonIdMessage면 뒤 3바이트는 MessageId 값에 포함하지 않음.
            if (!MessageWireFormat.HasEmbeddedMessageId(header))
                return messageId;

            if (data.Length < MessageWireFormat.IdHeaderSize)
                throw new ArgumentException($"Message data is too short to read the {MessageWireFormat.IdHeaderSize}-byte message id.", nameof(data));

            messageId |= (uint)data[1] << 16;
            messageId |= (uint)data[2] << 8;
            messageId |= data[3];

            return messageId;
        }

        private static void RegisterDeserializeInvoker(Type messageType, uint messageId)
        {
            _deserializeCache.GetOrAdd(messageId, _ => CreateDeserializeInvoker(messageType));
        }

        static NonGenericDeserializeInvoker CreateDeserializeInvoker(Type messageType)
        {
            try
            {
                var genericInvokerType = typeof(GenericDeserializeInvoker<>).MakeGenericType(messageType);
                var instance = Activator.CreateInstance(genericInvokerType);
                if (instance == null)
                {
                    throw new InvalidOperationException($"Failed to create instance of GenericDeserializeInvoker<{messageType.Name}>");
                }

                return (NonGenericDeserializeInvoker)instance;
            }
            catch (ArgumentException ex) when (ex.Message.Contains("violates the constraint"))
            {
                throw new InvalidOperationException(
                    $"Type '{messageType.FullName}' does not implement 'IMessageSerializable<{messageType.Name}>'. " +
                    $"This usually means the source generator (MessageProtocol.CodeGenerator) did not generate the required partial class implementation. " +
                    $"Please ensure that:\n" +
                    $"1. 'MessageProtocol.CodeGenerator' is properly referenced as an analyzer in your project\n" +
                    $"2. The project uses ProjectReference (not a DLL reference) to MessageProtocol.Core\n" +
                    $"3. The message class is marked as 'partial' and has the appropriate attribute ([NonIdMessage], [GroupElementMessage], [GroupRootMessage], or [StandaloneMessage])\n" +
                    $"Original error: {ex.Message}", ex);
            }
        }

        class GenericDeserializeInvoker<T> : NonGenericDeserializeInvoker where T : IMessageSerializable<T>
        {
            public override object Deserialize(byte[] data)
            {
                return T.Deserialize(data);
            }
        }

        abstract class NonGenericDeserializeInvoker
        {
            public abstract object Deserialize(byte[] data);
        }
    }
}

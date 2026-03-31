using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using MessageProtocol;

namespace MessageProtocol.Serialize
{
    public static partial class MessageSerializer
    {
        /// <summary>
        /// Key : Message Id
        /// Value : Deserialize 메서드를 호출하는 객체
        /// </summary>
        static Dictionary<uint, NonGenericDeserializeInvoker> _deserializeCache = new Dictionary<uint, NonGenericDeserializeInvoker>();

        public static T Deserialize<T>(byte[] data) where T : IMessageSerializable<T>
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            return (T)Deserialize(data);
        }

        public static object Deserialize(byte[] data)
        {
            uint messageId = ReadMessageId(data);
            if(!_deserializeCache.TryGetValue(messageId, out var invoker))
                throw new KeyNotFoundException($"Message type with ID {messageId} is not registered.");

            return invoker.Deserialize(data);
        }

        public static T DeserializeMessageStandalone<T>(byte[] data) where T : IMessageSerializable<T>
        {
            if(data == null) throw new ArgumentNullException(nameof(data));
            
            // Data로 들어온 Message Id 값이 제너릭으로 들어온 T의 메시지 MessageId와 같은지 유효성 검사
            if(ReadMessageId(data) != T.MessageId)
                throw  new InvalidCastException($"Message type with ID {T.MessageId} is not a standalone message.");
            
            return T.Deserialize(data);
        }

        static uint ReadMessageId(byte[] data)
        {
            if (data.Length == 0)
                throw new ArgumentException("Message data is empty.", nameof(data));

            byte messageFlag = data[0];
            uint messageId = (uint)messageFlag << 24;

            // first bit == 1: Message only, trailing 3 bytes are not part of message id.
            if ((messageFlag & 0x01) != 0)
                return messageId;

            if (data.Length < 4)
                throw new ArgumentException("Message data is too short to read 4-byte message id.", nameof(data));

            messageId |= (uint)data[1] << 16;
            messageId |= (uint)data[2] << 8;
            messageId |= data[3];

            return messageId;
        }

        private static void RegisterDeserializeInvoker(Type messageType)
        {
            uint messageId = GetMessageIdByType(messageType);

            try
            {
                var genericInvokerType = typeof(GenericDeserializeInvoker<>).MakeGenericType(messageType);
                var instance = Activator.CreateInstance(genericInvokerType);
                if (instance == null)
                {
                    throw new InvalidOperationException($"Failed to create instance of GenericDeserializeInvoker<{messageType.Name}>");
                }
                NonGenericDeserializeInvoker invoker = (NonGenericDeserializeInvoker)instance;
                _deserializeCache[messageId] = invoker;
            }
            catch (ArgumentException ex) when (ex.Message.Contains("violates the constraint"))
            {
                throw new InvalidOperationException(
                    $"Type '{messageType.FullName}' does not implement 'IMessageSerializable<{messageType.Name}>'. " +
                    $"This usually means the source generator (MessageProtocol.CodeGenerator) did not generate the required partial class implementation. " +
                    $"Please ensure that:\n" +
                    $"1. 'MessageProtocol.CodeGenerator' is properly referenced as an analyzer in your project\n" +
                    $"2. The project uses ProjectReference (not a DLL reference) to MessageProtocol.Core\n" +
                    $"3. The message class is marked as 'partial' and has the appropriate attribute ([Message], [MessageGroupElement], [MessageGroupRoot], or [MessageStandalone])\n" +
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

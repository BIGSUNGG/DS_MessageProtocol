using System;
using System.Collections.Concurrent;

namespace MessageProtocol.Serialize
{
    public static partial class MessageSerializer
    {
        /// <summary>
        /// Key : Message Type
        /// Value : Serialize 메서드를 호출하는 객체
        /// </summary>
        static readonly ConcurrentDictionary<Type, NonGenericSerializeInvoker> _serializeCache = new();

        public static byte[] Serialize<T>(T message) where T : IMessageSerializable<T>
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (message is IHasIdMessageSerializable<T>)
                return Serialize((object)message);

            return T.Serialize(message);
        }

        public static byte[] Serialize(object message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            return RegisterSerializeInvoker(message.GetType()).Serialize(message);
        }

        private static NonGenericSerializeInvoker RegisterSerializeInvoker(Type messageType)
        {
            return _serializeCache.GetOrAdd(messageType, CreateSerializeInvoker);
        }

        static NonGenericSerializeInvoker CreateSerializeInvoker(Type messageType)
        {
            try
            {
                var genericInvokerType = typeof(GenericSerializeInvoker<>).MakeGenericType(messageType);
                var instance = Activator.CreateInstance(genericInvokerType);
                if (instance == null)
                {
                    throw new InvalidOperationException($"Failed to create instance of GenericSerializeInvoker<{messageType.Name}>");
                }

                return (NonGenericSerializeInvoker)instance;
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

        class GenericSerializeInvoker<T> : NonGenericSerializeInvoker where T : IMessageSerializable<T>
        {
            public override byte[] Serialize(object message)
            {
                return T.Serialize((T)message);
            }
        }

        abstract class NonGenericSerializeInvoker
        {
            public abstract byte[] Serialize(object message);
        }
    }
}

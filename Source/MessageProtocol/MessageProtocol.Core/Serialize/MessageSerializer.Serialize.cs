using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MessageProtocol;

namespace MessageProtocol.Serialize
{
    public static partial class MessageSerializer
    {
        /// <summary>
        /// Key : Message Type
        /// Value : Serialize 메서드를 호출하는 객체
        /// </summary>
        static Dictionary<Type, NonGenericSerializeInvoker> _serializeCache = new Dictionary<Type, NonGenericSerializeInvoker>();

        public static byte[] Serialize<T>(T message) where T : IMessageSerializable<T>
        {
            if (message == null) 
                throw new ArgumentNullException(nameof(message));

            return T.Serialize(message);
        }

        public static byte[] Serialize(object message)
        {
            var messageType = message.GetType();

            return _serializeCache[messageType].Serialize(message);
        }

        private static void RegisterSerializeInvoker(Type messageType)
        {
            NonGenericSerializeInvoker invoker = (NonGenericSerializeInvoker)Activator.CreateInstance(typeof(GenericSerializeInvoker<>).MakeGenericType(messageType));
            _serializeCache[messageType] = invoker;
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

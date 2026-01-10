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
            uint messageId = BitConverter.ToUInt32(data, 0);
            return _deserializeCache[messageId].Deserialize(data);
        }

        private static void RegisterDeserializeInvoker(Type messageType)
        {
            uint messageId = (uint)messageType.
                GetProperty("MessageId", BindingFlags.Static | BindingFlags.Public)
                .GetValue(null);

            NonGenericDeserializeInvoker invoker = (NonGenericDeserializeInvoker)Activator.CreateInstance(typeof(GenericDeserializeInvoker<>).MakeGenericType(messageType));
            _deserializeCache[messageId] = invoker;
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

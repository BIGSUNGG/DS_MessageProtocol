using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MessageProtocol;

namespace MessageProtocol.Serialize
{
    public class MessageSerializer
    {
        public static MessageSerializer Instance => _instance.Value;
        static Lazy<MessageSerializer> _instance = new Lazy<MessageSerializer>(() => new MessageSerializer());

        private MessageSerializer()
        {

        }

        public byte[] Serialize<T>(T message) where T : IMessageSerializable<T>
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            return T.Serialize(message);
        }

        public T Deserialize<T>(byte[] data) where T : IMessageSerializable<T>
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            return T.Deserialize(data);
        }
    }
}

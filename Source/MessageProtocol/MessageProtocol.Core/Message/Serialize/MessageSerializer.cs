using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DS.MessageProtocol;

namespace DS.MessageProtocol.Serialize
{
    public class MessageSerializer : IMessageSerialize, IMessageDeserialize
    {
        public static MessageSerializer Instance => _instance.Value;
        static Lazy<MessageSerializer> _instance = new Lazy<MessageSerializer>(() => new MessageSerializer());

        /// <summary>
        /// Key : Serialize될 타입
        /// Value : Serialize될 타입에 맞는 Serailize Helper
        /// </summary>
        Dictionary<Type, IMessageSerialize> _serializeHelper = new Dictionary<Type, IMessageSerialize>();

        /// <summary>
        /// First Key : Deserialize될 루트 클래스 타입
        /// Value : Deserialize될 타입에 맞는 Deserialize Helper
        /// </summary>
        Dictionary<Type, IMessageDeserialize> _deserializeHelper = new Dictionary<Type, IMessageDeserialize>();

        private MessageSerializer()
        {
            foreach(var root in MessageGroupCollector.Instance.MessageGroupRoots.Values)
            {
                _deserializeHelper.Add(root.RootMessageType, new MessageGroupRootDeserializeHelper(root));
                foreach(var element in root.Elements.Values)
                {
                    _serializeHelper.Add(element.ElementMessageType, new MessageGroupElementSerializeHelper(element));
                    _deserializeHelper.Add(element.ElementMessageType, new MessageGroupElementDeserializeHelper(element));
                }
            }
        }

        public byte[] Serialize(object message)
        {
            if (_serializeHelper.TryGetValue(message.GetType(), out var helper))
            {
                return helper.Serialize(message);
            }
            else
                throw new InvalidOperationException();
        }

        public T Deserialize<T>(byte[] data)
        {
            if (_deserializeHelper.TryGetValue(typeof(T), out var helper))
            {
                return helper.Deserialize<T>(data);
            }
            else
                throw new InvalidOperationException();
        }

    }
}

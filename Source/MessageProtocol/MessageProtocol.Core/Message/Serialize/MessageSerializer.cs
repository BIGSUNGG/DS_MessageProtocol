using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DS.MessageProtocol;

namespace DS.MessageProtocol.Serialize
{
    public class MessageSerializer : IMessageSerialize
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

        public MessageSerializer()
        {
            // 현재 어셈블리에서 MessageInfo 어트리뷰트를 가진 모든 클래스를 찾은 후 SerializeHelper에 등록            
            var assembly = Assembly.GetExecutingAssembly();
            var types = assembly.GetTypes()
                .Where(type => type.GetCustomAttribute<MessageGroupElement>() != null);

            foreach (var type in types)
            {
                var attribute = type.GetCustomAttribute<MessageGroupElement>();
                if (attribute != null)
                {
                    _serializeHelper[type] = new MessageSerializeHelper(type);
                    _deserializeHelper[type] = new RootMessageDeserializeHelper(type);
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

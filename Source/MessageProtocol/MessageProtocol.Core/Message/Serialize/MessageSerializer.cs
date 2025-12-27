using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DS.Message;

namespace DS.Message.Serialize
{
    public class MessageSerializer
    {
        public static MessageSerializer Instance => _instance.Value;
        static Lazy<MessageSerializer> _instance = new Lazy<MessageSerializer>(() => new MessageSerializer());

        Dictionary<Type, MessageSerializeHelper> _serializeHelper = new Dictionary<Type, MessageSerializeHelper>();

        public MessageSerializer()
        {
            // 현재 어셈블리에서 MessageInfo 어트리뷰트를 가진 모든 클래스를 찾은 후 SerializeHelper에 등록            
            var assembly = Assembly.GetExecutingAssembly();
            var types = assembly.GetTypes()
                .Where(type => type.GetCustomAttribute<MessageInfo>() != null);

            foreach (var type in types)
            {
                var attribute = type.GetCustomAttribute<MessageInfo>();
                if (attribute != null)
                {
                    _serializeHelper[type] = new MessageSerializeHelper(type);
                }
            }
        }        

        //public byte[] Serialize(object message)
        //{
            
        //}
    }
}

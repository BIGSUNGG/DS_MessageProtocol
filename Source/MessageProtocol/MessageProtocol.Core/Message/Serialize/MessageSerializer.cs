using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        /// First Key : GroupRootId + ElementId (앞 2바이트는 RootId, 뒷 2바이트는 ElementId, Root인 경우 뒷 2바이트는 0)
        /// Value : Deserialize될 타입에 맞는 Deserialize Helper
        /// </summary>
        Dictionary<int, IMessageDeserialize> _deserializeHelper = new Dictionary<int, IMessageDeserialize>();

        private MessageSerializer()
        {
            foreach(var root in MessageGroupCollector.Instance.MessageGroupRoots.Values)
            {
                ushort rootId = root.RootMessageAttribute.MessageRootId;
                
                // Root Key: 앞 2바이트는 RootId, 뒷 2바이트는 0
                int rootKey = (rootId << 16) | 0;
                _deserializeHelper.Add(rootKey, new MessageGroupDeserializeHelper(root.RootMessageType));
                
                foreach(var element in root.Elements.Values)
                {
                    _serializeHelper.Add(element.ElementMessageType, new MessageGroupSerializeHelper(element.ElementMessageType, element.RootMessageAttribute.MessageRootId, element.ElementMessageAttribute.MessageElementId));
                    
                    // Element Key: 앞 2바이트는 RootId, 뒷 2바이트는 ElementId
                    ushort elementId = element.ElementMessageAttribute.MessageElementId;
                    int elementKey = (rootId << 16) | elementId;
                    _deserializeHelper.Add(elementKey, new MessageGroupDeserializeHelper(element.ElementMessageType));
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
            unsafe
            {
                fixed (byte* p = data)
                {
                    if (_deserializeHelper.TryGetValue(Unsafe.ReadUnaligned<int>(p), out var helper))
                    {
                        return helper.Deserialize<T>(data);
                    }
                    else
                        throw new InvalidOperationException($"No deserialize helper found for key {Unsafe.Read<int>(p)}");
                }
            }
        }
    }
}

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
        Dictionary<int, IMessageDeserialize> _deserializeHelperByGroupId = new Dictionary<int, IMessageDeserialize>();
        Dictionary<Type, IMessageDeserialize> _deserializeHelperByType = new Dictionary<Type, IMessageDeserialize>();

        private MessageSerializer()
        {
            foreach(var root in MessageTypeHelper.Instance.MessageGroupRoots.Values)
            {
                ushort rootId = root.RootMessageAttribute.MessageRootId;
                
                // Root Key: 앞 2바이트는 RootId, 뒷 2바이트는 0
                int rootKey = (rootId << 16) | 0;
                _deserializeHelperByGroupId.Add(rootKey, new MessageGroupDeserializeHelper(root.RootMessageType));
                
                // Root도 직렬화 가능하도록 추가 (elementId는 0)
                _serializeHelper.Add(root.RootMessageType, new MessageGroupSerializeHelper(root.RootMessageType, rootId, 0));
                
                foreach(var element in root.Elements.Values)
                {
                    _serializeHelper.Add(element.ElementMessageType, new MessageGroupSerializeHelper(element.ElementMessageType, element.RootMessageAttribute.MessageRootId, element.ElementMessageAttribute.MessageElementId));
                    
                    // Element Key: 앞 2바이트는 RootId, 뒷 2바이트는 ElementId
                    ushort elementId = element.ElementMessageAttribute.MessageElementId;
                    int elementKey = (rootId << 16) | elementId;
                    _deserializeHelperByGroupId.Add(elementKey, new MessageGroupDeserializeHelper(element.ElementMessageType));
                }
            }
        }

        public byte[] Serialize(object message)
        {
            Type messageClassType = message.GetType();
            return GetOrAddSerializeHelper(messageClassType).Serialize(message);
        }

        public T Deserialize<T>(byte[] data)
        {
            return GetOrAddDeserializeHelper<T>(data).Deserialize<T>(data);
        }

        IMessageSerialize GetOrAddSerializeHelper(Type messageClassType)
        {
            MessageType messageType = MessageTypeHelper.Instance.GetMessageType(messageClassType);
            if(messageType == MessageType.MessageGroup)
            {
                if (_serializeHelper.TryGetValue(messageClassType, out var helper))
                    return helper;
                else
                    throw new InvalidOperationException($"No serialize helper found for type {messageClassType.FullName}");
            }
            else 
            {
                if (_serializeHelper.TryGetValue(messageClassType, out var helper))
                    return helper;

                helper = new MessageStandaloneSerializeHelper(messageClassType);
                _serializeHelper.Add(messageClassType, helper);
                return helper;
            }
        }

        IMessageDeserialize GetOrAddDeserializeHelper<T>(byte[] data)
        {
            Type messageClassType = typeof(T);
            MessageType messageType = MessageTypeHelper.Instance.GetMessageType(messageClassType);
            if (messageType == MessageType.MessageGroup)
            {
                unsafe
                {
                    fixed (byte* p = data)
                    {
                        if (_deserializeHelperByGroupId.TryGetValue(Unsafe.ReadUnaligned<int>(p), out var helper))
                        {
                            return helper;
                        }
                        else
                            throw new InvalidOperationException($"No deserialize helper found for key {Unsafe.Read<int>(p)}");
                    }
                }
            }
            else
            {
                if (_deserializeHelperByType.TryGetValue(messageClassType, out var helper))
                    return helper;

                helper = new MessageStandaloneDeserializeHelper(messageClassType);
                _deserializeHelperByType.Add(messageClassType, helper);
                return helper;
            }
        }
    }
}

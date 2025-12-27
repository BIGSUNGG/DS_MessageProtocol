using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DS.MessageProtocol.Serialize
{
    internal class RootMessageDeserializeHelper : IMessageDeserialize
    {
        Dictionary<int, IMessageDeserialize> _elementMessageDeserialize = new();

        public RootMessageDeserializeHelper(Type rootMessageType)
        {
            foreach(var elementMessageType in MessageTypeCollector.Instance.MessageElementByRoot[rootMessageType])
            {
                var element = MessageTypeCollector.Instance.MessageGroupElements[elementMessageType];
                _elementMessageDeserialize[element.MessageElementId] = new ElementMessageDeserializeHelper(elementMessageType);
            }
        }

        public T Deserialize<T>(byte[] data)
        {
            ushort messageElementId = MessageTypeCollector.Instance.MessageGroupElements[typeof(T)].MessageElementId;
            
            if(_elementMessageDeserialize.TryGetValue(messageElementId, out var childMessageDeserialize) == false)
                throw new InvalidOperationException($"MessageId {messageElementId} is not found");

            return childMessageDeserialize.Deserialize<T>(data);
        }
    }
}
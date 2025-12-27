using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DS.MessageProtocol.Serialize
{
    internal class MessageGroupRootDeserializeHelper : IMessageDeserialize
    {
        MessageGroupRootWrapper _rootWrapper;
        Dictionary<ushort, IMessageDeserialize> _elementMessageDesirialize = new();

        public MessageGroupRootDeserializeHelper(MessageGroupRootWrapper rootWrapper)
        {
            _rootWrapper = rootWrapper; 

            foreach(var element in _rootWrapper.Elements)
            {
                _elementMessageDesirialize.Add(element.Value.ElementMessageAttribute.MessageElementId, new MessageGroupElementDeserializeHelper(element.Value));
            }
        }

        public T Deserialize<T>(byte[] data)
        {
            unsafe
            {
                fixed (byte* p = data)
                {
                    if (_elementMessageDesirialize.TryGetValue(Unsafe.Read<ushort>(p), out var deserialize) == false)
                        throw new InvalidOperationException($"Type {typeof(T).FullName} is not a child of {_rootWrapper.RootMessageType.FullName}");

                    return deserialize.Deserialize<T>(data);
                }
            }
        }
    }
}
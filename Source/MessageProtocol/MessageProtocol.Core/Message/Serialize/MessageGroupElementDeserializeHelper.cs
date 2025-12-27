using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DS.MessageProtocol.Serialize
{
    internal class MessageGroupElementDeserializeHelper : IMessageDeserialize
    {
        MessageGroupElementWrapper _elementWrapper;
        MethodInfo _sizeOf;

        public MessageGroupElementDeserializeHelper(MessageGroupElementWrapper elementWrapper)
        {
            _elementWrapper = elementWrapper;
            _sizeOf = typeof(Unsafe).GetMethod("SizeOf").MakeGenericMethod(_elementWrapper.ElementMessageType);
        }

        public T Deserialize<T>(byte[] data)
        {
            unsafe
            {
                fixed (byte* p = data)
                {
                    ushort messageId = Unsafe.Read<ushort>(p);
                    if (messageId != _elementWrapper.ElementMessageAttribute.MessageElementId)
                        throw new InvalidOperationException();
                    T message = Unsafe.Read<T>(p + 2);
                    return message;
                }
            }
        }
    }
}
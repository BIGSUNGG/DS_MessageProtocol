using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DS.MessageProtocol.Serialize
{
    internal class ElementMessageDeserializeHelper : IMessageDeserialize
    {
        Type _messageType;
        MessageGroupElement? _messageGroupElement;

        MethodInfo _sizeOf;

        public ElementMessageDeserializeHelper(Type messageType)
        {
            _messageType = messageType;
            _messageGroupElement = _messageType.GetCustomAttribute<MessageGroupElement>(false);
            _sizeOf = typeof(Unsafe).GetMethod("SizeOf").MakeGenericMethod(messageType);
        }

        public T Deserialize<T>(byte[] data)
        {
            unsafe
            {
                fixed (byte* p = data)
                {
                    ushort messageId = Unsafe.Read<ushort>(p);
                    if (messageId != _messageGroupElement.MessageElementId)
                        throw new InvalidOperationException();
                    T message = Unsafe.Read<T>(p + 2);
                    return message;
                }
            }
        }
    }
}
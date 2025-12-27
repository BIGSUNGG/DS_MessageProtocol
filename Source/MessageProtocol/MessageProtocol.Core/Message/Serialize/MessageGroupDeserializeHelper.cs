using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DS.MessageProtocol.Serialize
{
    internal class MessageGroupDeserializeHelper : IMessageDeserialize
    {
        MethodInfo _sizeOf;

        public MessageGroupDeserializeHelper(Type type)
        {
            _sizeOf = typeof(Unsafe).GetMethod("SizeOf").MakeGenericMethod(type);
        }

        public T Deserialize<T>(byte[] data)
        {
            unsafe
            {
                fixed (byte* p = data)
                {
                    T message = Unsafe.ReadUnaligned<T>(p + 4);
                    return message;
                }
            }
        }
    }
}
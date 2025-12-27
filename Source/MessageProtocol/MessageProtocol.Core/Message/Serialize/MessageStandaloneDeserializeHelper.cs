using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DS.MessageProtocol.Serialize
{
    internal class MessageStandaloneDeserializeHelper : IMessageDeserialize
    {
        public MessageStandaloneDeserializeHelper(Type type)
        {
        }

        public T Deserialize<T>(byte[] data)
        {
            unsafe
            {
                fixed (byte* p = data)
                {
                    T message = Unsafe.ReadUnaligned<T>(p);
                    return message;
                }
            }
        }
    }
}
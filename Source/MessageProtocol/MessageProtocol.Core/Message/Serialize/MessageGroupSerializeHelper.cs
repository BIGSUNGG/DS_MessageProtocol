using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DS.MessageProtocol.Serialize
{
    internal class MessageGroupSerializeHelper : IMessageSerialize
    {
        ushort _rootId;
        ushort _elementId;
        MethodInfo _sizeOf;

        public MessageGroupSerializeHelper(Type messageType, ushort rootId, ushort elementId)
        {
            _rootId = rootId;
            _elementId = elementId;

            _sizeOf = typeof(Unsafe).GetMethod("SizeOf").MakeGenericMethod(messageType);
        }

        public byte[] Serialize(object message)
        {
            int bufferSize = 4 + (int)_sizeOf.Invoke(null, null);
            byte[] result = new byte[bufferSize];
            unsafe
            {
                fixed (byte* p = result)
                {
                    int compositeId = (_rootId << 16) | _elementId; 
                    Unsafe.Write(p, compositeId);
                    Unsafe.Copy(p + 4, ref message);
                }
            }

            return result;
        }
    }
}
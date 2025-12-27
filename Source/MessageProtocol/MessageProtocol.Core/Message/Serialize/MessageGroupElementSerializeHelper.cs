using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DS.MessageProtocol.Serialize
{
    internal class MessageGroupElementSerializeHelper : IMessageSerialize
    {
        MessageGroupElementWrapper _element;
        MethodInfo _sizeOf;

        public MessageGroupElementSerializeHelper(MessageGroupElementWrapper element)
        {
            _element = element;
            _sizeOf = typeof(Unsafe).GetMethod("SizeOf").MakeGenericMethod(_element.ElementMessageType);
        }

        public byte[] Serialize(object message)
        {
            int bufferSize = 2 + (int)_sizeOf.Invoke(null, null);
            byte[] result = new byte[bufferSize];
            unsafe
            {
                fixed (byte* p = result)
                {
                    Unsafe.Write(p, _element.ElementMessageAttribute.MessageElementId);
                    Unsafe.Copy(p + 2, ref message);
                }
            }

            return result;
        }
    }
}
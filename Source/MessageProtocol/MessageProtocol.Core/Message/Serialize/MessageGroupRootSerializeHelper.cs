using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DS.MessageProtocol.Serialize
{
    internal class MessageGroupRootSerializeHelper : IMessageSerialize
    {
        MessageGroupRootWrapper _root;
        MethodInfo _sizeOf;

        public MessageGroupRootSerializeHelper(MessageGroupRootWrapper root)
        {
            _root = root;
            _sizeOf = typeof(Unsafe).GetMethod("SizeOf").MakeGenericMethod(_root.RootMessageType);
        }

        public byte[] Serialize(object message)
        {
            int bufferSize = 2 + (int)_sizeOf.Invoke(null, null);
            byte[] result = new byte[bufferSize];
            unsafe
            {
                fixed (byte* p = result)
                {
                    Unsafe.Write(p, (ushort)0);
                    Unsafe.Copy(p + 2, ref message);
                }
            }

            return result;
        }
    }
}
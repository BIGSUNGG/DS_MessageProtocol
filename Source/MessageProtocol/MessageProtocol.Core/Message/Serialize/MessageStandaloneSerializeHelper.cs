using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DS.MessageProtocol.Serialize
{
    internal class MessageStandaloneSerializeHelper : IMessageSerialize
    {
        Type _messageType;
        MethodInfo _sizeOf;

        public MessageStandaloneSerializeHelper(Type messageType)
        {
            _messageType = messageType;
            _sizeOf = typeof(Unsafe).GetMethod("SizeOf").MakeGenericMethod(messageType);
        }

        public byte[] Serialize(object message)
        {
            int bufferSize = (int)_sizeOf.Invoke(null, null);
            byte[] result = new byte[bufferSize];
            unsafe
            {
                fixed (byte* p = result)
                {
                    // 값 타입을 메모리에 직접 복사
                    if (_messageType.IsValueType)
                    {
                        // object를 언박싱하여 실제 타입으로 변환
                        object unboxed = Convert.ChangeType(message, _messageType);
                        Marshal.StructureToPtr(unboxed, (IntPtr)p, false);
                    }
                    else
                    {
                        // 참조 타입의 경우 기존 방식 사용
                        Unsafe.Copy(p, ref message);
                    }
                }
            }

            return result;
        }
    }
}
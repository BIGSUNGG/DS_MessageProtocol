using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MessageProtocol;

namespace MessageProtocol.Serialize
{
    public static partial class MessageSerializer
    {
        static MessageSerializer()
        {
  
        }

        public static void RegisterType(Type type)
        {
            RegisterSerializeInvoker(type);
            RegisterDeserializeInvoker(type);
        }
    }
}

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
        static  ConcurrentDictionary<Type, object> _registeredType = new();

        static MessageSerializer()
        {

        }

        public static void RegisterType(Type type)
        {
            uint messageId = GetMessageIdByType(type);
            if(_registeredType.TryGetValue(type, out var registered))
                throw new InvalidOperationException($@"
Message type with ID {messageId} is already registered.
{type.FullName} and {type.FullName} overlapped");
            else
                _registeredType.TryAdd(type, null);

            RegisterSerializeInvoker(type);
            RegisterDeserializeInvoker(type);
        }

        static uint GetMessageIdByType(Type type)
        {
            return (uint)type
                        .GetProperty("MessageId", BindingFlags.Static | BindingFlags.Public)
                        .GetValue(null);
        }
    }
}

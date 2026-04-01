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
        static ConcurrentDictionary<uint, Type> _registeredType = new();

        static MessageSerializer()
        {

        }

        public static void RegisterType(Type type)
        {
            uint messageId = GetMessageIdByType(type);
            if(_registeredType.TryGetValue(messageId, out var registedType))
                throw new InvalidOperationException($@"
Message type with ID {messageId} is already registered.
{type.FullName} and {registedType.FullName} overlapped");
            else
                _registeredType.TryAdd(messageId, type);

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

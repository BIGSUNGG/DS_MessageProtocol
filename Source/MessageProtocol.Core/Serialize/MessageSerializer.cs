using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using MessageProtocol;

namespace MessageProtocol.Serialize
{
    public static partial class MessageSerializer
    {
        static readonly ConcurrentDictionary<Type, byte> _registeredTypes = new();
        static readonly ConcurrentDictionary<uint, Type> _registeredMessageIds = new();
        static readonly ConcurrentDictionary<Type, uint> _messageIdCache = new();

        static MessageSerializer()
        {

        }

        public static void RegisterType(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            uint messageId = GetMessageIdByType(type);
            if (!_registeredTypes.TryAdd(type, 0))
            {
                throw new InvalidOperationException($"Message type '{type.FullName}' is already registered.");
            }

            bool registeredMessageId = false;
            try
            {
                RegisterSerializeInvoker(type);

                byte headerByte = (byte)(messageId >> 24);
                if (MessageWireFormat.HasEmbeddedMessageId(headerByte))
                {
                    var existingType = _registeredMessageIds.GetOrAdd(messageId, type);
                    if (!ReferenceEquals(existingType, type))
                    {
                        throw new InvalidOperationException(
                            $"Message type with ID {messageId} is already registered by '{existingType.FullName}'.");
                    }

                    registeredMessageId = true;
                    RegisterDeserializeInvoker(type, messageId);
                }
            }
            catch
            {
                _registeredTypes.TryRemove(type, out _);
                _serializeCache.TryRemove(type, out _);
                if (registeredMessageId)
                {
                    _registeredMessageIds.TryRemove(new KeyValuePair<uint, Type>(messageId, type));
                    _deserializeCache.TryRemove(messageId, out _);
                }

                throw;
            }
        }

        static uint GetMessageIdByType(Type type)
        {
            return _messageIdCache.GetOrAdd(type, ResolveMessageIdByType);
        }

        static uint ResolveMessageIdByType(Type type)
        {
            var messageIdProperty = type.GetProperty("MessageId", BindingFlags.Static | BindingFlags.Public);
            if (messageIdProperty == null)
            {
                throw new InvalidOperationException(
                    $"Type '{type.FullName}' does not expose a public static MessageId property.");
            }

            object? value = messageIdProperty.GetValue(null);
            if (value is uint messageId)
            {
                return messageId;
            }

            throw new InvalidOperationException(
                $"Type '{type.FullName}' has an invalid MessageId property. Expected a uint value.");
        }

    }
}

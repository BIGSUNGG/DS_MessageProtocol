using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DS.MessageProtocol
{
    public class MessageGroupRootWrapper
    {
        public Type RootMessageType { get; private set; }
        public MessageGroupRoot RootMessageAttribute { get; private set; }
        public IReadOnlyDictionary<Type, MessageGroupElementWrapper> Elements { get; private set; } = new Dictionary<Type, MessageGroupElementWrapper>();

        public MessageGroupRootWrapper(Type rootMessageType, IReadOnlyDictionary<Type, MessageGroupElementWrapper> elements)
        {
            RootMessageType = rootMessageType;
            RootMessageAttribute = rootMessageType.GetCustomAttribute<MessageGroupRoot>(false);
            if(RootMessageAttribute == null)
                throw new InvalidOperationException($"Type {rootMessageType.FullName} does not have MessageGroupRoot attribute");

            Elements = elements;
        }
    }

    public class MessageGroupElementWrapper
    {
        public Type RootMessageType { get; private set; }
        public MessageGroupRoot RootMessageAttribute { get; private set; }

        public Type ElementMessageType { get; private set; }
        public MessageGroupElement ElementMessageAttribute { get; private set; }

        public MessageGroupElementWrapper(Type rootMessageType, Type elementMessageType)
        {
            RootMessageType = rootMessageType;
            RootMessageAttribute = RootMessageType.GetCustomAttribute<MessageGroupRoot>(false);
            if (RootMessageAttribute == null)
                throw new InvalidOperationException($"Type {rootMessageType.FullName} does not have MessageGroupRoot attribute");

            ElementMessageType = elementMessageType;
            ElementMessageAttribute = elementMessageType.GetCustomAttribute<MessageGroupElement>(false);
            if(ElementMessageAttribute == null)
                throw new InvalidOperationException($"Type {elementMessageType.FullName} does not have MessageGroupElement attribute");
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DS.MessageProtocol;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class MessageGroupRoot : Attribute
{
    public ushort MessageRootId { get; private set; }

    public MessageGroupRoot(ushort messageRootId)
    {
        MessageRootId = messageRootId;
    }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class MessageGroupElement : Attribute
{
    public ushort MessageElementId { get; private set; }

    public MessageGroupElement(ushort messageElementId)
    {
        MessageElementId = messageElementId;
        if (MessageElementId.Equals(0))
            throw new InvalidOperationException("MessageElementId cannot be 0");
    }
}
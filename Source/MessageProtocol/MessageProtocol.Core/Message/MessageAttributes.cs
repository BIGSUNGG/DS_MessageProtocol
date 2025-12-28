using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DS.MessageProtocol;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public class MessageGroupRootAttribute : Attribute
{
    public ushort MessageRootId { get; private set; }

    public MessageGroupRootAttribute(ushort messageRootId)
    {
        MessageRootId = messageRootId;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public class MessageGroupElementAttribute : Attribute
{
    public ushort MessageElementId { get; private set; }

    public MessageGroupElementAttribute(ushort messageElementId)
    {
        MessageElementId = messageElementId;
        if (MessageElementId.Equals(0))
            throw new InvalidOperationException("MessageElementId cannot be 0");
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public class MessageStandaloneAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public class MessageIgnoreAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public class MessageIncludeAttribute : Attribute
{
}
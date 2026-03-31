using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MessageProtocol;

static class MessageAttributeRange
{
    public const uint MaxValue = 0x00FF_FFFF;

    public static void Validate(uint value, string parameterName)
    {
        if (value > MaxValue)
            throw new InvalidOperationException($"{parameterName} must be between 0 and {MaxValue} (2^24 - 1).");
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public class MessageGroupRootAttribute : Attribute
{
    public uint MessageRootId { get; private set; }

    public MessageGroupRootAttribute(uint messageRootId)
    {
        MessageAttributeRange.Validate(messageRootId, nameof(messageRootId));
        MessageRootId = messageRootId;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public class MessageGroupElementAttribute : Attribute
{
    public uint MessageElementId { get; private set; }

    public MessageGroupElementAttribute(uint messageElementId)
    {
        MessageAttributeRange.Validate(messageElementId, nameof(messageElementId));
        MessageElementId = messageElementId;
        if (MessageElementId.Equals(0))
            throw new InvalidOperationException("MessageElementId cannot be 0");
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public class MessageStandaloneAttribute : Attribute
{
    public uint MessageStandaloneId { get; private set; }

    public MessageStandaloneAttribute(uint messageStandaloneId)
    {
        MessageAttributeRange.Validate(messageStandaloneId, nameof(messageStandaloneId));
        MessageStandaloneId = messageStandaloneId;
    }
}


[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public class MessageAttribute : Attribute
{
    public MessageAttribute()
    {
    }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public class MessageIgnoreAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public class MessageIncludeAttribute : Attribute
{
}

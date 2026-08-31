using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MessageProtocol;

static class MessageAttributeRange
{
    public const uint MaxValue = MessageWireFormat.MessageIdValueMask;

    public static void Validate(uint value, string parameterName)
    {
        if (value > MaxValue)
            throw new InvalidOperationException($"{parameterName} must be between 0 and {MaxValue} (2^24 - 1).");
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public class GroupRootMessageAttribute : Attribute
{
    public uint GroupRootMessageId { get; private set; }

    public GroupRootMessageAttribute(uint groupRootMessageId)
    {
        MessageAttributeRange.Validate(groupRootMessageId, nameof(groupRootMessageId));
        GroupRootMessageId = groupRootMessageId;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public class GroupElementMessageAttribute : Attribute
{
    public uint GroupElementMessageId { get; private set; }

    public GroupElementMessageAttribute(uint groupElementMessageId)
    {
        MessageAttributeRange.Validate(groupElementMessageId, nameof(groupElementMessageId));
        GroupElementMessageId = groupElementMessageId;
        if (GroupElementMessageId.Equals(0))
            throw new InvalidOperationException("GroupElementMessageId cannot be 0");
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public class StandaloneMessageAttribute : Attribute
{
    public uint StandaloneMessageId { get; private set; }

    public StandaloneMessageAttribute(uint standaloneMessageId)
    {
        MessageAttributeRange.Validate(standaloneMessageId, nameof(standaloneMessageId));
        StandaloneMessageId = standaloneMessageId;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public class NonIdMessageAttribute : Attribute
{
    public NonIdMessageAttribute()
    {
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false,
    Inherited = false)]
public class MessageCategoryAttribute : Attribute
{
    MessageCategory  Category { get; }

    public MessageCategoryAttribute(MessageCategory category)
    {
        Category = category;
    }
}
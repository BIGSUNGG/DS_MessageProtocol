using System;

namespace MessageProtocol
{
    static class MessageAttributeRange
    {
        public const uint MaxValue = MessageWireFormat.MessageIdValueMask;

        public static void Validate(uint value, string parameterName)
        {
            if (value > MaxValue)
            {
                throw new InvalidOperationException($"{parameterName} must be between 0 and {MaxValue} (2^24 - 1).");
            }
        }
    }

    /// <summary>독립 ID 메시지. 헤더 4바이트.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public class StandaloneMessageAttribute : Attribute
    {
        public uint StandaloneMessageId { get; }

        public StandaloneMessageAttribute(uint standaloneMessageId)
        {
            MessageAttributeRange.Validate(standaloneMessageId, nameof(standaloneMessageId));
            StandaloneMessageId = standaloneMessageId;
        }
    }

    /// <summary>그룹 루트 메시지. 상속 계층의 꼭대기.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public class GroupRootMessageAttribute : Attribute
    {
        public uint GroupRootMessageId { get; }

        public GroupRootMessageAttribute(uint groupRootMessageId)
        {
            MessageAttributeRange.Validate(groupRootMessageId, nameof(groupRootMessageId));
            GroupRootMessageId = groupRootMessageId;
        }
    }

    /// <summary>그룹 요소 메시지. 상속 계층에 그룹 루트가 필수이며 id 는 0 일 수 없다.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public class GroupElementMessageAttribute : Attribute
    {
        public uint GroupElementMessageId { get; }

        public GroupElementMessageAttribute(uint groupElementMessageId)
        {
            MessageAttributeRange.Validate(groupElementMessageId, nameof(groupElementMessageId));
            if (groupElementMessageId == 0)
            {
                throw new InvalidOperationException("GroupElementMessageId cannot be 0");
            }
            GroupElementMessageId = groupElementMessageId;
        }
    }

    /// <summary>ID 없는 메시지. 헤더 1바이트. object Deserialize 대상이 아니다.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public class NonIdMessageAttribute : Attribute
    {
    }

    /// <summary>헤더 category 니블(0~15) 지정.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public class MessageCategoryAttribute : Attribute
    {
        public MessageCategory Category { get; }

        public MessageCategoryAttribute(MessageCategory category)
        {
            Category = category;
        }
    }
}

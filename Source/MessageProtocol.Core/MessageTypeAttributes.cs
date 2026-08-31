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

    /// <summary>
    /// 제네릭 메시지의 직렬화 지원 구성(닫힌 제네릭) 선언. 선언부·캐리어 등 임의의 타입 선언에
    /// 구성마다 반복 부착한다: <c>[GenericMessage(typeof(Envelope&lt;Ping&gt;), ClassId = 1)]</c>.
    /// 선언된 구성은 생성 코드가 (MessageId, ClassId) 키로 모듈 로드 시 자동 등록해 송수신 양쪽에서
    /// object dispatch 가 동작한다. 구성 미선언 제네릭 메시지의 직렬화는 예외.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public class GenericMessageAttribute : Attribute
    {
        public Type Construction { get; }

        uint _classId;

        /// <summary>구성 클래스 식별자. 헤더의 MessageId 뒤에 3바이트로 기록된다. 1 .. 2^24-1.</summary>
        public uint ClassId
        {
            get => _classId;
            set
            {
                if (value == 0)
                {
                    throw new InvalidOperationException("ClassId cannot be 0");
                }
                MessageAttributeRange.Validate(value, nameof(value));
                _classId = value;
            }
        }

        public GenericMessageAttribute(Type construction)
        {
            Construction = construction ?? throw new ArgumentNullException(nameof(construction));
        }
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

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

    /// <summary>
    /// 메시지 선언 속성 — 종류·ID·카테고리의 유일한 진입점.
    /// <para>
    /// <c>Kind</c> 는 <see cref="MessageKind"/> 값대로 종류를 확정하고, <see cref="MessageKind.Automatic"/> 은
    /// 계층에서 추론한다. <c>Id</c> 를 생략(0)하면 MessageId 는 타입 FullName 의 FNV-1a 해시(24비트,
    /// <see cref="MessageIdHash"/>)로 결정되고, 명시하면 수동 할당이다(단, 0 은 '생략'을 뜻하므로 수동 0 은 불가).
    /// 해시 충돌·Child 위치의 해시 0 은 진단 에러로 거부된다. <see cref="MessageKind.NonId"/> 는
    /// id·category 인자와 함께 쓰면 진단 에러(MSGPROT018)다.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public class MessageAttribute : Attribute
    {
        /// <summary>메시지 종류. 기본 Automatic(계층 추론).</summary>
        public MessageKind Kind { get; }

        /// <summary>수동 MessageId. 0(생략)이면 FullName 해시로 결정된다.</summary>
        public uint Id { get; }

        /// <summary>헤더 하위 니블(0~15). 기본 Category0. NonId 에서는 사용 불가.</summary>
        public MessageCategory Category { get; }

        public MessageAttribute(
            MessageKind kind = MessageKind.Automatic,
            uint id = 0,
            MessageCategory category = MessageCategory.Category0)
        {
            MessageAttributeRange.Validate(id, nameof(id));
            Kind = kind;
            Id = id;
            Category = category;
        }
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
}

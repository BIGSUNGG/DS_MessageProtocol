using MessageProtocol;
using MessageProtocol.Serialize;
using MessageProtocol.Tests.Fixtures;
using Xunit;

namespace MessageProtocol.Tests;

public class DispatchTests
{
    [Fact]
    public void object_dispatch는_헤더_MessageId로_타입을_라우팅한다()
    {
        object msg = new FlatMessage { Value = 12 };
        byte[] bytes = MessageSerializer.Serialize(msg);

        object? decoded = MessageSerializer.Deserialize(bytes);
        Assert.IsType<FlatMessage>(decoded);
        Assert.Equal(12, ((FlatMessage)decoded).Value);
    }

    [Fact]
    public void 다형성은_런타임_타입으로_직렬화한다()
    {
        EventBase e = new LogoutEvent { Timestamp = 5, Reason = 3 };
        byte[] bytes = MessageSerializer.Serialize((object)e);

        object? decoded = MessageSerializer.Deserialize(bytes);
        Assert.IsType<LogoutEvent>(decoded);
        var logout = Assert.IsType<LogoutEvent>(decoded);
        Assert.Equal(5, logout.Timestamp);
        Assert.Equal(3, logout.Reason);
    }

    [Fact]
    public void 제네릭_경로는_선언_타입을_사용한다()
    {
        EventBase e = new LogoutEvent { Timestamp = 5, Reason = 3 };

        // Serialize<T>(T=EventBase) → 런타임 파생 타입 무시, 베이스로 직렬화
        byte[] bytes = MessageSerializer.Serialize(e);
        Assert.Equal(EventBase.MessageId, ReadMessageId(bytes));
    }

    [Fact]
    public void 그룹_요소_타입들이_각자_라우팅된다()
    {
        object? login = MessageSerializer.Deserialize(MessageSerializer.Serialize((object)new LoginEvent { User = "u" }));
        object? logout = MessageSerializer.Deserialize(MessageSerializer.Serialize((object)new LogoutEvent { Reason = 1 }));

        Assert.IsType<LoginEvent>(login);
        Assert.IsType<LogoutEvent>(logout);
    }

    [Fact]
    public void NonId는_object_역직렬화에서_거부된다()
    {
        byte[] bytes = MessageSerializer.Serialize(new NoIdMessage { Flag = 1 });
        Assert.Throws<InvalidCastException>(() => MessageSerializer.Deserialize(bytes));
    }

    [Fact]
    public void 미등록_ID는_KeyNotFound()
    {
        // Standalone 플래그 + 아무도 등록하지 않은 ID 값
        byte[] bytes =
        [
            MessageWireFormat.ComposeHeaderByte(MessageFlag.Standalone, 0),
            0x7F, 0xFF, 0xFE,
        ];
        Assert.Throws<KeyNotFoundException>(() => MessageSerializer.Deserialize(bytes));
    }

    [Fact]
    public void 너무_짧은_ID_데이터는_예외()
    {
        byte[] bytes = [MessageWireFormat.ComposeHeaderByte(MessageFlag.Standalone, 0), 0x00];
        Assert.Throws<ArgumentException>(() => MessageSerializer.Deserialize(bytes));
    }

    [Fact]
    public void SerializeToWriter는_중첩_기록에_사용된다()
    {
        var writer = MessageBufferWriter.Create();
        MessageSerializer.SerializeToWriter(new FlatMessage { Value = 21 }, ref writer);

        var decoded = (FlatMessage)MessageSerializer.Deserialize(writer.WrittenReadOnlySpan);
        Assert.Equal(21, decoded.Value);
        writer.Dispose();
    }

    static uint ReadMessageId(byte[] bytes)
    {
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }
}

public class RegistrationTests
{
    [Fact]
    public void 수동_구현_타입을_RegisterType으로_등록한다()
    {
        MessageSerializer.RegisterType(typeof(ManualStandalone));

        var msg = new ManualStandalone { Value = 555 };
        byte[] bytes = MessageSerializer.Serialize(msg);

        Assert.Equal(555, MessageSerializer.Deserialize<ManualStandalone>(bytes).Value);

        var decoded = Assert.IsType<ManualStandalone>(MessageSerializer.Deserialize(bytes));
        Assert.Equal(555, decoded.Value);
    }

    [Fact]
    public void 중복_등록은_예외()
    {
        // FlatMessage 는 모듈 초기화에서 이미 등록됨
        Assert.Throws<InvalidOperationException>(() => MessageSerializer.RegisterHasIdMessage<FlatMessage>());
    }

    [Fact]
    public void 계약_미구현_타입_등록은_예외()
    {
        Assert.Throws<InvalidOperationException>(() => MessageSerializer.RegisterType(typeof(NotAMessage)));
    }

    [Fact]
    public void ID_충돌_등록은_예외()
    {
        Assert.Throws<InvalidOperationException>(() =>
            MessageSerializer.RegisterHasIdMessage<FlatMessage>(
                FlatMessage.Serialize,
                FlatMessage.Deserialize,
                LoginEvent.MessageId)); // 이미 LoginEvent 가 점유한 ID
    }

    [Fact]
    public void 미등록_타입_object_직렬화는_지연_등록을_시도하고_실패한다()
    {
        Assert.Throws<InvalidOperationException>(() => MessageSerializer.Serialize((object)new NotAMessage()));
    }

    [Fact]
    public void NonId_델리게이트_등록은_MessageId_라우팅에_등장하지_않는다()
    {
        // NoIdMessage 는 모듈 초기화에서 등록됨 — 다시 등록하면 중복 예외
        Assert.Throws<InvalidOperationException>(() => MessageSerializer.RegisterNonIdMessage<NoIdMessage>());
    }
}

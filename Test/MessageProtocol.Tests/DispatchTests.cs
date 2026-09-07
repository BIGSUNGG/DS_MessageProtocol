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
        // 와이어 내용 불법(NonId 플래그)은 InvalidDataException — InvalidCastException 은 캐스트가
        // 일어난 적 없는데 유형부터 오해를 줘 신뢰 경계 거부 분류에서 빠졌다(2026-09-08 퍼저, KI-41 계열).
        Assert.Throws<System.IO.InvalidDataException>(() => MessageSerializer.Deserialize(bytes));
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

    // ---------- 추상 메시지 타입 멤버 (KI-24) ----------

    [Fact]
    public void 추상_그룹_루트_멤버는_구체_요소로_왕복한다()
    {
        var envelope = new CommandEnvelope
        {
            Command = new StartCommand { Seq = 7, Target = "alpha" },
            History = new List<AbstractCommand>
            {
                new StartCommand { Seq = 1, Target = "a" },
                new StopCommand { Seq = 2, Code = 9 },
            },
        };

        var roundTrip = MessageSerializer.Deserialize<CommandEnvelope>(MessageSerializer.Serialize(envelope));

        // 런타임 디스패치라 선언 타입(추상 루트)이 아니라 구체 요소 타입이 복원되고 파생 멤버가 유실되지 않는다.
        var command = Assert.IsType<StartCommand>(roundTrip.Command);
        Assert.Equal(7, command.Seq);          // 베이스(루트) 멤버
        Assert.Equal("alpha", command.Target); // 파생 멤버

        Assert.Equal(2, roundTrip.History!.Count);
        Assert.Equal("a", Assert.IsType<StartCommand>(roundTrip.History[0]).Target);
        Assert.Equal(9, Assert.IsType<StopCommand>(roundTrip.History[1]).Code);
    }

    [Fact]
    public void 추상_그룹_루트_멤버의_null은_null로_왕복한다()
    {
        var roundTrip = MessageSerializer.Deserialize<CommandEnvelope>(
            MessageSerializer.Serialize(new CommandEnvelope()));

        Assert.Null(roundTrip.Command);
        Assert.Null(roundTrip.History);
    }

    [Fact]
    public void 추상_그룹_루트_멤버를_든_메시지도_object_dispatch로_왕복한다()
    {
        object envelope = new CommandEnvelope { Command = new StopCommand { Seq = 3, Code = 5 } };

        var roundTrip = (CommandEnvelope)MessageSerializer.Deserialize(MessageSerializer.Serialize(envelope))!;

        Assert.Equal(3, roundTrip.Command!.Seq);
        Assert.Equal(5, Assert.IsType<StopCommand>(roundTrip.Command).Code);
    }

    [Fact]
    public void 구체_베이스_멤버는_선언_타입으로_직렬화되어_파생_멤버가_유실된다()
    {
        // KI-29 현재 동작 고정: 파생 메시지 타입이 있는 **구체** 베이스를 멤버 정적 타입으로 쓰면
        // 선언 타입 기준으로 기록되어 파생 멤버가 예외 없이 사라지고 복원 타입도 베이스가 된다
        // (실험 확인: 13바이트 프레임에서 LoginEvent.User 유실). 생성기가 MSGPROT012 로 이 형태를 경고하며,
        // 다형이 필요하면 루트를 abstract 로 선언해 런타임 디스패치(KI-24)로 해결한다.
        var host = new EventHost { Event = new LoginEvent { Timestamp = 5, User = "kim" } };

        var roundTrip = MessageSerializer.Deserialize<EventHost>(MessageSerializer.Serialize(host));

        Assert.Equal(5, roundTrip.Event!.Timestamp);   // 베이스 멤버는 유지
        Assert.IsType<EventBase>(roundTrip.Event);     // 파생이 아니라 베이스 인스턴스로 복원 = User 는 와이어에 없다
    }

    // ---------- 디스패치 멤버 공유 참조 (KI-9 해소) ----------

    [Fact]
    public void 추상_디스패치_멤버를_통한_공유_참조는_참조_동일성을_복원한다()
    {
        // 같은 인스턴스가 두 추상 멤버에 등장하면 두 번째부터 백레퍼런스로 기록된다 — 수정 전에는
        // 매 디스패치마다 새 SerializeContext 로 풀 프레임이 중복 기록되어 수신 측에서 별개 인스턴스 2개가 되었다.
        var shared = new StartCommand { Seq = 42, Target = "t" };
        var envelope = new CommandEnvelope { Command = shared, History = new List<AbstractCommand> { shared } };

        var back = MessageSerializer.Deserialize<CommandEnvelope>(MessageSerializer.Serialize(envelope));

        var command = Assert.IsType<StartCommand>(back!.Command);
        Assert.Equal(42L, command.Seq);
        Assert.Same(back.Command, back.History![0]);
    }

    [Fact]
    public void 타입_매개변수_디스패치_멤버를_통한_공유_참조도_참조_동일성을_복원한다()
    {
        var shared = new FlatMessage { Value = 7 };
        var envelope = new GenericEnvelope<FlatMessage> { Value = shared, Items = new List<FlatMessage?> { shared, null } };

        var back = MessageSerializer.Deserialize<GenericEnvelope<FlatMessage>>(MessageSerializer.Serialize(envelope));

        Assert.Equal(7, back!.Value!.Value);
        Assert.Same(back.Value, back.Items![0]);
        Assert.Null(back.Items[1]);   // 공유가 없는 원소는 여전히 그대로
    }

    [Fact]
    public void 그래프_밖_위임_멤버를_통한_공유_참조도_참조_동일성을_복원한다()
    {
        // 다른 어셈블리의 구체 메시지 멤버(EmitOutOfGraphMessage*)도 같은 계약 — KI-9 의 나머지 절반.
        var shared = new MessageProtocol.NetStandardFixtures.FallbackCollections { Bulk = new List<int> { 1, 2 } };
        var host = new SharedOutOfGraphHost { First = shared, Second = shared };

        var back = MessageSerializer.Deserialize<SharedOutOfGraphHost>(MessageSerializer.Serialize(host));

        Assert.Equal(new[] { 1, 2 }, back!.Second!.Bulk!);
        Assert.Same(back.First, back.Second);
    }
}

// ---------- object 진입점 계약 가드 (2026-09-08 테스트 갭 일괄 폐쇄) ----------

/// <summary>object dispatch 진입점의 null·미등록 계약을 실행으로 고정한다(구현은 있었으나 무테스트).</summary>
public class ObjectEntryGuardTests
{
    [Fact]
    public void Serialize_object는_null을_거부한다()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => MessageSerializer.Serialize(null!));
        Assert.Equal("message", exception.ParamName);
    }

    [Fact]
    public void SerializePooled_object는_null을_거부한다()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => MessageSerializer.SerializePooled(null!));
        Assert.Equal("message", exception.ParamName);
    }

    [Fact]
    public void SerializeToWriter는_null을_거부한다()
    {
        var writer = MessageBufferWriter.Create();
        ArgumentNullException? exception = null;
        try
        {
            MessageSerializer.SerializeToWriter(null!, ref writer);
        }
        catch (ArgumentNullException caught)
        {
            exception = caught;
        }

        Assert.NotNull(exception);
        Assert.Equal("message", exception.ParamName);
    }

    [Fact]
    public void SerializeToWriter는_미등록_타입을_등록_안내_예외로_거부한다()
    {
        // 미등록 타입은 GetWriterInvoker 의 지연 RegisterType 을 탄다 — 메시지 구현이 없는 타입은
        // "IMessageSerializable 구현 없음" 안내로, 있는 타입은 지연 등록 후 정상 동작(다른 테스트 고정).
        var writer = MessageBufferWriter.Create();
        InvalidOperationException? exception = null;
        try
        {
            MessageSerializer.SerializeToWriter(new NotAMessage(), ref writer);
        }
        catch (InvalidOperationException caught)
        {
            exception = caught;
        }

        Assert.NotNull(exception);
        Assert.Contains(nameof(NotAMessage), exception.Message);
        Assert.Contains("IMessageSerializable", exception.Message);
        Assert.Equal(0, writer.Length); // 깊이 계상 전에 거부 — 상태 오염 없음
    }
}

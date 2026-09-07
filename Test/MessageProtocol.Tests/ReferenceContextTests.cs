using MessageProtocol.Serialize;
using MessageProtocol.Tests.Fixtures;
using Xunit;

namespace MessageProtocol.Tests;

/// <summary>
/// KI-30 회귀: 참조 추적 컨텍스트는 `_firstObject is null` 을 **빈 슬롯 sentinel** 로 쓰므로,
/// null 을 등록하면 슬롯이 차지되지 않아 다음 객체도 id 1 을 받았다(실험 확인: `RegisterObject(null)` → 1,
/// 이은 `RegisterObject(객체)` → 1). 읽기 쪽도 같아서 `GetObject(1)` 이 백레퍼런스를 다른 인스턴스로 해석했다 —
/// 예외 없이 객체 그래프가 조용히 손상되므로 공개 경계에서 null 을 거부한다.
/// 생성 코드는 null 을 `ReferenceKind.Null` 로 먼저 걸러 이 경로를 타지 않는다(수동 구현 대상 계약).
/// </summary>
public class ReferenceContextTests
{
    [Fact]
    public void SerializeContext는_null_등록을_거부한다()
    {
        var context = default(MessageSerializer.SerializeContext);

        Assert.Throws<ArgumentNullException>(() => context.RegisterObject(null!));
    }

    [Fact]
    public void SerializeContext는_null_id_조회를_거부한다()
    {
        var context = default(MessageSerializer.SerializeContext);

        Assert.Throws<ArgumentNullException>(() => context.TryGetObjectId(null!, out _));
    }

    [Fact]
    public void DeserializeContext는_null_등록을_거부한다()
    {
        var context = default(MessageSerializer.DeserializeContext);

        Assert.Throws<ArgumentNullException>(() => context.RegisterNewObject(null!));
    }

    [Fact]
    public void 거부_후에도_컨텍스트는_오염되지_않고_정상_사용된다()
    {
        var context = default(MessageSerializer.SerializeContext);
        Assert.Throws<ArgumentNullException>(() => context.RegisterObject(null!));

        var value = new object();

        Assert.Equal(1, context.RegisterObject(value));
        Assert.True(context.TryGetObjectId(value, out int objectId));
        Assert.Equal(1, objectId);
    }

    [Fact]
    public void 정상_경로_id_발급과_승격_백레퍼런스_복원은_그대로_동작한다()
    {
        // 가드가 빈 슬롯 sentinel·Dictionary 승격 경로를 깨지 않았는지 고정한다.
        var first = new object();
        var second = new object();
        var third = new object();

        var write = default(MessageSerializer.SerializeContext);
        Assert.Equal(1, write.RegisterObject(first));
        Assert.Equal(2, write.RegisterObject(second));   // 두 번째 등록에서 Dictionary 로 승격
        Assert.Equal(3, write.RegisterObject(third));

        Assert.True(write.TryGetObjectId(first, out int firstId));
        Assert.Equal(1, firstId);
        Assert.True(write.TryGetObjectId(third, out int thirdId));
        Assert.Equal(3, thirdId);
        Assert.False(write.TryGetObjectId(new object(), out _));

        var read = default(MessageSerializer.DeserializeContext);
        Assert.Equal(1, read.RegisterNewObject(first));
        Assert.Equal(2, read.RegisterNewObject(second));
        Assert.Equal(3, read.RegisterNewObject(third));

        Assert.Same(first, read.GetObject(1));
        Assert.Same(third, read.GetObject(3));
    }
}

// ---------- 베이스 타입 멤버 공유 백레퍼런스 판독 (감사 원장 HIGH — 2026-09-07 실험·완화 고정) ----------

public class SharedBaseBackReferenceTests
{
    [Fact]
    public void 베이스_파생_멤버로_같은_인스턴스를_공유하면_안내_InvalidDataException으로_거부된다()
    {
        // 실험(2026-09-07, 2.2.0 생성 코드): 구체 베이스 멤버(EventBase)가 먼저 베이스 필드만 기록하고 인스턴스를
        // 등록하면, 파생 멤버(LoginEvent)의 백레퍼런스 판독은 등록된 EventBase 인스턴스를 LoginEvent 로 캐스트한다.
        // 수정 전은 원인을 알려주지 않는 InvalidCastException — 이제 상황과 해법을 안내하는 InvalidDataException.
        var login = new LoginEvent { Timestamp = 5, User = "kim" };
        var host = new SharedBaseDerivedHost { First = login, Second = login };

        var bytes = MessageSerializer.Serialize(host);
        var exception = Assert.Throws<System.IO.InvalidDataException>(
            () => MessageSerializer.Deserialize<SharedBaseDerivedHost>(bytes));

        Assert.Contains("EventBase", exception.Message);
        Assert.Contains("LoginEvent", exception.Message);
        Assert.Contains(nameof(SharedBaseDerivedHost.Second), exception.Message);
        Assert.Contains("less derived", exception.Message);
    }

    [Fact]
    public void 베이스_멤버_2곳_공유는_조용한_타입_좁힘으로_복원된다_현재_동작_고정()
    {
        // KI-34 제약 고정: 두 멤버가 모두 베이스 타입이면 예외 없이 왕복하지만 파생 필드(User)는 유실되고
        // 두 멤버가 같은 **베이스** 인스턴스를 공유한다. 전체 해결은 와이어 변경(중첩 메시지 디스패치)이 필요해
        // 정책 결정 사항 — 다형이 필요하면 루트를 abstract 로 선언해 런타임 디스패치로 보낸다(MSGPROT012 안내).
        var login = new LoginEvent { Timestamp = 5, User = "kim" };
        var host = new SharedBaseBaseHost { First = login, Second = login };

        var back = MessageSerializer.Deserialize<SharedBaseBaseHost>(MessageSerializer.Serialize(host));

        Assert.Equal(5L, back.First!.Timestamp);          // 베이스 필드는 유지
        Assert.IsType<EventBase>(back.First);             // 파생이 아니라 베이스 인스턴스로 복원 = User 유실
        Assert.Same(back.First, back.Second);             // 참조 동일성은 유지(2.2.0, KI-9)
    }

    [Fact]
    public void 디스패치_멤버와_구체_멤버로_같은_인스턴스를_공유하면_파생_필드까지_복원된다()
    {
        // 대조군: 첫 등장이 런타임 디스패치(추상 멤버)면 구체 타입이 헤더째 기록되므로, 이후 어떤 멤버의
        // 백레퍼런스도 온전한 구체 인스턴스를 받는다 — KI-24 디스패치와 KI-9 참조 추적의 정상 조합.
        var start = new StartCommand { Seq = 9, Target = "t" };
        var host = new SharedDispatchConcreteHost { Command = start, Concrete = start };

        var back = MessageSerializer.Deserialize<SharedDispatchConcreteHost>(MessageSerializer.Serialize(host));

        var command = Assert.IsType<StartCommand>(back.Command);
        Assert.Equal(9L, command.Seq);
        Assert.Equal("t", command.Target);
        Assert.Same(back.Command, back.Concrete);
    }
}


// ---------- 알 수 없는 참조 태그 거부 (신뢰 경계 — 2026-09-08 감사) ----------

/// <summary>
/// 참조 태그 바이트는 규격상 0(Null)·1(NewObject)·2(BackReference) 뿐이다. 수정 전 생성 코드는
/// 그 외의 값(3–255)을 조용히 NewObject 로 해석해 손상·변조 프레임을 파싱했다(프레임 역동기화로
/// 공격자가 만든 형태의 객체로 복원됨). 세 읽기 경로(그래프 내부·외부 위임·런타임 디스패치) 모두
/// 이제 즉시 InvalidDataException 으로 거부한다.
/// </summary>
public class UnknownReferenceKindTests
{
    static byte[] SerializeWithLeadingReferenceMember<T>(T host) where T : class
    {
        var bytes = MessageSerializer.Serialize(host);
        // 루트 헤더(임베디드 id 4바이트) 뒤 첫 바이트가 첫 참조 멤버의 태그(NewObject=1)다 — 레이아웃 고정.
        Assert.True(bytes.Length > 4, "Serialized payload is too short to contain a reference tag.");
        Assert.Equal((byte)MessageSerializer.ReferenceKind.NewObject, bytes[4]);
        return bytes;
    }

    [Fact]
    public void 그래프_내부_멤버의_알수없는_참조태그는_즉시_거부된다()
    {
        var host = new SharedBaseBaseHost { First = new LoginEvent { Timestamp = 1, User = "u" } };
        var bytes = SerializeWithLeadingReferenceMember(host);

        foreach (byte hostile in new byte[] { 3, 0xFF })
        {
            bytes[4] = hostile;
            var exception = Assert.Throws<System.IO.InvalidDataException>(
                () => MessageSerializer.Deserialize<SharedBaseBaseHost>(bytes));
            Assert.Contains($"Unknown reference kind {hostile}", exception.Message);
        }
    }

    [Fact]
    public void 그래프_밖_위임_멤버의_알수없는_참조태그는_즉시_거부된다()
    {
        var host = new SharedOutOfGraphHost { First = new MessageProtocol.NetStandardFixtures.FallbackCollections() };
        var bytes = SerializeWithLeadingReferenceMember(host);

        bytes[4] = 3;
        var exception = Assert.Throws<System.IO.InvalidDataException>(
            () => MessageSerializer.Deserialize<SharedOutOfGraphHost>(bytes));
        Assert.Contains("Unknown reference kind 3", exception.Message);
    }

    [Fact]
    public void 런타임_디스패치_멤버의_알수없는_참조태그는_즉시_거부된다()
    {
        var host = new SharedDispatchConcreteHost { Command = new StartCommand { Seq = 1, Target = "t" } };
        var bytes = SerializeWithLeadingReferenceMember(host);

        bytes[4] = 3;
        var exception = Assert.Throws<System.IO.InvalidDataException>(
            () => MessageSerializer.Deserialize<SharedDispatchConcreteHost>(bytes));
        Assert.Contains("Unknown reference kind 3", exception.Message);
    }

    [Fact]
    public void 정상_태그_왕복은_그대로_동작한다()
    {
        // 가드가 합법 프레임(0/1/2)을 깨지 않는지 고정 — 세 경로 대표 1개씩 왕복.
        var host = new SharedDispatchConcreteHost { Command = new StartCommand { Seq = 7, Target = "t" } };
        var back = MessageSerializer.Deserialize<SharedDispatchConcreteHost>(MessageSerializer.Serialize(host));
        Assert.Equal(7L, Assert.IsType<StartCommand>(back.Command).Seq);
    }
}

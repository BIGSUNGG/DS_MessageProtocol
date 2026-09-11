using MessageProtocol;
using MessageProtocol.NetStandardFixtures;
using MessageProtocol.Serialize;
using MessageProtocol.Tests.Fixtures;
using Xunit;

namespace MessageProtocol.Tests.Fixtures
{
    // [Message] 종류 자동 추론 픽스처. FullName 해시 ID 핀 값은 테스트 단언에 하드코딩돼 있다 —
    // 알고리즘(FNV-1a → 24비트 마스크)이나 FullName 형식이 바뀌면 이 파일의 핀 부터 깨진다.

    /// <summary>조상 없음 + 파생 없음 → Standalone 추론. FullName "MessageProtocol.Tests.Fixtures.AutoStandalone" 해시 0x1F6FBD.</summary>
    [Message]
    public partial class AutoStandalone
    {
        public int Value { get; set; }
        public string? Text { get; set; }
    }

    /// <summary>동일 컴파일에 [Message] 파생(A/B) 존재 → GroupRoot 추론. 해시 0xA8E839.</summary>
    [Message]
    public partial class AutoGroupRoot
    {
        public int RootValue { get; set; }
        public string? RootText { get; set; }
    }

    [Message]
    public partial class AutoGroupElementA : AutoGroupRoot
    {
        public int ElementValue { get; set; }
    }

    [Message]
    public partial class AutoGroupElementB : AutoGroupRoot
    {
        public string? Note { get; set; }
    }

    /// <summary>[Message] 제네릭 선언부 — MessageId 는 선언부 FullName("…AutoGenericEnvelope`1") 해시 0x344C18,
    /// 닫힌 구성 등록은 기존 [GenericMessage] 수동 ClassId 방식 그대로.</summary>
    [Message]
    [GenericMessage(typeof(AutoGenericEnvelope<FlatMessage>), ClassId = 1)]
    public partial class AutoGenericEnvelope<T>
    {
        public T? Payload { get; set; }
        public int Stamp { get; set; }
    }

    /// <summary>참조 어셈블리(NetStandardFixtures)의 [Message] 베이스 상속 → GroupElement 추론. 해시 0xE93598.
    /// internal 이어도 생성·등록됨(선언 접근성 그대로 방출) — 검증만 필요하므로 xUnit 발견 대상에서도 제외된다.</summary>
    [Message]
    internal partial class CrossProjectElement : CrossProjectRoot
    {
        public int DerivedValue { get; set; }
    }
}

namespace MessageProtocol.Tests
{
    public class MessageAttributeTests
    {
    [Fact]
    public void Message_추론_Standalone은_해시_ID로_왕복한다()
    {
        var msg = new AutoStandalone { Value = -77, Text = "자동" };

        var rt = MessageSerializer.Deserialize<AutoStandalone>(MessageSerializer.Serialize(msg));

        Assert.Equal(-77, rt.Value);
        Assert.Equal("자동", rt.Text);
    }

    [Fact]
    public void Message_추론_그룹은_object_dispatch로_요소별_왕복한다()
    {
        var a = new AutoGroupElementA { RootValue = 1, RootText = "r", ElementValue = 9 };
        var b = new AutoGroupElementB { RootValue = 2, RootText = "t", Note = "note" };

        var decodedA = Assert.IsType<AutoGroupElementA>(MessageSerializer.Deserialize(MessageSerializer.Serialize((object)a)));
        var decodedB = Assert.IsType<AutoGroupElementB>(MessageSerializer.Deserialize(MessageSerializer.Serialize((object)b)));

        Assert.Equal((1, "r", 9), (decodedA.RootValue, decodedA.RootText, decodedA.ElementValue));
        Assert.Equal((2, "t", "note"), (decodedB.RootValue, decodedB.RootText, decodedB.Note));
    }

    [Fact]
    public void Message_해시_ID는_FullName_FNV1a_24비트_핀값과_일치한다()
    {
        // 알고리즘 핀 — 값은 FNV-1a 32(OffsetBasis 2166136261, Prime 16777619) 를 UTF-8 바이트로 돌리고
        // 0x00FF_FFFF 로 마스크한 것. 헬퍼 호출이 아니라 **리터럴**과 비교해 재구현을 막는다.
        Assert.Equal(0x1F6FBDu, MessageIdHash.FromFullName("MessageProtocol.Tests.Fixtures.AutoStandalone"));

        // 와이어 MessageId = 헤더 바이트(flags<<4 | category) << 24 | 해시. category 0.
        Assert.Equal(0x201F6FBDu, AutoStandalone.MessageId);           // Standalone 플래그(0x2)
        Assert.Equal(0x40A8E839u, AutoGroupRoot.MessageId);            // GroupRoot 플래그(0x4)
        Assert.Equal(0x807AE8F6u, AutoGroupElementA.MessageId);        // GroupElement 플래그(0x8)
        Assert.Equal(0x807AE763u, AutoGroupElementB.MessageId);
    }

    [Fact]
    public void Message_제네릭_선언부는_해시_MessageId로_구성_왕복한다()
    {
        object msg = new AutoGenericEnvelope<FlatMessage> { Payload = new FlatMessage { Value = 3 }, Stamp = 5 };

        var decoded = Assert.IsType<AutoGenericEnvelope<FlatMessage>>(
            MessageSerializer.Deserialize(MessageSerializer.Serialize(msg)));

        Assert.Equal(3, decoded.Payload!.Value);
        Assert.Equal(5, decoded.Stamp);
    }

    [Fact]
    public void Message_크로스_어셈블리_상속_요소는_참조_베이스_멤버까지_왕복한다()
    {
        // CrossProjectRoot 는 NetStandardFixtures(netstandard2.1) 에서 Standalone 으로 확정돼 있고,
        // 이 컴파일의 파생은 [Message] 하나로 GroupElement 로 추론·등록된다(와이어 멤버는 참조 베이스 체인에서 병합).
        object msg = new CrossProjectElement { BaseValue = 11, BaseText = "base", DerivedValue = 22 };

        var decoded = Assert.IsType<CrossProjectElement>(MessageSerializer.Deserialize(MessageSerializer.Serialize(msg)));

        Assert.Equal((11, "base", 22), (decoded.BaseValue, decoded.BaseText, decoded.DerivedValue));
    }
    }
}

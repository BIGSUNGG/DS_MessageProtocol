using MessageProtocol;
using MessageProtocol.Serialize;
using MessageProtocol.Tests.Fixtures;
using Xunit;

namespace MessageProtocol.Tests;

public class RoundTripTests
{
    [Fact]
    public void 전체_멤버_타입이_왕복한다()
    {
        var msg = new AllTypesMessage
        {
            Bool = true,
            Byte = 200,
            SByte = -100,
            Int16 = -30000,
            UInt16 = 60000,
            Int32 = -2_000_000_000,
            UInt32 = 4_000_000_000,
            Int64 = long.MaxValue,
            UInt64 = ulong.MaxValue,
            Single = 1.23456f,
            Double = -9.87654321,
            Decimal = 79228162514264337593543950335m,
            Char = '\u0007',
            Text = "경계값",
            Level = Level.Low,
            Blob = new byte[] { 0, 1, 255 },
            Samples = new List<double> { 0.1, -0.2 },
            Tags = new[] { "a", "b" },
            Codes = new List<byte> { 9, 8 },
            Nested = new FlatMessage { Value = -1 },
        };

        var rt = MessageSerializer.Deserialize<AllTypesMessage>(MessageSerializer.Serialize(msg));

        Assert.Equal(msg.Bool, rt.Bool);
        Assert.Equal(msg.Byte, rt.Byte);
        Assert.Equal(msg.SByte, rt.SByte);
        Assert.Equal(msg.Int16, rt.Int16);
        Assert.Equal(msg.UInt16, rt.UInt16);
        Assert.Equal(msg.Int32, rt.Int32);
        Assert.Equal(msg.UInt32, rt.UInt32);
        Assert.Equal(msg.Int64, rt.Int64);
        Assert.Equal(msg.UInt64, rt.UInt64);
        Assert.Equal(msg.Single, rt.Single);
        Assert.Equal(msg.Double, rt.Double);
        Assert.Equal(msg.Decimal, rt.Decimal);
        Assert.Equal(msg.Char, rt.Char);
        Assert.Equal(msg.Text, rt.Text);
        Assert.Equal(msg.Level, rt.Level);
        Assert.Equal(msg.Blob, rt.Blob);
        Assert.Equal(msg.Samples, rt.Samples);
        Assert.Equal(msg.Tags, rt.Tags);
        Assert.Equal(msg.Codes, rt.Codes!.ToList());
        Assert.Equal(msg.Nested!.Value, rt.Nested!.Value);
    }

    [Fact]
    public void Standalone_와이어_레이아웃이_고정이다()
    {
        byte[] bytes = MessageSerializer.Serialize(new FlatMessage { Value = 0x11223344 });

        // header(Standalone|cat0)=0x20, id=100 → 00 00 64, payload LE
        Assert.Equal(new byte[] { 0x20, 0x00, 0x00, 0x64, 0x44, 0x33, 0x22, 0x11 }, bytes);
    }

    [Fact]
    public void NonId_와이어_레이아웃이_고정이다()
    {
        byte[] bytes = MessageSerializer.Serialize(new NoIdMessage { Flag = 7 });
        // header(NonId|cat0)=0x10, Flag=0x07, Note=null → int32(-1) LE
        Assert.Equal(new byte[] { 0x10, 0x07, 0xFF, 0xFF, 0xFF, 0xFF }, bytes);
    }

    [Fact]
    public void 카테고리_니블이_헤더에_반영된다()
    {
        byte[] bytes = MessageSerializer.Serialize(new AllTypesMessage());
        Assert.Equal(MessageWireFormat.ComposeHeaderByte(MessageFlag.Standalone, 5), bytes[0]);
    }

    [Fact]
    public void MessageId_정적_속성이_조립_규칙과_일치한다()
    {
        Assert.Equal(MessageWireFormat.ComposeMessageId(MessageFlag.Standalone, 0, 100), FlatMessage.MessageId);
        Assert.Equal(MessageWireFormat.ComposeMessageId(MessageFlag.Standalone, 5, 101), AllTypesMessage.MessageId);
        Assert.Equal(MessageWireFormat.ComposeMessageId(MessageFlag.GroupRoot, 0, 110), EventBase.MessageId);
        Assert.Equal(MessageWireFormat.ComposeMessageId(MessageFlag.GroupElement, 0, 111), LoginEvent.MessageId);
    }

    [Fact]
    public void 구조체_메시지가_왕복한다()
    {
        var msg = new PointMessage { X = -3, Y = 9 };
        var rt = MessageSerializer.Deserialize<PointMessage>(MessageSerializer.Serialize(msg));
        Assert.Equal(-3, rt.X);
        Assert.Equal(9, rt.Y);
    }

    [Fact]
    public void 레코드_메시지가_왕복한다()
    {
        var msg = new SettingsRecord { Theme = "dark", Volume = 11 };
        var rt = MessageSerializer.Deserialize<SettingsRecord>(MessageSerializer.Serialize(msg));
        Assert.Equal("dark", rt.Theme);
        Assert.Equal(11, rt.Volume);
    }

    [Fact]
    public void 순환_참조와_공유_참조가_복원된다()
    {
        var a = new GraphMessage { Label = "a", Poco = new PlainPoco { Number = 1, Name = "p" } };
        var b = new GraphMessage { Label = "b" };
        a.Next = b;
        b.Next = a;        // 순환
        a.Other = b;       // 공유 (b가 두 번 등장)

        var rt = MessageSerializer.Deserialize<GraphMessage>(MessageSerializer.Serialize(a));

        Assert.Equal("a", rt.Label);
        Assert.Equal("b", rt.Next!.Label);
        Assert.True(ReferenceEquals(rt.Next.Next, rt));
        Assert.True(ReferenceEquals(rt.Other, rt.Next));
        Assert.Equal(1, rt.Poco!.Number);
        Assert.Equal("p", rt.Poco.Name);
    }

    [Fact]
    public void 상속_멤버가_함께_직렬화된다()
    {
        var msg = new LoginEvent { Timestamp = 1234L, User = "kim" };
        var rt = MessageSerializer.Deserialize<LoginEvent>(MessageSerializer.Serialize(msg));
        Assert.Equal(1234L, rt.Timestamp);   // 베이스 멤버
        Assert.Equal("kim", rt.User);
    }

    [Fact]
    public void MessageIgnore는_제외하고_MessageInclude는_포함한다()
    {
        var msg = new MemberControlMessage { Kept = 5, Excluded = 99 };
        msg.SetInternal(42);

        var rt = MessageSerializer.Deserialize<MemberControlMessage>(MessageSerializer.Serialize(msg));

        Assert.Equal(5, rt.Kept);
        Assert.Equal(0, rt.Excluded);
        Assert.Equal(42, rt.GetInternal());
    }

    [Fact]
    public void 제네릭_경로는_Span과_Memory_입력을_지원한다()
    {
        byte[] bytes = MessageSerializer.Serialize(new FlatMessage { Value = 9 });

        Assert.Equal(9, MessageSerializer.Deserialize<FlatMessage>((ReadOnlySpan<byte>)bytes).Value);
        Assert.Equal(9, MessageSerializer.Deserialize<FlatMessage>(new ReadOnlyMemory<byte>(bytes)).Value);
        Assert.Equal(9, MessageSerializer.Deserialize<FlatMessage>(bytes).Value);
    }

    [Fact]
    public void 빈_데이터_역직렬화는_예외()
    {
        Assert.Throws<ArgumentException>(() => MessageSerializer.Deserialize<FlatMessage>(Array.Empty<byte>()));
        Assert.Throws<ArgumentException>(() => MessageSerializer.Deserialize(Array.Empty<byte>()));
        Assert.Throws<ArgumentNullException>(() => MessageSerializer.Deserialize<FlatMessage>((byte[])null!));
    }

    [Fact]
    public void Pooled_경로는_호환_경로와_동일_바이트를_만든다()
    {
        var msg = new AllTypesMessage { Int32 = 3, Text = "pooled", Samples = new List<double> { 1.5 } };
        using var pooled = MessageSerializer.SerializePooled(msg);
        Assert.Equal(MessageSerializer.Serialize(msg), pooled.ToArray());
    }
}

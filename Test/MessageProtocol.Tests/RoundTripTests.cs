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

    // ---------- 제네릭 메시지 ----------

    [Fact]
    public void 제네릭_메시지가_왕복한다()
    {
        var msg = new GenericEnvelope<FlatMessage>
        {
            Note = "gen",
            Value = new FlatMessage { Value = 7 },
            Items = new List<FlatMessage?> { new() { Value = 1 }, null, new() { Value = 2 } },
        };

        var rt = MessageSerializer.Deserialize<GenericEnvelope<FlatMessage>>(MessageSerializer.Serialize(msg));

        Assert.Equal("gen", rt.Note);
        Assert.Equal(7, rt.Value!.Value);
        Assert.NotNull(rt.Items);
        Assert.Equal(3, rt.Items!.Count);
        Assert.Equal(1, rt.Items[0]!.Value);
        Assert.Null(rt.Items[1]);
        Assert.Equal(2, rt.Items[2]!.Value);
    }

    [Fact]
    public void 제네릭_NonId_메시지가_왕복한다()
    {
        var msg = new GenericPair<FlatMessage> { First = new FlatMessage { Value = 3 }, Tag = 9 };
        var rt = MessageSerializer.Deserialize<GenericPair<FlatMessage>>(MessageSerializer.Serialize(msg));
        Assert.Equal(3, rt.First!.Value);
        Assert.Equal(9, rt.Tag);
    }

    [Fact]
    public void 제네릭_구성도_object_dispatch로_왕복한다()
    {
        var msg = new GenericEnvelope<FlatMessage> { Value = new FlatMessage { Value = 11 } };
        object? decoded = MessageSerializer.Deserialize(MessageSerializer.Serialize((object)msg));
        var rt = Assert.IsType<GenericEnvelope<FlatMessage>>(decoded);
        Assert.Equal(11, rt.Value!.Value);
    }

    [Fact]
    public void 제네릭_헤더는_플래그0_MessageId_클래스ID_순서다()
    {
        var bytes = MessageSerializer.Serialize(new GenericEnvelope<FlatMessage> { Value = new FlatMessage { Value = 1 } });

        Assert.Equal(MessageWireFormat.ComposeHeaderByte(MessageFlag.Generic, 0), bytes[0]);
        Assert.Equal(0, bytes[1]);
        Assert.Equal(0, bytes[2]);
        Assert.Equal(120, bytes[3]); // MessageId 24비트 (스탠드얼론 ID)
        Assert.Equal(0, bytes[4]);
        Assert.Equal(0, bytes[5]);
        Assert.Equal(1, bytes[6]); // 구성 ClassId 24비트 (FlatMessage 구성 = 1)
    }

    [Fact]
    public void 같은_선언의_여러_구성이_함께_디스패치된다()
    {
        var a = new GenericEnvelope<FlatMessage> { Value = new FlatMessage { Value = 1 } };
        var b = new GenericEnvelope<SettingsRecord> { Value = new SettingsRecord { Theme = "dark", Volume = 3 } };

        var da = MessageSerializer.Deserialize(MessageSerializer.Serialize((object)a));
        var db = MessageSerializer.Deserialize(MessageSerializer.Serialize((object)b));

        var ra = Assert.IsType<GenericEnvelope<FlatMessage>>(da);
        var rb = Assert.IsType<GenericEnvelope<SettingsRecord>>(db);
        Assert.Equal(1, ra.Value!.Value);
        Assert.Equal("dark", rb.Value!.Theme);
    }

    [Fact]
    public void 다중_타입_매개변수_제네릭이_왕복한다()
    {
        var msg = new GenericDuo<FlatMessage, SettingsRecord>
        {
            First = new FlatMessage { Value = 5 },
            Second = new SettingsRecord { Theme = "t", Volume = 2 },
        };

        var rt = MessageSerializer.Deserialize<GenericDuo<FlatMessage, SettingsRecord>>(MessageSerializer.Serialize(msg));
        Assert.Equal(5, rt.First!.Value);
        Assert.Equal("t", rt.Second!.Theme);

        var decoded = MessageSerializer.Deserialize(MessageSerializer.Serialize((object)msg));
        var rd = Assert.IsType<GenericDuo<FlatMessage, SettingsRecord>>(decoded);
        Assert.Equal(2, rd.Second!.Volume);
    }

    [Fact]
    public void 분산_선언_구성이_선언부_구성과_공존하며_왕복한다()
    {
        // 캐리어 타입으로 선언한 구성 (ClassId 3)
        var msg = new GenericEnvelope<PointMessage> { Value = new PointMessage { X = 3, Y = 4 } };

        var rt = MessageSerializer.Deserialize<GenericEnvelope<PointMessage>>(MessageSerializer.Serialize(msg));
        Assert.Equal(3, rt.Value!.X);

        object? decoded = MessageSerializer.Deserialize(MessageSerializer.Serialize((object)msg));
        var rd = Assert.IsType<GenericEnvelope<PointMessage>>(decoded);
        Assert.Equal(4, rd.Value!.Y);

        // 선언부 [GenericMessage] 구성 (ClassId 1) 도 그대로 동작
        var decl = new GenericEnvelope<FlatMessage> { Value = new FlatMessage { Value = 9 } };
        var dd = Assert.IsType<GenericEnvelope<FlatMessage>>(MessageSerializer.Deserialize(MessageSerializer.Serialize((object)decl)));
        Assert.Equal(9, dd.Value!.Value);
    }

    [Fact]
    public void 구성_선언_없는_제네릭_메시지는_직렬화_시_예외()
    {
        var msg = new UnregisteredGeneric<FlatMessage> { X = 1 };
        var ex = Assert.Throws<InvalidOperationException>(() => MessageSerializer.Serialize(msg));
        Assert.Contains("GenericMessage", ex.Message); // 선언 방법 안내 포함
    }
}

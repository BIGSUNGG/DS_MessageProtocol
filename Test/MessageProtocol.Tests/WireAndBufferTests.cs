using MessageProtocol;
using MessageProtocol.Serialize;
using Xunit;

namespace MessageProtocol.Tests;

public class WireFormatTests
{
    [Fact]
    public void 헤더는_flags_상위니블과_category_하위니블로_구성된다()
    {
        byte header = MessageWireFormat.ComposeHeaderByte(MessageFlag.Standalone, 5);
        Assert.Equal(0x25, header);
        Assert.Equal(MessageFlag.Standalone, MessageWireFormat.GetFlags(header));
        Assert.Equal(5, MessageWireFormat.GetCategory(header));
    }

    [Fact]
    public void MessageId는_헤더바이트와_24비트_값으로_조립된다()
    {
        uint id = MessageWireFormat.ComposeMessageId(MessageFlag.GroupRoot, 3, 0xABCDEF);
        Assert.Equal((uint)0x43ABCDEF, id);
    }

    [Fact]
    public void MessageId_값은_24비트로_마스크된다()
    {
        uint id = MessageWireFormat.ComposeMessageId(MessageFlag.Standalone, 0, 0xFFFF_FFFF);
        Assert.Equal(0x00FF_FFFFu, id & MessageWireFormat.MessageIdValueMask);
    }

    [Theory]
    [InlineData(MessageFlag.NonIdMessage, false)]
    [InlineData(MessageFlag.Standalone, true)]
    [InlineData(MessageFlag.GroupRoot, true)]
    [InlineData(MessageFlag.GroupElement, true)]
    public void NonId만_임베디드_ID가_없다(MessageFlag flag, bool expected)
    {
        byte header = MessageWireFormat.ComposeHeaderByte(flag, 0);
        Assert.Equal(expected, MessageWireFormat.HasEmbeddedMessageId(header));
    }

    [Fact]
    public void 헤더_크기_상수()
    {
        Assert.Equal(1, MessageWireFormat.NonIdHeaderSize);
        Assert.Equal(4, MessageWireFormat.IdHeaderSize);
    }
}

public class BufferIOTests
{
    [Fact]
    public void 프리미티브_전체_타입이_리틀엔디안으로_왕복한다()
    {
        var writer = MessageBufferWriter.Create(1);
        writer.WriteBoolean(true);
        writer.WriteByte(0xAB);
        writer.WriteSByte(-5);
        writer.WriteInt16(short.MinValue);
        writer.WriteUInt16(ushort.MaxValue);
        writer.WriteInt32(int.MinValue);
        writer.WriteUInt32(uint.MaxValue);
        writer.WriteInt64(long.MinValue);
        writer.WriteUInt64(ulong.MaxValue);
        writer.WriteSingle(-1.5f);
        writer.WriteDouble(double.MaxValue);
        writer.WriteDecimal(-12345.6789m);
        writer.WriteChar('Z');

        // 리틀엔디안 검증: int32 -2 (0xFFFFFFFE)
        writer.WriteInt32(-2);

        var reader = new MessageBufferReader(writer.WrittenReadOnlySpan);
        Assert.True(reader.ReadBoolean());
        Assert.Equal(0xAB, reader.ReadByte());
        Assert.Equal(-5, reader.ReadSByte());
        Assert.Equal(short.MinValue, reader.ReadInt16());
        Assert.Equal(ushort.MaxValue, reader.ReadUInt16());
        Assert.Equal(int.MinValue, reader.ReadInt32());
        Assert.Equal(uint.MaxValue, reader.ReadUInt32());
        Assert.Equal(long.MinValue, reader.ReadInt64());
        Assert.Equal(ulong.MaxValue, reader.ReadUInt64());
        Assert.Equal(-1.5f, reader.ReadSingle());
        Assert.Equal(double.MaxValue, reader.ReadDouble());
        Assert.Equal(-12345.6789m, reader.ReadDecimal());
        Assert.Equal('Z', reader.ReadChar());

        int start = reader.Position;
        Assert.Equal(-2, reader.ReadInt32());
        var leBytes = writer.WrittenReadOnlySpan.Slice(start, 4).ToArray();
        Assert.Equal(new byte[] { 0xFE, 0xFF, 0xFF, 0xFF }, leBytes);

        writer.Dispose();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ascii")]
    [InlineData("한글·日本語·🌟")]
    public void 문자열은_길이접두로_왕복한다(string? value)
    {
        var writer = MessageBufferWriter.Create();
        writer.WriteString(value);

        var reader = new MessageBufferReader(writer.WrittenReadOnlySpan);
        Assert.Equal(value, reader.ReadString());
        writer.Dispose();
    }

    [Fact]
    public void null_문자열은_길이_마이너스1이다()
    {
        var writer = MessageBufferWriter.Create();
        writer.WriteString(null);
        Assert.Equal(4, writer.Length);
        Assert.Equal(-1, new MessageBufferReader(writer.WrittenReadOnlySpan).ReadInt32());
        writer.Dispose();
    }

    [Fact]
    public void 범위를_벗어난_읽기는_EndOfStreamException()
    {
        Assert.Throws<EndOfStreamException>(ReadPastEnd);
        Assert.Throws<EndOfStreamException>(ReadBlockPastEnd);
    }

    static void ReadPastEnd()
    {
        var reader = new MessageBufferReader(new byte[] { 1, 2 });
        reader.ReadByte();
        reader.ReadByte();
        reader.ReadByte();
    }

    static void ReadBlockPastEnd()
    {
        var reader = new MessageBufferReader(new byte[] { 1 });
        reader.ReadInt32();
    }

    [Fact]
    public void writer는_용량_부족시_자동_증량한다()
    {
        var writer = MessageBufferWriter.Create(4);
        for (int i = 0; i < 1000; i++)
        {
            writer.WriteInt32(i);
        }
        Assert.Equal(4000, writer.Length);

        var reader = new MessageBufferReader(writer.WrittenReadOnlySpan);
        for (int i = 0; i < 1000; i++)
        {
            Assert.Equal(i, reader.ReadInt32());
        }
        writer.Dispose();
    }

    [Fact]
    public void PooledBuffer는_스팬_뷰와_복사_배열을_제공한다()
    {
        var writer = MessageBufferWriter.Create();
        writer.WriteInt32(77);
        var pooled = writer.ToPooledBuffer();

        Assert.Equal(4, pooled.Length);
        Assert.Equal(77, new MessageBufferReader(pooled.Span).ReadInt32());
        Assert.Equal(4, pooled.ToArray().Length);

        pooled.Dispose();
        Assert.Equal(0, pooled.Length);
        pooled.Dispose(); // 멱등
    }
}

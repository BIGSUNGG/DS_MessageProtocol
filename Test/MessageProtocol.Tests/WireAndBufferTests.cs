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
    public void 고립_서로게이트_문자열은_쓰기에서_거부된다()
    {
        // KI-20 회귀: 고립 서로게이트를 대체 바이트로 조용히 바꾸지 않고 인코딩 실패를 표면화한다.
        Assert.ThrowsAny<ArgumentException>(WriteLoneSurrogate);
    }

    static void WriteLoneSurrogate()
    {
        var writer = MessageBufferWriter.Create();
        try
        {
            writer.WriteString("앞 \uD800 뒤");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Fact]
    public void 무효_UTF8_문자열_페이로드는_읽기에서_거부된다()
    {
        // KI-20 회귀: 길이 접두 2 + 2바이트 시퀀스 선도 바이트 0xC2 뒤에 연속 바이트가 아닌 0x01 → 무효 UTF-8.
        byte[] bytes = { 2, 0, 0, 0, 0xC2, 0x01 };
        Assert.Throws<InvalidDataException>(() => new MessageBufferReader(bytes).ReadString());
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(-3)]
    [InlineData(int.MinValue)]
    public void 마이너스1_외_음수_길이접두는_읽기에서_거부된다(int length)
    {
        // KI-6 회귀: null 규약은 -1 뿐 — 다른 음수가 null 로 조용히 복호되면 손상 패킷이 은폐된다.
        Assert.Throws<InvalidDataException>(() => ReadStringWithLengthPrefix(length));
    }

    static void ReadStringWithLengthPrefix(int length)
    {
        var writer = MessageBufferWriter.Create();
        try
        {
            writer.WriteInt32(length);
            _ = new MessageBufferReader(writer.WrittenReadOnlySpan).ReadString();
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Fact]
    public void 마이너스1_길이접두는_null로_복호된다()
    {
        var writer = MessageBufferWriter.Create();
        writer.WriteInt32(-1);
        Assert.Null(new MessageBufferReader(writer.WrittenReadOnlySpan).ReadString());
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

    [Fact]
    public void decimal_스케일이_28을_넘으면_읽기에서_거부된다()
    {
        byte[] bytes = WriteDecimalBytes(12.34m);
        bytes[14] = 78; // 스케일 바이트(비트 16–23)를 78로 — DecCalc 크래시 구간
        Assert.Throws<InvalidDataException>(() => new MessageBufferReader(bytes).ReadDecimal());
    }

    [Fact]
    public void decimal_flags에_예약_비트가_있으면_읽기에서_거부된다()
    {
        byte[] bytes = WriteDecimalBytes(12.34m);
        bytes[12] |= 0x01; // flags 비트 0(예약) 설정
        Assert.Throws<InvalidDataException>(() => new MessageBufferReader(bytes).ReadDecimal());
    }

    [Fact]
    public void decimal_경계_스케일28은_허용된다()
    {
        var writer = MessageBufferWriter.Create();
        writer.WriteDecimal(0.0000000000000000000000000001m); // 스케일 28(허용 최대)
        Assert.Equal(0.0000000000000000000000000001m, new MessageBufferReader(writer.WrittenReadOnlySpan).ReadDecimal());
        writer.Dispose();
    }

    static byte[] WriteDecimalBytes(decimal value)
    {
        var writer = MessageBufferWriter.Create();
        writer.WriteDecimal(value);
        byte[] bytes = writer.ToArray();
        writer.Dispose();
        return bytes;
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void 음수_Skip은_거부된다(int count)
    {
        // KI-21 회귀: Skip(-n) 이 리더를 뒤로 이동시켜 forward-only 규약을 깨는 것을 차단한다.
        Assert.Throws<ArgumentOutOfRangeException>(() => SkipAfterFourBytes(count));
    }

    static void SkipAfterFourBytes(int count)
    {
        var reader = new MessageBufferReader(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        reader.Skip(4);
        reader.Skip(count);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void 음수_Advance는_거부된다(int count)
    {
        // KI-21 회귀: Advance(-n) 이 기록 위치를 되돌려 이후 쓰기가 기존 페이로드를 덮어쓰는 것을 차단한다.
        Assert.Throws<ArgumentOutOfRangeException>(() => AdvanceAfterOneByte(count));
    }

    static void AdvanceAfterOneByte(int count)
    {
        var writer = MessageBufferWriter.Create();
        try
        {
            writer.WriteByte(0xAA);
            writer.Advance(count);
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Fact]
    public void 음수_Skip으로_소비한_바이트를_다시_읽을_수_없다()
    {
        // 수정 전 Skip(-1) 은 예외 없이 위치만 되돌려 같은 바이트를 두 번 소비하게 했다.
        Assert.False(TryRewindAndReread());
    }

    static bool TryRewindAndReread()
    {
        var reader = new MessageBufferReader(new byte[] { 0xAA, 0xBB });
        reader.ReadByte();
        try
        {
            reader.Skip(-1);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        return reader.ReadByte() == 0xAA; // 되돌아갔다면 같은 바이트를 다시 읽는다
    }

    [Fact]
    public void 음수_Advance로_기록한_페이로드를_덮어쓸_수_없다()
    {
        // 수정 전 Advance(-1) 은 길이를 줄여 다음 쓰기가 첫 바이트를 덮어쓰게 했다.
        Assert.False(TryRewindAndOverwrite());
    }

    static bool TryRewindAndOverwrite()
    {
        var writer = MessageBufferWriter.Create();
        try
        {
            writer.WriteByte(0xAA);
            try
            {
                writer.Advance(-1);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            writer.WriteByte(0xBB);
            return writer.Length == 1 && writer.WrittenSpan[0] == 0xBB; // 되돌아갔다면 첫 바이트가 덮어써진다
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Fact]
    public void 위치_전진은_0과_양수만_허용된다()
    {
        // 정상 경로 보존: Skip(0)·Advance(0) 은 무해하고 양수 전진은 기존대로 동작한다.
        var writer = MessageBufferWriter.Create();
        writer.WriteInt32(11);
        writer.WriteInt32(22);
        writer.Advance(0);
        Assert.Equal(8, writer.Length);

        var reader = new MessageBufferReader(writer.WrittenReadOnlySpan);
        reader.Skip(0);
        Assert.Equal(11, reader.ReadInt32());
        reader.Skip(4);
        Assert.Equal(0, reader.Remaining);
        writer.Dispose();
    }
}

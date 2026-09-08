using System.Text;
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

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(1024)]
    [InlineData(1_000_000)]
    [InlineData(715_827_881)] // 인코딩 자체 상한이 int 로 표현되는 마지막 부근
    public void 문자열_버퍼_요구량은_UTF8_인코딩_상한과_일치한다(int charCount)
    {
        // KI-22 회귀: 자체 long 공식이 `Encoding.GetMaxByteCount` + 길이 접두 4바이트와 같아야
        // 상한을 좁히지(버퍼 부족)도 헤프게(과할당)도 바꾸지 않는다.
        int maxBytes = StrictUtf8().GetMaxByteCount(charCount);
        Assert.Equal(4L + maxBytes, MessageBufferWriter.GetStringBufferRequirement(charCount));
    }

    [Fact]
    public void 문자열_버퍼_요구량은_int_상한_너머에서도_오버플로하지_않는다()
    {
        // KI-22 회귀: 715,827,882 자부터 필요 용량이 int.MaxValue 를 넘으므로 int 산술로는 표현 자체가 불가하다.
        const int charCount = 715_827_882;
        long required = MessageBufferWriter.GetStringBufferRequirement(charCount);
        Assert.True(required > int.MaxValue);                       // long 이라 정확히 표현됨
        Assert.True(unchecked(4 + (charCount * 3 + 3)) < 0);        // 기존 int 표현은 음수로 오버플로 → 증설 누락
    }

    [Fact]
    public void 문자열_버퍼_요구량은_문자_수에_단조증가한다()
    {
        long previous = MessageBufferWriter.GetStringBufferRequirement(0);
        foreach (int charCount in new[] { 1, 1000, 715_827_882, int.MaxValue })
        {
            long current = MessageBufferWriter.GetStringBufferRequirement(charCount);
            Assert.True(current > previous);
            previous = current;
        }
    }

    [Fact]
    public void 큰_문자열도_정상_증설되어_왕복한다()
    {
        // KI-22 정상 경로: 새 long 용량 산술이 기존 증설·기록 동작을 바꾸지 않았는지 확인.
        string value = new string('가', 100_000); // U+AC00 → UTF-8 문자당 3바이트
        var writer = MessageBufferWriter.Create(4);
        writer.WriteString(value);
        Assert.Equal(4 + 300_000, writer.Length);
        Assert.Equal(value, new MessageBufferReader(writer.WrittenReadOnlySpan).ReadString());
        writer.Dispose();
    }

    static Encoding StrictUtf8() =>
        Encoding.GetEncoding(65001, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
}

// ---------- PooledBuffer 사본 소유권 공유 (KI-37) ----------

/// <summary>
/// PooledBuffer 는 struct 라 대입·전달마다 사본이 생긴다. 수정 전은 사본의 Dispose 가 서로에게
/// 보이지 않아 같은 대여 배열이 풀에 두 번 반납됐다(다음 대여자가 남의 데이터를 봄). 이제 모든 사본이
/// 참조형 홀더를 공유해 정확히 한 번만 반납되고, 어떤 사본이 먼저 Dispose 해도 나머지는 빈 뷰를 본다.
/// </summary>
public class PooledBufferCopyOwnershipTests
{
    [Fact]
    public void 사본을_각각_Dispose해도_풀_반납은_정확히_한번이다()
    {
        var writer = MessageBufferWriter.Create();
        writer.WriteInt32(0x0A0B0C0D);
        var original = writer.ToPooledBuffer();
        var copy = original; // struct 사본 — 수정 전 이 사본의 Dispose 가 이중 반납이었다

        Assert.Equal(4, copy.Length);

        copy.Dispose();

        // 같은 소유 상태를 본다: 반납된 뒤 모든 사본의 뷰는 비어 있다.
        Assert.Equal(0, copy.Length);
        Assert.Equal(0, original.Length);
        Assert.True(original.Span.IsEmpty);
        Assert.Empty(original.ToArray());

        original.Dispose(); // 이미 반납됨 — 멱등, 예외 없음
    }

    [Fact]
    public void 원본을_Dispose하면_사본_뷰도_비어_있다()
    {
        var writer = MessageBufferWriter.Create();
        writer.WriteString("data");
        var original = writer.ToPooledBuffer();
        var copy = original;

        original.Dispose();

        Assert.Equal(0, copy.Length);
        Assert.True(copy.Span.IsEmpty);
    }

    [Fact]
    public void SerializePooled의_사본도_같은_소유권을_공유한다()
    {
        var message = new Fixtures.FlatMessage { Value = 77 };
        using var pooled = MessageSerializer.SerializePooled(message);
        var copy = pooled;

        Assert.True(copy.Span.SequenceEqual(pooled.Span));

        var roundTrip = MessageSerializer.Deserialize<Fixtures.FlatMessage>(pooled.Span.ToArray());
        Assert.Equal(77, roundTrip.Value);

        copy.Dispose();
        Assert.Equal(0, pooled.Length); // using 문의 이중 Dispose 도 안전
    }

    [Fact]
    public void 빈_writer의_ToPooledBuffer는_Dispose로_예외가_나지_않는다()
    {
        var writer = MessageBufferWriter.Create();
        var pooled = writer.ToPooledBuffer(); // Array.Empty 싱글턴 — 풀 반납 대상이 아니다

        Assert.Equal(0, pooled.Length);
        Assert.True(pooled.Span.IsEmpty);
        pooled.Dispose();
    }

    [Fact]
    public void GetSpan_음수는_계약_예외로_거부된다()
    {
        var writer = MessageBufferWriter.Create();
        writer.WriteInt32(1);
        int positionBefore = writer.Length;

        // writer 는 ref struct — 람다로 캡처할 수 없으므로 try/catch 로 계약 예외를 확인한다.
        ArgumentOutOfRangeException? exception = null;
        try
        {
            writer.GetSpan(-1);
        }
        catch (ArgumentOutOfRangeException caught)
        {
            exception = caught;
        }

        Assert.NotNull(exception);
        Assert.Equal("size", exception.ParamName);
        Assert.Equal(positionBefore, writer.Length); // 위치는 그대로 — 상태 오염 없음
    }

    [Fact]
    public void GetSpan_정상_경로는_전진_기록을_유지한다()
    {
        var writer = MessageBufferWriter.Create();
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(writer.GetSpan(4), 77);

        Assert.Equal(4, writer.Length);
        Assert.Equal(77, new MessageBufferReader(writer.WrittenReadOnlySpan).ReadInt32());
    }
}

// ---------- Create·FromRented 계약 (2026-09-08 테스트 갭 일괄 폐쇄) ----------

/// <summary>빈 버퍼 시작 경로와 FromRented 인자 검증(구현됨·무테스트)을 고정한다.</summary>
public class WriterCreateAndFromRentedContractTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Create_0이하_초기용량은_빈_버퍼로_시작해_첫_쓰기에_정상_증설된다(int initialCapacity)
    {
        var writer = MessageBufferWriter.Create(initialCapacity);

        Assert.Equal(0, writer.Capacity); // Array.Empty 시작
        writer.WriteInt32(77);
        writer.WriteString("ok");

        Assert.True(writer.Length > 0);
        var reader = new MessageBufferReader(writer.WrittenReadOnlySpan);
        Assert.Equal(77, reader.ReadInt32());
        Assert.Equal("ok", reader.ReadString());
    }

    [Fact]
    public void FromRented는_null_배열을_거부한다()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => PooledBuffer.FromRented(null!, 0));
        Assert.Equal("rented", exception.ParamName);
    }

    [Fact]
    public void FromRented는_길이_초과를_거부한다()
    {
        var rented = new byte[8];

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => PooledBuffer.FromRented(rented, 9));

        Assert.Equal("length", exception.ParamName);
    }

    [Fact]
    public void FromRented는_음수_길이도_거부한다()
    {
        var rented = new byte[8];

        // (uint)length > (uint)rented.Length 비교가 음수를 큰 양수로 잡는다.
        Assert.Throws<ArgumentOutOfRangeException>(() => PooledBuffer.FromRented(rented, -1));
    }
}

/// <summary>
/// 전체 소비 검사(<c>DeserializeExact</c>) 계약 — 스키마 표류(ADR-0006 레이아웃 동결 위반)에서
/// 발생하는 "남는 바이트"를 조용한 데이터 유실 대신 InvalidDataException 으로 전환한다.
/// 기본 Deserialize 는 전송 계층 프레이밍 여유로 뒤에 붙은 바이트를 계속 허용한다(대조군).
/// </summary>
public class DeserializeExactTests
{
    [Fact]
    public void 제네릭_진입은_깨끗한_프레임을_그대로_왕복한다()
    {
        byte[] frame = MessageSerializer.Serialize(new MessageProtocol.Tests.Fixtures.FlatMessage { Value = 42 });

        var restored = MessageSerializer.DeserializeExact<MessageProtocol.Tests.Fixtures.FlatMessage>(frame);

        Assert.Equal(42, restored.Value);
    }

    [Fact]
    public void 제네릭_진입은_남은_바이트가_있으면_거부한다()
    {
        byte[] clean = MessageSerializer.Serialize(new MessageProtocol.Tests.Fixtures.FlatMessage { Value = 7 });
        byte[] padded = new byte[clean.Length + 3];
        clean.CopyTo(padded, 0);
        padded[^1] = 0xFF;

        var exception = Assert.Throws<System.IO.InvalidDataException>(
            () => MessageSerializer.DeserializeExact<MessageProtocol.Tests.Fixtures.FlatMessage>(padded));

        Assert.Contains("trailing", exception.Message);
        // 대조군: 기본 Deserialize 는 접미 여유 바이트를 계속 허용한다(전송 계층 프레이밍 여유).
        Assert.Equal(7, MessageSerializer.Deserialize<MessageProtocol.Tests.Fixtures.FlatMessage>(padded).Value);
    }

    [Fact]
    public void object_dispatch_진입은_깨끗한_프레임을_그대로_왕복한다()
    {
        byte[] frame = MessageSerializer.Serialize(new MessageProtocol.Tests.Fixtures.FlatMessage { Value = 11 });

        var restored = Assert.IsType<MessageProtocol.Tests.Fixtures.FlatMessage>(MessageSerializer.DeserializeExact(frame));

        Assert.Equal(11, restored.Value);
    }

    [Fact]
    public void object_dispatch_진입은_남은_바이트가_있으면_거부한다()
    {
        byte[] clean = MessageSerializer.Serialize(new MessageProtocol.Tests.Fixtures.FlatMessage { Value = 9 });
        byte[] padded = new byte[clean.Length + 1];
        clean.CopyTo(padded, 0);

        var exception = Assert.Throws<System.IO.InvalidDataException>(
            () => MessageSerializer.DeserializeExact(padded));

        Assert.Contains("schema drift", exception.Message);
        Assert.IsType<MessageProtocol.Tests.Fixtures.FlatMessage>(MessageSerializer.Deserialize(padded));
    }

    [Fact]
    public void 제네릭_구성_프레임도_전체_소비_검사를_통과한다()
    {
        var envelope = new MessageProtocol.Tests.Fixtures.GenericEnvelope<MessageProtocol.Tests.Fixtures.FlatMessage> { Value = new MessageProtocol.Tests.Fixtures.FlatMessage { Value = 3 }, Note = "n" };
        byte[] frame = MessageSerializer.Serialize(envelope);

        var restored = MessageSerializer.DeserializeExact<MessageProtocol.Tests.Fixtures.GenericEnvelope<MessageProtocol.Tests.Fixtures.FlatMessage>>(frame);

        Assert.Equal(3, restored.Value!.Value);
        Assert.Equal("n", restored.Note);
        // object dispatch 경로(제네릭 헤더 라우팅)도 동일 계약.
        Assert.IsType<MessageProtocol.Tests.Fixtures.GenericEnvelope<MessageProtocol.Tests.Fixtures.FlatMessage>>(MessageSerializer.DeserializeExact(frame));
    }

    [Fact]
    public void 빈_span은_InvalidDataException_이_아니라_ArgumentException으로_거부한다()
    {
        // 진입 검증(인자 오류)은 와이어 오류(InvalidDataException)와 구분된다 — 호출자 측 버그와
        // 악성 프레임을 같은 타입으로 섞으면 상용 서버의 예외 필터가 분류를 못 한다. 두 진입 모두 고정.
        Assert.Throws<ArgumentException>(
            () => MessageSerializer.DeserializeExact<MessageProtocol.Tests.Fixtures.FlatMessage>(ReadOnlySpan<byte>.Empty));
        Assert.Throws<ArgumentException>(() => MessageSerializer.DeserializeExact(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void NonId_프레임도_전체_소비_검사를_통과한다()
    {
        // NonId 프레임은 헤더가 1바이트다 — 잔여 바이트 검사가 1바이트 헤더 프레임에서도
        // 동작함을 고정한다(KI-41 상호작용: 제네릭 진입은 NonId 거부를 우회한다).
        var message = new MessageProtocol.Tests.Fixtures.NoIdMessage { Flag = 7, Note = "nonid" };
        byte[] frame = MessageSerializer.Serialize(message);

        var restored = MessageSerializer.DeserializeExact<MessageProtocol.Tests.Fixtures.NoIdMessage>(frame);

        Assert.Equal(7, restored.Flag);
        Assert.Equal("nonid", restored.Note);

        byte[] padded = new byte[frame.Length + 1];
        frame.CopyTo(padded, 0);

        Assert.Throws<System.IO.InvalidDataException>(
            () => MessageSerializer.DeserializeExact<MessageProtocol.Tests.Fixtures.NoIdMessage>(padded));
    }
}

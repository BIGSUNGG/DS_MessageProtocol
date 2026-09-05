using MessageProtocol.Serialize;
using Xunit;

namespace MessageProtocol.Tests;

/// <summary>
/// KI-7 회귀: writer 의 증설 산술과 `PatchInt32` 경계.
/// 수정 전 실험(저장소 밖 소비자 프로세스): ① 1.5GB 버퍼에서 `_buffer.Length * 2` 가 **-1,294,967,296** 으로
/// 오버플로해 `Math.Max` 가 항상 정확 요구량을 골랐다 → 매 증설이 여유 없는 대여 + 전체 복사(성장 비용 제곱,
/// 그 크기면 풀링도 안 됨). 페이로드 상한 `0X7FEFFFFF`(약 2.1GB)은 이 라이브러리가 지원하는 범위라 실제 회귀다.
/// ② `EnsureCapacity(int.MaxValue)` 는 2GB 할당을 시도해 `OutOfMemoryException`("Array dimensions exceeded
/// supported range") — 명확한 거부 대신. ③ `PatchInt32(60)` 은 `Length = 4` 인데도 수용되어 대여 배열의
/// **미기록 바이트**에 썼고, 그 배열은 나중에 풀로 돌아간다.
/// </summary>
public class WriterGrowthTests
{
    /// <summary>`MessageBufferWriter.MaxBufferLength`(private const)와 같은 값 — .NET 배열 상한.</summary>
    const int MaxBufferLength = 0x7FEFFFFF;

    [Theory]
    [InlineData(0, 10, 256)]        // 빈 버퍼 첫 증설 = 기본 용량
    [InlineData(256, 300, 512)]     // 배증이 요구량보다 크면 배증
    [InlineData(256, 1000, 1000)]   // 배증으로 부족하면 요구량
    [InlineData(1024, 1500, 2048)]  // 요구량보다 배증이 크면 배증(여유 확보)
    [InlineData(1024, 2048, 2048)]  // 배증이 요구량과 같으면 그 값(이미 2배 여유)
    public void 증설_용량은_배증과_요구량_중_큰_값(int currentCapacity, long required, int expected)
    {
        Assert.Equal(expected, MessageBufferWriter.ComputeGrowCapacity(currentCapacity, required));
    }

    [Fact]
    public void 증설_용량은_1GB_너머에서도_음수가_아니고_여유를_유지한다()
    {
        // 수정 전 공식: Math.Max(1_500_000_000 * 2, required) = Math.Max(-1_294_967_296, required) = required(여유 0).
        int capacity = MessageBufferWriter.ComputeGrowCapacity(1_500_000_000, 1_500_000_100L);

        Assert.Equal(MaxBufferLength, capacity);   // 배열 상한까지 배증 여지를 확보
        Assert.True(capacity > 1_500_000_100L, "exact-fit growth means quadratic re-rent + full copy");
    }

    [Fact]
    public void 증설_용량은_상한을_넘지_않고_요구량보다_작지_않다()
    {
        int[] capacities = { 0, 1, 256, 65_536, 1_000_000, 1_073_741_824, 1_500_000_000, MaxBufferLength };
        long[] requirements = { 1, 1_000, 1_073_741_824, MaxBufferLength };

        foreach (int currentCapacity in capacities)
        {
            foreach (long required in requirements)
            {
                int capacity = MessageBufferWriter.ComputeGrowCapacity(currentCapacity, required);

                Assert.InRange(capacity, (int)Math.Min(required, MaxBufferLength), MaxBufferLength);
            }
        }
    }

    [Fact]
    public void 상한을_넘는_용량_요구는_할당_시도_대신_명확한_예외()
    {
        var writer = MessageBufferWriter.Create();

        // 수정 전: Rent(2,147,483,647) 시도 → OutOfMemoryException("Array dimensions exceeded supported range").
        var exception = Assert.IsType<InvalidOperationException>(CatchEnsureCapacity(ref writer, int.MaxValue));

        Assert.Contains("maximum buffer size", exception.Message);
        writer.Dispose();
    }

    [Fact]
    public void 정상_증설은_여전히_동작하고_여유_용량을_남긴다()
    {
        var writer = MessageBufferWriter.Create(256);
        writer.WriteInt32(7);

        writer.EnsureCapacity(1000);
        Assert.True(writer.Capacity >= 1004);

        // 이미 확보된 용량 안에서는 증설 없이 쓴다(생성 코드가 고정 크기를 일괄 확보하는 방식).
        int capacityBefore = writer.Capacity;
        writer.WriteInt64(9);

        Assert.Equal(capacityBefore, writer.Capacity);
        Assert.Equal(12, writer.Length);
        writer.Dispose();
    }

    [Fact]
    public void PatchInt32는_기록된_구간_안에서는_동작한다()
    {
        var writer = MessageBufferWriter.Create(64);
        writer.WriteInt32(1);
        writer.WriteInt32(2);
        Assert.Equal(8, writer.Length);

        writer.PatchInt32(0, 111);
        writer.PatchInt32(4, 222);   // 마지막 4바이트 = 경계 오프셋

        Assert.Equal(new byte[] { 111, 0, 0, 0, 222, 0, 0, 0 }, writer.ToArray());
        writer.Dispose();
    }

    [Theory]
    [InlineData(-1)]   // 음수 오프셋
    [InlineData(1)]    // 4바이트를 온전히 담지 못함 (Length 4 기준)
    [InlineData(4)]    // 기록된 구간 밖 = 미기록 풀 바이트
    [InlineData(60)]   // 수정 전 실험에서 수용되던 오프셋
    public void PatchInt32는_기록된_구간_밖을_거부한다(int offset)
    {
        var writer = MessageBufferWriter.Create(64);
        writer.WriteInt32(1);        // Length = 4 → 허용 오프셋은 0 뿐

        Assert.IsType<ArgumentOutOfRangeException>(CatchPatchInt32(ref writer, offset, 12345));
        Assert.Equal(4, writer.Length);   // 거부되어도 상태는 그대로
        writer.Dispose();
    }

    [Fact]
    public void PatchInt32는_빈_writer에서도_거부된다()
    {
        var writer = MessageBufferWriter.Create(64);   // Length = 0

        Assert.IsType<ArgumentOutOfRangeException>(CatchPatchInt32(ref writer, 0, 1));
        writer.Dispose();
    }

    // `MessageBufferWriter` 는 ref struct 라 람다에 포획할 수 없다(Assert.Throws 사용 불가) —
    // ref 인자로 받아 try/catch 하는 헬퍼로 예외를 관찰한다.

    static Exception? CatchEnsureCapacity(ref MessageBufferWriter writer, int additional)
    {
        try
        {
            writer.EnsureCapacity(additional);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    static Exception? CatchPatchInt32(ref MessageBufferWriter writer, int offset, int value)
    {
        try
        {
            writer.PatchInt32(offset, value);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}

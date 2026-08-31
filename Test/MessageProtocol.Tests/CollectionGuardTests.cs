using System.Buffers.Binary;
using MessageProtocol;
using MessageProtocol.Serialize;
using MessageProtocol.Tests.Fixtures;
using Xunit;

namespace MessageProtocol.Tests;

/// <summary>
/// KI-13 회귀: 컬렉션 길이·개수 접두사가 남은 바이트를 초과하면 할당 전에 예외를 던져
/// 악성 패킷의 거대 할당(OOM DoS)을 차단한다.
/// </summary>
public class CollectionGuardTests
{
    [Fact]
    public void 고정크기_배열_길이가_남은_바이트를_초과하면_할당_전에_예외()
    {
        byte[] bytes = MessageSerializer.Serialize(new AllTypesMessage { Blob = new byte[] { 1, 2, 3 } });
        // Blob 길이 접두사(3) + 페이로드 패턴
        int offset = FindPattern(bytes, new byte[] { 3, 0, 0, 0, 1, 2, 3 });
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), int.MaxValue);

        Assert.Throws<EndOfStreamException>(() => MessageSerializer.Deserialize<AllTypesMessage>(bytes));
    }

    [Fact]
    public void 고정크기_List_개수가_남은_바이트를_초과하면_할당_전에_예외()
    {
        byte[] bytes = MessageSerializer.Serialize(new AllTypesMessage { Samples = new List<double> { 1.5, 2.5 } });
        // 개수(2) + 첫 요소 1.5 의 리틀엔디안 바이트 패턴
        int offset = FindPattern(bytes, new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xF8, 0x3F });
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), int.MaxValue);

        Assert.Throws<EndOfStreamException>(() => MessageSerializer.Deserialize<AllTypesMessage>(bytes));
    }

    [Fact]
    public void 가변크기_배열_개수가_남은_바이트를_초과하면_할당_전에_예외()
    {
        byte[] bytes = MessageSerializer.Serialize(new AllTypesMessage { Tags = new[] { "a" } });
        // 개수(1) + 문자열 "a"(길이 1 + 0x61) 패턴
        int offset = FindPattern(bytes, new byte[] { 1, 0, 0, 0, 1, 0, 0, 0, (byte)'a' });
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), int.MaxValue);

        Assert.Throws<EndOfStreamException>(() => MessageSerializer.Deserialize<AllTypesMessage>(bytes));
    }

    [Fact]
    public void 정상_컬렉션은_가드_도입_후에도_왕복한다()
    {
        var msg = new AllTypesMessage
        {
            Blob = new byte[] { 1, 2, 3 },
            Samples = new List<double> { 1.5, 2.5 },
            Tags = new[] { "a", "bb" },
            Codes = new List<byte> { 9, 8 },
        };

        var rt = MessageSerializer.Deserialize<AllTypesMessage>(MessageSerializer.Serialize(msg));

        Assert.Equal(msg.Blob, rt.Blob);
        Assert.Equal(msg.Samples, rt.Samples);
        Assert.Equal(msg.Tags, rt.Tags);
        Assert.Equal(msg.Codes, rt.Codes);
    }

    static int FindPattern(byte[] data, byte[] pattern)
    {
        for (int i = 0; i + pattern.Length <= data.Length; i++)
        {
            if (data.AsSpan(i, pattern.Length).SequenceEqual(pattern)) return i;
        }
        throw new InvalidOperationException("Test fixture wire pattern not found.");
    }
}

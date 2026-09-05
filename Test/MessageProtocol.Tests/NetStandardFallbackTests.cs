using System.Buffers.Binary;
using System.Runtime.Versioning;
using MessageProtocol.NetStandardFixtures;
using MessageProtocol.Serialize;
using Xunit;

namespace MessageProtocol.Tests;

/// <summary>
/// netstandard2.1(Unity 호환 프로필) 어셈블리에서 생성된 코드를 **실행**으로 검증한다.
/// 그 타깃에는 `CollectionsMarshal` 이 없어 생성기가 `List&lt;T&gt;` 고속 경로 대신 폴백 변형(인덱서 루프)을 방출하는데,
/// 이 저장소의 테스트는 net8.0/net9.0 라 항상 고속 경로만 실행됐었다 — 폴백 경로는 생성 텍스트 단언에만 의존했다.
/// 여기서는 폴백 경로에서도 컬렉션 왕복·할당 가드(KI-17)·중첩 깊이 가드(KI-14 읽기, KI-25 쓰기)가 실제로 동작함을 실행으로 고정한다.
/// </summary>
public class NetStandardFallbackTests
{
    [Fact]
    public void 픽스처_어셈블리는_netstandard2_1_프로필이다()
    {
        // 이 어셈블리가 다른 TFM 으로 바뀌면 CollectionsMarshal 이 생기고 폴백 경로 검증이 조용히 사라진다 —
        // 커버리지 상실을 실패로 드러내기 위해 프로필 자체를 고정한다.
        var framework = typeof(FallbackCollections).Assembly
            .GetCustomAttributes(typeof(TargetFrameworkAttribute), false)
            .Cast<TargetFrameworkAttribute>()
            .Single();

        Assert.Equal(".NETStandard,Version=v2.1", framework.FrameworkName);
    }

    [Fact]
    public void 폴백_컬렉션_5형태가_왕복한다()
    {
        var message = new FallbackCollections
        {
            Bulk = new List<int> { 1, 2, 3 },
            Texts = new List<string> { "a", "bb", "한글" },
            Codes = new List<byte> { 9, 8, 7 },
            Tags = new[] { "x", "y" },
            Samples = new[] { 1.5, -2.5 },
        };

        var roundTrip = MessageSerializer.Deserialize<FallbackCollections>(MessageSerializer.Serialize(message));

        Assert.Equal(message.Bulk, roundTrip.Bulk);
        Assert.Equal(message.Texts, roundTrip.Texts);
        Assert.Equal(message.Codes, roundTrip.Codes);
        Assert.Equal(message.Tags, roundTrip.Tags);
        Assert.Equal(message.Samples, roundTrip.Samples);
    }

    [Fact]
    public void 폴백_경로에서도_null과_빈_컬렉션_규약이_유지된다()
    {
        var nulls = MessageSerializer.Deserialize<FallbackCollections>(
            MessageSerializer.Serialize(new FallbackCollections()));

        Assert.Null(nulls.Bulk);
        Assert.Null(nulls.Texts);
        Assert.Null(nulls.Codes);
        Assert.Null(nulls.Tags);
        Assert.Null(nulls.Samples);

        var empties = MessageSerializer.Deserialize<FallbackCollections>(MessageSerializer.Serialize(new FallbackCollections
        {
            Bulk = new List<int>(),
            Texts = new List<string>(),
            Codes = new List<byte>(),
            Tags = Array.Empty<string>(),
            Samples = Array.Empty<double>(),
        }));

        Assert.Empty(empties.Bulk!);
        Assert.Empty(empties.Texts!);
        Assert.Empty(empties.Codes!);
        Assert.Empty(empties.Tags!);
        Assert.Empty(empties.Samples!);
    }

    [Fact]
    public void 폴백_List_벌크_할당_가드가_실행된다()
    {
        // KI-17: CollectionsMarshal 미지원 타깃의 List<T> 벌크 판독은 `개수×요소크기 ≤ Remaining` 을 할당 전에 검증해야 한다.
        // 지금까지는 이미터 텍스트 단언으로만 검증됐고, 여기서는 그 가드가 실제로 예외를 던지는지 실행으로 확인한다.
        byte[] bytes = MessageSerializer.Serialize(new FallbackCollections { Bulk = new List<int> { 1, 2, 3 } });

        int offset = FindPattern(bytes, new byte[] { 3, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0 });
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), int.MaxValue);

        Assert.Throws<EndOfStreamException>(() => MessageSerializer.Deserialize<FallbackCollections>(bytes));
    }

    [Fact]
    public void 폴백_경로에서도_쓰기_중첩_깊이_가드가_실행된다()
    {
        FallbackNode shallow = BuildChain(10);
        Assert.Equal(10, CountChain(MessageSerializer.Deserialize<FallbackNode>(MessageSerializer.Serialize(shallow))));

        // KI-25: 기본 상한(64)을 넘는 자기참조 체인은 스택 오버플로 대신 예외로 거부되어야 한다 — 폴백 생성 코드에서도 동일.
        FallbackNode tooDeep = BuildChain(MessageBufferWriter.DefaultMaxNestingDepth + 1);
        Assert.Throws<InvalidOperationException>(() => MessageSerializer.Serialize(tooDeep));
    }

    [Fact]
    public void 폴백_경로에서도_읽기_중첩_깊이_가드가_실행된다()
    {
        // 쓰기 상한만 올려 100단계 프레임을 만든 뒤, 기본 상한(64) reader 로 읽으면 거부되어야 한다 (KI-14).
        FallbackNode head = BuildChain(100);
        var writer = MessageBufferWriter.Create(256, 512);
        byte[] bytes;
        try
        {
            MessageSerializer.Serialize(head, ref writer);
            bytes = writer.ToArray();
        }
        finally
        {
            writer.Dispose();
        }

        Assert.Throws<InvalidDataException>(() => MessageSerializer.Deserialize<FallbackNode>(bytes));

        // 양쪽 상한을 함께 올리면 같은 프레임이 정상 복호된다.
        var reader = new MessageBufferReader(bytes, 512);
        Assert.Equal(100, CountChain(MessageSerializer.Deserialize<FallbackNode>(ref reader)));
    }

    static FallbackNode BuildChain(int links)
    {
        var head = new FallbackNode { Label = "n0" };
        FallbackNode tail = head;
        for (int i = 1; i <= links; i++)
        {
            tail.Next = new FallbackNode { Label = "n" + i.ToString() };
            tail = tail.Next;
        }

        return head;
    }

    static int CountChain(FallbackNode? head)
    {
        int count = 0;
        while (head?.Next is not null)
        {
            head = head.Next;
            count++;
        }

        return count;
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

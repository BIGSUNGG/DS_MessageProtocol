using MessageProtocol.Serialize;
using MessageProtocol.Tests.Fixtures;
using Xunit;

namespace MessageProtocol.Tests;

/// <summary>
/// KI-26 회귀: 컬렉션 멤버 쓰기는 멤버 표현식을 **한 번만** 평가해 로컬로 스냅샷한다.
/// 이전 생성 코드는 길이 접두(`Count`)·루프 조건(`Count`)·요소 접근(`[i]`)이 각자 멤버를 다시 평가해서
/// 게터가 요소 N개당 2N+2회 돌았다 — 계산형 프로퍼티(`public IList&lt;int&gt; Codes =&gt; Build();`)에서는
/// 길이와 요소가 서로 다른 인스턴스에서 나와 프레임이 스스로 모순될 수 있고, 평범한 자동 프로퍼티에서도
/// 요소마다 게터·인터페이스 `Count` 호출이 낭비된다(특히 `CollectionsMarshal` 이 없는 Unity/netstandard2.1).
/// </summary>
public class CollectionSnapshotTests
{
    [Fact]
    public void 컬렉션_멤버는_직렬화_중_정확히_한_번만_평가된다()
    {
        var message = new SnapshotCollectionMessage();

        byte[] bytes = MessageSerializer.Serialize(message);

        // 스냅샷 전: Codes 3개 → 2*3+2 = 8회, Tags 2개 → 6회. 스냅샷 후: 각각 1회.
        Assert.Equal(1, message.CodesGetterCalls);
        Assert.Equal(1, message.TagsGetterCalls);

        var roundTrip = MessageSerializer.Deserialize<SnapshotCollectionMessage>(bytes);
        Assert.Equal(new[] { 1, 2, 3 }, roundTrip.Codes);
        Assert.Equal(new[] { "a", "b" }, roundTrip.Tags);
    }

    [Fact]
    public void 스냅샷_이후에도_빈_컬렉션과_null이_규약대로_기록된다()
    {
        var empty = new SnapshotCollectionMessage { Codes = new List<int>(), Tags = Array.Empty<string>() };

        var emptyRoundTrip = MessageSerializer.Deserialize<SnapshotCollectionMessage>(MessageSerializer.Serialize(empty));
        Assert.Empty(emptyRoundTrip.Codes);
        Assert.Empty(emptyRoundTrip.Tags);

        var nulls = new SnapshotCollectionMessage { Codes = null!, Tags = null! };

        var nullRoundTrip = MessageSerializer.Deserialize<SnapshotCollectionMessage>(MessageSerializer.Serialize(nulls));
        Assert.Null(nullRoundTrip.Codes);
        Assert.Null(nullRoundTrip.Tags);
    }
}

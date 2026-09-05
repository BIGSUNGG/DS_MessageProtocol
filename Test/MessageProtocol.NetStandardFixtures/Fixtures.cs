using MessageProtocol;

namespace MessageProtocol.NetStandardFixtures;

// 이 어셈블리는 netstandard2.1(Unity 호환 프로필)로 컴파일된다 — CollectionsMarshal 이 없어
// 생성기는 List<T> 고속 경로(AsSpan/SetCount) 대신 폴백 변형(인덱서 루프)을 방출한다.
// KI-17(폴백 벌크 가드)·KI-26(멤버 스냅샷)·KI-14/25(중첩 깊이 가드)의 생성 코드가
// 실제로 실행되는 유일한 곳이므로, 이 픽스처들은 소비자 환경의 와이어 행동을 고정한다.

/// <summary>폴백 컬렉션 경로 5형태: List(고정 크기)·List(가변 크기)·IList·배열(가변)·배열(고정 크기).</summary>
[StandaloneMessage(900)]
public partial class FallbackCollections
{
    public List<int>? Bulk { get; set; }
    public List<string>? Texts { get; set; }
    public IList<byte>? Codes { get; set; }
    public string[]? Tags { get; set; }
    public double[]? Samples { get; set; }
}

/// <summary>자기참조 + 중첩 컬렉션을 가진 NonId 페이로드 — 중첩 깊이 가드가 폴백 경로에서도 도는지 검증한다.</summary>
[NonIdMessage]
public partial class FallbackNode
{
    public string? Label { get; set; }
    public FallbackNode? Next { get; set; }
    public List<FallbackNode>? Children { get; set; }
}

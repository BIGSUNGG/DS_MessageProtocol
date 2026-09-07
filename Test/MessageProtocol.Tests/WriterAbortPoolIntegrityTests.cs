using MessageProtocol;
using MessageProtocol.Serialize;
using MessageProtocol.Tests.Fixtures;
using Xunit;

namespace MessageProtocol.Tests;

/// <summary>
/// 쓰기 측 예외 경로의 풀 무결성 — 직렬화 중 멤버 게터가 던지면 대여 버퍼는 **부분 기록 상태로** 풀에
/// 돌아간다(finally-dispose). 이때 이중 반납·누수·오염 상태 유출이 있으면 후속 직렬화가 조용히
/// 망가진다 — 중단 직후 연속 왕복이 모두 정확한지로 통합 수준에서 검증한다(단위 가드의 합이 아닌
/// 실제 풀 순환 아래 동작).
/// </summary>
public partial class WriterAbortPoolIntegrityTests
{
    /// <summary>두 번째 멤버 게터에서 던지는 제어 가능 픽스처 — 첫 멤버는 이미 기록된 상태로 중단시킨다.</summary>
    [StandaloneMessage(160)]
    public partial class AbortProbeMessage
    {
        public int First { get; set; }

        public int Boom
        {
            get => throw new InvalidOperationException("getter exploded");
            set { }
        }

        public int Last { get; set; }
    }

    [Fact]
    public void 게터가_던진_직렬화_중단_후에도_풀은_오염되지_않는다()
    {
        var aborting = new AbortProbeMessage { First = 1, Last = 2 };

        // 중단: 원인 예외가 그대로 전파되어야 한다(삼키거나 포장하지 않는다).
        for (int abort = 0; abort < 20; abort++)
        {
            var propagated = Assert.Throws<InvalidOperationException>(
                () => MessageSerializer.Serialize(aborting));
            Assert.Contains("getter exploded", propagated.Message);

            // 중단 직후 연속 왕복 — 부분 기록 풀 버퍼가 재대여돼도 결과는 항상 정확해야 한다.
            for (int followUp = 0; followUp < 10; followUp++)
            {
                int value = abort * 100 + followUp;
                var back = MessageSerializer.Deserialize<FlatMessage>(
                    MessageSerializer.Serialize(new FlatMessage { Value = value }));
                if (back.Value != value)
                {
                    Assert.Fail($"중단 {abort} 후 후속 {followUp}: 값 오염 {value}→{back.Value} — 풀 무결성 붕괴");
                }
            }
        }
    }

    [Fact]
    public void PooledBuffer_경로에서_중단해도_후속_Pooled_왕복은_정확하다()
    {
        var aborting = new AbortProbeMessage { First = 1, Last = 2 };

        for (int abort = 0; abort < 10; abort++)
        {
            Assert.Throws<InvalidOperationException>(() => MessageSerializer.SerializePooled(aborting));

            using var pooled = MessageSerializer.SerializePooled(new FlatMessage { Value = abort });
            var back = MessageSerializer.Deserialize<FlatMessage>(pooled.Span.ToArray());
            Assert.Equal(abort, back.Value);
        }
    }

    [Fact]
    public void 중단은_타입_캐시를_오염시키지_않는다()
    {
        // 같은 타입의 중단-성공 반복 — SerializerCache 상태는 등록 시점 이후 불변이어야 한다.
        var aborting = new AbortProbeMessage();
        var working = new AbortProbeMessage { First = 7, Last = 9 };

        for (int i = 0; i < 5; i++)
        {
            Assert.Throws<InvalidOperationException>(() => MessageSerializer.Serialize(aborting));
        }

        // Boom 은 항상 던지므로 이 타입의 성공 왕복은 불가능 — 캐시 불변성은 다른 타입으로 확인:
        // 중단들이 다른 타입의 캐시에 스며들지 않았는지.
        var back = MessageSerializer.Deserialize<FlatMessage>(
            MessageSerializer.Serialize(new FlatMessage { Value = 42 }));
        Assert.Equal(42, back.Value);
    }
}

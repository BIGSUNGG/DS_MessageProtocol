using MessageProtocol.Serialize;
using MessageProtocol.Tests.Fixtures;
using Xunit;

namespace MessageProtocol.Tests;

/// <summary>
/// 동시 핫패스 스트레스 — KI-38·KI-39(등록 경쟁·캐시 발행)는 추론으로 고쳤지만, 지속 혼합 트래픽에서
/// 기능적 오염을 잡는 상주 그물은 없었다. 이 테스트는 스레드별 메시지가 **자기 타입·자기 값으로만
/// 왕복하는지**를 검증한다 — SerializerCache&lt;T&gt; 나 디스패치 테이블이 타입 경계를 넘어 새면
/// (가장 파국적 무음 손상 클래스: 다른 타입의 델리게이트 실행) 여기서 즉시 폭발한다.
/// 제네릭 진입과 object 디스패치를 교대로 압박하고, 값은 스레드별 시드로 결정적 재현이 가능하다.
/// </summary>
public class ConcurrentRoundTripStressTests
{
    const int ThreadCount = 6;
    const int IterationsPerThread = 20_000;

    [Fact]
    public void 혼합_타입_동시_왕복은_타입_경계를_넘지_않는다()
    {
        var failures = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var threads = new Thread[ThreadCount];

        for (int t = 0; t < ThreadCount; t++)
        {
            int threadId = t;
            threads[t] = new Thread(() => Worker(threadId, failures));
        }

        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join();

        Assert.True(failures.IsEmpty, string.Join("\n", failures.Take(5)));
    }

    static void Worker(int threadId, System.Collections.Concurrent.ConcurrentQueue<string> failures)
    {
        var random = new Random(20260908 + threadId);   // 스레드별 고정 시드 — 실패 재현 가능

        for (int i = 0; i < IterationsPerThread; i++)
        {
            try
            {
                int mode = i % 4;
                switch (mode)
                {
                    case 0:  // 제네릭 진입 — 기본형
                    {
                        int value = random.Next(int.MinValue, int.MaxValue);
                        var back = MessageSerializer.Deserialize<FlatMessage>(
                            MessageSerializer.Serialize(new FlatMessage { Value = value }));
                        if (back.Value != value) failures.Enqueue($"t{threadId} i{i}: Flat 값 오염 {value}→{back.Value}");
                        break;
                    }
                    case 1:  // 제네릭 진입 — 참조 그래프(공유 포함)
                    {
                        int value = random.Next(1, int.MaxValue);
                        var shared = new ChainMessage();
                        var head = new ChainMessage { Next = new ChainMessage { Next = shared } };
                        // 공유 서브그래프: 두 갈래가 같은 인스턴스 — 역방향에서 참조 동일성 복원 확인
                        var envelope = new GenericEnvelope<FlatMessage>
                        {
                            Value = new FlatMessage { Value = value },
                            Items = new List<FlatMessage?> { new FlatMessage { Value = value }, null },
                        };
                        var backEnv = MessageSerializer.Deserialize<GenericEnvelope<FlatMessage>>(
                            MessageSerializer.Serialize(envelope));
                        if (backEnv.Value!.Value != value)
                            failures.Enqueue($"t{threadId} i{i}: Envelope 값 오염 {value}→{backEnv.Value!.Value}");
                        if (backEnv.Items is not { Count: 2 } || backEnv.Items[0]!.Value != value || backEnv.Items[1] is not null)
                            failures.Enqueue($"t{threadId} i{i}: Envelope 컬렉션 오염");
                        var backHead = MessageSerializer.Deserialize<ChainMessage>(MessageSerializer.Serialize(head));
                        if (backHead.Next!.Next is null)
                            failures.Enqueue($"t{threadId} i{i}: Chain 구조 오염");
                        break;
                    }
                    case 2:  // object 디스패치 — 왕복 타입·값 일치
                    {
                        long stamp = random.NextInt64();
                        object parsed = MessageSerializer.Deserialize(
                            MessageSerializer.Serialize(new LoginEvent { Timestamp = stamp, User = "t" + threadId }));
                        if (parsed is not LoginEvent login || login.Timestamp != stamp || login.User != "t" + threadId)
                            failures.Enqueue($"t{threadId} i{i}: 디스패치 타입/값 오염 ({parsed.GetType().Name})");
                        break;
                    }
                    case 3:  // 제네릭 진입 — 전체 원시형 매트릭스(값은 스레드 시드)
                    {
                        double value = random.NextDouble();
                        var message = new AllTypesMessage
                        {
                            Bool = true, Byte = (byte)threadId, Int32 = (int)(value * int.MaxValue),
                            Single = (float)value, Double = value, Text = "s" + value.ToString("E4"),
                            Blob = new byte[] { (byte)i, 255 }, Samples = new List<double> { value },
                        };
                        var back = MessageSerializer.Deserialize<AllTypesMessage>(MessageSerializer.Serialize(message));
                        if (back.Double != value || back.Blob![0] != (byte)i || back.Samples![0] != value)
                            failures.Enqueue($"t{threadId} i{i}: AllTypes 오염");
                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                failures.Enqueue($"t{threadId} i{i}: 예상 밖 예외 {exception.GetType().FullName}: {exception.Message}");
                return;
            }
        }
    }
}

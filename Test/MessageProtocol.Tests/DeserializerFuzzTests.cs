using MessageProtocol;
using MessageProtocol.Serialize;
using MessageProtocol.Tests.Fixtures;
using Xunit;

namespace MessageProtocol.Tests;

/// <summary>
/// 역직렬화 신뢰 경계의 적대적 차등 퍼징 — 개별 가드의 단위 테스트가 아니라 **통합 불변식**을 검증한다.
/// 유효 프레임에 결정적 변이(비트 뒤집기·절단·극값 치환)를 가한 뒤:
///  ① 거부는 항상 알려진 깨끗한 예외 유형으로만 일어난다(처음 보는 예외 유형 = 새로운 결함),
///  ② 성공한 판독은 조용한 손상이 아니다 — 결과를 재직렬화한 바이트를 다시 왕복하면 동일 바이트가
///     나와야 한다(멱등 왕복; 직렬화는 결정적이므로 바이트 비교가 동등성 오라클이 된다).
/// 시드 고정(재현 가능) — 실패 시 반복 번호가 진단에 노출된다.
/// </summary>
public class DeserializerFuzzTests
{
    static readonly System.Type[] CleanRejections =
    {
        typeof(System.IO.InvalidDataException),     // 와이어 내용 불법(헤더·태그·깊이·UTF-8·decimal flags)
        typeof(System.IO.EndOfStreamException),     // 경계 위반
        typeof(KeyNotFoundException),               // 미등록 MessageId/(MessageId,ClassId)
        typeof(InvalidOperationException),           // 등록/계약 위반 안내
        typeof(ArgumentException),                  // 빈 입력 등 인자 계약
        typeof(ArgumentNullException),
        typeof(ArgumentOutOfRangeException),
    };

    static byte[][] BuildSeedFrames()
    {
        var allTypes = new AllTypesMessage
        {
            Bool = true, Byte = 200, SByte = -3, Int16 = -1234, UInt16 = 51234,
            Int32 = -987654, UInt32 = 3_000_000_000, Int64 = long.MinValue / 2, UInt64 = ulong.MaxValue / 2,
            Single = 3.5f, Double = -2.718281828, Decimal = 123456.789m, Char = '한',
            Text = "텍스트 with ASCII 123", Level = Level.Mid,
            Blob = new byte[] { 1, 2, 3, 250, 251 },
            Samples = new List<double> { 1.5, -2.5, double.Epsilon },
            Tags = new[] { "a", "bb", "ccc" },
            Codes = new List<byte> { 9, 8, 7 }.AsReadOnly(),
            Nested = new FlatMessage { Value = 77 },
        };
        var chain = new ChainMessage { Next = new ChainMessage { Next = new ChainMessage() } };
        var envelope = new GenericEnvelope<FlatMessage>
        {
            Value = new FlatMessage { Value = 5 },
            Note = "note",
            Items = new List<FlatMessage?> { new FlatMessage { Value = 1 }, null, new FlatMessage { Value = 2 } },
        };

        return new[]
        {
            MessageSerializer.Serialize(allTypes),
            MessageSerializer.Serialize(chain),
            MessageSerializer.Serialize(envelope),
        };
    }

    [Fact]
    public void 변이_프레임은_깨끗하게_거부되거나_멱등하게_왕복한다()
    {
        const int seed = 20260908;
        const int mutationsPerFrame = 1_500;
        var random = new Random(seed);

        int rejected = 0, accepted = 0;
        string? firstFailure = null;

        for (int i = 0; i < mutationsPerFrame && firstFailure is null; i++)
        {
            foreach (var frame in BuildSeedFrames())
            {
                var mutant = Mutate(frame, random);
                if (mutant is null) continue;

                object parsed;
                try
                {
                    parsed = MessageSerializer.Deserialize(mutant);   // object dispatch — 헤더 라우팅 포함
                }
                catch (Exception rejection)
                {
                    if (CleanRejections.Contains(rejection.GetType()))
                    {
                        rejected++;
                    }
                    else
                    {
                        firstFailure = $"iter {i}: 예상 밖 예외 유형 {rejection.GetType().FullName}: {rejection.Message}";
                    }
                    continue;
                }

                accepted++;
                try
                {
                    // 멱등 왕복: parse → bytes2 → parse2 → bytes3; bytes3 == bytes2 여야 한다.
                    byte[] bytes2 = MessageSerializer.Serialize(parsed);
                    object parsed2 = MessageSerializer.Deserialize(bytes2);
                    byte[] bytes3 = MessageSerializer.Serialize(parsed2);
                    if (!bytes3.SequenceEqual(bytes2))
                    {
                        firstFailure = $"iter {i}: 비멱등 왕복 — 조용한 손상 의심";
                        break;
                    }
                }
                catch (Exception roundTrip)
                {
                    firstFailure = $"iter {i}: 성공 판독이 재왕복 실패({roundTrip.GetType().FullName}: {roundTrip.Message})";
                    break;
                }
            }
        }

        Assert.True(firstFailure is null, firstFailure);
        // 퍼저가 실제로 양쪽 경로를 쳤는지(죽은 퍼저 방지) — 최소 관측 하한.
        Assert.True(rejected > 100, $"거부 관측 부족: {rejected}");
        Assert.True(accepted > 50, $"수용 관측 부족: {accepted}");
    }

    static byte[]? Mutate(byte[] original, Random random)
    {
        var mutant = (byte[])original.Clone();
        switch (random.Next(3))
        {
            case 0: // 비트 뒤집기
                mutant[random.Next(mutant.Length)] ^= (byte)(1 << random.Next(8));
                break;
            case 1: // 절단
                if (mutant.Length <= 1) return null;
                Array.Resize(ref mutant, random.Next(mutant.Length));
                break;
            case 2: // 극값 치환
                mutant[random.Next(mutant.Length)] = (byte)random.Next(256);
                break;
        }
        return mutant.SequenceEqual(original) ? null : mutant;
    }
}

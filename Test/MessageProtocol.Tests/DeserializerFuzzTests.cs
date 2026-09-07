using MessageProtocol;
using MessageProtocol.Serialize;
using MessageProtocol.Tests.Fixtures;
using System.Reflection;
using Xunit;

namespace MessageProtocol.Tests;

/// <summary>
/// 역직렬화 신뢰 경계의 적대적 차등 퍼징 — 개별 가드의 단위 테스트가 아니라 **통합 불변식**을 검증한다.
/// 유효 프레임에 결정적 변이(비트 뒤집기·절단·극값 치환·다중 비트·가비지 접미)를 가한 뒤:
///  ① 거부는 항상 알려진 깨끗한 예외 유형으로만 일어난다(처음 보는 예외 유형 = 새로운 결함),
///  ② 성공한 판독은 조용한 손상이 아니다 — 결과를 재직렬화한 바이트를 다시 왕복하면 동일 바이트가
///     나와야 한다(멱등 왕복; 직렬화는 결정적이므로 바이트 비교가 동등성 오라클이 된다).
/// 두 진입을 모두 압박한다 — object dispatch(헤더 라우팅 포함)와 제네릭 진입(이형 헤더는 KI-5 검증에서 거부).
/// 시드 고정(재현 가능) — 실패 시 진입·반복 번호가 진단에 노출된다.
/// </summary>
public class DeserializerFuzzTests
{
    static readonly System.Type[] CleanRejections =
    {
        typeof(System.IO.InvalidDataException),     // 와이어 내용 불법(헤더·태그·깊이·UTF-8·decimal flags·디스패치 유형)
        typeof(System.IO.EndOfStreamException),     // 경계 위반·과할당 가드
        typeof(KeyNotFoundException),               // 미등록 MessageId/(MessageId,ClassId)
        typeof(InvalidOperationException),           // 등록/계약 위반 안내
        typeof(ArgumentException),                  // 빈 입력 등 인자 계약
        typeof(ArgumentNullException),
        typeof(ArgumentOutOfRangeException),
    };

    /// <summary>퍼징 시드 프레임 — object dispatch 진입과(옵션으로) 제네릭 진입의 쌍.</summary>
    static (byte[] Frame, System.Type? TypedEntry)[] BuildSeedFrames()
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
        var noId = new NoIdMessage { Flag = 7, Note = "nonid" };
        var login = new LoginEvent { Timestamp = 123L, User = "kim" };

        // 깊이 상한(64) 바로 아래의 체인 — 절단·비트 변이가 깊이 가드와 상호작용한다.
        ChainMessage deep = new ChainMessage();
        for (int i = 0; i < 60; i++) deep = new ChainMessage { Next = deep };

        return new[]
        {
            (MessageSerializer.Serialize(allTypes), typeof(AllTypesMessage)),
            (MessageSerializer.Serialize(chain), typeof(ChainMessage)),
            (MessageSerializer.Serialize(envelope), typeof(GenericEnvelope<FlatMessage>)),
            (MessageSerializer.Serialize(noId), (System.Type?)null),        // NonId: object dispatch 거부 경로
            (MessageSerializer.Serialize(login), (System.Type?)typeof(LoginEvent)),
            (MessageSerializer.Serialize(deep), (System.Type?)typeof(ChainMessage)),
        };
    }

    [Fact]
    public void 변이_프레임은_깨끗하게_거부되거나_멱등하게_왕복한다()
    {
        const int seed = 20260908;
        const int mutationsPerFrame = 2_000;
        var random = new Random(seed);

        int rejected = 0, accepted = 0;
        string? firstFailure = null;

        for (int i = 0; i < mutationsPerFrame && firstFailure is null; i++)
        {
            foreach (var (original, typedEntry) in BuildSeedFrames())
            {
                var mutant = Mutate(original, random);
                if (mutant is null) continue;

                // ① object dispatch 진입 — 헤더 라우팅 포함.
                firstFailure = ParseAndClassify(mutant, i, "dispatch", ref rejected, ref accepted);
                if (firstFailure is not null) break;

                // ② 제네릭 진입 — 이형 헤더는 KI-5 검증에서, 본문 변이는 생성 판독기에서 처리된다.
                if (typedEntry is not null)
                {
                    firstFailure = ParseTypedAndClassify(mutant, typedEntry, i, ref rejected);
                    if (firstFailure is not null) break;
                }
            }
        }

        Assert.True(firstFailure is null, firstFailure);
        // 퍼저가 실제로 양쪽 경로를 다 쳤는지(죽은 퍼저 방지) — 최소 관측 하한.
        Assert.True(rejected > 300, $"거부 관측 부족: {rejected}");
        Assert.True(accepted > 50, $"수용 관측 부족: {accepted}");
    }

    static string? ParseAndClassify(byte[] mutant, int iteration, string entry, ref int rejected, ref int accepted)
    {
        object parsed;
        try
        {
            parsed = MessageSerializer.Deserialize(mutant);
        }
        catch (Exception rejection)
        {
            if (CleanRejections.Contains(rejection.GetType()))
            {
                rejected++;
                return null;
            }
            return $"iter {iteration} [{entry}]: 예상 밖 예외 유형 {rejection.GetType().FullName}: {rejection.Message}";
        }

        accepted++;
        try
        {
            byte[] bytes2 = MessageSerializer.Serialize(parsed);
            object parsed2 = MessageSerializer.Deserialize(bytes2);
            byte[] bytes3 = MessageSerializer.Serialize(parsed2);
            return bytes3.SequenceEqual(bytes2)
                ? null
                : $"iter {iteration} [{entry}]: 비멱등 왕복 — 조용한 손상 의심";
        }
        catch (Exception roundTrip)
        {
            return $"iter {iteration} [{entry}]: 성공 판독이 재왕복 실패({roundTrip.GetType().FullName}: {roundTrip.Message})";
        }
    }

    static string? ParseTypedAndClassify(byte[] mutant, System.Type typedEntry, int iteration, ref int rejected)
    {
        try
        {
            // 제네릭 진입은 리플렉션으로 닫힌 제네릭 메서드를 호출 — 퍼저 본체와 같은 예외 계약을 검증한다.
            // Deserialize(byte[]) 는 제네릭·object 두 오버로드가 있어 이름+인자 조회는 모호하다 — 제네릭 정의만 골라 닫는다.
            var method = typeof(MessageSerializer)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == nameof(MessageSerializer.Deserialize)
                    && m.IsGenericMethodDefinition
                    && m.GetParameters() is { Length: 1 } parameters
                    && parameters[0].ParameterType == typeof(byte[]))
                .MakeGenericMethod(typedEntry);
            method.Invoke(null, new object[] { mutant });
            return null;
        }
        catch (TargetInvocationException invocation)
        {
            var inner = invocation.InnerException!;
            if (CleanRejections.Contains(inner.GetType()))
            {
                rejected++;
                return null;
            }
            return $"iter {iteration} [typed {typedEntry.Name}]: 예상 밖 예외 유형 {inner.GetType().FullName}: {inner.Message}";
        }
        catch (Exception direct)
        {
            // Invoke 자체의 실패(인자 계약)는 깨끗한 유형만 허용.
            if (CleanRejections.Contains(direct.GetType()))
            {
                rejected++;
                return null;
            }
            return $"iter {iteration} [typed {typedEntry.Name}]: 예상 밖 예외 유형 {direct.GetType().FullName}: {direct.Message}";
        }
    }

    static byte[]? Mutate(byte[] original, Random random)
    {
        var mutant = (byte[])original.Clone();
        switch (random.Next(5))
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
            case 3: // 다중 비트 — 실제 손상은 한 바이트에 그치지 않는다; 길이 접두 근처 동시 타격 포함
                for (int k = 0; k < 2 + random.Next(3); k++)
                {
                    mutant[random.Next(mutant.Length)] ^= (byte)(1 << random.Next(8));
                }
                break;
            case 4: // 가비지 접미 — 절단의 역방향: 뒤에 붙은 쓰레기는 소비되지 않고 남아야 한다
                if (mutant.Length > 256) return null;
                var extended = new byte[mutant.Length + 1 + random.Next(8)];
                mutant.CopyTo(extended, 0);
                for (int k = mutant.Length; k < extended.Length; k++) extended[k] = (byte)random.Next(256);
                mutant = extended;
                break;
        }
        return mutant.SequenceEqual(original) ? null : mutant;
    }
}

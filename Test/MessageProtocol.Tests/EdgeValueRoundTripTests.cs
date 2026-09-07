using MessageProtocol;
using MessageProtocol.Serialize;
using MessageProtocol.Tests.Fixtures;
using Xunit;

namespace MessageProtocol.Tests;

/// <summary>
/// 경계값 왕복 정밀 검증 — 값 동일성(==) 이 지나치는 무음 손상 클래스를 잡는다:
///  -0.0 과 +0.0 은 == 로 같다 — 부호 있는 0의 유실은 **비트 비교로만** 관찰된다.
/// NaN 은 페이로드를 가진다 — quiet/signaling 구분과 페이로드 비트가 와이어에서 보존되어야 한다.
/// (와이어는 원시 비트 복사다 — 이 테스트는 그 사실을 계약으로 못박는다. 전환·정규화가 끼어들면 즉시 깨진다.)
/// </summary>
public class EdgeValueRoundTripTests
{
    static float F(uint bits) => BitConverter.Int32BitsToSingle(unchecked((int)bits));
    static double D(ulong bits) => BitConverter.Int64BitsToDouble(unchecked((long)bits));

    public static TheoryData<uint> FloatPatterns => new()
    {
        0x00000000u,             // +0.0
        0x80000000u,             // -0.0 (부호 있는 0 — == 로는 +0.0 과 구분 불가)
        0x3F800000u,             // 1.0
        0x7F800000u,             // +Infinity
        0xFF800000u,             // -Infinity
        0x7FC00000u,             // quiet NaN (기본 페이로드)
        0xFFC00001u,             // quiet NaN (음수·페이로드 1)
        0x7F800001u,             // signaling NaN
        0x00000001u,             // 최소 denormal
        0x007FFFFFu,             // 최대 denormal
        0x7F7FFFFFu,             // float.MaxValue
        0xFF7FFFFFu,             // -float.MaxValue
        0x00800000u,             // 최소 normal
    };

    public static TheoryData<ulong> DoublePatterns => new()
    {
        0x0000000000000000ul,    // +0.0
        0x8000000000000000ul,    // -0.0
        0x3FF0000000000000ul,    // 1.0
        0x7FF0000000000000ul,    // +Infinity
        0xFFF0000000000000ul,    // -Infinity
        0x7FF8000000000000ul,    // quiet NaN
        0xFFF8000000000042ul,    // quiet NaN (음수·페이로드)
        0x7FF0000000000001ul,    // signaling NaN
        0x0000000000000001ul,    // 최소 denormal
        0x000FFFFFFFFFFFFFul,    // 최대 denormal
        0x7FEFFFFFFFFFFFFFul,    // double.MaxValue
        0xFFEFFFFFFFFFFFFFul,    // -double.MaxValue
    };

    [Theory]
    [MemberData(nameof(FloatPatterns))]
    public void float_특수_비트패턴은_비트까지_보존된다(uint bits)
    {
        var message = new AllTypesMessage { Single = F(bits) };

        var roundTrip = MessageSerializer.Deserialize<AllTypesMessage>(MessageSerializer.Serialize(message));

        Assert.Equal(bits, unchecked((uint)BitConverter.SingleToInt32Bits(roundTrip.Single)));
    }

    [Theory]
    [MemberData(nameof(DoublePatterns))]
    public void double_특수_비트패턴은_비트까지_보존된다(ulong bits)
    {
        var message = new AllTypesMessage { Double = D(bits) };

        var roundTrip = MessageSerializer.Deserialize<AllTypesMessage>(MessageSerializer.Serialize(message));

        Assert.Equal(bits, unchecked((ulong)BitConverter.DoubleToInt64Bits(roundTrip.Double)));
    }

    public static TheoryData<decimal> DecimalPatterns => new()
    {
        0m,                                        // zero
        decimal.MaxValue,                          // 79,228,162,514,264,337,593,543,950,335
        decimal.MinValue,
        decimal.One,
        decimal.MinusOne,
        0.0000000000000000000000000001m,           // scale 28 최소 양수 (허용 상한)
        -0.0000000000000000000000000001m,
        792281625142643375935439503.35m,           // 최대 유효숫자×scale 조합
        1.0000000000000000000000000000m,           // 후행 0 스케일 보존 (값은 같아도 bits 다름 가능)
    };

    [Theory]
    [MemberData(nameof(DecimalPatterns))]
    public void decimal_경계값은_스케일까지_보존된다(decimal value)
    {
        var message = new AllTypesMessage { Decimal = value };

        var roundTrip = MessageSerializer.Deserialize<AllTypesMessage>(MessageSerializer.Serialize(message));

        // decimal.Equals 는 스케일까지 비교한다(1.0 vs 1.00 구분) — GetBits 왕복도 함께 고정.
        Assert.Equal(decimal.GetBits(value), decimal.GetBits(roundTrip.Decimal));
    }

    public static TheoryData<char, string?> CharStringPatterns => new()
    {
        { '\0', "nul 포함 \0 문자열" },            // 문자열 내 NUL
        { '한', "한글 및 surrogate pair: 𝄞 🎮" },   // BMP + BMP 밖(서로게이트 쌍)
        { char.MaxValue, "max" },                  // U+FFFF (noncharacter — UTF-8 인코딩 가능)
        { '\uD7FF', "마지막 BMP-before-surrogates" }, // 서로게이트 블록 바로 아래
    };

    [Theory]
    [MemberData(nameof(CharStringPatterns))]
    public void char_및_문자열_경계값은_왕복한다(char value, string text)
    {
        var message = new AllTypesMessage { Char = value, Text = text };

        var roundTrip = MessageSerializer.Deserialize<AllTypesMessage>(MessageSerializer.Serialize(message));

        Assert.Equal(value, roundTrip.Char);
        Assert.Equal(text, roundTrip.Text);
    }

    [Fact]
    public void 음수_0은_양수_0과_구분되어_보존된다()
    {
        // == 로는 같아서 일반 왕복 테스트가 못 잡는 클래스 — 명시적 고정.
        var message = new AllTypesMessage { Single = -0.0f, Double = -0.0 };

        var roundTrip = MessageSerializer.Deserialize<AllTypesMessage>(MessageSerializer.Serialize(message));

        Assert.Equal(0x80000000u, unchecked((uint)BitConverter.SingleToInt32Bits(roundTrip.Single)));
        Assert.Equal(0x8000000000000000ul, unchecked((ulong)BitConverter.DoubleToInt64Bits(roundTrip.Double)));
    }
}

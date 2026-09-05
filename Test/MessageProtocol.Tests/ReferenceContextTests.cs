using MessageProtocol.Serialize;
using Xunit;

namespace MessageProtocol.Tests;

/// <summary>
/// KI-30 회귀: 참조 추적 컨텍스트는 `_firstObject is null` 을 **빈 슬롯 sentinel** 로 쓰므로,
/// null 을 등록하면 슬롯이 차지되지 않아 다음 객체도 id 1 을 받았다(실험 확인: `RegisterObject(null)` → 1,
/// 이은 `RegisterObject(객체)` → 1). 읽기 쪽도 같아서 `GetObject(1)` 이 백레퍼런스를 다른 인스턴스로 해석했다 —
/// 예외 없이 객체 그래프가 조용히 손상되므로 공개 경계에서 null 을 거부한다.
/// 생성 코드는 null 을 `ReferenceKind.Null` 로 먼저 걸러 이 경로를 타지 않는다(수동 구현 대상 계약).
/// </summary>
public class ReferenceContextTests
{
    [Fact]
    public void SerializeContext는_null_등록을_거부한다()
    {
        var context = default(MessageSerializer.SerializeContext);

        Assert.Throws<ArgumentNullException>(() => context.RegisterObject(null!));
    }

    [Fact]
    public void SerializeContext는_null_id_조회를_거부한다()
    {
        var context = default(MessageSerializer.SerializeContext);

        Assert.Throws<ArgumentNullException>(() => context.TryGetObjectId(null!, out _));
    }

    [Fact]
    public void DeserializeContext는_null_등록을_거부한다()
    {
        var context = default(MessageSerializer.DeserializeContext);

        Assert.Throws<ArgumentNullException>(() => context.RegisterNewObject(null!));
    }

    [Fact]
    public void 거부_후에도_컨텍스트는_오염되지_않고_정상_사용된다()
    {
        var context = default(MessageSerializer.SerializeContext);
        Assert.Throws<ArgumentNullException>(() => context.RegisterObject(null!));

        var value = new object();

        Assert.Equal(1, context.RegisterObject(value));
        Assert.True(context.TryGetObjectId(value, out int objectId));
        Assert.Equal(1, objectId);
    }

    [Fact]
    public void 정상_경로_id_발급과_승격_백레퍼런스_복원은_그대로_동작한다()
    {
        // 가드가 빈 슬롯 sentinel·Dictionary 승격 경로를 깨지 않았는지 고정한다.
        var first = new object();
        var second = new object();
        var third = new object();

        var write = default(MessageSerializer.SerializeContext);
        Assert.Equal(1, write.RegisterObject(first));
        Assert.Equal(2, write.RegisterObject(second));   // 두 번째 등록에서 Dictionary 로 승격
        Assert.Equal(3, write.RegisterObject(third));

        Assert.True(write.TryGetObjectId(first, out int firstId));
        Assert.Equal(1, firstId);
        Assert.True(write.TryGetObjectId(third, out int thirdId));
        Assert.Equal(3, thirdId);
        Assert.False(write.TryGetObjectId(new object(), out _));

        var read = default(MessageSerializer.DeserializeContext);
        Assert.Equal(1, read.RegisterNewObject(first));
        Assert.Equal(2, read.RegisterNewObject(second));
        Assert.Equal(3, read.RegisterNewObject(third));

        Assert.Same(first, read.GetObject(1));
        Assert.Same(third, read.GetObject(3));
    }
}

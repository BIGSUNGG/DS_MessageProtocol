using MessageProtocol.Serialize;
using MessageProtocol.Tests.Fixtures;
using Xunit;

namespace MessageProtocol.Tests;

/// <summary>
/// KI-11 회귀: <see cref="MessageSerializer"/> 의 타입별 정적 캐시(`SerializerCache{T}`)가 등록 시점 문제로
/// **영구히** 망가지던 두 형태를 막는다.
/// ① 캐시 cctor 가 리플렉션 실패 시 예외를 던지면 CLR 이 그 실패를 타입별로 영구 캐싱해서, 이후 델리게이트 등록이
/// 성공해도 해당 타입은 영원히 `TypeInitializationException` 이었다. ② cctor 필드가 readonly 라 등록 전 조기 접근으로
/// cctor 가 먼저 돌면 Prefill 이 영원히 무시됐다. 이제 cctor 는 던지지 않고(미해결은 null), 등록은 캐시를 직접 채워 복구한다.
/// </summary>
public class SerializerCacheTests
{
    [Fact]
    public void 계약_멤버_없는_타입의_조기_접근은_영구_초기화_실패가_아니라_명확한_예외를_던진다()
    {
        // 수정 전: cctor 가 던지고 CLR 이 캐싱 → TypeInitializationException(그 타입은 이후로도 영구 실패).
        var exception = Assert.Throws<InvalidOperationException>(
            () => MessageSerializer.Serialize(new UnregisteredContractMessage { Value = 1 }));

        Assert.Contains(nameof(UnregisteredContractMessage), exception.Message);
        Assert.Contains("Serialize", exception.Message);

        // 같은 타입을 다시 건드려도 초기화 실패가 아니라 같은 안내 예외가 나온다 = 상태가 오염되지 않았다.
        Assert.Throws<InvalidOperationException>(
            () => MessageSerializer.Serialize(new UnregisteredContractMessage { Value = 2 }));
    }

    [Fact]
    public void 조기_접근으로_cctor가_먼저_돌아도_이후_델리게이트_등록으로_복구된다()
    {
        // 1) 등록 전 조기 접근 — 캐시 cctor 가 리플렉션 경로로 돌아 아무것도 채우지 못한다.
        Assert.Throws<InvalidOperationException>(
            () => MessageSerializer.Serialize(new LateBoundMessage { Value = 1 }));

        // 2) 그 뒤 델리게이트 등록. 수정 전에는 여기서도 영구 실패(cctor 재실행 불가 + readonly 필드)였다.
        MessageSerializer.RegisterNonIdMessage<LateBoundMessage>(
            static (LateBoundMessage message, ref MessageBufferWriter writer) => writer.WriteInt32(message.Value),
            static (ref MessageBufferReader reader) => new LateBoundMessage { Value = reader.ReadInt32() });

        // 3) 제네릭 hot path 와 object dispatch 경로 모두 실제로 동작해야 한다.
        var roundTrip = MessageSerializer.Deserialize<LateBoundMessage>(
            MessageSerializer.Serialize(new LateBoundMessage { Value = 7 }));
        Assert.Equal(7, roundTrip.Value);

        var viaDispatch = MessageSerializer.Deserialize<LateBoundMessage>(
            MessageSerializer.Serialize((object)new LateBoundMessage { Value = 9 }));
        Assert.Equal(9, viaDispatch.Value);
    }

    [Fact]
    public void 계약_멤버_없는_타입의_리플렉션_등록은_나중_null_델리게이트가_아니라_등록_시점에_알린다()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => MessageSerializer.RegisterNonIdMessage<UnregisteredContractMessage>());

        Assert.Contains(nameof(UnregisteredContractMessage), exception.Message);
    }

    [Fact]
    public void 수동_구현_타입의_리플렉션_등록은_그대로_동작한다()
    {
        // 역방향 가드: cctor 를 비던짐으로 바꾼 변화가 정상 리플렉션 경로를 약화시키면 안 된다.
        byte[] bytes = MessageSerializer.Serialize(new ManualStandalone { Value = 42 });

        var roundTrip = MessageSerializer.Deserialize<ManualStandalone>(bytes);

        Assert.Equal(42, roundTrip.Value);
    }
}

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

    // ---------- KI-11 잔존: 거부된 등록의 캐시 잔류 (2026-09-07 해소) ----------

    [Fact]
    public void 거부된_HasId_등록은_SerializerCache에_충돌_MessageId를_남기지_않는다()
    {
        // 수정 전: prefill 이 등록 검증보다 먼저 돌아, 거부된 등록의 MessageId/HasId 가 캐시에 영구 잔류했고
        // 이후 올바른 id 로 재등록해도 복구 블록(Serialize is null)을 건너뛰어 잘못된 MessageId 가 남았다
        // (RegisterGenericConstruction 이 캐시의 MessageId 로 런타임 키를 조립하므로 오염은 키 충돌로 번진다).
        Assert.Throws<InvalidOperationException>(() =>
            MessageSerializer.RegisterHasIdMessage<ManualIdMessage>(
                ManualIdMessage.Serialize, ManualIdMessage.Deserialize, FlatMessage.MessageId)); // 이미 점유된 id

        // 거부로 캐시가 오염되지 않았다 — 이 접근이 cctor 를 돌려도 자기 자신의 MessageId 로만 채워진다.
        Assert.Equal(ManualIdMessage.MessageId, MessageSerializer.SerializerCache<ManualIdMessage>.MessageId);

        // 올바른 id 로 재등록하면 성공하고 object dispatch 왕복도 동작한다.
        MessageSerializer.RegisterHasIdMessage<ManualIdMessage>(
            ManualIdMessage.Serialize, ManualIdMessage.Deserialize, ManualIdMessage.MessageId);
        Assert.Equal(ManualIdMessage.MessageId, MessageSerializer.SerializerCache<ManualIdMessage>.MessageId);

        var roundTrip = (ManualIdMessage)MessageSerializer.Deserialize(
            MessageSerializer.Serialize((object)new ManualIdMessage { Value = 7 }));
        Assert.Equal(7, roundTrip.Value);
    }

    [Fact]
    public void NonId_비트가_박힌_HasId_등록은_조용한_반쪽_등록이_아니라_등록_시점에_거부된다()
    {
        // 수정 전: RegisterCore 가 MessageId·reader 등록을 조용히 건너뛰어 object 직렬화만 동작하고
        // 이후 Deserialize(object) 가 원인을 알려주지 않는 KeyNotFoundException 으로 실패했다 (감사 원장 LOW).
        uint nonIdFlagged = MessageProtocol.MessageWireFormat.ComposeMessageId(
            MessageProtocol.MessageFlag.NonIdMessage, 0, 777);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MessageSerializer.RegisterHasIdMessage<ManualFlagProbeMessage>(
                ManualFlagProbeMessage.Serialize, ManualFlagProbeMessage.Deserialize, nonIdFlagged));

        Assert.Contains("NonId", exception.Message);
        Assert.Contains(nameof(MessageSerializer.RegisterNonIdMessage), exception.Message);
    }

    // ---------- RegisterGenericConstruction 발행 순서 (2026-09-07 해소) ----------

    [Fact]
    public void RegisterGenericConstruction은_classId를_writer보다_먼저_발행한다()
    {
        // 감사 원장 MEDIUM: 수정 전 순서(writer → reader → classId)에서는 writer 디스패치가 보인 뒤 classId
        // 기록 전에 object dispatch 로 진입한 Serialize 가 GetGenericClassId=0 을 읽고 안내 없는
        // "not registered" 예외를 냈다. 샘플러가 writer 를 보는 순간 classId 도 보여야 한다.
        var violations = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var stop = new ManualResetEventSlim(false);
        var envelope = new GenericEnvelope<ChainMessage> { Value = new ChainMessage() };
        Type constructionType = typeof(GenericEnvelope<ChainMessage>);

        var samplers = Enumerable.Range(0, 3).Select(_ => new Thread(() =>
        {
            while (!stop.IsSet)
            {
                try
                {
                    MessageSerializer.Serialize((object)envelope);
                }
                catch (Exception ex)
                {
                    // 발행 전 정상 실패(미등록·generic-flag 안내)와 달리 classId=0 경쟁은 생성 코드의
                    // 전용 메시지로만 나타난다 — 이것이 관찰되면 발행 순서 위반이다.
                    if (ex.Message.Contains("This generic construction is not registered for serialization"))
                    {
                        violations.Enqueue(ex);
                    }
                }
            }
        })).ToArray();

        foreach (var sampler in samplers) sampler.Start();
        MessageSerializer.RegisterGenericConstruction<GenericEnvelope<ChainMessage>>(9);
        Thread.SpinWait(500_000);   // 등록 완료 후에도 압박 유지 — 완료 상태에서 위반이 나면 안 된다.
        stop.Set();
        foreach (var sampler in samplers) sampler.Join();

        Assert.Empty(violations);
        Assert.Equal(9u, MessageSerializer.GetGenericClassId<GenericEnvelope<ChainMessage>>());
        var back = (GenericEnvelope<ChainMessage>)MessageSerializer.Deserialize(
            MessageSerializer.Serialize((object)new GenericEnvelope<ChainMessage> { Value = new ChainMessage() }));
        Assert.NotNull(back.Value);
    }

    [Fact]
    public void RegisterGenericConstruction_실패_시_classId도_롤백된다()
    {
        // (MessageId, ClassId) reader 키를 선점해 reader 등록 단계에서 실패를 강제한다 — 재배치된 발행
        // 순서(classId 먼저)의 롤백이 classId 도 되돌리는지 검증. 롤백 누락이면 이후 재시도가
        // 잘못된 classId 로 성공하는 사고가 생긴다.
        // 선점: GenericEnvelope<FlatMessage> 는 ClassId=1 로 등록돼 있다(모듈 초기화) — 같은 (messageId, 1) 키로
        // reader 등록 단계에서 실패를 강제한다. 재배치된 발행 순서(classId 먼저)의 롤백이 classId 도 되돌리는지 검증.
        Assert.Throws<InvalidOperationException>(() =>
            MessageSerializer.RegisterGenericConstruction<GenericEnvelope<MemberControlMessage>>(1));

        // 실패했으므로 classId 도 기록돼 있으면 안 된다.
        Assert.Equal(0u, MessageSerializer.GetGenericClassId<GenericEnvelope<MemberControlMessage>>());
    }
}

// ---------- 동시 등록 경쟁 (KI-38) ----------

/// <summary>
/// 등록은 검증→prefill→클레임 순서였을 때 같은 타입을 다른 델리게이트로 동시 등록하면 두 스레드 모두
/// prefill 까지 도달해 권위적인 SerializerCache&lt;T&gt; 를 덮어쓴 뒤 TryAdd 패자만 실패했다 — 패자의
/// 델리게이트(또는 A/B 혼합)가 잔류해 거부된 등록의 직렬화기가 조용히 실행된다. 클레임 선점(패자는
/// prefill 전에 예외)으로 불가능해진다.
/// </summary>
public class RegistrationRaceTests
{
    [Fact]
    public void 같은_타입을_다른_델리게이트로_동시_등록하면_정확히_한쪽만_실패하고_캐시는_승자만_담는다()
    {
        var barrier = new Barrier(2);
        var failures = new System.Collections.Concurrent.ConcurrentQueue<Exception>();

        void Register(int offset)
        {
            barrier.SignalAndWait(10_000);
            try
            {
                MessageSerializer.RegisterHasIdMessage<ManualRaceMessage>(
                    (message, ref writer) =>
                    {
                        uint id = ManualRaceMessage.MessageId;
                        writer.WriteByte((byte)(id >> 24));
                        writer.WriteByte((byte)(id >> 16));
                        writer.WriteByte((byte)(id >> 8));
                        writer.WriteByte((byte)id);
                        writer.WriteInt32(message.Value + offset);
                    },
                    (ref MessageBufferReader reader) =>
                    {
                        reader.Skip(MessageProtocol.MessageWireFormat.IdHeaderSize);
                        return new ManualRaceMessage { Value = reader.ReadInt32() - offset };
                    },
                    ManualRaceMessage.MessageId);
            }
            catch (Exception exception)
            {
                failures.Enqueue(exception);
            }
        }

        var first = Task.Run(() => Register(0));
        var second = Task.Run(() => Register(1_000));
        Task.WaitAll(first, second);

        // 정확히 한쪽만 "already registered" — 다른 예외 유형이 관찰되면 등록 자체가 부패한 것이다.
        var failure = Assert.Single(failures);
        var invalid = Assert.IsType<InvalidOperationException>(failure);
        Assert.Contains("already registered", invalid.Message);

        // 캐시는 승자의 델리게이트 쌍만 담는다 — A(직렬화)+B(역직렬화) 혼합이면 값이 어긋난다.
        var back = MessageSerializer.Deserialize<ManualRaceMessage>(
            MessageSerializer.Serialize(new ManualRaceMessage { Value = 77 }));
        Assert.Equal(77, back.Value);
    }

    [Fact]
    public void 검증_거부로_실패한_등록은_클레임을_롤백해_재등록이_가능하다()
    {
        uint flatMessageId = Fixtures.FlatMessage.MessageId; // 이미 등록된 타입이 점유한 와이어 id

        // 클레임은 검증보다 먼저 일어난다 — 거부되면 클레임도 롤백되어야 잔류가 없다.
        var rejected = Assert.Throws<InvalidOperationException>(() =>
            MessageSerializer.RegisterHasIdMessage<ManualRollbackMessage>(
                (message, ref writer) => { },
                (ref reader) => new ManualRollbackMessage(),
                flatMessageId));
        Assert.Contains("already registered", rejected.Message);

        // 거부 시도의 클레임이 잔류하면 이 재등록은 "already registered" 로 막힌다.
        uint ownId = ManualRollbackMessage.MessageId;
        MessageSerializer.RegisterHasIdMessage<ManualRollbackMessage>(
            (message, ref writer) =>
            {
                uint id = ownId;
                writer.WriteByte((byte)(id >> 24));
                writer.WriteByte((byte)(id >> 16));
                writer.WriteByte((byte)(id >> 8));
                writer.WriteByte((byte)id);
            },
            (ref reader) =>
            {
                reader.Skip(MessageProtocol.MessageWireFormat.IdHeaderSize);
                return new ManualRollbackMessage();
            },
            ownId);
    }
}

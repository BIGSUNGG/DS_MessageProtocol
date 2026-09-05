using MessageProtocol.Serialize;
using MessageProtocol.Tests.Fixtures;
using Xunit;

namespace MessageProtocol.Tests;

/// <summary>
/// KI-14 회귀: 중첩 객체 역직렬화 재귀에 깊이 상한이 없었다. 불신 피어가 자기참조 메시지의
/// <c>ReferenceKind.NewObject</c> 바이트만 늘어놓은 작은 프레임(실험 검증: 20,005바이트)을 보내면
/// 스택 오버플로로 프로세스가 즉시 죽었다(catch 불가 — 5,005바이트는 생존, 20,005바이트는 사망).
/// 이제 reader 가 중첩 깊이를 세고 생성 코드·<see cref="MessageSerializer.DeserializeFromReader"/> 가
/// 재귀 지점에서 Enter/Leave 를 호출해 상한 초과를 <see cref="InvalidDataException"/> 으로 거부한다.
/// </summary>
public class NestingDepthTests
{
    [Fact]
    public void 기본_상한을_넘는_중첩은_스택오버플로_대신_InvalidDataException으로_거부된다()
    {
        byte[] payload = BuildChainPayload(MessageBufferReader.DefaultMaxNestingDepth + 1);

        var exception = Assert.Throws<InvalidDataException>(
            () => MessageSerializer.Deserialize<ChainMessage>(payload));

        Assert.Contains(MessageBufferReader.DefaultMaxNestingDepth.ToString(), exception.Message);
    }

    [Fact]
    public void object_dispatch_경로도_같은_상한을_적용한다()
    {
        byte[] payload = BuildChainPayload(MessageBufferReader.DefaultMaxNestingDepth + 1);

        Assert.Throws<InvalidDataException>(() => MessageSerializer.Deserialize(payload));
    }

    [Fact]
    public void 기본_상한_딱만큼의_중첩은_정상_복호된다()
    {
        int depth = MessageBufferReader.DefaultMaxNestingDepth;
        byte[] payload = BuildChainPayload(depth);

        var roundTrip = MessageSerializer.Deserialize<ChainMessage>(payload);

        Assert.Equal(depth, CountChain(roundTrip));
    }

    [Fact]
    public void reader_생성자로_상한을_올리면_더_깊은_그래프를_허용한다()
    {
        int depth = 200;
        byte[] payload = BuildChainPayload(depth);
        var reader = new MessageBufferReader(payload, 512);

        var roundTrip = MessageSerializer.Deserialize<ChainMessage>(ref reader);

        Assert.Equal(depth, CountChain(roundTrip));
        // 재귀가 되돌아 나오며 카운터가 짝 맞게 감소한다.
        Assert.Equal(0, reader.NestingDepth);
    }

    [Fact]
    public void 깊지_않고_넓은_그래프는_상한에_걸리지_않는다()
    {
        var message = new WideChainMessage
        {
            Items = Enumerable.Range(0, 500).Select(_ => new ChainMessage()).ToList(),
        };

        var roundTrip = MessageSerializer.Deserialize<WideChainMessage>(MessageSerializer.Serialize(message));

        Assert.Equal(500, roundTrip.Items!.Count);
    }

    [Fact]
    public void 기존_자기참조_그래프_왕복은_가드_도입_후에도_동작한다()
    {
        var a = new GraphMessage { Label = "a" };
        var b = new GraphMessage { Label = "b" };
        a.Next = b;
        b.Next = a;   // 순환 — 백레퍼런스로 복원
        a.Other = b;

        var roundTrip = MessageSerializer.Deserialize<GraphMessage>(MessageSerializer.Serialize(a));

        Assert.Equal("b", roundTrip.Next!.Label);
        Assert.True(ReferenceEquals(roundTrip.Next.Next, roundTrip));
        Assert.True(ReferenceEquals(roundTrip.Other, roundTrip.Next));
    }

    [Fact]
    public void Enter는_상한에서_거부되고_Leave는_0_아래로_내려가지_않는다()
    {
        var reader = new MessageBufferReader(new byte[8], 2);

        reader.EnterNestedObject();
        reader.EnterNestedObject();
        Assert.Equal(2, reader.NestingDepth);

        // ref struct 로컬은 람다에 포획할 수 없어 try/catch 로 직접 검증한다.
        Exception? thrown = null;
        try
        {
            reader.EnterNestedObject();
        }
        catch (Exception exception)
        {
            thrown = exception;
        }

        Assert.IsType<InvalidDataException>(thrown);
        Assert.Equal(2, reader.NestingDepth);   // 거부된 Enter 는 깊이를 올리지 않는다

        reader.LeaveNestedObject();
        reader.LeaveNestedObject();
        reader.LeaveNestedObject();             // 짝이 맞지 않는 호출 — 음수 깊이가 가드를 무력화하면 안 된다
        Assert.Equal(0, reader.NestingDepth);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void 상한이_0_이하면_reader_생성에서_거부된다(int maxNestingDepth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MessageBufferReader(new byte[4], maxNestingDepth));
    }

    // ---------- 쓰기 경로 (KI-25) ----------

    [Fact]
    public void writer와_reader_기본_상한은_의도적으로_동일하다()
    {
        // 비대칭이면 송신 측이 성공적으로 쓴 프레임을 수신 측이 기본 설정으로 읽지 못한다.
        Assert.Equal(MessageBufferReader.DefaultMaxNestingDepth, MessageBufferWriter.DefaultMaxNestingDepth);
    }

    [Fact]
    public void 깊은_체인_직렬화는_스택오버플로_대신_InvalidOperationException으로_거부된다()
    {
        ChainMessage head = BuildChain(MessageBufferReader.DefaultMaxNestingDepth + 1);

        var exception = Assert.Throws<InvalidOperationException>(() => MessageSerializer.Serialize(head));

        Assert.Contains(MessageBufferWriter.DefaultMaxNestingDepth.ToString(), exception.Message);
    }

    [Fact]
    public void 디스패치_멤버_순환_그래프는_스택오버플로_대신_InvalidOperationException으로_거부된다()
    {
        // 추상 메시지 멤버는 런타임 디스패치로 기록되고 그 경로는 백레퍼런스를 추적하지 않는다 —
        // 가드 없으면 이 순환은 쓰기 재귀를 무한히 깊게 만들어 프로세스를 죽인다.
        var envelope = new CommandEnvelope();
        envelope.Command = new WrapCommand { Seq = 1, Inner = envelope };

        Assert.Throws<InvalidOperationException>(() => MessageSerializer.Serialize(envelope));
    }

    [Fact]
    public void writer_상한을_올리면_깊은_체인을_쓰고_맞춘_reader_상한으로_되읽는다()
    {
        int links = 200;
        ChainMessage head = BuildChain(links);

        var writer = MessageBufferWriter.Create(256, 512);
        byte[] bytes;
        try
        {
            MessageSerializer.Serialize(head, ref writer);
            bytes = writer.ToArray();
            Assert.Equal(0, writer.NestingDepth);   // 재귀가 되돌아 나오며 짝 맞게 감소
        }
        finally
        {
            writer.Dispose();
        }

        var reader = new MessageBufferReader(bytes, 512);
        var roundTrip = MessageSerializer.Deserialize<ChainMessage>(ref reader);

        Assert.Equal(links, CountChain(roundTrip));
    }

    [Fact]
    public void 깊지_않고_넓은_그래프_직렬화는_쓰기_상한에_걸리지_않는다()
    {
        var message = new WideChainMessage
        {
            Items = Enumerable.Range(0, 500).Select(_ => new ChainMessage()).ToList(),
        };

        var roundTrip = MessageSerializer.Deserialize<WideChainMessage>(MessageSerializer.Serialize(message));

        Assert.Equal(500, roundTrip.Items!.Count);
    }

    [Fact]
    public void writer_Enter는_상한에서_거부되고_Leave는_0_아래로_내려가지_않는다()
    {
        var writer = MessageBufferWriter.Create(8, 2);

        writer.EnterNestedObject();
        writer.EnterNestedObject();
        Assert.Equal(2, writer.NestingDepth);

        // ref struct 로컬은 람다에 포획할 수 없어 try/catch 로 직접 검증한다.
        Exception? thrown = null;
        try
        {
            writer.EnterNestedObject();
        }
        catch (Exception exception)
        {
            thrown = exception;
        }

        Assert.IsType<InvalidOperationException>(thrown);
        Assert.Equal(2, writer.NestingDepth);   // 거부된 Enter 는 깊이를 올리지 않는다

        writer.LeaveNestedObject();
        writer.LeaveNestedObject();
        writer.LeaveNestedObject();             // 짝이 맞지 않는 호출 — 음수 깊이가 가드를 무력화하면 안 된다
        Assert.Equal(0, writer.NestingDepth);
        writer.Dispose();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void 상한이_0_이하면_writer_생성에서_거부된다(int maxNestingDepth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MessageBufferWriter.Create(16, maxNestingDepth));
    }

    /// <summary>links 개만큼 자기참조가 이어진 체인을 만든다(노드 수는 links + 1). 재귀 없이 반복으로 조립한다.</summary>
    static ChainMessage BuildChain(int links)
    {
        var head = new ChainMessage();
        var tail = head;
        for (int i = 0; i < links; i++)
        {
            tail.Next = new ChainMessage();
            tail = tail.Next;
        }

        return head;
    }

    /// <summary>
    /// 적대 프레임을 바이트로 직접 조립한다 — 객체 그래프를 만들어 직렬화하면 쓰기 측이 먼저
    /// 같은 깊이만큼 재귀하므로, 수신 경로만 검증하려면 와이어 바이트가 필요하다.
    /// 헤더 4바이트 + 수준당 NewObject 1바이트 + 종단 Null 1바이트.
    /// </summary>
    static byte[] BuildChainPayload(int depth)
    {
        byte[] serialized = MessageSerializer.Serialize(new ChainMessage());
        var payload = new byte[MessageWireFormat.IdHeaderSize + depth + 1];
        Array.Copy(serialized, payload, MessageWireFormat.IdHeaderSize);

        int position = MessageWireFormat.IdHeaderSize;
        for (int i = 0; i < depth; i++)
        {
            payload[position++] = (byte)MessageSerializer.ReferenceKind.NewObject;
        }

        payload[position] = (byte)MessageSerializer.ReferenceKind.Null;
        return payload;
    }

    /// <summary>복호된 체인 길이. 재귀가 아니라 반복으로 세어 검증 자체가 스택을 쓰지 않는다.</summary>
    static int CountChain(ChainMessage? head)
    {
        int count = 0;
        while (head?.Next is not null)
        {
            head = head.Next;
            count++;
        }

        return count;
    }
}

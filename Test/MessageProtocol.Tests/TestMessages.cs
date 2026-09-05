using MessageProtocol;

namespace MessageProtocol.Tests.Fixtures;

// ---------- 라운드트립 ----------

public enum Level : short { Low = -1, Mid = 0, High = 1 }

[StandaloneMessage(100)]
public partial class FlatMessage
{
    public int Value { get; set; }
}

[StandaloneMessage(101)]
[MessageCategory(MessageCategory.Category5)]
public partial class AllTypesMessage
{
    public bool Bool { get; set; }
    public byte Byte { get; set; }
    public sbyte SByte { get; set; }
    public short Int16 { get; set; }
    public ushort UInt16 { get; set; }
    public int Int32 { get; set; }
    public uint UInt32 { get; set; }
    public long Int64 { get; set; }
    public ulong UInt64 { get; set; }
    public float Single { get; set; }
    public double Double { get; set; }
    public decimal Decimal { get; set; }
    public char Char { get; set; }
    public string? Text { get; set; }
    public Level Level { get; set; }
    public byte[]? Blob { get; set; }
    public List<double>? Samples { get; set; }
    public string[]? Tags { get; set; }
    public IList<byte>? Codes { get; set; }
    public FlatMessage? Nested { get; set; }
}

[StandaloneMessage(102)]
public partial struct PointMessage
{
    public int X { get; set; }
    public int Y { get; set; }
}

[NonIdMessage]
public partial class NoIdMessage
{
    public byte Flag { get; set; }
    public string? Note { get; set; }
}

[StandaloneMessage(103)]
public partial record SettingsRecord
{
    public string? Theme { get; set; }
    public int Volume { get; set; }
}

[StandaloneMessage(104)]
public partial class GraphMessage
{
    public string? Label { get; set; }
    public GraphMessage? Next { get; set; }
    public GraphMessage? Other { get; set; }
    public PlainPoco? Poco { get; set; }
}

public class PlainPoco
{
    public int Number { get; set; }
    public string? Name { get; set; }
}

[StandaloneMessage(105)]
public partial class MemberControlMessage
{
    public int Kept { get; set; }

    [MessageIgnore]
    public int Excluded { get; set; }

    [MessageInclude]
    int _internal;

    public void SetInternal(int value) => _internal = value;
    public int GetInternal() => _internal;
}

// ---------- 상속/그룹 ----------

[GroupRootMessage(110)]
public partial class EventBase
{
    public long Timestamp { get; set; }
}

[GroupElementMessage(111)]
public partial class LoginEvent : EventBase
{
    public string? User { get; set; }
}

[GroupElementMessage(112)]
public partial class LogoutEvent : EventBase
{
    public int Reason { get; set; }
}

// ---------- 수동 구현 ----------

public class ManualStandalone : MessageProtocol.Serialize.IHasIdMessageSerializable<ManualStandalone>
{
    public int Value { get; set; }

    public static uint MessageId => MessageProtocol.MessageWireFormat.ComposeMessageId(
        MessageProtocol.MessageFlag.Standalone, (byte)MessageProtocol.MessageCategory.Category0, 130);

    public static void Serialize(ManualStandalone message, ref MessageProtocol.Serialize.MessageBufferWriter writer)
    {
        uint id = MessageId;
        writer.WriteByte((byte)(id >> 24));
        writer.WriteByte((byte)(id >> 16));
        writer.WriteByte((byte)(id >> 8));
        writer.WriteByte((byte)id);
        writer.WriteInt32(message.Value);
    }

    public static byte[] Serialize(ManualStandalone message)
    {
        var writer = MessageProtocol.Serialize.MessageBufferWriter.Create();
        try
        {
            Serialize(message, ref writer);
            return writer.ToArray();
        }
        finally
        {
            writer.Dispose();
        }
    }

    public static ManualStandalone Deserialize(ref MessageProtocol.Serialize.MessageBufferReader reader)
    {
        reader.Skip(MessageProtocol.MessageWireFormat.IdHeaderSize);
        return new ManualStandalone { Value = reader.ReadInt32() };
    }

    public static ManualStandalone Deserialize(byte[] data)
    {
        var reader = new MessageProtocol.Serialize.MessageBufferReader(data);
        return Deserialize(ref reader);
    }
}

// 계약 미구현 타입 (등록 실패 검증용)
public class NotAMessage
{
    public int Value { get; set; }
}

// ---------- 제네릭 ----------

[StandaloneMessage(120)]
[GenericMessage(typeof(GenericEnvelope<FlatMessage>), ClassId = 1)]
[GenericMessage(typeof(GenericEnvelope<SettingsRecord>), ClassId = 2)]
public partial class GenericEnvelope<T>
{
    public T? Value { get; set; }
    public string? Note { get; set; }
    public List<T?>? Items { get; set; }
}

[StandaloneMessage(121)]
[GenericMessage(typeof(GenericDuo<FlatMessage, SettingsRecord>), ClassId = 1)]
public partial class GenericDuo<TFirst, TSecond>
{
    public TFirst? First { get; set; }
    public TSecond? Second { get; set; }
}

[NonIdMessage]
public partial class GenericPair<T>
{
    public T? First { get; set; }
    public int Tag { get; set; }
}

// 동일 제네릭 페이로드의 두 구성이 한 그래프에 공존 — 헬퍼 이름 충돌 회귀 픽스처
[StandaloneMessage(123)]
public partial class DuplicateGenericPayloadsMessage
{
    public GenericPair<int>? IntPair { get; set; }
    public GenericPair<string>? TextPair { get; set; }
}

// 구성 선언이 없는 제네릭 메시지 — 직렬화 시 예외 검증용 (구성 선언 필수 규칙)
[StandaloneMessage(122)]
public partial class UnregisteredGeneric<T>
{
    public int X { get; set; }
}

// ---------- 분산 선언 ----------

// 선언부를 수정하지 않고 별도 캐리어 타입으로 GenericEnvelope 의 추가 구성을 선언한다.
[GenericMessage(typeof(GenericEnvelope<PointMessage>), ClassId = 3)]
static class GenericEnvelopeExtraConstructions { }

// ---------- 중첩 깊이 가드 (KI-14) ----------

// 자기참조 체인 — 작은 프레임에 깊은 중첩을 담아 재귀 스택을 소진시키는 적대 페이로드의 최소 형태.
// 와이어: 헤더 4바이트 + 수준당 ReferenceKind.NewObject 1바이트 + 종단 Null 1바이트.
[StandaloneMessage(124)]
public partial class ChainMessage
{
    public ChainMessage? Next { get; set; }
}

// 깊이가 아니라 *개수*로 중첩 객체를 많이 담는 픽스처 — 깊이 카운터가 짝 맞게 감소(Leave)하는지 검증한다.
[StandaloneMessage(125)]
public partial class WideChainMessage
{
    public List<ChainMessage>? Items { get; set; }
}

// ---------- 추상 그룹 루트 다형 멤버 (KI-24) ----------

// abstract [GroupRootMessage] 는 다형 그룹의 자연스러운 선언 형태지만 생성기는 인스턴스를 만들 수 없어
// 정적 Serialize/Deserialize 를 방출하지 않는다 — 멤버 타입으로 쓰이면 런타임 메시지 디스패치로
// *구체* 요소가 헤더째 기록되어야 한다 (정적 위임은 소비자 빌드를 CS0117 로 깨뜨렸다).
[GroupRootMessage(126)]
public abstract partial class AbstractCommand
{
    public long Seq { get; set; }
}

[GroupElementMessage(127)]
public partial class StartCommand : AbstractCommand
{
    public string? Target { get; set; }
}

[GroupElementMessage(128)]
public partial class StopCommand : AbstractCommand
{
    public int Code { get; set; }
}

[StandaloneMessage(129)]
public partial class CommandEnvelope
{
    public AbstractCommand? Command { get; set; }
    public List<AbstractCommand>? History { get; set; }
}

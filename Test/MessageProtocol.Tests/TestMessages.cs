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

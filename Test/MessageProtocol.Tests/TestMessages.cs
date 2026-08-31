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

using MessageProtocol;
using MessageProtocol.Serialize;

namespace SandboxMessages;

/// <summary>
/// 생성기 없이 계약 인터페이스를 직접 구현한 수동 메시지.
/// 헤더 4바이트를 구현이 직접 기록하며, 메시지 속성은 붙이지 않는다.
/// </summary>
public class ManualMessage : IHasIdMessageSerializable<ManualMessage>
{
    public int Value { get; set; }

    public static uint MessageId => MessageWireFormat.ComposeMessageId(
        MessageFlag.Standalone, (byte)MessageCategory.Category0, 20);

    public static void Serialize(ManualMessage message, ref MessageBufferWriter writer)
    {
        // 헤더는 와이어 순서(헤더 바이트 → ID 3바이트)로 직접 기록한다.
        uint id = MessageId;
        writer.WriteByte((byte)(id >> 24));
        writer.WriteByte((byte)(id >> 16));
        writer.WriteByte((byte)(id >> 8));
        writer.WriteByte((byte)id);
        writer.WriteInt32(message.Value);
    }

    public static byte[] Serialize(ManualMessage message)
    {
        var writer = MessageBufferWriter.Create();
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

    public static ManualMessage Deserialize(ref MessageBufferReader reader)
    {
        reader.Skip(MessageWireFormat.IdHeaderSize); // 헤더 4바이트는 라우팅이 소비함 (skip the 4 header bytes; routing consumes them)
        return new ManualMessage { Value = reader.ReadInt32() };
    }

    public static ManualMessage Deserialize(byte[] data)
    {
        var reader = new MessageBufferReader(data);
        return Deserialize(ref reader);
    }
}

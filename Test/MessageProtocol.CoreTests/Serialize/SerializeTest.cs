using System;
using System.Collections.Generic;
using MessageProtocol;
using MessageProtocol.Serialize;
using Xunit;

namespace MessageProtocol.Tests.Serialize
{
    public class SeralizeTest
    {
        [Fact]
        public void MessageGroupRoot_Serialize_Test()
        {
            RootMessage original = new();
            original.Id = 10;

            var bytes = MessageSerializer.Serialize(original);

            Assert.NotNull(bytes);
            Assert.True(bytes.Length > 0);
            Assert.True(bytes.Length >= 8);
        }

        [Fact]
        public void MessageGroupRoot_Deserialize_Test()
        {
            RootMessage original = new();
            original.Id = 42;
            var bytes = MessageSerializer.Serialize(original);

            var deserialized = MessageSerializer.Deserialize(bytes) as RootMessage;

            Assert.NotNull(deserialized);
            Assert.Equal(original.Id, deserialized.Id);
        }

        [Fact]
        public void MessageGroupElement_Serialize_Test()
        {
            ElementMessage original = new();
            original.Id = 20;
            original.Name = "TestElement";

            var bytes = MessageSerializer.Serialize(original);

            Assert.NotNull(bytes);
            Assert.True(bytes.Length > 0);
        }

        [Fact]
        public void MessageGroupElement_Deserialize_Test()
        {
            ElementMessage original = new();
            original.Id = 30;
            original.Name = "ElementName";
            var bytes = MessageSerializer.Serialize(original);

            var deserialized = MessageSerializer.Deserialize(bytes) as ElementMessage;

            Assert.NotNull(deserialized);
            Assert.Equal(original.Id, deserialized.Id);
            Assert.Equal(original.Name, deserialized.Name);
        }

        [Fact]
        public void MessageStandalone_Serialize_Test()
        {
            StandaloneMessage original = new();
            original.Flag = true;

            var bytes = MessageSerializer.Serialize(original);

            Assert.NotNull(bytes);
            Assert.True(bytes.Length > 0);
        }

        [Fact]
        public void MessageStandalone_Deserialize_Test()
        {
            StandaloneMessage original = new();
            original.Flag = false;
            var bytes = MessageSerializer.Serialize(original);

            var deserialized = MessageSerializer.Deserialize(bytes) as StandaloneMessage;

            Assert.NotNull(deserialized);
            Assert.Equal(original.Flag, deserialized.Flag);
        }

        [Fact]
        public void MessageStandalone_WithTrueFlag_Serialize_Test()
        {
            StandaloneMessage original = new();
            original.Flag = true;

            var bytes = MessageSerializer.Serialize(original);

            Assert.NotNull(bytes);
            Assert.True(bytes.Length > 0);
        }

        [Fact]
        public void MessageStandalone_WithTrueFlag_Deserialize_Test()
        {
            StandaloneMessage original = new();
            original.Flag = true;
            var bytes = MessageSerializer.Serialize(original);

            var deserialized = MessageSerializer.Deserialize(bytes) as StandaloneMessage;
            var deserialized2 = MessageSerializer.Deserialize<StandaloneMessage>(bytes);

            Assert.NotNull(deserialized);
            Assert.True(deserialized.Flag);
            Assert.True(deserialized2.Flag);
        }

        [Fact]
        public void MessageId_FlagBits_Should_BeEncodedInFirstByte()
        {
            Assert.Equal(0x04000001u, RootMessage.MessageId);
            Assert.Equal(0x0800000Au, ElementMessage.MessageId);
            Assert.Equal(0x02000000u, StandaloneMessage.MessageId);
            Assert.Equal(0x01000000u, PlainMessage.MessageId);
        }

        [Fact]
        public void MessageAttribute_Serialize_Should_WriteMessageFlag()
        {
            PlainMessage original = new();
            original.Value = 99;

            var bytes = MessageSerializer.Serialize(original);

            Assert.NotNull(bytes);
            Assert.True(bytes.Length >= 5);
            Assert.Equal(0x01, bytes[0] & 0x01);
        }

        [Fact]
        public void MessageAttribute_Deserialize_Test()
        {
            PlainMessage original = new();
            original.Value = 777;
            var bytes = MessageSerializer.Serialize(original);

            var deserialized = MessageSerializer.Deserialize<PlainMessage>(bytes);

            Assert.NotNull(deserialized);
            Assert.Equal(original.Value, deserialized.Value);
        }

        [Fact]
        public void MessageStruct_Serialize_Deserialize_Test()
        {
            MessageStruct original = new()
            {
                Value = 1234,
                Flag = true,
            };

            var bytes = MessageSerializer.Serialize(original);
            var deserialized = MessageSerializer.Deserialize<MessageStruct>(bytes);

            Assert.NotNull(bytes);
            Assert.True(bytes.Length > 0);
            Assert.Equal(original.Value, deserialized.Value);
            Assert.Equal(original.Flag, deserialized.Flag);
        }

        [Fact]
        public void Deserialize_Object_WithPlainMessage_Should_ThrowInvalidCastException()
        {
            PlainMessage original = new();
            original.Value = 777;
            var bytes = MessageSerializer.Serialize(original);

            Assert.Throws<InvalidCastException>(() => MessageSerializer.Deserialize(bytes));
        }

        [Fact]
        public void Deserialize_WithEmptyData_Should_ThrowArgumentException()
        {
            Assert.Throws<ArgumentException>(() => MessageSerializer.Deserialize(Array.Empty<byte>()));
            Assert.Throws<ArgumentException>(() => MessageSerializer.Deserialize<PlainMessage>(Array.Empty<byte>()));
        }

        [Fact]
        public void Deserialize_WithTooShortGroupedMessageHeader_Should_ThrowArgumentException()
        {
            byte[] bytes = [0x04, 0x00, 0x01];

            Assert.Throws<ArgumentException>(() => MessageSerializer.Deserialize(bytes));
        }

        [Fact]
        public void Deserialize_Object_WithUnregisteredMessageId_Should_ThrowKeyNotFoundException()
        {
            byte[] bytes = [0x02, 0x00, 0x00, 0x7F];

            Assert.Throws<KeyNotFoundException>(() => MessageSerializer.Deserialize(bytes));
        }
    }

    [MessageGroupRoot(1)]
    public partial class RootMessage
    {
        public int Id { get; set; }
    }

    [MessageGroupElement(10)]
    public partial class ElementMessage : RootMessage
    {
        public string? Name { get; set; }
    }

    [MessageStandalone(0)]
    public partial class StandaloneMessage
    {
        public bool Flag { get; set; }
    }

    [Message]
    public partial class PlainMessage
    {
        public int Value { get; set; }
    }

    [Message]
    public partial struct MessageStruct
    {
        public int Value { get; set; }
        public bool Flag { get; set; }
    }
}

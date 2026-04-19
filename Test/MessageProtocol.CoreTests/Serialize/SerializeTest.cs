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
        public void GroupRootMessage_Serialize_Test()
        {
            RootMessage original = new();
            original.Id = 10;

            var bytes = MessageSerializer.Serialize(original);

            Assert.NotNull(bytes);
            Assert.True(bytes.Length > 0);
            Assert.True(bytes.Length >= 8);
        }

        [Fact]
        public void GroupRootMessage_Deserialize_Test()
        {
            RootMessage original = new();
            original.Id = 42;
            var bytes = MessageSerializer.Serialize(original);

            var deserialized = MessageSerializer.Deserialize(bytes) as RootMessage;

            Assert.NotNull(deserialized);
            Assert.Equal(original.Id, deserialized.Id);
        }

        [Fact]
        public void GroupElementMessage_Serialize_Test()
        {
            ElementMessage original = new();
            original.Id = 20;
            original.Name = "TestElement";

            var bytes = MessageSerializer.Serialize(original);

            Assert.NotNull(bytes);
            Assert.True(bytes.Length > 0);
        }

        [Fact]
        public void GroupElementMessage_Deserialize_Test()
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
        public void StandaloneMessage_Serialize_Test()
        {
            StandalonePayload original = new();
            original.Flag = true;

            var bytes = MessageSerializer.Serialize(original);

            Assert.NotNull(bytes);
            Assert.True(bytes.Length > 0);
        }

        [Fact]
        public void StandaloneMessage_Deserialize_Test()
        {
            StandalonePayload original = new();
            original.Flag = false;
            var bytes = MessageSerializer.Serialize(original);

            var deserialized = MessageSerializer.Deserialize(bytes) as StandalonePayload;

            Assert.NotNull(deserialized);
            Assert.Equal(original.Flag, deserialized.Flag);
        }

        [Fact]
        public void StandaloneMessage_WithTrueFlag_Serialize_Test()
        {
            StandalonePayload original = new();
            original.Flag = true;

            var bytes = MessageSerializer.Serialize(original);

            Assert.NotNull(bytes);
            Assert.True(bytes.Length > 0);
        }

        [Fact]
        public void StandaloneMessage_WithTrueFlag_Deserialize_Test()
        {
            StandalonePayload original = new();
            original.Flag = true;
            var bytes = MessageSerializer.Serialize(original);

            var deserialized = MessageSerializer.Deserialize(bytes) as StandalonePayload;
            var deserialized2 = MessageSerializer.Deserialize<StandalonePayload>(bytes);

            Assert.NotNull(deserialized);
            Assert.True(deserialized.Flag);
            Assert.True(deserialized2.Flag);
        }

        [Fact]
        public void MessageId_FirstByte_Should_EncodeFlagNibbleAndCategoryNibble()
        {
            // 상위 4비트: MessageFlag, 하위 4비트: Category(기본 0)
            Assert.Equal(0x40000001u, RootMessage.MessageId);
            Assert.Equal(0x8000000Au, ElementMessage.MessageId);
            Assert.Equal(0x20000000u, StandalonePayload.MessageId);
            Assert.Equal(0x10000000u, PlainPayload.MessageId);
            Assert.Equal(0x43000001u, CategorizedRootMessage.MessageId);
        }

        [Fact]
        public void MessageCategory_WithoutAttribute_Should_DefaultToCategory0()
        {
            Assert.Equal(0u, (RootMessage.MessageId >> 24) & 0x0Fu);
            Assert.Equal(0u, (ElementMessage.MessageId >> 24) & 0x0Fu);
            Assert.Equal(0u, (StandalonePayload.MessageId >> 24) & 0x0Fu);
        }

        [Fact]
        public void MessageCategory_WithAttribute_Should_EncodeInMessageIdLowNibble()
        {
            Assert.Equal(3u, (CategorizedRootMessage.MessageId >> 24) & 0x0Fu);
            Assert.Equal(15u, (StandaloneCategory15.MessageId >> 24) & 0x0Fu);
        }

        [Fact]
        public void MessageCategory_Should_AppearInSerializedFirstByteLowNibble()
        {
            var root = new CategorizedRootMessage { Id = 5 };
            var bytes = MessageSerializer.Serialize(root);

            Assert.True(bytes.Length >= 4);
            Assert.Equal(3, bytes[0] & 0x0F);
            Assert.Equal(0x4, (bytes[0] >> 4) & 0x0F);
        }

        [Fact]
        public void CategorizedRootMessage_Serialize_Deserialize_Should_PreservePayload()
        {
            var original = new CategorizedRootMessage { Id = 91 };
            var bytes = MessageSerializer.Serialize(original);

            var deserialized = MessageSerializer.Deserialize(bytes) as CategorizedRootMessage;

            Assert.NotNull(deserialized);
            Assert.Equal(original.Id, deserialized.Id);
        }

        [Fact]
        public void StandaloneCategory15_MessageId_Should_MatchFlagAndCategoryNibbles()
        {
            Assert.Equal(0x2Fu, (StandaloneCategory15.MessageId >> 24) & 0xFF);
            Assert.Equal(0x2F000000u, StandaloneCategory15.MessageId);
        }

        [Fact]
        public void NonIdMessageAttribute_Serialize_Should_WriteMessageFlag()
        {
            PlainPayload original = new();
            original.Value = 99;

            var bytes = MessageSerializer.Serialize(original);

            Assert.NotNull(bytes);
            Assert.True(bytes.Length >= 5);
            Assert.NotEqual(0, (bytes[0] >> 4) & 0x01);
        }

        [Fact]
        public void NonIdMessageAttribute_Deserialize_Test()
        {
            PlainPayload original = new();
            original.Value = 777;
            var bytes = MessageSerializer.Serialize(original);

            var deserialized = MessageSerializer.Deserialize<PlainPayload>(bytes);

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
        public void Deserialize_Object_WithNonIdMessage_Should_ThrowInvalidCastException()
        {
            PlainPayload original = new();
            original.Value = 777;
            var bytes = MessageSerializer.Serialize(original);

            Assert.Throws<InvalidCastException>(() => MessageSerializer.Deserialize(bytes));
        }

        [Fact]
        public void Deserialize_WithEmptyData_Should_ThrowArgumentException()
        {
            Assert.Throws<ArgumentException>(() => MessageSerializer.Deserialize(Array.Empty<byte>()));
            Assert.Throws<ArgumentException>(() => MessageSerializer.Deserialize<PlainPayload>(Array.Empty<byte>()));
        }

        [Fact]
        public void Deserialize_WithTooShortGroupedMessageHeader_Should_ThrowArgumentException()
        {
            byte[] bytes = [0x40, 0x00, 0x01];

            Assert.Throws<ArgumentException>(() => MessageSerializer.Deserialize(bytes));
        }

        [Fact]
        public void Deserialize_Object_WithUnregisteredMessageId_Should_ThrowKeyNotFoundException()
        {
            byte[] bytes = [0x20, 0x00, 0x00, 0x7F];

            Assert.Throws<KeyNotFoundException>(() => MessageSerializer.Deserialize(bytes));
        }
    }

    [GroupRootMessage(1)]
    [MessageCategory(MessageCategory.Category3)]
    public partial class CategorizedRootMessage
    {
        public int Id { get; set; }
    }

    [GroupRootMessage(1)]
    public partial class RootMessage
    {
        public int Id { get; set; }
    }

    [GroupElementMessage(10)]
    public partial class ElementMessage : RootMessage
    {
        public string? Name { get; set; }
    }

    [StandaloneMessage(0)]
    [MessageCategory(MessageCategory.Category15)]
    public partial class StandaloneCategory15
    {
        public int Value { get; set; }
    }

    [StandaloneMessage(0)]
    public partial class StandalonePayload
    {
        public bool Flag { get; set; }
    }

    [NonIdMessage]
    public partial class PlainPayload
    {
        public int Value { get; set; }
    }

    [NonIdMessage]
    public partial struct MessageStruct
    {
        public int Value { get; set; }
        public bool Flag { get; set; }
    }
}

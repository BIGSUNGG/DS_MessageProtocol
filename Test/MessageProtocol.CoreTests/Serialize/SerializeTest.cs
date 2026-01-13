using MessageProtocol;
using MessageProtocol.Serialize;
using System.Collections.Generic;
using Xunit;

namespace MessageProtocol.Tests.Serialize
{
    public class SeralizeTest
    {
        [Fact]
        public void MessageGroupRoot_Serialize_Test()
        {
            // Arrange
            RootMessage original = new();
            original.Id = 10;

            // Act
            var bytes = MessageSerializer.Serialize(original);

            // Assert
            Assert.NotNull(bytes);
            Assert.True(bytes.Length > 0);
            // MessageId(4바이트) + Id(4바이트) = 최소 8바이트
            Assert.True(bytes.Length >= 8);
        }

        [Fact]
        public void MessageGroupRoot_Deserialize_Test()
        {
            // Arrange
            RootMessage original = new();
            original.Id = 42;
            var bytes = MessageSerializer.Serialize(original);

            // Act
            var deserialized = MessageSerializer.Deserialize(bytes) as RootMessage;

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(original.Id, deserialized.Id);
        }

        [Fact]
        public void MessageGroupElement_Serialize_Test()
        {
            // Arrange
            ElementMessage original = new();
            original.Id = 20;
            original.Name = "TestElement";

            // Act
            var bytes = MessageSerializer.Serialize(original);

            // Assert
            Assert.NotNull(bytes);
            Assert.True(bytes.Length > 0);
        }

        [Fact]
        public void MessageGroupElement_Deserialize_Test()
        {
            // Arrange
            ElementMessage original = new();
            original.Id = 30;
            original.Name = "ElementName";
            var bytes = MessageSerializer.Serialize(original);

            // Act
            var deserialized = MessageSerializer.Deserialize(bytes) as ElementMessage;

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(original.Id, deserialized.Id);
            Assert.Equal(original.Name, deserialized.Name);
        }

        [Fact]
        public void MessageStandalone_Serialize_Test()
        {
            // Arrange
            StandaloneMessage original = new();
            original.Flag = true;

            // Act
            var bytes = MessageSerializer.Serialize(original);

            // Assert
            Assert.NotNull(bytes);
            Assert.True(bytes.Length > 0);
        }

        [Fact]
        public void MessageStandalone_Deserialize_Test()
        {
            // Arrange
            StandaloneMessage original = new();
            original.Flag = false;
            var bytes = MessageSerializer.Serialize(original);

            // Act
            var deserialized = MessageSerializer.Deserialize(bytes) as StandaloneMessage;

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(original.Flag, deserialized.Flag);
        }

        [Fact]
        public void MessageStandalone_WithTrueFlag_Serialize_Test()
        {
            // Arrange
            StandaloneMessage original = new();
            original.Flag = true;

            // Act
            var bytes = MessageSerializer.Serialize(original);

            // Assert
            Assert.NotNull(bytes);
            Assert.True(bytes.Length > 0);
        }

        [Fact]
        public void MessageStandalone_WithTrueFlag_Deserialize_Test()
        {
            // Arrange
            StandaloneMessage original = new();
            original.Flag = true;
            var bytes = MessageSerializer.Serialize(original);

            // Act
            var deserialized = MessageSerializer.Deserialize(bytes) as StandaloneMessage;

            // Assert
            Assert.NotNull(deserialized);
            Assert.True(deserialized.Flag);
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
}
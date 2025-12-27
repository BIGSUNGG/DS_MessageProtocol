using Microsoft.VisualStudio.TestTools.UnitTesting;
using DS.MessageProtocol.Serialize;
using DS.MessageProtocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DS.MessageProtocol.Serialize.Tests
{
    [TestClass()]
    public class MessageSerializerTests
    {
        [TestMethod()]
        public void SerializeTest()
        {
            // Arrange
            var serializer = MessageSerializer.Instance;
            var testMessage = new TestElementMessage
            {
                A = 123,
                B = 456L,
                C = 789L
            };

            // Act
            byte[] serialized = serializer.Serialize(testMessage);

            // Assert
            Assert.IsNotNull(serialized);
            Assert.IsTrue(serialized.Length > 0);
            // MessageElementId (2 bytes) + 데이터 크기
            Assert.IsTrue(serialized.Length >= 2);
        }

        [TestMethod()]
        public void DeserializeTest()
        {
            // Arrange
            var serializer = MessageSerializer.Instance;
            var originalMessage = new TestElementMessage
            {
                A = 123,
                B = 456L,
                C = 789L
            };

            // Act
            byte[] serialized = serializer.Serialize(originalMessage);
            var deserialized = serializer.Deserialize<TestRootMessage>(serialized) as TestElementMessage;

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(originalMessage.A, deserialized.A);
            Assert.AreEqual(originalMessage.B, deserialized.B);
            Assert.AreEqual(originalMessage.C, deserialized.C);
        }

        [TestMethod()]
        public void SerializeDeserializeRoundTripTest()
        {
            // Arrange
            var serializer = MessageSerializer.Instance;
            var testCases = new[]
            {
                new TestElementMessage { A = 0, B = 0L, C = 0L },
                new TestElementMessage { A = int.MaxValue, B = long.MaxValue, C = long.MinValue },
                new TestElementMessage { A = int.MinValue, B = 0L, C = 0L },
                new TestElementMessage { A = 42, B = 100L, C = 200L }
            };

            foreach (var original in testCases)
            {
                // Act
                byte[] serialized = serializer.Serialize(original);
                var deserialized = serializer.Deserialize<TestElementMessage>(serialized);

                // Assert
                Assert.AreEqual(original.A, deserialized.A, $"Failed for A={original.A}");
                Assert.AreEqual(original.B, deserialized.B, $"Failed for B={original.B}");
                Assert.AreEqual(original.C, deserialized.C, $"Failed for C={original.C}");
            }
        }

        [TestMethod()]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SerializeInvalidTypeTest()
        {
            // Arrange
            var serializer = MessageSerializer.Instance;
            var invalidMessage = new SerializeTestClass { A = 123 };

            // Act & Assert
            serializer.Serialize(invalidMessage);
        }

        [TestMethod()]
        [ExpectedException(typeof(InvalidOperationException))]
        public void DeserializeInvalidTypeTest()
        {
            // Arrange
            var serializer = MessageSerializer.Instance;
            var invalidData = new byte[] { 0, 1, 2, 3 };

            // Act & Assert
            serializer.Deserialize<SerializeTestClass>(invalidData);
        }
    }

    // 테스트용 메시지 클래스들
    [MessageGroupRoot(1)]
    internal class TestRootMessage
    {
    }

    [MessageGroupElement(1)]
    internal class TestElementMessage : TestRootMessage
    {
        public int A;
        public long B;
        public long C;
    }

    // 어트리뷰트가 없는 테스트 클래스
    internal class SerializeTestClass
    {
        public int A;
        public long B = 0;
        public long C = 0;
        public string? D = null;
    }
}
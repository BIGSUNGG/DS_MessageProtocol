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
        #region GroupRoot 테스트

        [TestMethod()]
        public void GroupRoot_SerializeTest()
        {
            // Arrange
            var serializer = MessageSerializer.Instance;
            var testMessage = new TestRootMessageWithData
            {
                RootData = 12345
            };

            // Act
            byte[] serialized = serializer.Serialize(testMessage);

            // Assert
            Assert.IsNotNull(serialized);
            Assert.IsTrue(serialized.Length > 0);
            // RootId (2 bytes) + 0 (2 bytes) + 데이터 크기
            Assert.IsTrue(serialized.Length >= 4);
        }

        [TestMethod()]
        public void GroupRoot_DeserializeTest()
        {
            // Arrange
            var serializer = MessageSerializer.Instance;
            var originalMessage = new TestRootMessageWithData
            {
                RootData = 12345
            };

            // Act
            byte[] serialized = serializer.Serialize(originalMessage);
            var deserialized = serializer.Deserialize<TestRootMessageWithData>(serialized);

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(originalMessage.RootData, deserialized.RootData);
        }

        [TestMethod()]
        public void GroupRoot_SerializeDeserializeRoundTripTest()
        {
            // Arrange
            var serializer = MessageSerializer.Instance;
            var testCases = new[]
            {
                new TestRootMessageWithData { RootData = 0 },
                new TestRootMessageWithData { RootData = int.MaxValue },
                new TestRootMessageWithData { RootData = int.MinValue },
                new TestRootMessageWithData { RootData = 42 }
            };

            foreach (var original in testCases)
            {
                // Act
                byte[] serialized = serializer.Serialize(original);
                var deserialized = serializer.Deserialize<TestRootMessageWithData>(serialized);

                // Assert
                Assert.AreEqual(original.RootData, deserialized.RootData, $"Failed for RootData={original.RootData}");
            }
        }

        #endregion

        #region GroupElement 테스트

        [TestMethod()]
        public void GroupElement_SerializeTest()
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
            // RootId (2 bytes) + ElementId (2 bytes) + 데이터 크기
            Assert.IsTrue(serialized.Length >= 4);
        }

        [TestMethod()]
        public void GroupElement_DeserializeTest()
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
        public void GroupElementHasList_DeserializeTest()
        {
            // Arrange
            var serializer = MessageSerializer.Instance;
            var originalMessage = new TestElementHasListMessage
            {
                First = new() { 1, 2, 3, 4, 5 },
                A = 123,
                B = 456L,
                C = 789L,
                D = new() { 1, 2, 3, 4 }
            };

            // Act
            byte[] serialized = serializer.Serialize(originalMessage);
            var deserialized = serializer.Deserialize<TestRootMessage>(serialized) as TestElementHasListMessage;

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(originalMessage.First.Count, deserialized.First.Count);
            Assert.AreEqual(originalMessage.A, deserialized.A);
            Assert.AreEqual(originalMessage.B, deserialized.B);
            Assert.AreEqual(originalMessage.C, deserialized.C);
            Assert.AreEqual(originalMessage.D.Count, deserialized.D.Count);
        }

        [TestMethod()]
        public void GroupElement_SerializeDeserializeRoundTripTest()
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

        #endregion

        #region Standalone 테스트

        [TestMethod()]
        public void Standalone_SerializeTest()
        {
            // Arrange
            var serializer = MessageSerializer.Instance;
            var testMessage = new TestStandaloneMessage
            {
                X = 123,
                Y = 456L,
                Z = 789.5f
            };

            // Act
            byte[] serialized = serializer.Serialize(testMessage);

            // Assert
            Assert.IsNotNull(serialized);
            Assert.IsTrue(serialized.Length > 0);
            // Standalone은 ID 헤더 없이 데이터만
        }

        [TestMethod()]
        public void Standalone_DeserializeTest()
        {
            // Arrange
            var serializer = MessageSerializer.Instance;
            var originalMessage = new TestStandaloneMessage
            {
                X = 123,
                Y = 456L,
                Z = 789.5f
            };

            // Act
            byte[] serialized = serializer.Serialize(originalMessage);
            var deserialized = serializer.Deserialize<TestStandaloneMessage>(serialized);

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(originalMessage.X, deserialized.X);
            Assert.AreEqual(originalMessage.Y, deserialized.Y);
            Assert.AreEqual(originalMessage.Z, deserialized.Z);
        }

        [TestMethod()]
        public void Standalone_SerializeDeserializeRoundTripTest()
        {
            // Arrange
            var serializer = MessageSerializer.Instance;
            var testCases = new[]
            {
                new TestStandaloneMessage { X = 0, Y = 0L, Z = 0f },
                new TestStandaloneMessage { X = int.MaxValue, Y = long.MaxValue, Z = float.MaxValue },
                new TestStandaloneMessage { X = int.MinValue, Y = long.MinValue, Z = float.MinValue },
                new TestStandaloneMessage { X = 42, Y = 100L, Z = 200.5f }
            };

            foreach (var original in testCases)
            {
                // Act
                byte[] serialized = serializer.Serialize(original);
                var deserialized = serializer.Deserialize<TestStandaloneMessage>(serialized);

                // Assert
                Assert.AreEqual(original.X, deserialized.X, $"Failed for X={original.X}");
                Assert.AreEqual(original.Y, deserialized.Y, $"Failed for Y={original.Y}");
                Assert.AreEqual(original.Z, deserialized.Z, $"Failed for Z={original.Z}");
            }
        }

        #endregion

        #region Unmanaged 테스트

        [TestMethod()]
        public void Unmanaged_Int_SerializeDeserializeTest()
        {
            // Arrange
            var serializer = MessageSerializer.Instance;
            int originalValue = 12345;

            // Act
            byte[] serialized = serializer.Serialize(originalValue);
            int deserialized = serializer.Deserialize<int>(serialized);

            // Assert
            Assert.AreEqual(originalValue, deserialized);
        }

        [TestMethod()]
        public void Unmanaged_Long_SerializeDeserializeTest()
        {
            // Arrange
            var serializer = MessageSerializer.Instance;
            long originalValue = 123456789012345L;

            // Act
            byte[] serialized = serializer.Serialize(originalValue);
            long deserialized = serializer.Deserialize<long>(serialized);

            // Assert
            Assert.AreEqual(originalValue, deserialized);
        }

        [TestMethod()]
        public void Unmanaged_Float_SerializeDeserializeTest()
        {
            // Arrange
            var serializer = MessageSerializer.Instance;
            float originalValue = 123.456f;

            // Act
            byte[] serialized = serializer.Serialize(originalValue);
            float deserialized = serializer.Deserialize<float>(serialized);

            // Assert
            Assert.AreEqual(originalValue, deserialized);
        }

        [TestMethod()]
        public void Unmanaged_Double_SerializeDeserializeTest()
        {
            // Arrange
            var serializer = MessageSerializer.Instance;
            double originalValue = 123.456789;

            // Act
            byte[] serialized = serializer.Serialize(originalValue);
            double deserialized = serializer.Deserialize<double>(serialized);

            // Assert
            Assert.AreEqual(originalValue, deserialized);
        }

        [TestMethod()]
        public void Unmanaged_SerializeDeserializeRoundTripTest()
        {
            // Arrange
            var serializer = MessageSerializer.Instance;
            var testCases = new[]
            {
                (0, 0L, 0f, 0.0),
                (int.MaxValue, long.MaxValue, float.MaxValue, double.MaxValue),
                (int.MinValue, long.MinValue, float.MinValue, double.MinValue),
                (42, 100L, 200.5f, 300.75)
            };

            foreach (var (intVal, longVal, floatVal, doubleVal) in testCases)
            {
                // Act & Assert - int
                byte[] intSerialized = serializer.Serialize(intVal);
                int intDeserialized = serializer.Deserialize<int>(intSerialized);
                Assert.AreEqual(intVal, intDeserialized, $"Failed for int={intVal}");

                // Act & Assert - long
                byte[] longSerialized = serializer.Serialize(longVal);
                long longDeserialized = serializer.Deserialize<long>(longSerialized);
                Assert.AreEqual(longVal, longDeserialized, $"Failed for long={longVal}");

                // Act & Assert - float
                byte[] floatSerialized = serializer.Serialize(floatVal);
                float floatDeserialized = serializer.Deserialize<float>(floatSerialized);
                Assert.AreEqual(floatVal, floatDeserialized, $"Failed for float={floatVal}");

                // Act & Assert - double
                byte[] doubleSerialized = serializer.Serialize(doubleVal);
                double doubleDeserialized = serializer.Deserialize<double>(doubleSerialized);
                Assert.AreEqual(doubleVal, doubleDeserialized, $"Failed for double={doubleVal}");
            }
        }

        #endregion
    }

    // 테스트용 메시지 클래스들
    [MessageGroupRoot(1)]
    internal class TestRootMessage
    {
    }

    [MessageGroupRoot(2)]
    internal class TestRootMessageWithData
    {
        public int RootData;
    }

    [MessageGroupElement(1)]
    internal class TestElementMessage : TestRootMessage
    {
        public int A;
        public long B;
        public long C;
    }

    [MessageGroupElement(2)]
    internal class TestElementHasListMessage : TestRootMessage
    {
        public List<float> First = new List<float>();
        public int A;
        public long B;
        public long C;
        public List<int> D = new List<int>();
    }

    [MessageStandalone]
    internal class TestStandaloneMessage
    {
        public int X;
        public long Y;
        public float Z;
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
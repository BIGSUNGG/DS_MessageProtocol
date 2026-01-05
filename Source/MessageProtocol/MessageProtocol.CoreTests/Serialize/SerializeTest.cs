using MessageProtocol;
using MessageProtocol.Serialize;
using System.Collections.Generic;
using Xunit;

namespace MessageProtocol.Tests.Serialize
{
    public class SeralizeTest
    {
        [Fact]
        void MessageGroupRootSerializeTest()
        {
            RootMessage target = new();
            target.Id = 10;

            bool a = target is IMessageSerializable<RootMessage>;

            var bytes = MessageSerializer.Instance.Serialize(target);
        }

        [Fact]
        public void MessageGroupRootDeserializeTest()
        {
            RootMessage target = new();
            target.Id = 10;

            bool a = target is IMessageSerializable<RootMessage>;

            var bytes = MessageSerializer.Instance.Serialize(target);
            var deserialized = MessageSerializer.Instance.Deserialize<RootMessage>(bytes);
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
        public string Name { get; set; }
    }

    [MessageStandalone]
    public partial class StandaloneMessage
    {
        public bool Flag { get; set; }
    }
}
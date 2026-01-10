using MessageProtocol;
using MessageProtocol.Serialize;
using System.Collections.Generic;

namespace MessageProtocol.Tests.Serialize
{
    public class SeralizeTest
    {
        public void MessageGroupRootSerializeTest()
        {
            RootMessage target = new();
            target.Id = 10;

            bool a = target is IMessageSerializable<RootMessage>;

            var bytes = MessageSerializer.Serialize(target);
        }

        public void MessageGroupRootDeserializeTest()
        {
            RootMessage target = new();
            target.Id = 10;

            bool a = target is IMessageSerializable<RootMessage>;

            var bytes = MessageSerializer.Serialize(target);
            var deserialized = MessageSerializer.Deserialize<RootMessage>(bytes);
        }
    }

    [MessageGroupRoot(1)]
    public partial class RootMessage
    {
        public int Id { get; set; }
    }

    [MessageGroupElement(10)]
    public partial class ElementMessage
    {
        public string Name { get; set; }
    }

    [MessageStandalone]
    public partial class StandaloneMessage
    {
        public bool Flag { get; set; }
    }
}
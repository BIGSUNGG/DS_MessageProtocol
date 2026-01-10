using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageProtocol.Serialize
{
    public interface IMessageSerializable<T>
    {
        static abstract uint MessageId { get; }

        static abstract byte[] Serialize(T message);
        static abstract T Deserialize(byte[] data);
    }
}

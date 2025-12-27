using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DS.MessageProtocol.Serialize
{
    internal interface IMessageSerialize
    {
        byte[] Serialize(object message);
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DS.MessageProtocol;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class MessageGroupRoot : Attribute
{
    // TODO : 사용처 만들기
    public ushort MessageRootId { get; private set; }

    public MessageGroupRoot(ushort messageRootId)
    {
        MessageRootId = messageRootId;
        if(MessageRootId.Equals(0))
            throw new InvalidOperationException("MessageRootId cannot be 0");
    }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class MessageGroupElement : Attribute
{
    public ushort MessageElementId { get; private set; }

    public MessageGroupElement(ushort messageElementId)
    {
        MessageElementId = messageElementId;
    }
}
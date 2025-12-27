using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DS.Message;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class MessageInfo : Attribute
{
    public Type RootClassType { get; private set; }
    public ushort MessageId { get; private set; }

    public MessageInfo(Type rootClassType, ushort messageId)
    {
        RootClassType = rootClassType;
        MessageId = messageId;
    }
}
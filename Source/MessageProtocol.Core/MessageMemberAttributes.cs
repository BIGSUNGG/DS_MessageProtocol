using System;

namespace MessageProtocol
{
    /// <summary>직렬화 대상 멤버에서 제외한다.</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public class MessageIgnoreAttribute : Attribute
    {
    }

    /// <summary>public 이 아닌 멤버를 직렬화 대상에 포함한다.</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public class MessageIncludeAttribute : Attribute
    {
    }
}

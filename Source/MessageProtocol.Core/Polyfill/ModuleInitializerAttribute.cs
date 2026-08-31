#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    /// <summary>netstandard2.1 대상용 polyfill. net6+ 에서는 런타임 제공 타입을 사용한다.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ModuleInitializerAttribute : Attribute
    {
    }
}
#endif

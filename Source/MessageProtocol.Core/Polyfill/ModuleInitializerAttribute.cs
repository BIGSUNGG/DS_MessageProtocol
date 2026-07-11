#if !NET5_0_OR_GREATER
using System;

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ModuleInitializerAttribute : Attribute
    {
    }
}
#endif

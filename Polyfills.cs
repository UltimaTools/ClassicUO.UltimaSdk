#if NETFRAMEWORK

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
    public sealed class InlineArrayAttribute : Attribute
    {
        public int Length { get; }

        public InlineArrayAttribute(int length)
        {
            Length = length;
        }
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, AllowMultiple = false)]
    public sealed class NotNullAttribute : Attribute
    {
    }
}

#endif

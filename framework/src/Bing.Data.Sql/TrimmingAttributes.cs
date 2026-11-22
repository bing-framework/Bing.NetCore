namespace System.Diagnostics.CodeAnalysis;

// netstandard2.0 的编译兼容声明；裁剪器按完整类型名识别这些标记。
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property |
    AttributeTargets.ReturnValue | AttributeTargets.GenericParameter, Inherited = false)]
internal sealed class DynamicallyAccessedMembersAttribute : Attribute
{
    public DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes) => MemberTypes = memberTypes;

    public DynamicallyAccessedMemberTypes MemberTypes { get; }
}

[Flags]
internal enum DynamicallyAccessedMemberTypes
{
    None = 0,
    PublicProperties = 512,
    Interfaces = 8192
}

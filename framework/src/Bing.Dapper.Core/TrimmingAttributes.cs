namespace System.Diagnostics.CodeAnalysis;

// netstandard2.0 的编译兼容声明；裁剪器按完整类型名识别这些标记。
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property |
    AttributeTargets.ReturnValue | AttributeTargets.GenericParameter, Inherited = false)]
internal sealed class DynamicallyAccessedMembersAttribute : Attribute
{
    public DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes) => MemberTypes = memberTypes;

    public DynamicallyAccessedMemberTypes MemberTypes { get; }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
internal sealed class DynamicDependencyAttribute : Attribute
{
    public DynamicDependencyAttribute(DynamicallyAccessedMemberTypes memberTypes, Type type)
    {
        MemberTypes = memberTypes;
        Type = type;
    }
    public DynamicallyAccessedMemberTypes MemberTypes { get; }
    public Type Type { get; }
}

[Flags]
internal enum DynamicallyAccessedMemberTypes { PublicConstructors = 3, PublicProperties = 512 }

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
internal sealed class UnconditionalSuppressMessageAttribute : Attribute
{
    public UnconditionalSuppressMessageAttribute(string category, string checkId)
    {
        Category = category;
        CheckId = checkId;
    }
    public string Category { get; }
    public string CheckId { get; }
    public string Justification { get; set; }
}

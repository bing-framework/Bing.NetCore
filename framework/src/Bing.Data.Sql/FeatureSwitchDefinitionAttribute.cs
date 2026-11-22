namespace System.Diagnostics.CodeAnalysis;

/// <summary>为 netstandard2.0 提供由 .NET 9 及以上裁剪器识别的功能开关标记。</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
internal sealed class FeatureSwitchDefinitionAttribute : Attribute
{
    public FeatureSwitchDefinitionAttribute(string switchName) => SwitchName = switchName;
    public string SwitchName { get; }
}

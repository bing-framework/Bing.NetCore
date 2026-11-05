using System.ComponentModel;
using Bing.Domain.Entities;

/// <summary>验证真实 JIT 与 NativeAOT 构建中的同名重载不会阻断生成的比较钩子。</summary>
internal static class HookOverrideScenarios
{
    public static void Run()
    {
        var changes = new HookOverrideRoot { Name = "old" }
            .GetChanges(new HookOverrideRoot { Name = "new" }).ToArray();
        if (changes.Length != 1 || changes[0].PropertyName != nameof(HookOverrideRoot.Name) ||
            changes[0].Description != "名称" || changes[0].OldValue != "\"old\"" ||
            changes[0].NewValue != "\"new\"")
            throw new InvalidOperationException("无关 AddChanges 重载阻断了派生对象的变更采集。");
        Console.WriteLine("PASS | 无关 AddChanges 重载不会阻断领域对象比较");
    }
}

internal abstract partial class HookOverrideBase : DomainObjectBase<HookOverrideRoot>
{
    protected virtual void AddChanges(int value) { }
}

internal partial class HookOverrideRoot : HookOverrideBase
{
    [Description("名称")]
    public string Name { get; set; } = string.Empty;

    protected override void AddChanges(int value) { }
}

namespace Bing.Data.Sql;

/// <summary>宿主级兼容功能开关；关闭后裁剪器可移除反射与 Dapper 动态执行分支。</summary>
internal static class SqlRuntimeFeatures
{
    internal const string ReflectionFallbackSwitch = "Bing.Data.Sql.ReflectionFallbackEnabled";

    [System.Diagnostics.CodeAnalysis.FeatureSwitchDefinition(ReflectionFallbackSwitch)]
    internal static bool ReflectionFallbackEnabled =>
        !AppContext.TryGetSwitch(ReflectionFallbackSwitch, out var enabled) || enabled;

    internal static NotSupportedException DynamicExecutionDisabled() =>
        new("宿主已关闭 SQL 反射兼容功能。请登记生成模型、静态 Provider 工厂，并使用受支持的生成式执行入口。");
}

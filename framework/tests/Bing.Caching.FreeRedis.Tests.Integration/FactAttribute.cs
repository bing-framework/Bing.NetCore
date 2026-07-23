using Bing.Test.Shared;

namespace Bing.Caching.FreeRedis.Tests;

/// <summary>
/// Redis 集成测试专用 Fact 特性。
/// </summary>
public sealed class FactAttribute : Xunit.FactAttribute
{
    /// <summary>
    /// 初始化 Redis 集成测试特性。
    /// </summary>
    public FactAttribute()
    {
        if (IntegrationTestGate.IsProviderEnabled() == false)
            Skip = "Redis 集成测试已跳过。设置环境变量 RUN_INTEGRATION_TESTS=true 以启用。";
    }
}
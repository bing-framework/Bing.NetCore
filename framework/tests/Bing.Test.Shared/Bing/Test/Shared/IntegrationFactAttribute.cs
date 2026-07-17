using Xunit;

namespace Bing.Test.Shared;

/// <summary>
/// 集成测试专用 Fact 特性。
/// 默认跳过（不执行），必须将环境变量 <c>RUN_INTEGRATION_TESTS=true</c> 时才运行。
/// 用法：
/// <code>
///   [IntegrationFact]
///   public async Task MyIntegrationTest() { ... }
/// </code>
/// 在 CI 中启用：
/// <code>
///   RUN_INTEGRATION_TESTS=true dotnet test
/// </code>
/// </summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化集成测试特性，若环境变量未设置则自动跳过
    /// </summary>
    public IntegrationFactAttribute() : this(null)
    {
    }

    /// <summary>
    /// 初始化指定 Provider 的集成测试特性，若对应环境变量未设置则自动跳过。
    /// </summary>
    /// <param name="provider">数据库 Provider 名称。</param>
    public IntegrationFactAttribute(string provider) => Skip = IntegrationTestGate.GetSkipReason(provider);
}

/// <summary>
/// 集成测试专用 Theory 特性。
/// 与 <see cref="IntegrationFactAttribute"/> 相同的跳过策略。
/// </summary>
public sealed class IntegrationTheoryAttribute : TheoryAttribute
{
    /// <summary>
    /// 初始化集成测试特性，若环境变量未设置则自动跳过
    /// </summary>
    public IntegrationTheoryAttribute() : this(null)
    {
    }

    /// <summary>
    /// 初始化指定 Provider 的集成测试特性，若对应环境变量未设置则自动跳过。
    /// </summary>
    /// <param name="provider">数据库 Provider 名称。</param>
    public IntegrationTheoryAttribute(string provider) => Skip = IntegrationTestGate.GetSkipReason(provider);
}

using Bing.Test.Shared;
using Xunit;

namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// MySQL 跨数据库集成测试专用 Fact 特性。
/// </summary>
public sealed class MySqlCrossDatabaseFactAttribute : FactAttribute
{
    /// <summary>
    /// 跨数据库集成测试显式启用环境变量。
    /// </summary>
    public const string EnvironmentVariable = "BING_INTEGRATION_MYSQL_CROSS_DATABASE";

    /// <summary>
    /// 跨数据库测试库名称环境变量。
    /// </summary>
    public const string DatabaseNameEnvironmentVariable = "BING_INTEGRATION_MYSQL_CROSS_DATABASE_NAME";

    /// <summary>
    /// 初始化跨数据库集成测试特性。
    /// </summary>
    public MySqlCrossDatabaseFactAttribute()
    {
        if (IntegrationTestGate.IsProviderEnabled("MySql") == false)
        {
            Skip = "当前测试环境未启用MySQL集成测试。";
            return;
        }
        if (IsEnabled(EnvironmentVariable) == false)
        {
            Skip = "当前测试环境未启用MySQL跨数据库集成测试。";
            return;
        }
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(DatabaseNameEnvironmentVariable)))
            Skip = "当前测试环境未配置MySQL跨数据库测试库名称。";
    }

    /// <summary>
    /// 判断指定环境变量是否显式启用。
    /// </summary>
    /// <param name="environmentVariable">环境变量名称。</param>
    /// <returns>已启用时返回 true。</returns>
    private static bool IsEnabled(string environmentVariable) => string.Equals(
        Environment.GetEnvironmentVariable(environmentVariable), "true", StringComparison.OrdinalIgnoreCase);
}
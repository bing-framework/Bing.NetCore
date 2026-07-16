using System.Data.Common;

namespace Bing.Test.Shared;

/// <summary>
/// 集成测试数据库安全校验器。
/// </summary>
public static class IntegrationDatabaseSafetyValidator
{
    private const string RunIntegrationTestsEnvironmentVariable = "RUN_INTEGRATION_TESTS";
    private const string AllowDatabaseResetEnvironmentVariable = "ALLOW_DATABASE_RESET_FOR_TESTS";

    /// <summary>
    /// 验证当前进程是否允许重置指定的专用测试数据库。
    /// </summary>
    /// <param name="connectionString">数据库连接字符串。</param>
    /// <param name="databaseNamePrefix">允许的数据库名称前缀。</param>
    /// <param name="databaseNameSuffix">允许的数据库名称后缀。</param>
    public static void EnsureResetAllowed(string connectionString, string databaseNamePrefix = "bing_",
        string databaseNameSuffix = "_test")
    {
        if (IsEnabled(RunIntegrationTestsEnvironmentVariable) == false)
            throw new InvalidOperationException($"重置集成测试数据库要求设置 {RunIntegrationTestsEnvironmentVariable}=true。");
        if (IsEnabled(AllowDatabaseResetEnvironmentVariable) == false)
            throw new InvalidOperationException($"重置集成测试数据库要求设置 {AllowDatabaseResetEnvironmentVariable}=true。");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("重置集成测试数据库要求提供连接字符串。");

        var databaseName = GetDatabaseName(connectionString);
        if (string.IsNullOrWhiteSpace(databaseName) ||
            databaseName.StartsWith(databaseNamePrefix, StringComparison.OrdinalIgnoreCase) == false ||
            databaseName.EndsWith(databaseNameSuffix, StringComparison.OrdinalIgnoreCase) == false)
        {
            throw new InvalidOperationException(
                $"拒绝重置数据库 {databaseName ?? "<unknown>"}。仅允许名称以 {databaseNamePrefix} 开头且以 {databaseNameSuffix} 结尾的专用测试数据库。");
        }
    }

    /// <summary>
    /// 从连接字符串解析数据库名称。
    /// </summary>
    /// <param name="connectionString">数据库连接字符串。</param>
    /// <returns>数据库名称。</returns>
    private static string GetDatabaseName(string connectionString)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        foreach (var key in new[] { "Database", "Initial Catalog" })
        {
            if (builder.TryGetValue(key, out var value) && string.IsNullOrWhiteSpace(value?.ToString()) == false)
                return value.ToString().Trim();
        }
        return null;
    }

    /// <summary>
    /// 判断环境变量是否显式启用。
    /// </summary>
    /// <param name="environmentVariable">环境变量名称。</param>
    /// <returns>启用时返回 true。</returns>
    private static bool IsEnabled(string environmentVariable) => string.Equals(
        Environment.GetEnvironmentVariable(environmentVariable), "true", StringComparison.OrdinalIgnoreCase);
}
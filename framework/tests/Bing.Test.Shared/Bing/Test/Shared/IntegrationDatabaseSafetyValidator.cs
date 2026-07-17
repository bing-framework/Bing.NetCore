using System.Data.Common;

namespace Bing.Test.Shared;

/// <summary>
/// 集成测试数据库安全校验器。
/// </summary>
public static class IntegrationDatabaseSafetyValidator
{
    private const string AllowDatabaseResetEnvironmentVariable = "ALLOW_DATABASE_RESET_FOR_TESTS";

    private static readonly HashSet<string> SystemDatabaseNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "information_schema", "master", "model", "msdb", "mysql", "performance_schema", "postgres", "sys",
        "tempdb", "template0", "template1"
    };

    /// <summary>
    /// 验证当前进程是否允许重置指定的专用测试数据库。
    /// </summary>
    /// <param name="connectionString">数据库连接字符串。</param>
    /// <param name="provider">数据库 Provider 名称。</param>
    public static void EnsureResetAllowed(string connectionString, string provider = null)
    {
        EnsureDatabaseOperationAllowed(connectionString, provider);
        if (IsEnabled(AllowDatabaseResetEnvironmentVariable) == false)
            throw new InvalidOperationException($"重置集成测试数据库要求设置 {AllowDatabaseResetEnvironmentVariable}=true。");
    }

    /// <summary>
    /// 验证当前进程是否允许操作指定的专用测试数据库。
    /// </summary>
    /// <param name="connectionString">数据库连接字符串。</param>
    /// <param name="provider">数据库 Provider 名称。</param>
    public static void EnsureDatabaseOperationAllowed(string connectionString, string provider = null)
    {
        if (IntegrationTestGate.IsProviderEnabled(provider) == false)
            throw new InvalidOperationException($"集成测试数据库操作未启用。{IntegrationTestGate.GetSkipReason(provider)}");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("集成测试数据库操作要求提供连接字符串。");
        var databaseName = GetDatabaseName(connectionString);
        if (IsSafeTestDatabaseName(databaseName) == false)
            throw new InvalidOperationException($"拒绝操作数据库 {databaseName ?? "<unknown>"}。仅允许专用测试数据库。");
    }

    /// <summary>
    /// 判断数据库名称是否符合专用集成测试数据库安全约定。
    /// </summary>
    /// <param name="databaseName">数据库名称。</param>
    /// <returns>安全时返回 true。</returns>
    public static bool IsSafeTestDatabaseName(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            return false;
        var normalizedName = databaseName.Trim();
        if (SystemDatabaseNames.Contains(normalizedName) || HasUnsafeEnvironmentToken(normalizedName))
            return false;
        return normalizedName.EndsWith("_test", StringComparison.OrdinalIgnoreCase) ||
               normalizedName.EndsWith("_tests", StringComparison.OrdinalIgnoreCase) ||
               normalizedName.EndsWith("_integration", StringComparison.OrdinalIgnoreCase) ||
               normalizedName.EndsWith("_integration_test", StringComparison.OrdinalIgnoreCase);
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
    /// 判断数据库名是否含有危险的环境标识。
    /// </summary>
    /// <param name="databaseName">数据库名称。</param>
    /// <returns>包含危险环境标识时返回 true。</returns>
    private static bool HasUnsafeEnvironmentToken(string databaseName)
    {
        var tokens = databaseName.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Any(token => string.Equals(token, "prod", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(token, "production", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(token, "development", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 判断环境变量是否显式启用。
    /// </summary>
    /// <param name="environmentVariable">环境变量名称。</param>
    /// <returns>启用时返回 true。</returns>
    private static bool IsEnabled(string environmentVariable) => string.Equals(
        Environment.GetEnvironmentVariable(environmentVariable), "true", StringComparison.OrdinalIgnoreCase);
}
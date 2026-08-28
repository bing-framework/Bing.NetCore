namespace Bing.Test.Shared;

/// <summary>
/// 集成测试连接字符串解析器。
/// </summary>
public static class IntegrationTestConnectionStringResolver
{
    private const string DefaultConnectionEnvironmentVariable = "ConnectionStrings__DefaultConnection";

    /// <summary>
    /// 获取指定 Provider 的集成测试连接字符串。
    /// </summary>
    /// <param name="provider">数据库 Provider 名称。</param>
    /// <returns>Provider 专属连接字符串，未配置时仅为本地兼容回退默认连接字符串。</returns>
    /// <exception cref="ArgumentException">Provider 不受支持时抛出。</exception>
    /// <exception cref="InvalidOperationException">未配置连接字符串时抛出。</exception>
    public static string Resolve(string provider)
    {
        var settings = GetSettings(provider);
        var connectionString = Environment.GetEnvironmentVariable(settings.ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString) == false)
            return connectionString;

        connectionString = Environment.GetEnvironmentVariable(DefaultConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString) == false)
            return connectionString;

        throw new InvalidOperationException(
            $"未配置 {settings.Provider} 集成测试连接字符串。请设置环境变量 " +
            $"{settings.ConnectionEnvironmentVariable}；仅在该变量缺失时才会回退到 " +
            $"{DefaultConnectionEnvironmentVariable}。本地可显式使用忽略的 " +
            $"integration.local.runsettings；受保护 CI 必须设置 Provider 专属变量。" +
            "详见 docs/testing/database-integration-tests.md。");
    }

    /// <summary>
    /// 获取 Provider 配置。
    /// </summary>
    /// <param name="provider">数据库 Provider 名称。</param>
    /// <returns>Provider 配置。</returns>
    /// <exception cref="ArgumentException">Provider 不受支持时抛出。</exception>
    private static ProviderSettings GetSettings(string provider)
    {
        var normalizedProvider = IntegrationTestGate.NormalizeProvider(provider);
        return normalizedProvider switch
        {
            "MYSQL" => new ProviderSettings("MySql", "ConnectionStrings__MySqlConnection"),
            "DORIS" => new ProviderSettings("Doris", "ConnectionStrings__DorisConnection"),
            "POSTGRESQL" => new ProviderSettings("PostgreSql", "ConnectionStrings__PostgreSqlConnection"),
            "SQLSERVER" => new ProviderSettings("SqlServer", "ConnectionStrings__SqlServerConnection"),
            "ORACLE" => new ProviderSettings("Oracle", "ConnectionStrings__OracleConnection"),
            _ => throw new ArgumentException($"不支持 Provider '{provider}' 的集成测试连接字符串解析。", nameof(provider))
        };
    }

    /// <summary>
    /// Provider 集成测试配置。
    /// </summary>
    private sealed class ProviderSettings
    {
        /// <summary>
        /// 初始化 Provider 集成测试配置。
        /// </summary>
        /// <param name="provider">Provider 名称。</param>
        /// <param name="connectionEnvironmentVariable">连接字符串环境变量名称。</param>
            public ProviderSettings(string provider, string connectionEnvironmentVariable)
        {
            Provider = provider;
            ConnectionEnvironmentVariable = connectionEnvironmentVariable;
        }

        /// <summary>
        /// 获取 Provider 名称。
        /// </summary>
        public string Provider { get; }

        /// <summary>
        /// 获取连接字符串环境变量名称。
        /// </summary>
        public string ConnectionEnvironmentVariable { get; }

    }
}
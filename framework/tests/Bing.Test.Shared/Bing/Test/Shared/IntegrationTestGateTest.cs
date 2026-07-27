using Xunit;

namespace Bing.Test.Shared;

/// <summary>
/// 环境变量测试集合定义。
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EnvironmentVariableTestCollection
{
    /// <summary>
    /// 环境变量测试集合名称。
    /// </summary>
    public const string Name = "EnvironmentVariableTests";
}

/// <summary>
/// 集成测试门控和数据库安全校验测试。
/// </summary>
[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class IntegrationTestGateTest : IDisposable
{
    private const string MySqlVariable = "RUN_MYSQL_INTEGRATION_TESTS";
    private const string PostgreSqlVariable = "RUN_POSTGRESQL_INTEGRATION_TESTS";
    private const string SqlServerVariable = "RUN_SQLSERVER_INTEGRATION_TESTS";
    private const string OracleVariable = "RUN_ORACLE_INTEGRATION_TESTS";
    private const string ResetVariable = "ALLOW_DATABASE_RESET_FOR_TESTS";
    private const string DefaultConnectionVariable = "ConnectionStrings__DefaultConnection";
    private const string MySqlConnectionVariable = "ConnectionStrings__MySqlConnection";
    private const string PostgreSqlConnectionVariable = "ConnectionStrings__PostgreSqlConnection";
    private readonly Dictionary<string, string> _originalValues = new();

    /// <summary>
    /// 初始化集成测试环境变量隔离。
    /// </summary>
    public IntegrationTestGateTest()
    {
        ClearEnvironmentVariable(IntegrationTestGate.GlobalEnvironmentVariable);
        ClearEnvironmentVariable(MySqlVariable);
        ClearEnvironmentVariable(PostgreSqlVariable);
        ClearEnvironmentVariable(SqlServerVariable);
        ClearEnvironmentVariable(OracleVariable);
        ClearEnvironmentVariable(ResetVariable);
        ClearEnvironmentVariable(DefaultConnectionVariable);
        ClearEnvironmentVariable(MySqlConnectionVariable);
        ClearEnvironmentVariable(PostgreSqlConnectionVariable);
    }

    /// <summary>
    /// 测试 - 全局集成测试开关应启用所有Provider。
    /// </summary>
    [Fact]
    public void GetSkipReason_ShouldEnableAllProvidersWhenGlobalSwitchIsEnabled()
    {
        SetEnvironmentVariable(IntegrationTestGate.GlobalEnvironmentVariable, "true");

        Assert.Null(IntegrationTestGate.GetSkipReason("MySql"));
        Assert.Null(IntegrationTestGate.GetSkipReason("PostgreSql"));
    }

    /// <summary>
    /// 测试 - MySQL开关应仅启用MySQL集成测试。
    /// </summary>
    [Fact]
    public void GetSkipReason_ShouldEnableOnlyMySqlWhenMySqlSwitchIsEnabled()
    {
        SetEnvironmentVariable(MySqlVariable, "true");

        Assert.Null(IntegrationTestGate.GetSkipReason("MySql"));
    }

    /// <summary>
    /// 测试 - MySQL开关不应启用PostgreSQL集成测试。
    /// </summary>
    [Fact]
    public void GetSkipReason_ShouldNotEnablePostgreSqlWhenOnlyMySqlSwitchIsEnabled()
    {
        SetEnvironmentVariable(MySqlVariable, "true");

        Assert.NotNull(IntegrationTestGate.GetSkipReason("PostgreSql"));
    }

    /// <summary>
    /// 测试 - Oracle 开关应仅启用 Oracle 集成测试，不启用其他 Provider。
    /// </summary>
    [Fact]
    public void GetSkipReason_ShouldEnableOnlyOracleWhenOracleSwitchIsEnabled()
    {
        SetEnvironmentVariable(OracleVariable, "true");

        Assert.Null(IntegrationTestGate.GetSkipReason("Oracle"));
        Assert.NotNull(IntegrationTestGate.GetSkipReason("SqlServer"));
    }

    /// <summary>
    /// 测试 - Oracle 开关缺失时跳过原因应包含全局和 Oracle 专属配置名称。
    /// </summary>
    [Fact]
    public void GetSkipReason_ShouldDescribeOracleSwitchWhenOracleIsDisabled()
    {
        var result = IntegrationTestGate.GetSkipReason("Oracle");

        Assert.Contains(IntegrationTestGate.GlobalEnvironmentVariable, result);
        Assert.Contains(OracleVariable, result);
    }

    /// <summary>
    /// 测试 - 环境变量值比较应忽略大小写。
    /// </summary>
    [Fact]
    public void GetSkipReason_ShouldIgnoreEnvironmentVariableValueCasing()
    {
        SetEnvironmentVariable(MySqlVariable, "TrUe");

        Assert.Null(IntegrationTestGate.GetSkipReason("MySql"));
    }

    /// <summary>
    /// 测试 - 未配置开关时应返回清晰的跳过原因。
    /// </summary>
    [Fact]
    public void GetSkipReason_ShouldReturnClearReasonWhenSwitchIsMissing()
    {
        var result = IntegrationTestGate.GetSkipReason("MySql");

        Assert.Contains(IntegrationTestGate.GlobalEnvironmentVariable, result);
        Assert.Contains(MySqlVariable, result);
    }

    /// <summary>
    /// 测试 - Provider名称应规范化为环境变量名称。
    /// </summary>
    [Fact]
    public void GetProviderEnvironmentVariable_ShouldNormalizeProviderName()
    {
        var result = IntegrationTestGate.GetProviderEnvironmentVariable("Postgre-Sql.Provider");

        Assert.Equal("RUN_POSTGRESQLPROVIDER_INTEGRATION_TESTS", result);
    }

    /// <summary>
    /// 测试 - 数据库安全校验应拒绝危险数据库名称。
    /// </summary>
    [Theory]
    [InlineData("bing_prod_test")]
    [InlineData("production-bing_test")]
    [InlineData("bing_development")]
    public void IsSafeTestDatabaseName_ShouldRejectUnsafeDatabaseName(string databaseName)
    {
        Assert.False(IntegrationDatabaseSafetyValidator.IsSafeTestDatabaseName(databaseName));
    }

    /// <summary>
    /// 测试 - bing_mysql_test应被识别为合法测试数据库。
    /// </summary>
    [Fact]
    public void IsSafeTestDatabaseName_ShouldAllowMySqlTestDatabaseName()
    {
        Assert.True(IntegrationDatabaseSafetyValidator.IsSafeTestDatabaseName("bing_mysql_test"));
    }

    /// <summary>
    /// 测试 - bing_product_test不应因包含prod被误判。
    /// </summary>
    [Fact]
    public void IsSafeTestDatabaseName_ShouldAllowProductTestDatabaseName()
    {
        Assert.True(IntegrationDatabaseSafetyValidator.IsSafeTestDatabaseName("bing_product_test"));
    }

    /// <summary>
    /// 测试 - MySQL系统数据库应被拒绝。
    /// </summary>
    [Theory]
    [InlineData("mysql")]
    [InlineData("information_schema")]
    [InlineData("performance_schema")]
    [InlineData("sys")]
    public void IsSafeTestDatabaseName_ShouldRejectMySqlSystemDatabaseName(string databaseName)
    {
        Assert.False(IntegrationDatabaseSafetyValidator.IsSafeTestDatabaseName(databaseName));
    }

    /// <summary>
    /// 测试 - SQLServer系统数据库应被拒绝。
    /// </summary>
    [Theory]
    [InlineData("master")]
    [InlineData("model")]
    [InlineData("msdb")]
    [InlineData("tempdb")]
    public void IsSafeTestDatabaseName_ShouldRejectSqlServerSystemDatabaseName(string databaseName)
    {
        Assert.False(IntegrationDatabaseSafetyValidator.IsSafeTestDatabaseName(databaseName));
    }

    /// <summary>
    /// 测试 - PostgreSQL系统数据库应被拒绝。
    /// </summary>
    [Theory]
    [InlineData("postgres")]
    [InlineData("template0")]
    [InlineData("template1")]
    public void IsSafeTestDatabaseName_ShouldRejectPostgreSqlSystemDatabaseName(string databaseName)
    {
        Assert.False(IntegrationDatabaseSafetyValidator.IsSafeTestDatabaseName(databaseName));
    }

    /// <summary>
    /// 测试 - 明确生产数据库名称应被拒绝。
    /// </summary>
    [Theory]
    [InlineData("bing-prod_test")]
    [InlineData("bing_production_test")]
    [InlineData("prod-bing_test")]
    [InlineData("production_bing_test")]
    public void IsSafeTestDatabaseName_ShouldRejectExplicitProductionDatabaseName(string databaseName)
    {
        Assert.False(IntegrationDatabaseSafetyValidator.IsSafeTestDatabaseName(databaseName));
    }

    /// <summary>
    /// 测试 - 没有测试后缀的数据库应被拒绝。
    /// </summary>
    [Theory]
    [InlineData("bing_framework")]
    [InlineData("bing_product")]
    public void IsSafeTestDatabaseName_ShouldRejectDatabaseNameWithoutTestSuffix(string databaseName)
    {
        Assert.False(IntegrationDatabaseSafetyValidator.IsSafeTestDatabaseName(databaseName));
    }

    /// <summary>
    /// 测试 - 数据库安全校验应允许约定后缀。
    /// </summary>
    [Theory]
    [InlineData("bing_dapper_test")]
    [InlineData("bing_dapper_tests")]
    [InlineData("bing_dapper_integration")]
    [InlineData("bing_dapper_integration_test")]
    public void IsSafeTestDatabaseName_ShouldAllowDedicatedTestDatabaseName(string databaseName)
    {
        Assert.True(IntegrationDatabaseSafetyValidator.IsSafeTestDatabaseName(databaseName));
    }

    /// <summary>
    /// 测试 - 整库重置应要求Provider开关和双显式开关。
    /// </summary>
    [Fact]
    public void EnsureResetAllowed_ShouldRequireProviderAndResetSwitch()
    {
        const string connectionString = "Server=127.0.0.1;Database=bing_dapper_test;User Id=test";
        SetEnvironmentVariable(MySqlVariable, "true");

        Assert.Throws<InvalidOperationException>(() =>
            IntegrationDatabaseSafetyValidator.EnsureResetAllowed(connectionString, "MySql"));

        SetEnvironmentVariable(ResetVariable, "true");
        IntegrationDatabaseSafetyValidator.EnsureResetAllowed(connectionString, "MySql");
    }

    /// <summary>
    /// 测试 - Provider专属连接字符串应优先于默认连接字符串。
    /// </summary>
    [Fact]
    public void Resolve_ShouldPreferProviderSpecificConnectionString()
    {
        const string providerConnectionString = "Server=127.0.0.1;Database=bing_mysql_test;User Id=test";
        SetEnvironmentVariable(MySqlConnectionVariable, providerConnectionString);
        SetEnvironmentVariable(DefaultConnectionVariable, "Server=127.0.0.1;Database=default_test;User Id=test");

        var result = IntegrationTestConnectionStringResolver.Resolve("MySql");

        Assert.Equal(providerConnectionString, result);
    }

    /// <summary>
    /// 测试 - Provider专属连接字符串缺失时应回退默认连接字符串。
    /// </summary>
    [Fact]
    public void Resolve_ShouldFallbackToDefaultConnectionStringWhenProviderConnectionIsMissing()
    {
        const string defaultConnectionString = "Host=127.0.0.1;Database=bing_postgresql_test;Username=test";
        SetEnvironmentVariable(DefaultConnectionVariable, defaultConnectionString);

        var result = IntegrationTestConnectionStringResolver.Resolve("PostgreSql");

        Assert.Equal(defaultConnectionString, result);
    }

    /// <summary>
    /// 测试 - 连接字符串缺失错误应说明Provider配置且不包含密码。
    /// </summary>
    [Fact]
    public void Resolve_ShouldProvideSafeConfigurationGuidanceWhenConnectionStringIsMissing()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            IntegrationTestConnectionStringResolver.Resolve("PostgreSql"));

        Assert.Contains("PostgreSql", exception.Message);
        Assert.Contains(PostgreSqlConnectionVariable, exception.Message);
        Assert.Contains("integration.postgresql.runsettings.example", exception.Message);
        Assert.DoesNotContain("Password", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 测试 - 安全校验错误不应包含数据库密码。
    /// </summary>
    [Fact]
    public void EnsureDatabaseOperationAllowed_ShouldNotExposePasswordInException()
    {
        SetEnvironmentVariable(MySqlVariable, "true");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            IntegrationDatabaseSafetyValidator.EnsureDatabaseOperationAllowed(
                "Server=127.0.0.1;Database=master;User Id=test;Password=secret-value", "MySql"));

        Assert.DoesNotContain("secret-value", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 释放环境变量。
    /// </summary>
    public void Dispose()
    {
        foreach (var item in _originalValues)
            Environment.SetEnvironmentVariable(item.Key, item.Value);
    }

    /// <summary>
    /// 设置测试环境变量并保存原始值。
    /// </summary>
    /// <param name="name">环境变量名称。</param>
    /// <param name="value">环境变量值。</param>
    private void SetEnvironmentVariable(string name, string value)
    {
        if (_originalValues.ContainsKey(name) == false)
            _originalValues[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    /// <summary>
    /// 清除测试环境变量并保存原始值。
    /// </summary>
    /// <param name="name">环境变量名称。</param>
    private void ClearEnvironmentVariable(string name)
    {
        if (_originalValues.ContainsKey(name) == false)
            _originalValues[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, null);
    }
}
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
    private const string DorisVariable = "RUN_DORIS_INTEGRATION_TESTS";
    private const string PostgreSqlVariable = "RUN_POSTGRESQL_INTEGRATION_TESTS";
    private const string SqlServerVariable = "RUN_SQLSERVER_INTEGRATION_TESTS";
    private const string OracleVariable = "RUN_ORACLE_INTEGRATION_TESTS";
    private const string ResetVariable = "ALLOW_DATABASE_RESET_FOR_TESTS";
    private const string DefaultConnectionVariable = "ConnectionStrings__DefaultConnection";
    private const string MySqlConnectionVariable = "ConnectionStrings__MySqlConnection";
    private const string DorisConnectionVariable = "ConnectionStrings__DorisConnection";
    private const string PostgreSqlConnectionVariable = "ConnectionStrings__PostgreSqlConnection";
    private const string SqlServerConnectionVariable = "ConnectionStrings__SqlServerConnection";
    private const string LegacyPostgreSqlVariable = "RUN_PGSQL_INTEGRATION_TESTS";
    private readonly Dictionary<string, string> _originalValues = new();

    /// <summary>
    /// 初始化集成测试环境变量隔离。
    /// </summary>
    public IntegrationTestGateTest()
    {
        ClearEnvironmentVariable(IntegrationTestGate.GlobalEnvironmentVariable);
        ClearEnvironmentVariable(MySqlVariable);
        ClearEnvironmentVariable(DorisVariable);
        ClearEnvironmentVariable(PostgreSqlVariable);
        ClearEnvironmentVariable(SqlServerVariable);
        ClearEnvironmentVariable(OracleVariable);
        ClearEnvironmentVariable(ResetVariable);
        ClearEnvironmentVariable(DefaultConnectionVariable);
        ClearEnvironmentVariable(MySqlConnectionVariable);
        ClearEnvironmentVariable(DorisConnectionVariable);
        ClearEnvironmentVariable(PostgreSqlConnectionVariable);
        ClearEnvironmentVariable(SqlServerConnectionVariable);
        ClearEnvironmentVariable(LegacyPostgreSqlVariable);
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
    /// 测试目的：Doris 专属开关只能启用受控 Doris 只读集成测试。
    /// </summary>
    [Fact]
    public void GetSkipReason_ShouldEnableOnlyDorisWhenDorisSwitchIsEnabled()
    {
        // Arrange
        SetEnvironmentVariable(DorisVariable, "true");

        // Act and Assert
        Assert.Null(IntegrationTestGate.GetSkipReason("Doris"));
        Assert.NotNull(IntegrationTestGate.GetSkipReason("MySql"));
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
    /// 测试目的：全局开关显式为 false 时，Provider 专属 true 仍必须启用对应测试。
    /// </summary>
    [Fact]
    public void GetSkipReason_WhenGlobalIsFalseAndProviderIsTrue_ShouldEnableProvider()
    {
        // Arrange
        SetEnvironmentVariable(IntegrationTestGate.GlobalEnvironmentVariable, "false");
        SetEnvironmentVariable(PostgreSqlVariable, "true");

        // Act and Assert
        Assert.Null(IntegrationTestGate.GetSkipReason("PostgreSql"));
    }

    /// <summary>
    /// 测试目的：除 true 外的开关值不得意外启用集成测试。
    /// </summary>
    /// <param name="value">待验证的环境变量值。</param>
    [Theory]
    [InlineData("false")]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("on")]
    [InlineData("")]
    [InlineData("invalid")]
    public void GetSkipReason_WhenProviderValueIsNotTrue_ShouldKeepProviderDisabled(string value)
    {
        // Arrange
        SetEnvironmentVariable(SqlServerVariable, value);

        // Act
        var skipReason = IntegrationTestGate.GetSkipReason("SqlServer");

        // Assert
        Assert.NotNull(skipReason);
        Assert.Contains(SqlServerVariable, skipReason);
    }

    /// <summary>
    /// 测试目的：PostgreSQL 只接受规范 RUN_POSTGRESQL_INTEGRATION_TESTS 变量，不接受未文档化的别名。
    /// </summary>
    [Fact]
    public void GetSkipReason_WhenOnlyLegacyPostgreSqlVariableIsTrue_ShouldKeepProviderDisabled()
    {
        // Arrange
        SetEnvironmentVariable(LegacyPostgreSqlVariable, "true");

        // Act
        var skipReason = IntegrationTestGate.GetSkipReason("PostgreSql");

        // Assert
        Assert.NotNull(skipReason);
        Assert.Contains(PostgreSqlVariable, skipReason);
        Assert.DoesNotContain(LegacyPostgreSqlVariable, skipReason);
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
    /// 测试目的：Doris 集成测试必须优先解析 Doris 专属连接字符串，不能误用 MySQL Provider 配置。
    /// </summary>
    [Fact]
    public void Resolve_WhenDorisConnectionIsConfigured_ShouldUseDorisSpecificConnectionString()
    {
        // Arrange
        const string dorisConnectionString = "Server=127.0.0.1;Port=9030;Database=bing_doris_test;User Id=test";
        SetEnvironmentVariable(DorisConnectionVariable, dorisConnectionString);
        SetEnvironmentVariable(MySqlConnectionVariable,
            "Server=127.0.0.1;Port=3306;Database=bing_mysql_test;User Id=test");

        // Act
        var result = IntegrationTestConnectionStringResolver.Resolve("Doris");

        // Assert
        Assert.Equal(dorisConnectionString, result);
    }

    /// <summary>
    /// 测试目的：Doris 连接字符串缺失时应给出专属配置指引，且不得泄露敏感信息。
    /// </summary>
    [Fact]
    public void Resolve_WhenDorisConnectionIsMissing_ShouldProvideSafeConfigurationGuidance()
    {
        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            IntegrationTestConnectionStringResolver.Resolve("Doris"));

        // Assert
        Assert.Contains("Doris", exception.Message);
        Assert.Contains(DorisConnectionVariable, exception.Message);
        Assert.Contains("docs/testing/database-integration-tests.md", exception.Message);
        Assert.DoesNotContain("Password", exception.Message, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("docs/testing/database-integration-tests.md", exception.Message);
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
    /// 测试目的：SQL Server 专属连接字符串必须优先于默认连接字符串，并被环境隔离清理。
    /// </summary>
    [Fact]
    public void Resolve_WhenSqlServerConnectionIsConfigured_ShouldUseProviderSpecificConnectionString()
    {
        // Arrange
        const string sqlServerConnectionString = "Server=127.0.0.1;Database=bing_sqlserver_test;User Id=test";
        SetEnvironmentVariable(SqlServerConnectionVariable, sqlServerConnectionString);
        SetEnvironmentVariable(DefaultConnectionVariable, "Server=127.0.0.1;Database=default_test;User Id=test");

        // Act
        var result = IntegrationTestConnectionStringResolver.Resolve("SqlServer");

        // Assert
        Assert.Equal(sqlServerConnectionString, result);
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
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Reflection;
using Bing.Data;
using Bing.Data.Sql.Builders;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// 官方 SQL Provider 共享实例测试。
/// </summary>
public class OfficialProviderInstanceTest
{
    /// <summary>
    /// 测试目的：五个官方 Provider 重复读取 Dialect 和参数字面量解析器时应返回同一实例。
    /// </summary>
    [Fact]
    public void Provider_WhenSharedMembersAreReadRepeatedly_ShouldReturnSameInstances()
    {
        // Arrange
        var providers = CreateProviders();

        // Act / Assert
        foreach (var provider in providers)
        {
            Assert.Same(provider.Dialect, provider.Dialect);
            Assert.Same(provider.ParamLiteralsResolver, provider.ParamLiteralsResolver);
        }
    }

    /// <summary>
    /// 测试目的：Provider 的共享 Dialect 和参数字面量解析器应支持多线程只读访问。
    /// </summary>
    [Fact]
    public void Provider_WhenSharedMembersAreReadConcurrently_ShouldKeepSameInstances()
    {
        // Arrange
        var providers = CreateProviders();
        var observedDialects = new ConcurrentBag<object>();
        var observedResolvers = new ConcurrentBag<object>();

        // Act
        Parallel.For(0, 1000, index =>
        {
            var provider = providers[index % providers.Length];
            observedDialects.Add(provider.Dialect);
            observedResolvers.Add(provider.ParamLiteralsResolver);
        });

        // Assert
        foreach (var provider in providers)
            Assert.Equal(200, observedDialects.Count(instance => ReferenceEquals(instance, provider.Dialect)));
        Assert.Equal(800, observedResolvers.Count(instance =>
            ReferenceEquals(instance, MySqlSqlProvider.Instance.ParamLiteralsResolver)));
        Assert.Equal(200, observedResolvers.Count(instance =>
            ReferenceEquals(instance, PostgreSqlSqlProvider.Instance.ParamLiteralsResolver)));
    }

    /// <summary>
    /// 测试目的：官方 Provider 应仅通过统一 Profile 声明参数上限，SQL Server 必须遵循 2100 参数限制。
    /// </summary>
    [Fact]
    public void Provider_WhenParameterLimitIsRequested_ShouldReturnOfficialContract()
    {
        // Arrange
        var providers = CreateProviders();

        // Act / Assert
        Assert.Equal(2100, SqlServerSqlProvider.Instance.Profile.Limits.MaxParameterCount);
        foreach (var provider in providers.Where(provider => provider != SqlServerSqlProvider.Instance))
            Assert.Null(((ISqlProviderProfileProvider)provider).Profile.Limits.MaxParameterCount);
    }

    /// <summary>
    /// 测试目的：官方 Provider 应明确声明 Right Join 的方言支持状态，避免运行时数据库错误泄漏到调用方。
    /// </summary>
    [Fact]
    public void Provider_WhenRightJoinCapabilityIsRequested_ShouldReturnOfficialContract()
    {
        // Arrange
        var providers = CreateProviders();

        // Act and Assert
        foreach (var provider in providers.Where(provider => provider != SqliteSqlProvider.Instance))
            Assert.Equal(SqlQueryCapabilityState.Supported, ((ISqlProviderProfileProvider)provider).Profile.Query.RightJoin);
        Assert.Equal(SqlQueryCapabilityState.Unsupported, SqliteSqlProvider.Instance.Profile.Query.RightJoin);
    }

    /// <summary>
    /// 测试目的：官方 Provider 应明确声明 Full Join 的方言支持状态，MySQL 与 SQLite 必须在生成 SQL 前拒绝。
    /// </summary>
    [Fact]
    public void Provider_WhenFullJoinCapabilityIsRequested_ShouldReturnOfficialContract()
    {
        // Arrange
        var providers = CreateProviders();

        // Act and Assert
        foreach (var provider in providers.Where(provider =>
                     provider != MySqlSqlProvider.Instance && provider != SqliteSqlProvider.Instance))
            Assert.Equal(SqlQueryCapabilityState.Supported, ((ISqlProviderProfileProvider)provider).Profile.Query.FullJoin);
        Assert.Equal(SqlQueryCapabilityState.Unsupported, MySqlSqlProvider.Instance.Profile.Query.FullJoin);
        Assert.Equal(SqlQueryCapabilityState.Unsupported, SqliteSqlProvider.Instance.Profile.Query.FullJoin);
    }

    /// <summary>
    /// 测试目的：官方 Provider 的事务能力声明必须匹配驱动实际覆盖链路，SQLite 必须保留同步回退。
    /// </summary>
    [Fact]
    public void Provider_WhenNativeAsyncTransactionCapabilitiesAreRequested_ShouldMatchDriverEvidence()
    {
        // Arrange
        var contracts = new[]
        {
            (Provider: (ISqlProvider)MySqlSqlProvider.Instance,
                ConnectionType: typeof(MySqlConnector.MySqlConnection),
                TransactionType: typeof(MySqlConnector.MySqlTransaction),
                Begin: true, Commit: true, Rollback: true),
            (Provider: (ISqlProvider)PostgreSqlSqlProvider.Instance,
                ConnectionType: typeof(Npgsql.NpgsqlConnection),
                TransactionType: typeof(Npgsql.NpgsqlTransaction),
                Begin: true, Commit: true, Rollback: true),
            (Provider: (ISqlProvider)SqlServerSqlProvider.Instance,
                ConnectionType: typeof(Microsoft.Data.SqlClient.SqlConnection),
                TransactionType: typeof(Microsoft.Data.SqlClient.SqlTransaction),
                Begin: false, Commit: false, Rollback: false),
            (Provider: (ISqlProvider)OracleSqlProvider.Instance,
                ConnectionType: typeof(global::Oracle.ManagedDataAccess.Client.OracleConnection),
                TransactionType: typeof(global::Oracle.ManagedDataAccess.Client.OracleTransaction),
                Begin: false, Commit: false, Rollback: false),
            (Provider: (ISqlProvider)SqliteSqlProvider.Instance,
                ConnectionType: typeof(Microsoft.Data.Sqlite.SqliteConnection),
                TransactionType: typeof(Microsoft.Data.Sqlite.SqliteTransaction),
                Begin: false, Commit: false, Rollback: false)
        };

        // Act / Assert
        foreach (var contract in contracts)
        {
            var profile = ((ISqlProviderProfileProvider)contract.Provider).Profile.Transaction;
            Assert.Equal(contract.Begin, profile.SupportsNativeAsyncBegin);
            Assert.Equal(contract.Commit, profile.SupportsNativeAsyncCommit);
            Assert.Equal(contract.Rollback, profile.SupportsNativeAsyncRollback);
            Assert.True(typeof(DbConnection).IsAssignableFrom(contract.ConnectionType));
            Assert.True(typeof(DbTransaction).IsAssignableFrom(contract.TransactionType));
            Assert.Equal(contract.Begin, HasDeclaredPublicMethod(contract.ConnectionType,
                "BeginTransactionAsync", typeof(IsolationLevel), typeof(CancellationToken)));
            Assert.Equal(contract.Commit, HasDeclaredPublicMethod(contract.TransactionType,
                "CommitAsync", typeof(CancellationToken)));
            Assert.Equal(contract.Rollback, HasDeclaredPublicMethod(contract.TransactionType,
                "RollbackAsync", typeof(CancellationToken)));
        }
    }

    /// <summary>
    /// 判断驱动类型是否直接声明了指定公开实例方法，排除 ADO.NET 基类默认实现。
    /// </summary>
    private static bool HasDeclaredPublicMethod(Type type, string name, params Type[] parameterTypes) =>
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Any(method => method.Name == name && method.GetParameters().Select(parameter => parameter.ParameterType)
                .SequenceEqual(parameterTypes));

    /// <summary>
    /// 创建官方 Provider 集合。
    /// </summary>
    private static ISqlProvider[] CreateProviders() =>
    [
        MySqlSqlProvider.Instance,
        PostgreSqlSqlProvider.Instance,
        SqlServerSqlProvider.Instance,
        OracleSqlProvider.Instance,
        SqliteSqlProvider.Instance
    ];
}
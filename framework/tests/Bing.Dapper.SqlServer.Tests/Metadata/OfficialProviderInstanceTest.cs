using System.Collections.Concurrent;
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
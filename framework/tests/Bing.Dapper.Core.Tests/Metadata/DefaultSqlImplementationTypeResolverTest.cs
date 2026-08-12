using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Dapper;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Dapper.Core.Tests.Metadata;

/// <summary>
/// <see cref="SqlProviderRuntime"/> 单元测试。
/// </summary>
public class SqlProviderRuntimeRegistrationTest
{
    /// <summary>
    /// 测试 - 不同 Provider 注册应保存各自服务实现，避免相同数据库类型的 Provider 相互覆盖。
    /// </summary>
    [Fact]
    public void Constructor_WhenProviderRuntimesDiffer_ShouldKeepProviderSpecificImplementations()
    {
        // Arrange
        var first = new SqlProviderRuntime("custom.sqlserver.first", typeof(QueryService), typeof(ExecutorService));
        var second = new SqlProviderRuntime("custom.sqlserver.second", typeof(AlternateQueryService),
            typeof(AlternateExecutorService), typeof(MultipleQueryExecutorService));

        // Act
        var firstResult = first.QueryType;
        var secondResult = second.QueryType;

        // Assert
        Assert.Equal(typeof(QueryService), firstResult);
        Assert.Equal(typeof(AlternateQueryService), secondResult);
        Assert.Equal(typeof(ExecutorService), first.ExecutorType);
        Assert.Equal(typeof(AlternateExecutorService), second.ExecutorType);
        Assert.Equal(typeof(MultipleQueryExecutorService), second.MultipleQueryExecutorType);
    }

    /// <summary>
    /// 测试 - 未提供多结果集实现时，该可选能力应保持未注册而非回退到任意执行器。
    /// </summary>
    [Fact]
    public void Constructor_WhenMultipleQueryExecutorIsOmitted_ShouldKeepOptionalCapabilityNull()
    {
        // Arrange
        var runtime = new SqlProviderRuntime("custom.mysql", typeof(QueryService), typeof(ExecutorService));

        // Act
        var result = runtime.MultipleQueryExecutorType;

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// 测试 - 注册时必须验证固定服务实现关系，避免 Factory 在运行时创建不兼容的类型。
    /// </summary>
    [Fact]
    public void Constructor_WhenImplementationDoesNotImplementContract_ShouldThrowArgumentException()
    {
        // Arrange
        // Act and Assert
        Assert.Throws<ArgumentException>(() => new SqlProviderRuntime("custom.provider", typeof(ConcreteService),
            typeof(ExecutorService)));
        Assert.Throws<ArgumentException>(() => new SqlProviderRuntime("custom.provider", typeof(QueryService),
            typeof(ConcreteService)));
    }

    /// <summary>
    /// 测试 - 同一 Provider Key 只能登记一套固定运行时服务；相同描述可重复注册。
    /// </summary>
    [Fact]
    public void AddSqlProviderRuntime_WhenProviderKeyConflicts_ShouldBeIdempotentOrRejectConflict()
    {
        // Arrange
        var services = new ServiceCollection();
        var runtime = new SqlProviderRuntime(" custom.sqlserver ", typeof(QueryService), typeof(ExecutorService));

        // Act
        services.AddSqlProviderRuntime(runtime);
        services.AddSqlProviderRuntime(runtime);

        // Assert
        Assert.Throws<InvalidOperationException>(() =>
            services.AddSqlProviderRuntime(new SqlProviderRuntime("CUSTOM.SQLSERVER", typeof(AlternateQueryService),
                typeof(AlternateExecutorService))));
    }

    private class QueryService : SqlQueryBase
    {
        public QueryService(IServiceProvider serviceProvider, SqlOptions options) : base(serviceProvider, options) { }
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }

    private sealed class AlternateQueryService : QueryService
    {
        public AlternateQueryService(IServiceProvider serviceProvider, SqlOptions options) : base(serviceProvider, options) { }
    }

    private class ExecutorService : SqlExecutorBase
    {
        public ExecutorService(IServiceProvider serviceProvider, SqlOptions options) : base(serviceProvider, options) { }
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }

    private sealed class AlternateExecutorService : ExecutorService
    {
        public AlternateExecutorService(IServiceProvider serviceProvider, SqlOptions options) : base(serviceProvider, options) { }
    }

    private sealed class MultipleQueryExecutorService : SqlMultipleQueryExecutorBase
    {
        public MultipleQueryExecutorService(IServiceProvider serviceProvider, SqlOptions options) : base(serviceProvider, options) { }
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }

    /// <summary>
    /// 未映射的具体测试服务。
    /// </summary>
    private sealed class ConcreteService
    {
    }

}
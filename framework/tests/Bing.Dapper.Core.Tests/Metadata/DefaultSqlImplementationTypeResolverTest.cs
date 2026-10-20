using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Dapper;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Dapper.Core.Tests.Metadata;

/// <summary>
/// <see cref="SqlProviderRuntime"/> 注册行为测试。
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

    /// <summary>
    /// 用于 Provider 注册测试的查询服务实现。
    /// </summary>
    private class QueryService : SqlQueryBase
    {
        /// <summary>
        /// 初始化一个 <see cref="QueryService"/> 类型的实例。
        /// </summary>
        /// <param name="serviceProvider">提供查询依赖项的服务提供程序。</param>
        /// <param name="options">查询配置。</param>
        public QueryService(IServiceProvider serviceProvider, SqlOptions options) : base(serviceProvider, options) { }

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }

    /// <summary>
    /// 使用替代类型标识的查询服务测试实现。
    /// </summary>
    private sealed class AlternateQueryService : QueryService
    {
        /// <summary>
        /// 初始化一个 <see cref="AlternateQueryService"/> 类型的实例。
        /// </summary>
        /// <param name="serviceProvider">提供查询依赖项的服务提供程序。</param>
        /// <param name="options">查询配置。</param>
        public AlternateQueryService(IServiceProvider serviceProvider, SqlOptions options) : base(serviceProvider, options) { }
    }

    /// <summary>
    /// 用于 Provider 注册测试的执行器实现。
    /// </summary>
    private class ExecutorService : SqlExecutorBase
    {
        /// <summary>
        /// 初始化一个 <see cref="ExecutorService"/> 类型的实例。
        /// </summary>
        /// <param name="serviceProvider">提供执行依赖项的服务提供程序。</param>
        /// <param name="options">执行器配置。</param>
        public ExecutorService(IServiceProvider serviceProvider, SqlOptions options) : base(serviceProvider, options) { }

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }

    /// <summary>
    /// 使用替代类型标识的执行器测试实现。
    /// </summary>
    private sealed class AlternateExecutorService : ExecutorService
    {
        /// <summary>
        /// 初始化一个 <see cref="AlternateExecutorService"/> 类型的实例。
        /// </summary>
        /// <param name="serviceProvider">提供执行依赖项的服务提供程序。</param>
        /// <param name="options">执行器配置。</param>
        public AlternateExecutorService(IServiceProvider serviceProvider, SqlOptions options) : base(serviceProvider, options) { }
    }

    /// <summary>
    /// 用于 Provider 注册测试的多结果集执行器实现。
    /// </summary>
    private sealed class MultipleQueryExecutorService : SqlMultipleQueryExecutorBase
    {
        /// <summary>
        /// 初始化一个 <see cref="MultipleQueryExecutorService"/> 类型的实例。
        /// </summary>
        /// <param name="serviceProvider">提供执行依赖项的服务提供程序。</param>
        /// <param name="options">执行器配置。</param>
        public MultipleQueryExecutorService(IServiceProvider serviceProvider, SqlOptions options) : base(serviceProvider, options) { }

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }

    /// <summary>
    /// 未映射的具体测试服务。
    /// </summary>
    private sealed class ConcreteService
    {
    }

}

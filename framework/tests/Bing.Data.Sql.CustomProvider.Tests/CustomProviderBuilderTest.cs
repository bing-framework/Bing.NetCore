using System.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Bing.Data.Enums;
using Bing.Data.Sql.CustomProvider.Tests.Samples;
using Bing.Data.Sql.Configs;
using Bing.Dapper;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Data.Sql.CustomProvider.Tests;

/// <summary>
/// 外部 SQL Provider Builder 验收测试。
/// </summary>
public class CustomProviderBuilderTest
{
    /// <summary>
    /// 测试 - 外部程序集应只通过公开 Provider Runtime SPI 路由 Builder、Query、Executor 和连接工厂，不依赖友元访问。
    /// </summary>
    [Fact]
    public void ProviderRuntime_WhenRegisteredFromExternalAssembly_ShouldRoutePublicSqlServicesWithoutFriendAccess()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddSqlBuilderProvider(CustomSqlProvider.Instance, builderServices => new CustomSqlBuilder(builderServices));
        services.AddSqlDataSource("external", CustomSqlProvider.Instance.DatabaseType, "external-connection",
            providerKey: CustomSqlProvider.Instance.Key);
        services.AddSqlDbConnectionFactory(CustomSqlProvider.Instance.Key,
            connectionString => new ExternalConnection(connectionString));
        services.AddSqlProviderRuntime(new SqlProviderRuntime(CustomSqlProvider.Instance.Key,
            typeof(ExternalQuery), typeof(ExternalExecutor), typeof(ExternalMultipleQueryExecutor)));
        using var provider = services.BuildServiceProvider();

        // Act
        var builder = provider.GetRequiredService<ISqlBuilderFactory>().Create(CustomSqlProvider.Instance.Key);
        using var query = provider.GetRequiredService<ISqlQueryFactory>().Create("external");
        using var executor = provider.GetRequiredService<ISqlExecutorFactory>().Create("external");
        using var multipleExecutor = provider.GetRequiredService<ISqlMultipleQueryExecutorFactory>().Create("external");
        using var connection = provider.GetRequiredService<ISqlDbConnectionFactoryResolver>()
            .Create(CustomSqlProvider.Instance.Key, "external-connection");

        // Assert
        Assert.IsType<CustomSqlBuilder>(builder);
        Assert.IsType<ExternalQuery>(query);
        Assert.IsType<ExternalExecutor>(executor);
        Assert.IsType<ExternalMultipleQueryExecutor>(multipleExecutor);
        Assert.IsType<ExternalConnection>(connection);
        Assert.Equal("external-connection", connection.ConnectionString);
    }

    /// <summary>
    /// 测试目的：外部 Provider 应可仅通过公开 SPI 创建全部 SQL 子句。
    /// </summary>
    [Fact]
    public void Constructor_WhenUsingPublicProviderSpi_ShouldCreateAllClauses()
    {
        // Arrange
        var builder = new CustomSqlBuilder();
        var accessor = (ISqlQueryClauseAccessor)builder;

        // Act
        var provider = builder.Provider;

        // Assert
        Assert.Same(CustomSqlProvider.Instance, provider);
        Assert.IsType<CustomClauseFactory>(provider.ClauseFactory);
        Assert.IsType<CustomSelectClause>(accessor.SelectClause);
        Assert.IsType<FromClause>(accessor.FromClause);
        Assert.IsType<JoinClause>(accessor.JoinClause);
        Assert.IsType<WhereClause>(accessor.WhereClause);
        Assert.IsType<GroupByClause>(accessor.GroupByClause);
        Assert.IsType<OrderByClause>(accessor.OrderByClause);
    }

    /// <summary>
    /// 测试 - 统一 Builder 必须通过外部 Provider 的可选 Mutation Clause Factory 创建 Update From、Delete Using 与 Returning 子句。
    /// </summary>
    [Fact]
    public void MutationClauseFactory_WhenUnifiedBuilderUsesOptionalProviderSpi_ShouldCreateCustomClausesAndPreserveCloneState()
    {
        // Arrange
        var update = new CustomSqlBuilder();
        var delete = new CustomSqlBuilder();

        // Act
        update.Update(new SqlTableReference { TableName = "samples", Alias = "t" })
            .UpdateFrom(new SqlTableReference { TableName = "sample_updates", Alias = "s" })
            .Set("Status", 1)
            .SetFrom("Name", "Name")
            .WhereFrom("Id", "Id")
            .Returning("Id");
        delete.DeleteFrom(new SqlTableReference { TableName = "samples", Alias = "t" })
            .DeleteUsing(new SqlTableReference { TableName = "sample_deletes", Alias = "s" })
            .WhereUsing("Id", "Id")
            .Returning("Id");
        var clone = Assert.IsType<CustomSqlBuilder>(update.Clone());
        update.Clear();

        // Assert
        Assert.IsType<CustomUpdateFromClause>(((IUpdateFromClauseAccessor)update).UpdateFromClause);
        Assert.IsType<CustomReturningClause>(((IReturningClauseAccessor)update).ReturningClause);
        Assert.IsType<CustomDeleteUsingClause>(((IDeleteUsingClauseAccessor)delete).DeleteUsingClause);
        Assert.IsType<CustomReturningClause>(((IReturningClauseAccessor)delete).ReturningClause);
        Assert.Equal("Update [samples] As [t] Set [Status] = @_p_0, [Name] = [s].[Name] From [sample_updates] As [s] Where [t].[Id]=[s].[Id] Returning [t].[Id]",
            clone.ToSql());
        Assert.Empty(update.GetParams());
        Assert.Equal(1, clone.GetParam("@_p_0"));
        Assert.Equal("Delete From [samples] As [t] Using [sample_deletes] As [s] Where [t].[Id]=[s].[Id] Returning [t].[Id]",
            delete.ToSql());
    }

    /// <summary>
    /// 测试 - 专用 Update 与 Delete Builder 必须选择外部 Provider 的可选 Mutation 子句，并在 Clone 与 Clear 后保持隔离。
    /// </summary>
    [Fact]
    public void MutationClauseFactory_WhenDedicatedBuildersUseOptionalProviderSpi_ShouldCreateCustomClausesAndIsolateLifecycle()
    {
        // Arrange
        var update = new SqlUpdateBuilder(CustomSqlProvider.Instance, new SqlBuilderServices());
        var delete = new SqlDeleteBuilder(CustomSqlProvider.Instance, new SqlBuilderServices());

        // Act
        update.Update(new SqlTableReference { TableName = "samples", Alias = "t" })
            .UpdateFrom(new SqlTableReference { TableName = "sample_updates", Alias = "s" })
            .Set("Status", 1)
            .SetFrom("Name", "Name")
            .WhereFrom("Id", "Id");
        delete.DeleteFrom(new SqlTableReference { TableName = "samples", Alias = "t" })
            .DeleteUsing(new SqlTableReference { TableName = "sample_deletes", Alias = "s" })
            .WhereUsing("Id", "Id");
        var updateClone = update.Clone();
        var deleteClone = delete.Clone();
        update.Clear();
        delete.Clear();
        update.Update(new SqlTableReference { TableName = "samples" })
            .Set("Status", 2)
            .AllowAllRows();

        // Assert
        Assert.IsType<CustomUpdateFromClause>(update.UpdateFromClause);
        Assert.IsType<CustomDeleteUsingClause>(delete.DeleteUsingClause);
        Assert.IsType<CustomUpdateFromClause>(updateClone.UpdateFromClause);
        Assert.IsType<CustomDeleteUsingClause>(deleteClone.DeleteUsingClause);
        Assert.Equal("Update [samples] As [t] Set [Status] = @_p_0, [Name] = [s].[Name] From [sample_updates] As [s] Where [t].[Id]=[s].[Id]",
            updateClone.ToSql());
        Assert.Equal(1, updateClone.BuildCommand().Parameters.Single().Value);
        Assert.Equal(2, update.BuildCommand().Parameters.Single().Value);
        Assert.Equal("Delete From [samples] As [t] Using [sample_deletes] As [s] Where [t].[Id]=[s].[Id]",
            deleteClone.ToSql());
        Assert.Null(delete.DeleteUsingClause.Table);
    }

    /// <summary>
    /// 外部程序集定义的查询实现。
    /// </summary>
    private sealed class ExternalQuery : SqlQueryBase
    {
        public ExternalQuery(IServiceProvider serviceProvider, SqlOptions<ExternalQuery> options)
            : base(serviceProvider, options)
        {
        }

        protected override ISqlBuilder CreateSqlBuilder() =>
            new CustomSqlBuilder(CreateSqlBuilderServices());
    }

    /// <summary>
    /// 外部程序集定义的执行器实现。
    /// </summary>
    private sealed class ExternalExecutor : SqlExecutorBase
    {
        public ExternalExecutor(IServiceProvider serviceProvider, SqlOptions<ExternalExecutor> options)
            : base(serviceProvider, options)
        {
        }

        protected override ISqlBuilder CreateSqlBuilder() =>
            new CustomSqlBuilder(CreateSqlBuilderServices());
    }

    /// <summary>
    /// 外部程序集定义的多结果集执行器实现。
    /// </summary>
    private sealed class ExternalMultipleQueryExecutor : SqlMultipleQueryExecutorBase
    {
        public ExternalMultipleQueryExecutor(IServiceProvider serviceProvider,
            SqlOptions<ExternalMultipleQueryExecutor> options)
            : base(serviceProvider, options)
        {
        }

        protected override ISqlBuilder CreateSqlBuilder() =>
            new CustomSqlBuilder(CreateSqlBuilderServices());
    }

    /// <summary>
    /// 外部 Provider 返回的最小连接实现，仅用于验证独立连接工厂路由。
    /// </summary>
    private sealed class ExternalConnection : IDbConnection
    {
        public ExternalConnection(string connectionString) => ConnectionString = connectionString;

        public string ConnectionString { get; set; }

        public int ConnectionTimeout => 0;

        public string Database => "external";

        public ConnectionState State => ConnectionState.Closed;

        public IDbTransaction BeginTransaction() => throw new NotSupportedException();

        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();

        public void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public void Close()
        {
        }

        public IDbCommand CreateCommand() => throw new NotSupportedException();

        public void Open() => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// 测试目的：外部 Provider 应能生成包含 From、Join 和 Where 的完整 SQL 与参数。
    /// </summary>
    [Fact]
    public void Build_WhenFromJoinWhereConfigured_ShouldRenderSqlAndParameters()
    {
        // Arrange
        var builder = new CustomSqlBuilder();

        // Act
        var sql = builder.Select("u.Id")
            .From("Users", "u")
            .LeftJoin("Orders", "o")
            .Where("u.Enabled", true)
            .ToSql();

        // Assert
        Assert.Equal("Select [u].[Id] \r\nFrom [Users] As [u] \r\nLeft Join [Orders] As [o] \r\nWhere [u].[Enabled]=@_p_0", sql);
        Assert.Equal(true, builder.GetParam("@_p_0"));
    }

    /// <summary>
    /// 测试目的：外部 Provider 的表引用解析器应参与 From、Join 及 Clone 后新增 Join 的字符串表名解析。
    /// </summary>
    [Fact]
    public void TableReferenceParser_WhenCustomNamesConfigured_ShouldRenderParserResultsAcrossClone()
    {
        // Arrange
        var source = new CustomSqlBuilder();
        source.Select("u.Id").From("custom:users", "u");

        // Act
        var clone = Assert.IsType<CustomSqlBuilder>(source.Clone());
        clone.Join("custom:orders", "o");

        // Assert
        Assert.Equal("Select [u].[Id] \r\nFrom [ParsedUsers] As [u]", source.ToSql());
        Assert.Equal("Select [u].[Id] \r\nFrom [ParsedUsers] As [u] \r\nJoin [ParsedOrders] As [o]",
            clone.ToSql());
    }

    /// <summary>
    /// 测试目的：公开 Builder 工厂应按 Provider 实例创建对应外部 Builder。
    /// </summary>
    [Fact]
    public void Factory_WhenProviderRegistered_ShouldCreateExpectedBuilder()
    {
        // Arrange
        var factory = new SqlBuilderFactory(new[]
        {
            new SqlBuilderFactoryRegistration(CustomSqlProvider.Instance, _ => new CustomSqlBuilder())
        });

        // Act
        var byProvider = factory.Create(CustomSqlProvider.Instance);

        // Assert
        Assert.IsType<CustomSqlBuilder>(byProvider);
    }

    /// <summary>
    /// 测试目的：Provider Key 应为大小写不敏感的正式创建入口，并接受首尾空白。
    /// </summary>
    [Fact]
    public void Factory_WhenProviderKeyUsesDifferentCaseAndWhitespace_ShouldCreateExpectedBuilder()
    {
        // Arrange
        var factory = new SqlBuilderFactory(new[]
        {
            new SqlBuilderFactoryRegistration(CustomSqlProvider.Instance, _ => new CustomSqlBuilder())
        });

        // Act
        var builder = factory.Create("  CUSTOM.TEST  ");

        // Assert
        Assert.IsType<CustomSqlBuilder>(builder);
    }

    /// <summary>
    /// 测试目的：Factory 应将调用方提供的查询级共享服务原样传递给外部 Builder。
    /// </summary>
    [Fact]
    public void Factory_WhenQueryServicesAreProvided_ShouldPassSameInstanceToBuilder()
    {
        // Arrange
        var services = new SqlBuilderServices();
        var factory = new SqlBuilderFactory(new[]
        {
            new SqlBuilderFactoryRegistration(CustomSqlProvider.Instance, builderServices => new CustomSqlBuilder(builderServices))
        });

        // Act
        var builder = Assert.IsType<CustomSqlBuilder>(factory.Create(CustomSqlProvider.Instance, services));

        // Assert
        Assert.Same(services, builder.SharedServices);
    }

    /// <summary>
    /// 测试目的：不同 Key 的外部 Provider 应可复用同一个 DatabaseType，并始终通过 Key 显式创建。
    /// </summary>
    [Fact]
    public void Factory_WhenDifferentProviderKeysShareDatabaseType_ShouldAllowRegistration()
    {
        // Arrange
        var factory = new SqlBuilderFactory(new[]
        {
            new SqlBuilderFactoryRegistration(CustomSqlProvider.Instance, _ => new CustomSqlBuilder()),
            new SqlBuilderFactoryRegistration(CustomSqliteAliasProvider.Instance, _ => new CustomSqlBuilder())
        });

        // Act
        var first = factory.Create(CustomSqlProvider.Instance.Key);
        var alias = factory.Create(CustomSqliteAliasProvider.Instance.Key);

        // Assert
        Assert.IsType<CustomSqlBuilder>(first);
        Assert.IsType<CustomSqlBuilder>(alias);
    }

    /// <summary>
    /// 测试目的：未知和重复 Provider Key 应返回包含 Key 的明确异常。
    /// </summary>
    [Fact]
    public void Factory_WhenProviderKeyIsUnknownOrDuplicated_ShouldThrowWithKey()
    {
        // Arrange
        var registration = new SqlBuilderFactoryRegistration(CustomSqlProvider.Instance, _ => new CustomSqlBuilder());
        var factory = new SqlBuilderFactory(new[] { registration });

        // Act
        var unknown = Assert.Throws<NotSupportedException>(() => factory.Create("custom.missing"));
        var duplicated = Assert.Throws<ArgumentException>(() => new SqlBuilderFactory(new[] { registration, registration }));

        // Assert
        Assert.Contains("custom.missing", unknown.Message);
        Assert.Contains("custom.test", duplicated.Message);
    }

    /// <summary>
    /// 测试目的：Provider 声明参数上限时，新增参数应在达到上限后被拒绝，已有参数仍可替换。
    /// </summary>
    [Fact]
    public void ParameterLimit_WhenLimitReached_ShouldRejectNewParameterAndAllowReplacement()
    {
        // Arrange
        var builder = new LimitedCustomSqlBuilder();
        var parameterManager = ((ISqlCommonPartAccessor)builder).ParameterManager;

        // Act
        builder.Where("u.Id", 1);
        parameterManager.Add("@_p_0", 2);
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Where("u.Name", "blocked"));

        // Assert
        Assert.Equal("SQL Provider 'custom.limited' 的参数数量超出上限。当前参数数量: 1；尝试添加后数量: 2；最大参数数量: 1。", exception.Message);
        Assert.Single(builder.GetParams());
        Assert.Equal(2, builder.GetParam("@_p_0"));
    }

    /// <summary>
    /// 测试目的：参数上限配置应在 New 与 Clone 后保留，且 New 不继承来源参数。
    /// </summary>
    [Fact]
    public void ParameterLimit_WhenBuilderIsNewOrCloned_ShouldPreserveLimitAndIsolateParameters()
    {
        // Arrange
        var source = new LimitedCustomSqlBuilder();
        source.Where("u.Id", 1);

        // Act
        var clone = Assert.IsType<LimitedCustomSqlBuilder>(source.Clone());
        var fresh = Assert.IsType<LimitedCustomSqlBuilder>(source.New());
        var cloneException = Assert.Throws<InvalidOperationException>(() => clone.Where("u.Name", "blocked"));
        fresh.Where("u.Name", "fresh");
        var freshException = Assert.Throws<InvalidOperationException>(() => fresh.Where("u.Enabled", true));

        // Assert
        Assert.Equal("SQL Provider 'custom.limited' 的参数数量超出上限。当前参数数量: 1；尝试添加后数量: 2；最大参数数量: 1。", cloneException.Message);
        Assert.Equal("SQL Provider 'custom.limited' 的参数数量超出上限。当前参数数量: 1；尝试添加后数量: 2；最大参数数量: 1。", freshException.Message);
        Assert.Single(source.GetParams());
        Assert.Single(clone.GetParams());
        Assert.Single(fresh.GetParams());
        Assert.Equal(1, source.GetParam("@_p_0"));
        Assert.Equal("fresh", fresh.GetParam("@_p_0"));
    }

    /// <summary>
    /// 测试目的：显式注入普通或增强参数管理器时，Provider 参数上限均不应被绕过。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParameterLimit_WhenExplicitManagerIsInjected_ShouldRejectParametersBeyondProviderLimit(bool useAdvancedManager)
    {
        // Arrange
        IParameterManager parameterManager = useAdvancedManager
            ? new ParameterManager(LimitedCustomSqlProvider.Instance.Dialect)
            : new PlainParameterManager(LimitedCustomSqlProvider.Instance.Dialect);
        var builder = new LimitedCustomSqlBuilder(parameterManager);

        // Act
        builder.Where("u.Id", 1);
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Where("u.Name", "blocked"));

        // Assert
        Assert.Equal("SQL Provider 'custom.limited' 的参数数量超出上限。当前参数数量: 1；尝试添加后数量: 2；最大参数数量: 1。", exception.Message);
        Assert.Single(builder.GetParams());
        Assert.Equal(1, builder.GetParam("@_p_0"));
    }

    /// <summary>
    /// 测试目的：外部 Provider 的分页渲染器应参与完整 SQL 输出。
    /// </summary>
    [Fact]
    public void PaginationRenderer_WhenSkipAndTakeConfigured_ShouldRenderProviderSql()
    {
        // Arrange
        var builder = new CustomSqlBuilder();

        // Act
        var sql = builder.Select("*").From("Users").OrderBy("Id").Skip(3).Take(5).ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom [Users] \r\nOrder By [Id] \r\nLimit @_p_1 Offset @_p_0", sql);
        Assert.Equal(3, builder.GetParam("@_p_0"));
        Assert.Equal(5, builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试目的：New 应复用共享服务，同时提供独立且为空的参数管理器。
    /// </summary>
    [Fact]
    public void New_WhenSourceContainsParameters_ShouldShareServicesAndIsolateParameters()
    {
        // Arrange
        var source = new CustomSqlBuilder();
        source.Select("u.Id").From("Users", "u").Where("u.Id", 7);

        // Act
        var fresh = Assert.IsType<CustomSqlBuilder>(source.New());
        fresh.Select("o.Id").From("Orders", "o").Where("o.Id", 9);

        // Assert
        Assert.Same(source.SharedServices, fresh.SharedServices);
        Assert.Equal(7, source.GetParam("@_p_0"));
        Assert.Equal(9, fresh.GetParam("@_p_0"));
        Assert.Single(source.GetParams());
        Assert.Single(fresh.GetParams());
    }

    /// <summary>
    /// 测试目的：Clone 修改 Join 与参数后不得影响来源 Builder 的 SQL 或参数。
    /// </summary>
    [Fact]
    public void Clone_WhenJoinAndParametersChange_ShouldPreserveSourceAndIsolateState()
    {
        // Arrange
        var source = new CustomSqlBuilder();
        source.Select("u.Id").From("Users", "u").Where("u.Enabled", true);
        var sourceSql = source.ToSql();

        // Act
        var clone = Assert.IsType<CustomSqlBuilder>(source.Clone());
        clone.LeftJoin("Orders", "o").Where("o.Paid", false);

        // Assert
        Assert.Equal("Select [u].[Id] \r\nFrom [Users] As [u] \r\nWhere [u].[Enabled]=@_p_0", sourceSql);
        Assert.Equal(sourceSql, source.ToSql());
        Assert.Equal("Select [u].[Id] \r\nFrom [Users] As [u] \r\nLeft Join [Orders] As [o] \r\nWhere [u].[Enabled]=@_p_0 And [o].[Paid]=@_p_1", clone.ToSql());
        Assert.Equal(true, source.GetParam("@_p_0"));
        Assert.Equal(false, clone.GetParam("@_p_1"));
        Assert.Single(source.GetParams());
        Assert.Equal(2, clone.GetParams().Count);
    }

    /// <summary>
    /// 测试目的：外部 Provider 的自定义子句在首次创建、Clear、New 和 Clone 后均应保持实际运行类型，且 Clone 必须保留列状态。
    /// </summary>
    [Fact]
    public void CustomClause_WhenBuilderLifecycleChanges_ShouldPreserveTypeAndCloneState()
    {
        // Arrange
        var source = new CustomSqlBuilder();
        source.Select("u.Id").From("Users", "u");

        // Act
        var clone = Assert.IsType<CustomSqlBuilder>(source.Clone());
        var fresh = Assert.IsType<CustomSqlBuilder>(source.New());
        source.Clear();

        // Assert
        Assert.IsType<CustomSelectClause>(((ISqlQueryClauseAccessor)source).SelectClause);
        Assert.IsType<CustomSelectClause>(((ISqlQueryClauseAccessor)clone).SelectClause);
        Assert.IsType<CustomSelectClause>(((ISqlQueryClauseAccessor)fresh).SelectClause);
        Assert.Equal("Select [u].[Id] \r\nFrom [Users] As [u]", clone.ToSql());
    }

    /// <summary>
    /// 仅公开基础参数能力的测试代理，用于验证 Builder 的普通参数管理器包装路径。
    /// </summary>
    private sealed class PlainParameterManager : IParameterManager
    {
        /// <summary>
        /// 实际存储参数的内部管理器。
        /// </summary>
        private readonly IParameterManager _inner;

        /// <summary>
        /// 初始化基础参数管理器测试代理。
        /// </summary>
        /// <param name="dialect">参数名称使用的 SQL 方言。</param>
        public PlainParameterManager(IDialect dialect) : this(new ParameterManager(dialect))
        {
        }

        /// <summary>
        /// 使用已有内部管理器初始化基础参数管理器测试代理。
        /// </summary>
        /// <param name="inner">实际存储参数的内部管理器。</param>
        private PlainParameterManager(IParameterManager inner) => _inner = inner;

        /// <inheritdoc />
        public string GenerateName() => _inner.GenerateName();

        /// <inheritdoc />
        public string NormalizeName(string name) => _inner.NormalizeName(name);

        /// <inheritdoc />
        public int Count => _inner.Count;

        /// <inheritdoc />
        public void Add(string name, object value, Operator? @operator = null) => _inner.Add(name, value, @operator);

        /// <inheritdoc />
        public IReadOnlyDictionary<string, object> GetParams() => _inner.GetParams();

        /// <inheritdoc />
        public bool Contains(string name) => _inner.Contains(name);

        /// <inheritdoc />
        public object GetValue(string name) => _inner.GetValue(name);

        /// <inheritdoc />
        public IParameterManager Clone() => new PlainParameterManager(_inner.Clone());

        /// <inheritdoc />
        public void Clear() => _inner.Clear();

        /// <inheritdoc />
        public IParameterManager CreateEmpty() => new PlainParameterManager(_inner.CreateEmpty());
    }
}
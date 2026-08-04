using System.Text;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Operations;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Clauses;

/// <summary>
/// SQL 子句公共合同测试。
/// </summary>
public class SqlClauseContractTest
{
    /// <summary>
    /// 测试目的：通用 SQL 子句合同必须继承内容追加能力，并提供状态清空方法。
    /// </summary>
    [Fact]
    public void ISqlClause_ShouldInheritSqlContentAndDeclareClear()
    {
        // Arrange
        var clear = typeof(ISqlClause).GetMethod(nameof(ISqlClause.Clear));

        // Act
        var inheritsSqlContent = typeof(ISqlContent).IsAssignableFrom(typeof(ISqlClause));

        // Assert
        Assert.True(inheritsSqlContent);
        Assert.NotNull(clear);
        Assert.Equal(typeof(void), clear.ReturnType);
        Assert.Empty(clear.GetParameters());
    }

    /// <summary>
    /// 测试目的：六个查询 Clause 接口应统一实现可追加、可清空的公共 Clause 合同。
    /// </summary>
    [Fact]
    public void QueryClauseInterfaces_ShouldInheritSqlClause()
    {
        // Arrange
        var queryClauseTypes = new[]
        {
            typeof(ISelectClause), typeof(IFromClause), typeof(IJoinClause), typeof(IWhereClause),
            typeof(IGroupByClause), typeof(IOrderByClause)
        };

        // Act
        var result = queryClauseTypes.Select(type => typeof(ISqlClause).IsAssignableFrom(type));

        // Assert
        Assert.All(result, Assert.True);
    }

    /// <summary>
    /// 测试目的：历史聚合访问器已移除，SQL Builder 应直接暴露按职责拆分的两个访问器。
    /// </summary>
    [Fact]
    public void SplitAccessors_ShouldReplaceLegacyPartAccessor()
    {
        // Arrange
        var assemblyTypes = typeof(ISqlCommonPartAccessor).Assembly.GetTypes();

        // Act
        var supportsCommonParts = typeof(ISqlCommonPartAccessor).IsAssignableFrom(typeof(TestSqlBuilder));
        var supportsQueryClauses = typeof(ISqlQueryClauseAccessor).IsAssignableFrom(typeof(TestSqlBuilder));
        var hasLegacyPartAccessor = assemblyTypes.Any(type => type.Name == "ISqlPartAccessor");

        // Assert
        Assert.True(supportsCommonParts);
        Assert.True(supportsQueryClauses);
        Assert.False(hasLegacyPartAccessor);
    }

    /// <summary>
    /// 测试目的：查询与参数 Fluent API 应仅依赖各自的细分访问器，不要求实现兼容组合接口。
    /// </summary>
    [Fact]
    public void SplitAccessors_WhenUsingFluentApis_ShouldNotRequireLegacyPartAccessor()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        var queryAccessor = new QueryClauseAccessorProxy(builder);
        var parameterAccessor = new CommonPartAccessorProxy();

        // Act
        queryAccessor.Distinct().From("orders").Where("Id", 1);
        parameterAccessor.AddParam("id", 1);

        // Assert
        Assert.IsAssignableFrom<ISqlQueryClauseAccessor>(queryAccessor);
        Assert.Equal("Select Distinct *", builder.SelectClause.ToSql());
        Assert.Equal("From [orders]", builder.FromClause.ToSql());
        Assert.NotNull(builder.WhereClause.ToSql());
        Assert.IsAssignableFrom<ISqlCommonPartAccessor>(parameterAccessor);
        Assert.Equal(1, parameterAccessor.ParameterManager.GetValue("id"));
    }

    /// <summary>
    /// 测试目的：仅实现公开 Fluent 标记而未提供 Clause SPI 的外部对象必须快速失败，避免调用看似成功但未修改查询状态。
    /// </summary>
    [Fact]
    public void FluentOperation_WhenClauseAccessorIsMissing_ShouldThrow()
    {
        // Arrange
        var source = new SelectOnlyProxy();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => source.Distinct());

        // Assert
        Assert.Contains(nameof(ISqlQueryClauseAccessor), exception.Message);
        Assert.Contains(typeof(SelectOnlyProxy).FullName, exception.Message);
    }

    /// <summary>
    /// 测试目的：实体执行器仅提供 Mutation 与实体 CRUD 操作，不得泄露查询或 Fluent Mutation Builder 接口。
    /// </summary>
    [Fact]
    public void SqlExecutor_ShouldNotInheritFluentMutationOperationMarker()
    {
        // Arrange
        var executor = typeof(ISqlExecutor);

        // Act
        var exposesMutationMarker = typeof(ISqlOperation).IsAssignableFrom(executor);
        var exposesQuery = typeof(ISqlQuery).IsAssignableFrom(executor);
        var returningMethod = executor.GetMethod(nameof(ISqlExecutor.ExecuteReturningQueryAsync));

        // Assert
        Assert.False(exposesMutationMarker);
        Assert.False(exposesQuery);
        Assert.NotNull(returningMethod);
    }

    /// <summary>
    /// 测试目的：完整 SQL Operation 实现必须同时提供查询和三类 Mutation Clause Accessor。
    /// </summary>
    [Fact]
    public void SqlOperationImplementation_ShouldProvideCompleteClauseAccessors()
    {
        // Arrange
        var builder = typeof(TestSqlBuilder);

        // Act
        var contracts = new[]
        {
            typeof(ISqlQueryClauseAccessor), typeof(IInsertClauseAccessor), typeof(IUpdateClauseAccessor),
            typeof(IDeleteClauseAccessor), typeof(ISqlMutationContextAccessor)
        };

        // Assert
        Assert.True(typeof(ISqlOperation).IsAssignableFrom(builder));
        Assert.All(contracts, contract => Assert.True(contract.IsAssignableFrom(builder)));
    }

    /// <summary>
    /// 测试目的：Clause AppendTo 应与兼容 ToSql 输出完全一致，并在 Clear 后只移除自身状态。
    /// </summary>
    [Fact]
    public void QueryClauses_WhenAppendedAndCleared_ShouldPreserveOutputAndResetState()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        builder.SelectClause.Select("Id");
        builder.FromClause.From("orders", "o");
        builder.JoinClause.Join("customers", "c");
        builder.JoinClause.On("o.CustomerId", "c.Id");
        builder.WhereClause.Where("o.Status", "active");
        builder.GroupByClause.GroupBy("o.Status");
        builder.OrderByClause.OrderBy("o.Status");
        var clauses = new ISqlClause[]
        {
            builder.SelectClause, builder.FromClause, builder.JoinClause, builder.WhereClause,
            builder.GroupByClause, builder.OrderByClause
        };
        var expectedSql = new[]
        {
            builder.SelectClause.ToSql(), builder.FromClause.ToSql(), builder.JoinClause.ToSql(),
            builder.WhereClause.ToSql(), builder.GroupByClause.ToSql(), builder.OrderByClause.ToSql()
        };

        // Act
        var appendedSql = clauses.Select(clause =>
        {
            var result = new StringBuilder();
            clause.AppendTo(result);
            return result.ToString();
        }).ToArray();
        foreach (var clause in clauses)
            clause.Clear();

        // Assert
        Assert.Equal(expectedSql, appendedSql);
        Assert.Equal("Select *", builder.SelectClause.ToSql());
        Assert.Null(builder.FromClause.ToSql());
        Assert.Empty(builder.JoinClause.ToSql());
        Assert.Null(builder.WhereClause.ToSql());
        Assert.Null(builder.GroupByClause.ToSql());
        Assert.Null(builder.OrderByClause.ToSql());
    }

    /// <summary>
    /// 仅提供查询 Clause 的 Fluent API 测试代理。
    /// </summary>
    private sealed class QueryClauseAccessorProxy : ISelect, IFrom, IWhere, ISqlQueryClauseAccessor
    {
        private readonly TestSqlBuilder _builder;

        public QueryClauseAccessorProxy(TestSqlBuilder builder) => _builder = builder;

        public ISelectClause SelectClause => _builder.SelectClause;

        public IFromClause FromClause => _builder.FromClause;

        public IJoinClause JoinClause => _builder.JoinClause;

        public IWhereClause WhereClause => _builder.WhereClause;

        public IGroupByClause GroupByClause => _builder.GroupByClause;

        public IOrderByClause OrderByClause => _builder.OrderByClause;
    }

    /// <summary>
    /// 仅实现 Select 标记的无效外部 Fluent 操作代理。
    /// </summary>
    private sealed class SelectOnlyProxy : ISelect
    {
    }

    /// <summary>
    /// 仅提供通用 SQL 组件的参数 Fluent API 测试代理。
    /// </summary>
    private sealed class CommonPartAccessorProxy : ISqlParameter, ISqlCommonPartAccessor
    {
        public IDialect Dialect { get; } = TestDialect.Instance;

        public IParameterManager ParameterManager { get; } = new ParameterManager(TestDialect.Instance);
    }
}
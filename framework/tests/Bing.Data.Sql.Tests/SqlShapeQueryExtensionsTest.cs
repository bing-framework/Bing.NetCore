using System.Data;
using Bing.Data.Sql.Builders.Params;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>形状模板直接接入原生 SQL 查询的职责测试。</summary>
public sealed class SqlShapeQueryExtensionsTest
{
    /// <summary>无参数模板通过两个入口都可创建文本查询。</summary>
    [Fact]
    public void Sql_WhenTemplateHasNoParameters_ShouldAcceptEmptyValues()
    {
        var source = CreateSource(new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict).Object);
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT 1"));

        var byMask = source.Object.Sql(template, 0, ReadOnlySpan<object>.Empty);
        var byShape = source.Object.Sql(template, default(SqlQueryShapeKey), default);

        Assert.Equal("SELECT 1", byMask.CommandText);
        Assert.Equal("SELECT 1", byShape.CommandText);
        Assert.Empty(Parameters(byMask.Parameters));
        Assert.Empty(Parameters(byShape.Parameters));
    }

    /// <summary>按掩码绑定后仍延迟执行，并沿用文本查询的列表终结入口。</summary>
    [Fact]
    public void Sql_ByOptionalMask_ShouldCreateDeferredTextQuery()
    {
        var executor = new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict);
        SqlQueryPlan executed = null;
        executor.Setup(item => item.ToList<int>(It.IsAny<SqlQueryPlan>(), 12))
            .Callback<SqlQueryPlan, int?>((plan, _) => executed = plan)
            .Returns(new List<int> { 7 });
        var source = CreateSource(executor.Object);
        var template = CreateTemplate();

        var query = source.Object.Sql(template, 1, new object[] { 7, "paid", 10 });

        Assert.Equal("SELECT @tenant WHERE @state='paid' LIMIT @limit", query.CommandText);
        Assert.Equal(new object[] { 7, "paid", 10 }, Parameters(query.Parameters).Select(item => item.Value));
        executor.VerifyNoOtherCalls();
        Assert.Equal(new[] { 7 }, query.ToList<int>(12));
        Assert.Equal(query.CommandText, executed.CommandText);
        Assert.False(executed.IsBuilderPlan);
        Assert.Equal(CommandType.Text, executed.CommandType);
        source.Verify(item => item.Sql(query.CommandText, It.IsAny<object>()), Times.Once);
        source.VerifyNoOtherCalls();
    }

    /// <summary>完整结构键控制形状缓存命中与隔离，参数值不进入缓存键。</summary>
    [Fact]
    public void Sql_ByShapeKey_ShouldReuseAndIsolateCachedShapes()
    {
        var source = CreateSource(new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict).Object);
        var template = CreateTemplate();
        var shape = new SqlQueryShapeKey("orders", "sqlite", "v1", "route-a", 0, "");

        var first = source.Object.Sql(template, shape, new object[] { 1, 10 });
        var hit = source.Object.Sql(template, shape, new object[] { 2, 20 });
        var miss = source.Object.Sql(template, shape with { MappingVersion = "v2" }, new object[] { 3, 30 });

        Assert.Equal("SELECT @tenant LIMIT @limit", first.CommandText);
        Assert.Equal(first.CommandText, miss.CommandText);
        Assert.Same(first.CommandText, hit.CommandText);
        Assert.Equal(new object[] { 1, 10 }, Parameters(first.Parameters).Select(item => item.Value));
        Assert.Equal(new object[] { 2, 20 }, Parameters(hit.Parameters).Select(item => item.Value));
        Assert.Equal(new object[] { 3, 30 }, Parameters(miss.Parameters).Select(item => item.Value));
        Assert.Equal(2, template.CachedShapeCount);
    }

    /// <summary>作用域只改写参数名称，保留参数顺序、元数据及请求值。</summary>
    [Fact]
    public void Sql_WithScope_ShouldUseScopedTextAndParameters()
    {
        var source = CreateSource(new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict).Object);
        var shape = new SqlQueryShapeKey("orders", "sqlite", "v1", "route-a", 1, "");

        var query = source.Object.Sql(CreateTemplate(), shape, new object[] { 7, "paid", 10 },
            new SqlQueryScope("child"));

        Assert.Equal("SELECT @child_tenant WHERE @child_state='paid' LIMIT @child_limit", query.CommandText);
        var parameters = Parameters(query.Parameters);
        Assert.Equal(new[] { "child_tenant", "child_state", "child_limit" }, parameters.Select(item => item.Name));
        Assert.Equal(new object[] { 7, "paid", 10 }, parameters.Select(item => item.Value));
        Assert.Equal(new DbType?[] { DbType.Int32, DbType.String, DbType.Int32 }, parameters.Select(item => item.DbType));
    }

    /// <summary>输入数组、公开参数快照及前次执行计划均不能修改下一次执行的参数。</summary>
    [Fact]
    public void Sql_WhenInputsOrSnapshotsChange_ShouldKeepEveryExecutionIndependent()
    {
        var executor = new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict);
        var plans = new List<SqlQueryPlan>();
        executor.Setup(item => item.Scalar<int>(It.IsAny<SqlQueryPlan>(), null))
            .Callback<SqlQueryPlan, int?>((plan, _) => plans.Add(plan)).Returns(1);
        var source = CreateSource(executor.Object);
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT @payload, @name", new[]
        {
            new SqlParam("@payload", Array.Empty<byte>(), DbType.Binary, size: 40),
            new SqlParam("@name", "default", DbType.String, size: 20)
        }));
        var payload = new byte[] { 1, 2 };
        object[] values = { payload, null };

        var query = source.Object.Sql(template, 0, values);
        payload[0] = 9;
        values[1] = "changed";
        var exposed = Parameters(query.Parameters);
        Assert.IsType<byte[]>(exposed[0].Value)[1] = 9;
        exposed[1].Value = "also changed";
        query.Scalar<int>();
        Assert.IsType<byte[]>(Parameters(plans[0].Parameters)[0].Value)[0] = 8;
        query.Scalar<int>();

        Assert.Equal("SELECT @payload, @name", plans[1].CommandText);
        var second = Parameters(plans[1].Parameters);
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(second[0].Value));
        Assert.Null(second[1].Value);
        Assert.Equal((DbType.Binary, 40), (second[0].DbType, second[0].Size));
        Assert.Equal((DbType.String, 20), (second[1].DbType, second[1].Size));
        Assert.NotSame(plans[0], plans[1]);
    }

    /// <summary>异步终结沿用执行器的超时和取消令牌传递。</summary>
    [Fact]
    public async Task Sql_WhenExecutedAsynchronously_ShouldForwardTimeoutAndCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var executor = new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict);
        executor.Setup(item => item.ScalarAsync<int>(It.Is<SqlQueryPlan>(plan =>
                plan.CommandText == "SELECT @id"), 15, cancellation.Token))
            .ReturnsAsync(17);
        var source = CreateSource(executor.Object);
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT @id",
            new[] { new SqlParam("@id", 0, DbType.Int32) }));

        var result = await source.Object.Sql(template, 0, new object[] { 17 })
            .ScalarAsync<int>(15, cancellation.Token);

        Assert.Equal(17, result);
        executor.VerifyAll();
    }

    /// <summary>第三方根查询持有或修改收到的参数也不能污染模板的后续绑定。</summary>
    [Fact]
    public void Sql_WhenCustomSourceMutatesParameters_ShouldKeepTemplateReusable()
    {
        var executor = new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict);
        var source = new Mock<ISqlQuery>(MockBehavior.Strict);
        source.Setup(item => item.Sql(It.IsAny<string>(), It.IsAny<object>()))
            .Returns((string sql, object parameters) =>
            {
                var query = SqlQueryRuntimeFactory.CreateTextQuery(executor.Object, sql, parameters);
                var received = Parameters(parameters);
                received[0].Value = -1;
                received[0].ColumnName = "changed";
                return query;
            });
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT @id",
            new[] { new SqlParam("@id", 0, DbType.Int32) }));

        var first = source.Object.Sql(template, 0, new object[] { 7 });
        var second = source.Object.Sql(template, 0, new object[] { 9 });

        Assert.Equal("SELECT @id", first.CommandText);
        Assert.Equal("SELECT @id", second.CommandText);
        Assert.Equal((7, DbType.Int32), (Parameters(first.Parameters)[0].Value, Parameters(first.Parameters)[0].DbType));
        Assert.Equal((9, DbType.Int32), (Parameters(second.Parameters)[0].Value, Parameters(second.Parameters)[0].DbType));
        Assert.Equal((0, DbType.Int32), (template.Render().Parameters[0].Value, template.Render().Parameters[0].DbType));
    }

    /// <summary>两个入口对缺失根查询和模板给出一致参数错误。</summary>
    [Fact]
    public void Sql_WhenSourceOrTemplateIsNull_ShouldRejectBeforeDelegating()
    {
        ISqlQuery missing = null;
        var source = CreateSource(new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict).Object);
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT 1"));
        var shape = default(SqlQueryShapeKey);

        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => missing.Sql(template, 0, Array.Empty<object>())).ParamName);
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => missing.Sql(template, shape, Array.Empty<object>())).ParamName);
        Assert.Equal("template", Assert.Throws<ArgumentNullException>(() => source.Object.Sql(null, 0, Array.Empty<object>())).ParamName);
        Assert.Equal("template", Assert.Throws<ArgumentNullException>(() => source.Object.Sql(null, shape, Array.Empty<object>())).ParamName);
        source.VerifyNoOtherCalls();
    }

    /// <summary>绑定失败时不创建文本查询；合法绑定仍可重用模板。</summary>
    [Fact]
    public void Sql_WhenBindingsAreInvalid_ShouldNotCallRootQuery()
    {
        var source = CreateSource(new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict).Object);
        var template = CreateTemplate();

        Assert.Throws<ArgumentException>(() => source.Object.Sql(template, 1, new object[] { 1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Object.Sql(template, 2, new object[] { 1, 10 }));
        source.VerifyNoOtherCalls();
        Assert.Equal("SELECT @tenant LIMIT @limit", source.Object.Sql(template, 0, new object[] { 1, 10 }).CommandText);
    }

    /// <summary>多个根查询可共享同一模板，缓存 SQL 与本次请求参数分别管理。</summary>
    [Fact]
    public void Sql_WhenCalledConcurrently_ShouldIsolateQueryParameters()
    {
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT @id",
            new[] { new SqlParam("@id", 0, DbType.Int32) }));
        var queries = new SqlTextQuery[128];

        Parallel.For(0, queries.Length, index =>
        {
            var source = CreateSource(new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict).Object);
            queries[index] = source.Object.Sql(template, 0, new object[] { index });
        });

        for (var index = 0; index < queries.Length; index++)
        {
            Assert.Equal("SELECT @id", queries[index].CommandText);
            Assert.Same(queries[0].CommandText, queries[index].CommandText);
            Assert.Equal(index, Parameters(queries[index].Parameters)[0].Value);
        }
        Assert.Equal(1, template.CachedShapeCount);
    }

    private static Mock<ISqlQuery> CreateSource(ISqlQueryPlanExecutor executor)
    {
        var source = new Mock<ISqlQuery>(MockBehavior.Strict);
        source.Setup(item => item.Sql(It.IsAny<string>(), It.IsAny<object>()))
            .Returns((string sql, object parameters) => SqlQueryRuntimeFactory.CreateTextQuery(executor, sql, parameters));
        return source;
    }

    private static SqlParam[] Parameters(object value) => Assert.IsAssignableFrom<IEnumerable<SqlParam>>(value).ToArray();

    private static SqlQueryShapeTemplate CreateTemplate() => new(
        new SqlQueryFragment("SELECT @tenant", new[] { new SqlParam("@tenant", 0, DbType.Int32) }),
        new[] { new SqlQueryOptionalFragment(0, "WHERE @state='paid'", new[] { new SqlParam("@state", "", DbType.String) }) },
        " ", new SqlQueryFragment("LIMIT @limit", new[] { new SqlParam("@limit", 0, DbType.Int32) }));
}

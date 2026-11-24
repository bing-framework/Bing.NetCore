using System.Data;
using System.Reflection;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 查询 IR 的不可变快照测试。
/// </summary>
public sealed class SqlQueryIrTest
{
    /// <summary>
    /// 测试目的：默认 Fluent 查询提交给执行器时应直接使用无 Builder 的统一计划。
    /// </summary>
    [Fact]
    public void DefaultQuery_ShouldSubmitBuilderlessIrPlan()
    {
        SqlQueryPlan captured = null;
        var executor = new Mock<ISqlQueryPlanExecutor>();
        executor.Setup(item => item.ToList<SqlQueryIrRow>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns((SqlQueryPlan plan, int? _) =>
            {
                captured = plan;
                return new List<SqlQueryIrRow>();
            });
        var query = SqlQueryRuntimeFactory.CreateQuery(executor.Object, new TestSqlBuilder())
            .Select("Id")
            .From("Users");

        query.ToList<SqlQueryIrRow>();

        Assert.NotNull(captured);
        Assert.False(captured.IsBuilderPlan);
        Assert.NotNull(captured.QueryIr);
        Assert.Null(captured.PlanningBuilder);
        Assert.Equal("Select [Id] \r\nFrom [Users]", captured.CommandText);
    }

    /// <summary>
    /// 测试目的：CTE 查询直接提交冻结结构，不再保留可完整派生的规划 Builder。
    /// </summary>
    [Fact]
    public void CteQuery_ShouldSubmitBuilderlessIr()
    {
        SqlQueryPlan captured = null;
        var executor = new Mock<ISqlQueryPlanExecutor>();
        executor.Setup(item => item.ToList<SqlQueryIrRow>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns((SqlQueryPlan plan, int? _) =>
            {
                captured = plan;
                return new List<SqlQueryIrRow>();
            });
        var cte = new SqlQueryFragment("Select Id From Users", Array.Empty<SqlParam>(), SqlQueryFragmentKind.Cte);
        var query = SqlQueryRuntimeFactory.CreateQuery(executor.Object, new TestSqlBuilder())
            .Select("Id")
            .From("recent_users")
            .With("recent_users", cte);

        query.ToList<SqlQueryIrRow>();

        Assert.NotNull(captured);
        Assert.False(captured.IsBuilderPlan);
        Assert.NotNull(captured.QueryIr);
        Assert.Null(captured.PlanningBuilder);
        Assert.Equal("With [recent_users] \r\nAs (Select Id From Users)\r\nSelect [Id] \r\nFrom [recent_users]",
            captured.CommandText);
    }

    /// <summary>
    /// 测试目的：复杂查询计划每次读取规划 Builder 时都应返回独立副本，避免派生规划修改计划内部状态。
    /// </summary>
    [Fact]
    public void ComplexPlan_PlanningBuilder_ShouldReturnIndependentClones()
    {
        var union = new TestSqlBuilder().Select("Id").From("archived_users");
        var plan = SqlQueryPlan.Create(new CustomTailBuilder()
                .Select("Id")
                .From("recent_users")
                .Union(union), "Id")
            .Freeze();

        var first = plan.PlanningBuilder;
        Assert.NotNull(first);
        first.Where("Id", 1);

        var second = plan.PlanningBuilder;
        Assert.NotNull(second);

        Assert.NotSame(first, second);
        Assert.Equal("(Select [Id] \r\nFrom [recent_users] \r\n) \r\nUnion \r\n(Select [Id] \r\nFrom [archived_users] \r\n) \r\n /*custom*/", second.ToSql());
    }

    private sealed class CustomTailBuilder : TestSqlBuilder
    {
        internal CustomTailBuilder(Bing.Data.Sql.Builders.Params.IParameterManager parameters = null) : base(parameterManager: parameters) { }
        protected override Bing.Data.Sql.Builders.Core.SqlBuilderBase CreateBuilder(Bing.Data.Sql.Builders.Params.IParameterManager parameters) => new CustomTailBuilder(parameters);
        protected override void CreateSql(System.Text.StringBuilder result)
        {
            base.CreateSql(result);
            result.Append(" /*custom*/");
        }
    }

    /// <summary>
    /// 测试目的：统一查询 IR 不应直接持有可变 Builder，规划来源必须留在计划派生边界。
    /// </summary>
    [Fact]
    public void Ir_ShouldNotExposeBuilderPlanningState()
    {
        var builderType = typeof(SqlQueryIr);
        var builderMembers = builderType
            .GetMembers(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(item => item.Name.Contains("Builder", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(builderMembers);
        Assert.DoesNotContain(builderType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            item => typeof(ISqlBuilder).IsAssignableFrom(item.FieldType));
    }

    private sealed class SqlQueryIrRow
    {
        public int Id { get; set; }
    }

    /// <summary>
    /// 测试目的：IR 计划应只保存冻结 SQL 和参数，不再暴露可变 Builder。
    /// </summary>
    [Fact]
    public void IrPlan_ShouldBeBuilderlessAndKeepParameterSnapshot()
    {
        // Arrange
        var values = new[] { 1, 2 };
        var snapshot = new SqlBuilderExecutionSnapshot(
            "Select * From Users Where Id In (@id1,@id2)",
            new[]
            {
                new SqlParam("@id1", values[0]),
                new SqlParam("@id2", values[1])
            });
        var ir = SqlQueryIr.Create(snapshot, CommandType.Text, new Pager(2, 10, 0, "Id"));

        // Act
        var plan = SqlQueryPlan.Create(ir, "Id");
        values[0] = 99;
        var parameters = Assert.IsType<SqlParam[]>(plan.Parameters);

        // Assert
        Assert.False(plan.IsBuilderPlan);
        Assert.NotNull(plan.QueryIr);
        Assert.Equal("Select * From Users Where Id In (@id1,@id2)", plan.CommandText);
        Assert.Equal(CommandType.Text, plan.CommandType);
        Assert.Collection(parameters,
            first => Assert.Equal(1, first.Value),
            second => Assert.Equal(2, second.Value));
    }

    /// <summary>
    /// 测试目的：IR 计划每次获取默认分页参数都应返回独立快照，调用方修改不得污染来源。
    /// </summary>
    [Fact]
    public void IrPlan_DefaultPager_ShouldBeIndependentSnapshot()
    {
        // Arrange
        var snapshot = new SqlBuilderExecutionSnapshot("Select * From Users", Array.Empty<SqlParam>());
        var ir = SqlQueryIr.Create(snapshot, CommandType.Text, new Pager(3, 15, 42, "Id", true));
        var plan = SqlQueryPlan.Create(ir, "Id");

        // Act
        var first = Assert.IsType<Pager>(SqlBuilderRuntimeBridge.GetPlanPager(plan, null));
        first.Page = 9;
        first.PageSize = 1;
        first.Order = "Changed";
        first.TotalCount = 1;
        var second = Assert.IsType<Pager>(SqlBuilderRuntimeBridge.GetPlanPager(plan, null));

        // Assert
        Assert.Equal(3, second.Page);
        Assert.Equal(15, second.PageSize);
        Assert.Equal(42, second.TotalCount);
        Assert.True(second.IsTotalCountKnown);
        Assert.Equal("Id", second.Order);
    }

    /// <summary>
    /// 测试目的：Count 派生应基于冻结 IR 文本生成精确 SQL，并继续保持无 Builder。
    /// </summary>
    [Fact]
    public void IrPlan_Count_ShouldDeriveFromFrozenSql()
    {
        // Arrange
        var snapshot = new SqlBuilderExecutionSnapshot(
            "Select Id From Users Where TenantId = @tenantId",
            new[] { new SqlParam("@tenantId", 7) });
        var plan = SqlQueryPlan.Create(SqlQueryIr.Create(snapshot, CommandType.Text, null), "Id");

        // Act
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(plan);

        // Assert
        Assert.False(count.IsBuilderPlan);
        Assert.NotNull(count.QueryIr);
        Assert.Equal(
            "Select Count(*) From (Select Id From Users Where TenantId = @tenantId) As __bing_raw_count",
            count.CommandText);
        Assert.Equal(7, Assert.Single(Assert.IsType<SqlParam[]>(count.Parameters)).Value);
        Assert.Equal("Id", count.SplitOn);
    }

    /// <summary>
    /// 测试目的：Builder 派生的 Count 计划完成渲染后应立即脱离 Builder。
    /// </summary>
    [Fact]
    public void BuilderPlan_Count_ShouldReturnBuilderlessIrPlan()
    {
        var builder = new TestSqlBuilder()
            .Select("Id")
            .From("Users")
            .Where("TenantId", 7);
        var source = SqlQueryPlan.Create(builder, "Id");

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(source);

        Assert.False(count.IsBuilderPlan);
        Assert.NotNull(count.QueryIr);
        Assert.Equal("Select Count(*) \r\nFrom [Users] \r\nWhere [TenantId]=@_p_0", count.CommandText);
        Assert.Equal(7, Assert.Single(Assert.IsType<SqlParam[]>(count.Parameters)).Value);
    }
}

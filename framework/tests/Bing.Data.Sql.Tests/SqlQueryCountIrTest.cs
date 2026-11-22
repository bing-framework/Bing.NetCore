using System.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>验证 Count 从不可变渲染结构派生，不再复制或重新渲染查询草稿。</summary>
public sealed class SqlQueryCountIrTest
{
    /// <summary>普通终结和 SQL 缓存入口都应按未分页的原查询主体计数。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DefaultQuery_Count_ShouldUseFrozenStructure(bool cached, bool limited)
    {
        var builder = new TestSqlBuilder().Select("Name").From("samples").Where("Status", 7).OrderBy("Name");
        if (limited)
            builder.Skip(5).Take(2);
        var query = new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder);
        if (cached)
            query.ToSql();
        var plan = query.GetPlan();

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(plan);

        Assert.Equal("Select Count(*) \r\nFrom [samples] \r\nWhere [Status]=@_p_0", count.CommandText);
        Assert.Equal(7, Assert.Single(Assert.IsType<SqlParam[]>(count.Parameters)).Value);
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0 \r\nOrder By [Name]" +
            (limited ? " \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only" : string.Empty), plan.CommandText);
    }

    /// <summary>结构修改成功会更新 Count 边界，修改失败则恢复与 SQL 配套的旧边界。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CachedCount_AfterAtomicChange_ShouldUseMatchingStructure(bool rollback)
    {
        var query = new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object,
            new TestSqlBuilder().Select("Name").From("samples").Where("Status", 7));
        query.ToSql();
        void Change() => query.AtomicChange(() =>
        {
            query.GetBuilder().GroupBy("Name");
            query.Touch();
            query.ToSql();
            if (rollback)
                throw new InvalidOperationException("rollback");
            return true;
        });
        if (rollback)
            Assert.Throws<InvalidOperationException>(Change);
        else
            Change();

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(query.GetPlan());

        Assert.Equal(rollback
            ? "Select Count(*) \r\nFrom [samples] \r\nWhere [Status]=@_p_0"
            : "Select Count(*) \r\nFrom (Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0 \r\nGroup By [Name]) As [t]",
            count.CommandText);
        Assert.Equal(7, Assert.Single(Assert.IsType<SqlParam[]>(count.Parameters)).Value);
    }

    /// <summary>完整 SQL 有自定义尾部时不发布失效边界，保留原 Builder 派生扩展。</summary>
    [Fact]
    public void FrozenCount_WithCustomSqlSuffix_ShouldKeepBuilderFallback()
    {
        var plan = SqlQueryPlan.Create(new SuffixBuilder().Select("Name").From("samples")).Freeze();

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(plan);

        Assert.Equal("Select Count(*) \r\nFrom [samples] /* custom */", count.CommandText);
    }

    /// <summary>普通、分组、去重和聚合的冻结 Count 均不复制 Builder，也不再次执行渲染。</summary>
    [Theory]
    [InlineData("simple", "Select [Name]", "")]
    [InlineData("group", "Select [Name]", " \r\nGroup By [Name]")]
    [InlineData("distinct", "Select Distinct [Name]", "")]
    [InlineData("aggregate", "Select Count(*) As [Total]", "")]
    public void FrozenCount_ShouldNotCloneOrRender(string shape, string select, string group)
    {
        var calls = new RenderCalls();
        var builder = new CountedBuilder(calls);
        if (shape == "aggregate")
            builder.CountAll("Total");
        else
            builder.Select("Name");
        if (shape == "distinct")
            builder.Distinct();
        builder.From("samples").Where("Status", 7).OrderBy("Name").Skip(5).Take(2);
        if (shape == "group")
            builder.GroupBy("Name");
        var plan = SqlQueryPlan.Create(builder).Freeze();
        calls.Clones = calls.Renders = 0;

        var first = SqlBuilderRuntimeBridge.CreateCountPlan(plan);
        var second = SqlBuilderRuntimeBridge.CreateCountPlan(plan);

        var body = "From [samples] \r\nWhere [Status]=@_p_0";
        Assert.Equal(shape == "simple" ? $"Select Count(*) \r\n{body}" :
            $"Select Count(*) \r\nFrom ({select} \r\n{body}{group}) As [t]", first.CommandText);
        Assert.Equal(first.CommandText, second.CommandText);
        Assert.Equal(0, calls.Clones);
        Assert.Equal(0, calls.Renders);
        Assert.Null(first.GetBuilder());
        Assert.Null(first.PlanningBuilder);
        Assert.Equal(7, Assert.Single(Assert.IsType<SqlParam[]>(first.Parameters)).Value);
    }

    /// <summary>移除外层分页时必须保留子查询自己的分页 SQL 与参数。</summary>
    [Fact]
    public void Count_ShouldKeepNestedPageAndItsParameters()
    {
        var nested = new TestSqlBuilder().Select("Name").From("archive").OrderBy("Name").Skip(1).Take(3);
        var builder = new TestSqlBuilder().Select("Name").From(nested, "a")
            .OrderBy("Name").Skip(5).Take(2);
        var plan = new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder).GetPlan();

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(plan);

        Assert.Equal("Select Count(*) \r\nFrom (Select [Name] \r\nFrom [archive] \r\nOrder By [Name] \r\n" +
            "Offset @_p_0 Rows Fetch Next @_p_1 Rows Only) As [a]", count.CommandText);
        var parameters = Assert.IsType<SqlParam[]>(count.Parameters);
        Assert.Equal(new[] { "@_p_0", "@_p_1" }, parameters.Select(item => item.Name));
        Assert.Equal(new object[] { 1, 3 }, parameters.Select(item => item.Value));
    }

    /// <summary>并发派生复用只读结构，各次暴露的参数仍需独立且保留完整元数据。</summary>
    [Fact]
    public async Task Count_ConcurrentDerivation_ShouldKeepParameterMetadataAndIsolation()
    {
        var payload = new byte[] { 1, 2 };
        var builder = new TestSqlBuilder().Select("Name").From("samples");
        var manager = Assert.IsAssignableFrom<IAdvancedParameterManager>(((ISqlCommonPartAccessor)builder).ParameterManager);
        manager.Add(new SqlParam("@payload", payload, DbType.Binary, size: 2));
        builder.AppendWhere("[Payload]=@payload").OrderBy("Name").Skip(5).Take(2);
        var plan = SqlQueryPlan.Create(builder).Freeze();
        payload[0] = 9;

        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            var count = SqlBuilderRuntimeBridge.CreateCountPlan(plan);
            Assert.Equal("Select Count(*) \r\nFrom [samples] \r\nWhere [Payload]=@payload", count.CommandText);
            var parameter = Assert.Single(Assert.IsType<SqlParam[]>(count.Parameters));
            Assert.Equal(DbType.Binary, parameter.DbType);
            Assert.Equal(2, parameter.Size);
            Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(parameter.Value));
            Assert.IsType<byte[]>(parameter.Value)[0] = 8;
        })));
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(Assert.IsType<SqlParam[]>(plan.Parameters)[0].Value));
    }

    /// <summary>Count 保持各 Provider 的名称引号，且不携带原外层分页参数。</summary>
    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("sqlserver", false)]
    [InlineData("sqlserver", true)]
    public void FrozenCount_ShouldKeepProviderSql(string provider, bool grouped)
    {
        ISqlBuilder builder = provider switch
        {
            "sqlite" => new SqliteBuilder(),
            "mysql" => new MySqlBuilder(),
            "postgresql" => new PostgreSqlBuilder(),
            _ => new SqlServerBuilder()
        };
        builder.Select("Name").From("samples").Where("Status", 7).OrderBy("Name");
        // SQL Server 当前公开能力配置未开放分页；分页语法另由 TestSqlBuilder 覆盖。
        if (provider != "sqlserver")
            builder.Skip(5).Take(2);
        if (grouped)
            builder.GroupBy("Name");

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(SqlQueryPlan.Create(builder).Freeze());

        var open = provider == "postgresql" ? "\"" : provider == "sqlserver" ? "[" : "`";
        var close = provider == "sqlserver" ? "]" : open;
        string Name(string name) => open + name + close;
        var body = $"From {Name("samples")} \r\nWhere {Name("Status")}=@_p_0";
        Assert.Equal(grouped
            ? $"Select Count(*) \r\nFrom (Select {Name("Name")} \r\n{body} \r\nGroup By {Name("Name")}) As {Name("t")}"
            : $"Select Count(*) \r\n{body}", count.CommandText);
        Assert.Equal(7, Assert.Single(Assert.IsType<SqlParam[]>(count.Parameters)).Value);
    }

    private sealed class RenderCalls
    {
        internal int Clones;
        internal int Renders;
    }

    private sealed class SuffixBuilder : TestSqlBuilder
    {
        internal SuffixBuilder(IParameterManager parameters = null) : base(parameterManager: parameters) { }

        public override string ToSql() => base.ToSql() + " /* custom */";

        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
            new SuffixBuilder(parameterManager);
    }

    private sealed class CountedBuilder : TestSqlBuilder
    {
        private readonly RenderCalls _calls;

        internal CountedBuilder(RenderCalls calls, IParameterManager parameters = null)
            : base(parameterManager: parameters) => _calls = calls;

        public override ISqlBuilder Clone()
        {
            _calls.Clones++;
            return base.Clone();
        }

        public override string ToSql()
        {
            _calls.Renders++;
            return base.ToSql();
        }

        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
            new CountedBuilder(_calls, parameterManager);
    }
}

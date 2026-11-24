using Bing.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Bing.Properties;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>验证默认查询可从冻结 IR 使用 Pager.Order 派生分页。</summary>
public sealed class SqlQueryPagerOrderIrTest
{
    public static IEnumerable<object[]> Providers()
    {
        foreach (var provider in new[] { "sqlserver", "mysql", "postgresql", "sqlite" })
            yield return new object[] { provider };
    }

    /// <summary>分页查询入口在缓存和非缓存渲染后都释放规划 Builder，并隔离后续派生。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetPlanForPaging_ShouldDerivePagerOrderFromFrozenIr(bool cached)
    {
        var calls = new RenderCalls();
        var query = new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object,
            new CountedBuilder(calls).Select("Name").From("samples").Where("Status", "active"));
        const string sourceSql = "Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0";
        if (cached)
            Assert.Equal(sourceSql, query.ToSql());

        var source = query.GetPlan(forPaging: true);

        Assert.Null(source.PlanningBuilder);
        Assert.Equal(sourceSql, source.CommandText);
        AssertParameters(source, "active");
        calls.Clones = calls.Renders = 0;

        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4, "Name"));
        var repeated = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(2, 2, "Id"));
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(repeated);

        Assert.Equal(sourceSql + " \r\nOrder By [Name] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
        Assert.Equal(sourceSql + " \r\nOrder By [Name],[Id] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", repeated.CommandText);
        Assert.Equal("Select Count(*) \r\nFrom [samples] \r\nWhere [Status]=@_p_0", count.CommandText);
        AssertParameters(page, "active", 8, 4);
        AssertParameters(repeated, "active", 2, 2);
        AssertParameters(count, "active");
        Assert.Equal(sourceSql, source.CommandText);
        AssertParameters(source, "active");
        Assert.Null(page.PlanningBuilder);
        Assert.Null(repeated.PlanningBuilder);
        Assert.Null(count.PlanningBuilder);
        Assert.Equal(0, calls.Clones);
        Assert.Equal(0, calls.Renders);
    }

    /// <summary>四个内置 Provider 均从无序源 IR 添加排序、替换分页值并保持源计划不变。</summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public void ProviderPage_ShouldUsePagerOrderAndKeepFrozenSource(string provider)
    {
        var source = SqlQueryPlan.Create(CreateBuilder(provider).Select("Name").From("samples")
            .Where("Status", "active")).Freeze();
        var sourceSql = ExpectedSourceSql(provider);
        Assert.Null(source.PlanningBuilder);
        Assert.Equal(sourceSql, source.CommandText);
        AssertParameters(source, "active");

        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4, "Name"));
        var repeated = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(2, 2, "Id"));
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(repeated);

        Assert.Equal(ExpectedPageSql(provider, "Name"), page.CommandText);
        Assert.Equal(ExpectedPageSql(provider, "Name,Id"), repeated.CommandText);
        Assert.Equal(ExpectedCountSql(provider), count.CommandText);
        AssertParameters(page, "active", 8, 4);
        AssertParameters(repeated, "active", 2, 2);
        AssertParameters(count, "active");
        Assert.Equal(sourceSql, source.CommandText);
        AssertParameters(source, "active");
        Assert.Null(page.PlanningBuilder);
        Assert.Null(repeated.PlanningBuilder);
        Assert.Null(count.PlanningBuilder);

        var error = Assert.Throws<ArgumentException>(() =>
            SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(1, 2, string.Empty)));
        Assert.Equal(LibraryResource.OrderIsEmptyForPage, error.Message);
    }

    /// <summary>并发派生的分页值互相隔离，且修改各自 Pager 不影响已冻结计划。</summary>
    [Fact]
    public async Task PagerOrderPages_ShouldIsolateSixteenConcurrentDerivations()
    {
        var source = SqlQueryPlan.Create(CreateBuilder("sqlite").Select("Name").From("samples")
            .Where("Status", "active")).Freeze();
        const string sourceSql = "Select `Name` \r\nFrom `samples` \r\nWhere `Status`=@_p_0";

        var pages = await Task.WhenAll(Enumerable.Range(1, 16).Select(number => Task.Run(() =>
        {
            var pager = new Pager(number, 4, "Name");
            var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, pager);
            pager.Page = 99;

            Assert.Equal(sourceSql + " \r\nOrder By `Name` \r\nLimit @_p_2 OFFSET @_p_1", page.CommandText);
            AssertParameters(page, "active", (number - 1) * 4, 4);
            Assert.Null(page.PlanningBuilder);
            return page;
        })));

        Assert.Equal(16, pages.Length);
        Assert.Equal(sourceSql, source.CommandText);
        AssertParameters(source, "active");
    }

    private static ISqlBuilder CreateBuilder(string provider)
    {
        var services = new SqlBuilderServices(options: new SqlOptions
        {
            QueryCapabilities = new SqlQueryCapabilities { Pagination = SqlQueryCapabilityState.Supported }
        });
        return provider switch
        {
            "sqlserver" => new SqlServerBuilder(services),
            "mysql" => new MySqlBuilder(services),
            "postgresql" => new PostgreSqlBuilder(services),
            "sqlite" => new SqliteBuilder(services),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
    }

    private static string ExpectedSourceSql(string provider)
    {
        var (open, close) = Quote(provider);
        return $"Select {open}Name{close} \r\nFrom {open}samples{close} \r\nWhere {open}Status{close}=@_p_0";
    }

    private static string ExpectedPageSql(string provider, string order)
    {
        var (open, close) = Quote(provider);
        var columns = string.Join(",", order.Split(',').Select(column => open + column + close));
        var paging = provider == "sqlserver"
            ? "Offset @_p_1 Rows Fetch Next @_p_2 Rows Only"
            : "Limit @_p_2 OFFSET @_p_1";
        return $"{ExpectedSourceSql(provider)} \r\nOrder By {columns} \r\n{paging}";
    }

    private static string ExpectedCountSql(string provider)
    {
        var (open, close) = Quote(provider);
        return $"Select Count(*) \r\nFrom {open}samples{close} \r\nWhere {open}Status{close}=@_p_0";
    }

    private static (string Open, string Close) Quote(string provider) => provider switch
    {
        "sqlserver" => ("[", "]"),
        "postgresql" => ("\"", "\""),
        _ => ("`", "`")
    };

    private static void AssertParameters(SqlQueryPlan plan, params object[] values)
    {
        var parameters = Assert.IsType<SqlParam[]>(plan.Parameters);
        Assert.Equal(values, parameters.Select(parameter => parameter.Value));
        Assert.Equal(Enumerable.Range(0, values.Length).Select(index => "@_p_" + index),
            parameters.Select(parameter => parameter.Name));
    }

    private sealed class RenderCalls
    {
        internal int Clones;
        internal int Renders;
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

        protected override SqlBuilderBase CreateBuilder(IParameterManager parameters) =>
            new CountedBuilder(_calls, parameters);
    }
}

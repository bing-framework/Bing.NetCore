using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>模板作为独立查询执行时应保留冻结结构，无需重新创建 Builder。</summary>
public sealed class SqlQueryTemplateIrTest
{
    public static IEnumerable<object[]> Entries()
    {
        foreach (var entry in new[] { "template", "fragment", "snapshot", "key" })
        foreach (var scoped in new[] { false, true })
            yield return new object[] { entry, scoped };
    }

    /// <summary>片段复制、冻结和逻辑键调整不应将 CTE 退化为原生文本计数。</summary>
    [Theory]
    [MemberData(nameof(Entries))]
    public void Create_FromTemplate_ShouldKeepCteCountAndPageStructure(string entry, bool scoped)
    {
        var calls = new Calls();
        var builder = new CountedBuilder(calls);
        builder.Select("Name").From("recent").Where("TenantId", 9)
            .With("recent", new TestSqlBuilder().Select("Name").From("archive").Where("Status", 7));
        var template = builder.ToTemplate(scoped ? new SqlQueryScope("consumer") : null);
        ISqlQueryFragment fragment = entry switch
        {
            "fragment" => template.CreateFragment(),
            "snapshot" => template.Freeze().Render(),
            "key" => template.CreateFragment().WithParameterKey(scoped ? "consumer_p_0" : "_p_0", new SqlParameterKey("tenant")),
            _ => template
        };
        var source = SqlQueryPlan.Create(fragment, "split");
        builder.Where("Changed", 99);
        calls.Clones = calls.Renders = 0;
        source.SetContext("template-ir", "parent", "Data");

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(source);
        var page = SqlBuilderRuntimeBridge.CreatePageExecutionPlan(source, new Pager(3, 4, "Name"), new RejectBuilderSource());
        var prefix = scoped ? "@consumer_p_" : "@_p_";
        var cte = $"With [recent] \r\nAs (Select [Name] \r\nFrom [archive] \r\nWhere [Status]={prefix}1)\r\n";
        var body = $"From [recent] \r\nWhere [TenantId]={prefix}0";
        Assert.Equal(cte + "Select Count(*) \r\n" + body, count.CommandText);
        Assert.Equal(cte + "Select [Name] \r\n" + body + " \r\nOrder By [Name] \r\n" +
            $"Offset {prefix}2 Rows Fetch Next {prefix}3 Rows Only", page.CommandText);
        Assert.Equal(new object[] { 9, 7 }, Values(count));
        Assert.Equal(new object[] { 9, 7, 8, 4 }, Values(page));
        Assert.Equal(cte + "Select [Name] \r\n" + body, source.CommandText);
        Assert.Equal("template-ir", count.QueryContextId);
        Assert.Equal("parent", page.ParentQueryContextId);
        Assert.Equal("Count", count.Phase);
        Assert.Equal("split", page.SplitOn);
        Assert.Null(source.PlanningBuilder);
        Assert.Null(page.PlanningBuilder);
        Assert.Equal(0, calls.Clones);
        Assert.Equal(0, calls.Renders);
    }

    public static IEnumerable<object[]> ProviderCases()
    {
        foreach (var provider in new[] { "sqlserver", "mysql", "postgresql", "sqlite" })
        foreach (var paged in new[] { false, true })
            yield return new object[] { provider, paged };
    }

    /// <summary>重命名现有或预留分页槽位，重复分页和 Count 使用同一组冻结边界。</summary>
    [Theory]
    [MemberData(nameof(ProviderCases))]
    public void ScopedTemplate_ShouldKeepProviderPaginationAndRemoveOnlyOuterPage(string provider, bool paged)
    {
        var builder = CreateBuilder(provider).Select("Name").From("samples").Where("Status", 7).OrderBy("Name");
        if (paged) builder.Skip(5).Take(2);
        var template = builder.ToTemplate(new SqlQueryScope("a"));
        var source = SqlQueryPlan.Create(template.Render(new SqlQueryScope("long_consumer")));
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4, "Id Desc"));
        var repeated = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(2, 2));
        var quote = provider == "sqlserver" ? ('[', ']') : provider == "postgresql" ? ('"', '"') : ('`', '`');
        string Q(string value) => quote.Item1 + value + quote.Item2;
        const string prefix = "@long_consumer_a_p_";
        var body = $"From {Q("samples")} \r\nWhere {Q("Status")}={prefix}0";
        var pagination = provider == "sqlserver" ? $"Offset {prefix}1 Rows Fetch Next {prefix}2 Rows Only" :
            $"Limit {prefix}2 OFFSET {prefix}1";
        var expected = $"Select {Q("Name")} \r\n{body} \r\nOrder By {Q("Name")},{Q("Id")} Desc \r\n{pagination}";
        Assert.Equal(expected, page.CommandText);
        Assert.Equal(expected, repeated.CommandText);
        Assert.Equal(new object[] { 7, 8, 4 }, Values(page));
        Assert.Equal(new object[] { 7, 2, 2 }, Values(repeated));
        Assert.Equal($"Select Count(*) \r\n{body}", SqlBuilderRuntimeBridge.CreateCountPlan(repeated).CommandText);
        Assert.Equal(new object[] { 7 }, Values(SqlBuilderRuntimeBridge.CreateCountPlan(repeated)));
        Assert.Equal(paged ? new object[] { 7, 5, 2 } : new object[] { 7 }, Values(source));
    }

    /// <summary>无参数模板同样需要为将来创建的分页参数捕获作用域。</summary>
    [Fact]
    public void ScopedTemplate_WithoutParameters_ShouldScopeFuturePageSlots()
    {
        var source = SqlQueryPlan.Create(new TestSqlBuilder().Select("Name").From("samples")
            .ToTemplate(new SqlQueryScope("root")));
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(1, 2, "Name"));
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nOrder By [Name] \r\nOffset @root_p_0 Rows Fetch Next @root_p_1 Rows Only", page.CommandText);
        Assert.Equal(new object[] { 0, 2 }, Values(page));
    }

    /// <summary>模板中的默认分页参数是值快照，调用者修改原 Pager 不影响执行。</summary>
    [Fact]
    public void Template_ShouldKeepIndependentDefaultPager()
    {
        var pager = new Pager(2, 3, "Name");
        var builder = new TestSqlBuilder().Select("Name").From("samples").Page(pager);
        var template = builder.ToTemplate();
        pager.Page = 9;
        var source = SqlQueryPlan.Create(template);
        var first = SqlBuilderRuntimeBridge.GetPlanPager(source, null);
        first.Page = 7;
        var second = SqlBuilderRuntimeBridge.GetPlanPager(source, null);
        Assert.Equal(2, second.Page);
        Assert.Equal(3, second.PageSize);
        Assert.Equal("Name", second.Order);
    }

    /// <summary>集合查询经模板和作用域变换后，Count 仍统计整个集合且移除根分页。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void UnionTemplate_ShouldKeepCountAndRepeatedPageStructure(bool scoped, bool paged)
    {
        var builder = new TestSqlBuilder().Select("Name").From("a").Where("Status", 7)
            .UnionAll(new TestSqlBuilder().Select("Name").From("b").Where("Status", 9)).OrderBy("Name");
        if (paged) builder.Skip(5).Take(2);
        var source = SqlQueryPlan.Create(builder.ToTemplate(scoped ? new SqlQueryScope("root") : null));
        var branch = paged ? 3 : 1;
        var offset = paged ? 1 : 2;
        var limit = offset + 1;
        var prefix = scoped ? "@root_p_" : "@_p_";
        var body = $"(Select [Name] \r\nFrom [a] \r\nWhere [Status]={prefix}0 \r\n) \r\nUnion All \r\n" +
            $"(Select [Name] \r\nFrom [b] \r\nWhere [Status]={prefix}{branch} \r\n)";
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 3, "Id"));
        var repeated = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(3, 4));
        var expected = body + $" \r\nOrder By [Name],[Id] \r\nOffset {prefix}{offset} Rows Fetch Next {prefix}{limit} Rows Only";
        Assert.Equal(expected, page.CommandText);
        Assert.Equal(expected, repeated.CommandText);
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(repeated);
        Assert.Equal("Select Count(*) \r\nFrom (" + body + ") As [t]", count.CommandText);
        Assert.Equal(new object[] { 7, 9 }, Values(count));
        Assert.Equal(paged ? new object[] { 7, 8, 4, 9 } : new object[] { 7, 9, 8, 4 }, Values(repeated));
    }

    /// <summary>外层页参数清理不能误删 CTE 内部自己的页参数。</summary>
    [Fact]
    public void ScopedNestedPageTemplate_ShouldKeepInnerPaginationWhenCounting()
    {
        var inner = new TestSqlBuilder().Select("Name").From("archive").OrderBy("Name").Skip(1).Take(3);
        var source = SqlQueryPlan.Create(new TestSqlBuilder().Select("Name").From("recent").With("recent", inner)
            .OrderBy("Name").Skip(5).Take(2).ToTemplate(new SqlQueryScope("outer")));
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4));
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(page);
        const string prefix = "With [recent] \r\nAs (Select [Name] \r\nFrom [archive] \r\nOrder By [Name] \r\n" +
            "Offset @outer_p_2 Rows Fetch Next @outer_p_3 Rows Only)\r\n";
        Assert.Equal(prefix + "Select [Name] \r\nFrom [recent] \r\nOrder By [Name] \r\n" +
            "Offset @outer_p_0 Rows Fetch Next @outer_p_1 Rows Only", page.CommandText);
        Assert.Equal(prefix + "Select Count(*) \r\nFrom [recent]", count.CommandText);
        Assert.Equal(new object[] { 1, 3 }, Values(count));
        Assert.Equal(new object[] { 8, 4, 1, 3 }, Values(page));
        Assert.Equal(new object[] { 5, 2, 1, 3 }, Values(source));
    }

    /// <summary>包装成 CTE 或任意文本组合后不能沿用内层 Select 的位置描述。</summary>
    [Fact]
    public void TextComposition_ShouldDiscardInnerStructure()
    {
        var template = new TestSqlBuilder().Select("Name").From("samples").ToTemplate();
        var cte = SqlQueryPlan.Create(template.AsCte("recent").Render());
        var composed = SqlQueryPlan.Create(SqlQueryTemplate.Compose(" UNION ALL ", template.CreateFragment(), template.CreateFragment()));
        Assert.Null(cte.QueryIr);
        Assert.Null(composed.QueryIr);
        Assert.Equal("recent AS (Select [Name] \r\nFrom [samples])", cte.CommandText);
        Assert.Equal("Select [Name] \r\nFrom [samples] UNION ALL Select [Name] \r\nFrom [samples]", composed.CommandText);
    }

    /// <summary>重命名后的并行派生只修改各次参数，不污染模板或其他请求。</summary>
    [Fact]
    public async Task Template_ConcurrentPages_ShouldKeepValuesIsolated()
    {
        var bytes = new byte[] { 1, 2 };
        var template = new TestSqlBuilder().Select("Name").From("samples").Where("Data", bytes)
            .ToTemplate(new SqlQueryScope("shared"));
        bytes[0] = 9;
        await Task.WhenAll(Enumerable.Range(1, 16).Select(number => Task.Run(() =>
        {
            var source = SqlQueryPlan.Create(template);
            var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(number, 2, "Name"));
            Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Data]=@shared_p_0 \r\nOrder By [Name] \r\nOffset @shared_p_1 Rows Fetch Next @shared_p_2 Rows Only", page.CommandText);
            var parameters = Assert.IsType<SqlParam[]>(page.Parameters);
            Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(parameters[0].Value));
            Assert.Equal(new object[] { (number - 1) * 2, 2 }, parameters.Skip(1).Select(parameter => parameter.Value));
            ((byte[])parameters[0].Value)[0] = 8;
        })));
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(template.Parameters[0].Value));
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
            _ => new SqliteBuilder(services)
        };
    }

    private static object[] Values(SqlQueryPlan plan) => Assert.IsType<SqlParam[]>(plan.Parameters).Select(parameter => parameter.Value).ToArray();
    private sealed class Calls { internal int Clones; internal int Renders; }
    private sealed class CountedBuilder : TestSqlBuilder
    {
        private readonly Calls _calls;
        internal CountedBuilder(Calls calls, IParameterManager parameters = null) : base(parameterManager: parameters) => _calls = calls;
        public override ISqlBuilder Clone() { _calls.Clones++; return base.Clone(); }
        public override string ToSql() { _calls.Renders++; return base.ToSql(); }
        protected override SqlBuilderBase CreateBuilder(IParameterManager parameters) => new CountedBuilder(_calls, parameters);
    }
    private sealed class RejectBuilderSource : ISqlQueryBuilderSource
    {
        public ISqlBuilder CreateIndependentSqlBuilder() => throw new InvalidOperationException("模板 IR 不应创建 Builder。");
    }
}

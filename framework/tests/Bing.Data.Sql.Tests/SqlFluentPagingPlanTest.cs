using Bing.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Moq;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bing.Data.Sql.Tests;

/// <summary>验证 Fluent 分页计划在原始查询作用域内派生。</summary>
public sealed class SqlFluentPagingPlanTest
{
    /// <summary>排序列无需出现在投影中，自动计数也不应继承旧分页。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ToPage_ShouldDeriveCountAndPageFromOriginalQuery(bool asynchronous)
    {
        SqlQueryPlan captured = null;
        var executor = new Mock<ISqlQueryPlanExecutor>();
        var result = new PagerList<string>(1, 1, 0, Array.Empty<string>());
        executor.Setup(item => item.ToPage<string>(It.IsAny<SqlQueryPlan>(), It.IsAny<IPager>(), null))
            .Returns((SqlQueryPlan plan, IPager _, int? _) => { captured = plan; return result; });
        executor.Setup(item => item.ToPageAsync<string>(It.IsAny<SqlQueryPlan>(), It.IsAny<IPager>(), null,
                It.IsAny<CancellationToken>()))
            .Returns((SqlQueryPlan plan, IPager _, int? _, CancellationToken _) =>
            { captured = plan; return Task.FromResult(result); });
        var builder = new TestSqlBuilder().Select("Name").From("samples")
            .Where("TenantId", 7).OrderBy("Id").Skip(5).Take(2);
        var query = SqlQueryRuntimeFactory.CreateQuery(executor.Object, builder);
        var pager = new Pager(2, 1, "Id");

        if (asynchronous)
            await query.ToPageAsync<string>(pager);
        else
            query.ToPage<string>(pager);

        Assert.NotNull(captured);
        Assert.False(captured.IsBuilderPlan);
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(captured);
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(captured, pager, new TestSqlBuilder());
        Assert.Equal("Select Count(*) \r\nFrom [samples] \r\nWhere [TenantId]=@_p_0", count.CommandText);
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [TenantId]=@_p_0 \r\nOrder By [Id] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
        Assert.Equal(new object[] { 7 }, Assert.IsType<SqlParam[]>(count.Parameters).Select(item => item.Value));
        Assert.Equal(new object[] { 7, 1, 1 }, Assert.IsType<SqlParam[]>(page.Parameters).Select(item => item.Value));
    }

    /// <summary>一个分页操作的 Count 与数据 SQL 必须共用同一次动态过滤结果。</summary>
    [Fact]
    public void ToPage_ShouldFreezeDynamicFilterOnceForCountAndData()
    {
        SqlQueryPlan captured = null;
        var executor = new Mock<ISqlQueryPlanExecutor>();
        executor.Setup(item => item.ToPage<string>(It.IsAny<SqlQueryPlan>(), It.IsAny<IPager>(), null))
            .Returns((SqlQueryPlan plan, IPager _, int? _) =>
            { captured = plan; return new PagerList<string>(1, 1, 0, Array.Empty<string>()); });
        var filter = new SequenceFilter();
        var services = new SqlBuilderServices(filters: new ISqlFilter[] { filter });
        var builder = new TestSqlBuilder(services, TestDialect.Instance).Select("Name").From<FilterRow>("s");
        var query = SqlQueryRuntimeFactory.CreateQuery(executor.Object, builder);
        var pager = new Pager(1, 1, "Id");

        query.ToPage<string>(pager);
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(captured);
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(captured, pager, new TestSqlBuilder());

        Assert.Equal(1, filter.Calls);
        Assert.Equal("Select Count(*) \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0", count.CommandText);
        Assert.Equal("Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0 \r\nOrder By [Id] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
        Assert.Equal(1, Assert.Single(Assert.IsType<SqlParam[]>(count.Parameters)).Value);
        Assert.Equal(1, Assert.IsType<SqlParam[]>(page.Parameters)[0].Value);
    }

    /// <summary>显式冻结后的派生查询使用冻结时的原始作用域，并隔离后续修改。</summary>
    [Fact]
    public void Freeze_ShouldPreserveSourceScopeForCountAndPage()
    {
        var builder = new TestSqlBuilder().Select("Name").From("samples")
            .Where("TenantId", 7).OrderBy("Id").Skip(5).Take(2);
        var frozen = SqlQueryPlan.Create(builder).Freeze();
        builder.Where("Status", 1);
        var pager = new Pager(2, 1, "Id");

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(frozen);
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(frozen, pager, new TestSqlBuilder());
        var repeated = SqlBuilderRuntimeBridge.CreatePagePlan(frozen, pager, new TestSqlBuilder());

        Assert.Equal("Select Count(*) \r\nFrom [samples] \r\nWhere [TenantId]=@_p_0", count.CommandText);
        const string pageSql = "Select [Name] \r\nFrom [samples] \r\nWhere [TenantId]=@_p_0 \r\nOrder By [Id] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only";
        Assert.Equal(pageSql, page.CommandText);
        Assert.Equal(pageSql, repeated.CommandText);
        Assert.Equal(new object[] { 7 }, Assert.IsType<SqlParam[]>(count.Parameters).Select(item => item.Value));
        Assert.Equal(new object[] { 7, 1, 1 }, Assert.IsType<SqlParam[]>(page.Parameters).Select(item => item.Value));
    }

    /// <summary>显式冻结只计算一次动态过滤，并让 Count 与数据页共享结果。</summary>
    [Fact]
    public void Freeze_ShouldUseOneDynamicFilterResultForCountAndPage()
    {
        var filter = new SequenceFilter();
        var services = new SqlBuilderServices(filters: new ISqlFilter[] { filter });
        var builder = new TestSqlBuilder(services, TestDialect.Instance).Select("Name").From<FilterRow>("s");
        var frozen = SqlQueryPlan.Create(builder).Freeze();
        var pager = new Pager(1, 1, "Id");

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(frozen);
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(frozen, pager, new TestSqlBuilder());

        Assert.Equal(1, filter.Calls);
        Assert.Equal("Select Count(*) \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0", count.CommandText);
        Assert.Equal("Select [Name] \r\nFrom [samples] As [s] \r\nWhere [s].[TenantId]=@_p_0 \r\nOrder By [Id] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
        Assert.Equal(1, Assert.Single(Assert.IsType<SqlParam[]>(count.Parameters)).Value);
        Assert.Equal(1, Assert.IsType<SqlParam[]>(page.Parameters)[0].Value);
    }

    /// <summary>派生计划应使用冻结时的数组值，即使调用方后来修改了输入。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PagingPlan_ShouldSnapshotMutableParameterValues(bool fluentPaging)
    {
        var value = new byte[] { 1, 2 };
        var builder = new TestSqlBuilder().Select("Name").From("samples")
            .Where("Payload", value).OrderBy("Id");
        SqlQueryPlan frozen;
        if (fluentPaging)
        {
            frozen = null;
            var executor = new Mock<ISqlQueryPlanExecutor>();
            executor.Setup(item => item.ToPage<string>(It.IsAny<SqlQueryPlan>(), It.IsAny<IPager>(), null))
                .Returns((SqlQueryPlan plan, IPager _, int? _) =>
                { frozen = plan; return new PagerList<string>(1, 1, 0, Array.Empty<string>()); });
            SqlQueryRuntimeFactory.CreateQuery(executor.Object, builder).ToPage<string>(new Pager(1, 1, "Id"));
        }
        else
            frozen = SqlQueryPlan.Create(builder).Freeze();

        value[0] = 9;
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(frozen);
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(frozen, new Pager(1, 1, "Id"), new TestSqlBuilder());

        Assert.Equal("Select Count(*) \r\nFrom [samples] \r\nWhere [Payload]=@_p_0", count.CommandText);
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Payload]=@_p_0 \r\nOrder By [Id] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(Assert.IsType<SqlParam[]>(count.Parameters)[0].Value));
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(Assert.IsType<SqlParam[]>(page.Parameters)[0].Value));
    }

    /// <summary>同一冻结计划可并发派生不同的页，不共享分页参数。</summary>
    [Fact]
    public async Task Freeze_ShouldAllowConcurrentIndependentPaging()
    {
        var value = new byte[] { 1, 2 };
        var frozen = SqlQueryPlan.Create(new TestSqlBuilder().Select("Name").From("samples")
            .Where("Payload", value).OrderBy("Id")).Freeze();
        value[0] = 9;

        var pages = await Task.WhenAll(Enumerable.Range(1, 16).Select(index => Task.Run(() =>
            SqlBuilderRuntimeBridge.CreatePagePlan(frozen, new Pager(index, 1, "Id"), new TestSqlBuilder()))));

        for (var index = 0; index < pages.Length; index++)
        {
            Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Payload]=@_p_0 \r\nOrder By [Id] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", pages[index].CommandText);
            var parameters = Assert.IsType<SqlParam[]>(pages[index].Parameters);
            Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(parameters[0].Value));
            Assert.Equal(index, parameters[1].Value);
            Assert.Equal(1, parameters[2].Value);
        }
    }

    /// <summary>恢复冻结值时应保留参数元数据，且不重复执行方言转换。</summary>
    [Fact]
    public void Freeze_ShouldPreserveParameterMetadataWithoutReconversion()
    {
        var dialect = new BinaryCountingDialect();
        var value = new byte[] { 1, 2 };
        var builder = new TestSqlBuilder(dialect).Select("Name").From("samples")
            .Where("Payload", value).OrderBy("Id");
        var manager = (IAdvancedParameterManager)((ISqlCommonPartAccessor)builder).ParameterManager;
        manager.Add(new SqlParam("@_p_0", value, System.Data.DbType.Binary)
        { OriginalValue = value, IsProviderValue = true });
        var frozen = SqlQueryPlan.Create(builder).Freeze();
        var conversions = dialect.BinaryConversions;
        value[0] = 9;

        var page = SqlBuilderRuntimeBridge.CreatePagePlan(frozen, new Pager(1, 1, "Id"), new TestSqlBuilder(dialect));

        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Payload]=@_p_0 \r\nOrder By [Id] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
        var parameter = Assert.IsType<SqlParam[]>(page.Parameters)[0];
        Assert.Equal(System.Data.DbType.Binary, parameter.DbType);
        Assert.True(parameter.IsProviderValue);
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(parameter.Value));
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(parameter.OriginalValue));
        Assert.Equal(conversions, dialect.BinaryConversions);
    }

    /// <summary>派生的内置参数管理器也应恢复已转换值，不重复执行方言转换。</summary>
    [Fact]
    public void Freeze_ShouldNotReconvertParametersForDerivedParameterManager()
    {
        var dialect = new BinaryCountingDialect();
        var value = new byte[] { 1, 2 };
        var builder = new TestSqlBuilder(dialect, parameterManager: new DerivedParameterManager(dialect))
            .Select("Name").From("samples").Where("Payload", value).OrderBy("Id");
        var frozen = SqlQueryPlan.Create(builder).Freeze();
        var conversions = dialect.BinaryConversions;
        value[0] = 9;

        var page = SqlBuilderRuntimeBridge.CreatePagePlan(frozen, new Pager(1, 1, "Id"), new TestSqlBuilder(dialect));

        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Payload]=@_p_0 \r\nOrder By [Id] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", page.CommandText);
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(Assert.IsType<SqlParam[]>(page.Parameters)[0].Value));
        Assert.Equal(conversions, dialect.BinaryConversions);
    }

    [Table("samples")]
    private sealed class FilterRow
    {
        public int TenantId { get; set; }
    }

    private sealed class SequenceFilter : ISqlFilter
    {
        public int Calls { get; private set; }

        public void Filter(SqlFilterContext context)
        {
            Calls++;
            var source = Assert.Single(context.Sources);
            context.AddPredicate(source, context.GetColumn(source, nameof(FilterRow.TenantId)), Calls);
        }
    }

    private sealed class BinaryCountingDialect : DialectBase
    {
        public int BinaryConversions { get; private set; }

        public override object GetParamValue(object value)
        {
            if (value is byte[])
                BinaryConversions++;
            return value;
        }
    }

    private sealed class DerivedParameterManager : ParameterManager
    {
        public DerivedParameterManager(IDialect dialect) : base(dialect) { }

        private DerivedParameterManager(DerivedParameterManager source) : base(source) { }

        public override IParameterManager Clone() => new DerivedParameterManager(this);
    }
}

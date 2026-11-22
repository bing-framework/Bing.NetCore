using Bing.Data;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>验证移除旧分页参数时保留已经转换的查询参数。</summary>
public sealed class SqlPagingParameterConversionTest
{
    /// <summary>派生总数和新页面时，已有筛选参数不能再次执行方言转换。</summary>
    [Fact]
    public void Freeze_WithExistingPage_ShouldPreserveConvertedFilterParameters()
    {
        var dialect = new NonIdempotentDialect();
        var builder = new TestSqlBuilder(dialect).Select("Name").From("samples")
            .Where("Name", "one").Where("Payload", new byte[] { 5, 6 })
            .OrderBy("Id").Skip(5).Take(2);
        var frozen = SqlQueryPlan.Create(builder).Freeze();
        var original = Assert.IsType<SqlParam[]>(frozen.Parameters);

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(frozen);
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(frozen, new Pager(2, 1, "Id"), new TestSqlBuilder(dialect));

        Assert.Equal("Select Count(*) \r\nFrom [samples] \r\nWhere [Name]=@_p_0 And [Payload]=@_p_1", count.CommandText);
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Name]=@_p_0 And [Payload]=@_p_1 \r\nOrder By [Id] \r\nOffset @_p_2 Rows Fetch Next @_p_3 Rows Only", page.CommandText);
        var countParameters = Assert.IsType<SqlParam[]>(count.Parameters);
        var pageParameters = Assert.IsType<SqlParam[]>(page.Parameters);
        Assert.Equal(original[0].Value, countParameters[0].Value);
        Assert.Equal(original[0].Value, pageParameters[0].Value);
        Assert.Equal(Assert.IsType<byte[]>(original[1].Value), Assert.IsType<byte[]>(countParameters[1].Value));
        Assert.Equal(Assert.IsType<byte[]>(original[1].Value), Assert.IsType<byte[]>(pageParameters[1].Value));
        Assert.Equal(1, pageParameters[2].Value);
        Assert.Equal(1, pageParameters[3].Value);
    }

    /// <summary>直接清除并重新设置分页也不能改变筛选参数或参数命名。</summary>
    [Fact]
    public void ClearPageParams_ShouldPreserveConvertedValuesAndReuseAvailableNames()
    {
        var builder = new TestSqlBuilder(new NonIdempotentDialect()).Select("Name").From("samples")
            .Where("Name", "one").OrderBy("Id").Skip(5).Take(2);

        builder.ClearPageParams().Skip(1).Take(3);

        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Name]=@_p_0 \r\nOrder By [Id] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", builder.ToSql());
        Assert.Equal("one!", builder.GetParams()["@_p_0"]);
        Assert.Equal(1, builder.GetParams()["@_p_1"]);
        Assert.Equal(3, builder.GetParams()["@_p_2"]);
    }

    /// <summary>分组计数使用子查询时，参数应与原冻结查询一致。</summary>
    [Fact]
    public void Freeze_WithGroupedCount_ShouldPreserveConvertedFilterParameter()
    {
        var dialect = new NonIdempotentDialect();
        var builder = new TestSqlBuilder(dialect).Select("Name").From("samples")
            .Where("Name", "one").GroupBy("Name").OrderBy("Name").Skip(5).Take(2);
        var frozen = SqlQueryPlan.Create(builder).Freeze();

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(frozen);

        Assert.Equal("Select Count(*) \r\nFrom (Select [Name] \r\nFrom [samples] \r\nWhere [Name]=@_p_0 \r\nGroup By [Name]) As [t]", count.CommandText);
        Assert.Equal("one!", Assert.Single(Assert.IsType<SqlParam[]>(count.Parameters)).Value);
    }

    /// <summary>分组计数应保留片段参数的逻辑键和值。</summary>
    [Fact]
    public void Freeze_WithGroupedFragment_ShouldKeepLogicalParameterBinding()
    {
        var fragment = new SqlQueryFragment("select @name as Name", new[] { new SqlParam("name", "one") })
            .WithParameterKey("name", new SqlParameterKey("request", "name"));
        var builder = new TestSqlBuilder(new NonIdempotentDialect()).Select("Name").From(fragment, "items")
            .GroupBy("Name").OrderBy("Name").Skip(1).Take(2);
        var frozen = SqlQueryPlan.Create(builder).Freeze();

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(frozen);

        Assert.Equal("Select Count(*) \r\nFrom (Select [Name] \r\nFrom (select @name as Name) As [items] \r\nGroup By [Name]) As [t]", count.CommandText);
        Assert.Equal("one!", Assert.Single(Assert.IsType<SqlParam[]>(count.Parameters)).Value);
    }

    /// <summary>去重和聚合计数包装应保留原筛选参数和完整 SQL。</summary>
    [Theory]
    [InlineData(false, "Select Distinct [Name]")]
    [InlineData(true, "Select Count(*) As [Total]")]
    public void Freeze_WithDistinctOrAggregate_ShouldPreserveConvertedFilterParameter(bool aggregate, string select)
    {
        var builder = new TestSqlBuilder(new NonIdempotentDialect());
        if (aggregate)
            builder.CountAll("Total");
        else
            builder.Distinct().Select("Name");
        builder.From("samples").Where("Name", "one").OrderBy("Name").Skip(1).Take(2);
        var frozen = SqlQueryPlan.Create(builder).Freeze();

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(frozen);

        Assert.Equal($"Select Count(*) \r\nFrom ({select} \r\nFrom [samples] \r\nWhere [Name]=@_p_0) As [t]", count.CommandText);
        Assert.Equal("one!", Assert.Single(Assert.IsType<SqlParam[]>(count.Parameters)).Value);
    }

    /// <summary>联合查询包装计数时，保留两个分支和各自的参数绑定。</summary>
    [Fact]
    public void Freeze_WithUnion_ShouldPreserveConvertedFilterParameters()
    {
        var dialect = new NonIdempotentDialect();
        var branch = new TestSqlBuilder(dialect).Select("Name").From("archive").Where("Id", 7);
        var builder = new TestSqlBuilder(dialect).Select("Name").From("samples").Where("Name", "one")
            .Union(branch).OrderBy("Name").Skip(1).Take(2);
        var frozen = SqlQueryPlan.Create(builder).Freeze();

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(frozen);

        Assert.Equal("Select Count(*) \r\nFrom ((Select [Name] \r\nFrom [samples] \r\nWhere [Name]=@_p_0 \r\n) \r\nUnion \r\n(Select [Name] \r\nFrom [archive] \r\nWhere [Id]=@_p_3 \r\n)) As [t]", count.CommandText);
        var parameters = Assert.IsType<SqlParam[]>(count.Parameters);
        Assert.Equal(2, parameters.Length);
        Assert.Equal("one!", parameters[0].Value);
        Assert.Equal(7, parameters[1].Value);
    }

    private sealed class NonIdempotentDialect : DialectBase
    {
        public override object GetParamValue(object value) => value switch
        {
            string text => text + "!",
            byte[] bytes => bytes.Select(item => (byte)(item + 1)).ToArray(),
            _ => value
        };
    }
}

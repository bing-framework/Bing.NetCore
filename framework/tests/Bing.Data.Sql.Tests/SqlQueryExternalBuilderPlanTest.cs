using Bing.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>验证直接实现 ISqlBuilder 的第三方 Builder 可通过冻结规划源派生查询计划。</summary>
public sealed class SqlQueryExternalBuilderPlanTest
{
    /// <summary>重复分页和计数应克隆非基类 Builder、累加排序并保留来源计划。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FrozenThirdPartyBuilder_WhenPagedRepeatedlyAndCounted_ShouldRebindAndKeepSource(bool explicitFreeze)
    {
        var innerBuilder = new TestSqlBuilder();
        innerBuilder.Select("Name").From("samples").Where("Status", 7)
            .OrderBy("Id").Skip(5).Take(2);
        var sourceBuilder = Wrap(innerBuilder);
        Assert.False(sourceBuilder.Object is SqlBuilderBase);
        var source = SqlQueryPlan.Create(sourceBuilder.Object);
        if (explicitFreeze) Assert.Same(source, source.Freeze());
        var sourceSql = "Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0 \r\nOrder By [Id] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only";

        var firstPage = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4, "Name"));
        var secondPage = SqlBuilderRuntimeBridge.CreatePagePlan(firstPage, new Pager(2, 3, "Status Desc"));
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(secondPage);

        Assert.Equal(sourceSql, source.CommandText);
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0 \r\nOrder By [Id],[Name] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", firstPage.CommandText);
        Assert.Equal("Select [Name] \r\nFrom [samples] \r\nWhere [Status]=@_p_0 \r\nOrder By [Id],[Name],[Status] Desc \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only", secondPage.CommandText);
        Assert.Equal(new object[] { 7, 8, 4 }, Assert.IsType<SqlParam[]>(firstPage.Parameters).Select(parameter => parameter.Value));
        Assert.Equal(new object[] { 7, 3, 3 }, Assert.IsType<SqlParam[]>(secondPage.Parameters).Select(parameter => parameter.Value));
        Assert.Equal("Select Count(*) \r\nFrom [samples] \r\nWhere [Status]=@_p_0", count.CommandText);
        Assert.Equal(new object[] { 7 }, Assert.IsType<SqlParam[]>(count.Parameters).Select(parameter => parameter.Value));
        Assert.Equal(new object[] { 7, 8, 4 }, Assert.IsType<SqlParam[]>(firstPage.Parameters).Select(parameter => parameter.Value));
        Assert.Equal(sourceSql, source.CommandText);
        Assert.Equal(new object[] { 7, 5, 2 }, Assert.IsType<SqlParam[]>(source.Parameters).Select(parameter => parameter.Value));
        Assert.Equal(7, sourceBuilder.Object.GetParams()["@_p_0"]);
        Assert.Equal(sourceSql, sourceBuilder.Object.ToSql());
    }

    private static Mock<ISqlBuilder> Wrap(TestSqlBuilder builder)
    {
        var mock = new Mock<ISqlBuilder>();
        mock.SetupGet(item => item.Provider).Returns(builder.Provider);
        mock.SetupGet(item => item.OperationKind).Returns(SqlOperationKind.Select);
        mock.SetupGet(item => item.Pager).Returns(() => builder.Pager);
        mock.SetupGet(item => item.SelectClause).Returns(() => builder.SelectClause);
        mock.SetupGet(item => item.GroupByClause).Returns(() => builder.GroupByClause);
        mock.SetupGet(item => item.OrderByClause).Returns(() => builder.OrderByClause);
        mock.Setup(item => item.Clone()).Returns(() => Wrap((TestSqlBuilder)builder.Clone()).Object);
        mock.Setup(item => item.ToSql()).Returns(() => builder.ToSql());
        mock.Setup(item => item.ClearOrderBy()).Returns(() =>
        {
            builder.ClearOrderBy();
            return mock.Object;
        });
        mock.Setup(item => item.ClearPageParams()).Returns(() =>
        {
            builder.ClearPageParams();
            return mock.Object;
        });
        mock.Setup(item => item.ClearSelect()).Returns(() =>
        {
            builder.ClearSelect();
            return mock.Object;
        });
        mock.Setup(item => item.Page(It.IsAny<IPager>())).Returns((IPager pager) =>
        {
            builder.Page(pager);
            return mock.Object;
        });
        mock.As<ISqlCommonPartAccessor>()
            .SetupGet(item => item.ParameterManager)
            .Returns(() => ((ISqlCommonPartAccessor)builder).ParameterManager);
        return mock;
    }
}

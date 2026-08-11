using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// SQL Builder 子查询组合测试。
/// </summary>
public class SqlBuilderSubqueryCompositionTest
{
    /// <summary>
    /// 测试目的：子查询 From 在当前 Mutation 状态不允许查询转换时，应在渲染和合并参数前失败。
    /// </summary>
    [Fact]
    public void From_WhenBuilderIsMutation_ShouldRejectBeforeMergingSubqueryParameters()
    {
        // Arrange
        var outer = new TestSqlBuilder()
            .Update(new SqlTableReference { TableName = "outer" })
            .Set("Name", "existing");
        var subquery = new TestSqlBuilder()
            .Select("*")
            .From("source")
            .Where("Id", 1);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => outer.From(subquery, "summary"));

        // Assert
        Assert.Equal("当前 Builder 已处于 Update 状态，不能调用 Where。", exception.Message);
        Assert.Equal(SqlOperationKind.Update, outer.OperationKind);
        Assert.Equal(new object[] { "existing" }, outer.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：子查询 Join 在当前 Mutation 状态不允许查询转换时，应在渲染和合并参数前失败。
    /// </summary>
    [Fact]
    public void Join_WhenBuilderIsMutation_ShouldRejectBeforeMergingSubqueryParameters()
    {
        // Arrange
        var outer = new TestSqlBuilder()
            .Update(new SqlTableReference { TableName = "outer" })
            .Set("Name", "existing");
        var subquery = new TestSqlBuilder()
            .Select("*")
            .From("source")
            .Where("Id", 1);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => outer.Join(subquery, "summary"));

        // Assert
        Assert.Equal("当前 Builder 已处于 Update 状态，不能调用 Where。", exception.Message);
        Assert.Equal(SqlOperationKind.Update, outer.OperationKind);
        Assert.Equal(new object[] { "existing" }, outer.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：子查询别名与原始根表冲突时，应在渲染和合并参数前失败。
    /// </summary>
    [Fact]
    public void Join_WhenSubqueryAliasDuplicatesRawFromAlias_ShouldRejectBeforeMergingParameters()
    {
        // Arrange
        var outer = new TestSqlBuilder()
            .Select("*")
            .From("outer", "summary")
            .Where("Id", 1);
        var subquery = new TestSqlBuilder()
            .Select("*")
            .From("source")
            .Where("Id", 2);
        var expectedSql = outer.ToSql();
        var expectedParameters = outer.GetParams();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => outer.Join(subquery, "summary"));

        // Assert
        Assert.Equal("查询中已存在表别名 \"summary\"。", exception.Message);
        Assert.Equal(expectedSql, outer.ToSql());
        Assert.Equal(expectedParameters, outer.GetParams());
    }

    /// <summary>
    /// 测试目的：子查询参数超过上限或别名重复时，失败不得污染外层参数、SQL 或别名注册状态。
    /// </summary>
    [Fact]
    public void Join_WhenSubqueryParameterLimitExceeded_ShouldKeepParametersSqlAndAliasStateUnchanged()
    {
        // Arrange
        var parameterManager = new ParameterLimitManager(new ParameterManager(TestDialect.Instance), 2, "test");
        var outer = new TestSqlBuilder(parameterManager: parameterManager)
            .Select("*")
            .From("outer")
            .Where("Id", 1);
        var oversized = new TestSqlBuilder()
            .Select("*")
            .From("source")
            .Where("Id", 2)
            .Where("Name", "invalid");
        var expectedSql = outer.ToSql();
        var expectedParameters = outer.GetParams();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => outer.Join(oversized, "summary"));

        // Assert
        Assert.Equal("SQL Provider 'test' 的参数数量超出上限。当前参数数量: 2；尝试添加后数量: 3；最大参数数量: 2。", exception.Message);
        Assert.Equal(expectedSql, outer.ToSql());
        Assert.Equal(expectedParameters, outer.GetParams());

        // Act
        var valid = new TestSqlBuilder()
            .Select("*")
            .From("source")
            .Where("Id", 2);
        outer.Join(valid, "summary");

        // Assert
        Assert.Equal(new object[] { 1, 2 }, outer.GetParams().Values.ToArray());
        Assert.Equal("Select * \r\nFrom [outer] \r\nJoin (Select * \r\nFrom [source] \r\nWhere [Id]=@_p_1) As [summary] \r\nWhere [Id]=@_p_0", outer.ToSql());
    }

    /// <summary>
    /// 测试目的：子查询参数合并在副本预演成功后，应保留重命名并生成完整 SQL。
    /// </summary>
    [Fact]
    public void RenderSubquery_WhenParameterNamesConflict_ShouldUsePlannedNamesWithoutChangingSource()
    {
        // Arrange
        var outer = new TestSqlBuilder()
            .Select("*")
            .From("outer")
            .Where("Id", 1);
        var source = new TestSqlBuilder()
            .Select("*")
            .From("source")
            .Where("Id", 2);
        var sourceSql = source.ToSql();

        // Act
        var sql = outer.RenderSubqueryForTest(source);

        // Assert
        Assert.Equal("Select * \r\nFrom [source] \r\nWhere [Id]=@_p_1", sql);
        Assert.Equal("Select * \r\nFrom [source] \r\nWhere [Id]=@_p_0", sourceSql);
        Assert.Equal(new object[] { 1, 2 }, outer.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：严格 DTO 派生根来源在 Clone 后应保留投影白名单和 SQL，原 Builder 清除来源后应释放其别名且不影响副本。
    /// </summary>
    [Fact]
    public void From_WhenStrictDtoSubqueryIsClonedAndCleared_ShouldKeepCloneStateAndReleaseSourceAlias()
    {
        // Arrange
        var child = new TestSqlBuilder()
            .Select("Id")
            .From("source");
        var subquery = new SqlSubquery<DerivedSummary>(child, "summary", new[] { nameof(DerivedSummary.Id) },
            "test.sqlserver", null, null, null, null, null);
        var source = new TestSqlBuilder();
        ((FromClause)source.FromClause).From(subquery);
        source.Select("summary.Id");

        // Act
        var clone = (TestSqlBuilder)source.Clone();
        clone.Where("summary.Id", 2);
        source.ClearFrom()
            .ClearSelect()
            .Select("*")
            .From("orders", "summary");

        // Assert
        var cloneFrom = (FromClause)clone.FromClause;
        Assert.Equal(new[] { nameof(DerivedSummary.Id) }, cloneFrom.Sources.Single().ProjectedMembers);
        Assert.Equal("Select [summary].[Id] \r\nFrom (Select [Id] \r\nFrom [source]) As [summary] \r\nWhere [summary].[Id]=@_p_0",
            clone.ToSql());
        Assert.Equal(2, clone.GetParams().Values.Single());
        Assert.Equal("Select * \r\nFrom [orders] As [summary]", source.ToSql());
        Assert.Empty(source.GetParams());
    }

    /// <summary>
    /// 测试目的：严格 DTO 派生 Join 在 Clone 后应保留投影白名单，两个 Builder 清除 Join 后均应独立释放其别名。
    /// </summary>
    [Fact]
    public void Join_WhenStrictDtoSubqueryIsClonedAndCleared_ShouldKeepCloneStateAndReleaseAlias()
    {
        // Arrange
        var child = new TestSqlBuilder()
            .Select("Id")
            .From("source");
        var subquery = new SqlSubquery<DerivedSummary>(child, "summary", new[] { nameof(DerivedSummary.Id) },
            "test.sqlserver", null, null, null, null, null);
        var source = new TestSqlBuilder()
            .Select("order.Id")
            .From("orders", "order");
        ((JoinClause)source.JoinClause).Join(subquery);
        var clone = (TestSqlBuilder)source.Clone();

        // Act
        var cloneSource = ((JoinClause)clone.JoinClause).GetTypedSources().Single();
        var cloneSql = clone.ToSql();
        source.ClearJoin().Join("Invoices", "summary");
        clone.ClearJoin().Join("Payments", "summary");

        // Assert
        Assert.Equal("summary", cloneSource.Alias);
        Assert.Equal(new[] { nameof(DerivedSummary.Id) }, cloneSource.ProjectedMembers);
        Assert.Equal("Select [order].[Id] \r\nFrom [orders] As [order] \r\nJoin (Select [Id] \r\nFrom [source]) As [summary]", cloneSql);
        Assert.Equal("Select [order].[Id] \r\nFrom [orders] As [order] \r\nJoin [Invoices] As [summary]", source.ToSql());
        Assert.Equal("Select [order].[Id] \r\nFrom [orders] As [order] \r\nJoin [Payments] As [summary]", clone.ToSql());
        Assert.Empty(source.GetParams());
        Assert.Empty(clone.GetParams());
    }

    /// <summary>
    /// 严格派生表的最小投影模型。
    /// </summary>
    private sealed class DerivedSummary
    {
        /// <summary>
        /// 标识。
        /// </summary>
        public int Id { get; set; }
    }
}
namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// Sql生成器测试 - 集合操作
/// </summary>
public partial class SqlBuilderTest
{
    #region Union

    /// <summary>
    /// 测试联合操作
    /// </summary>
    [Fact]
    public void Test_Union_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("(Select [a],[b] ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Where [c]=@_p_0 ");
        result.AppendLine(") ");
        result.AppendLine("Union ");
        result.AppendLine("(Select [a],[b] ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [c]=@_p_1 ");
        result.Append(")");

        //执行
        var builder2 = _builder.New().Select("a,b").From("Test2").Where("c", 1);
        _builder.Select("a,b").From("Test").Where("c", 2).Union(builder2);
        _output.WriteLine(builder2.ToSql());
        _output.WriteLine(result.ToString());
        _output.WriteLine(_builder.ToSql());

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParam("@_p_0"));
        Assert.Equal(1, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试联合操作 - 排序
    /// </summary>
    [Fact]
    public void Test_Union_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("(Select [a],[b] ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Where [c]=@_p_0 ");
        result.AppendLine(") ");
        result.AppendLine("Union ");
        result.AppendLine("(Select [a],[b] ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [c]=@_p_1 ");
        result.AppendLine(") ");
        result.Append("Order By [a]");

        //执行
        var builder2 = _builder.New().Select("a,b").From("Test2").Where("c", 1);
        _builder.Select("a,b").From("Test").Where("c", 2).OrderBy("a").Union(builder2);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParam("@_p_0"));
        Assert.Equal(1, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试联合操作 - 排序 - 联合查询中带排序被过滤
    /// </summary>
    [Fact]
    public void Test_Union_3()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("(Select [a],[b] ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Where [c]=@_p_0 ");
        result.AppendLine(") ");
        result.AppendLine("Union ");
        result.AppendLine("(Select [a],[b] ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [c]=@_p_1 ");
        result.AppendLine(") ");
        result.Append("Order By [a]");

        //执行
        var builder2 = _builder.New().Select("a,b").From("Test2").Where("c", 1).OrderBy("b");
        _builder.Select("a,b").From("Test").Where("c", 2).OrderBy("a").Union(builder2);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParam("@_p_0"));
        Assert.Equal(1, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试目的：已渲染的 Union 子查询在克隆后应复用冻结的参数重命名，不遗留未引用参数。
    /// </summary>
    [Fact]
    public void Union_WhenRenderedBeforeClone_ShouldPreserveSubqueryParameterNames()
    {
        // Arrange
        var union = _builder.New().Select("Id").From("ArchivedOrders").Where("Status", "archived");
        _builder.Select("Id").From("Orders").Where("Status", "active").Union(union);
        var expectedSql = _builder.ToSql();
        var expectedParameters = _builder.GetParams().OrderBy(item => item.Key).ToArray();

        // Act
        var clone = _builder.Clone();
        var cloneSql = clone.ToSql();

        // Assert
        Assert.Equal(expectedSql, cloneSql);
        Assert.Equal(expectedParameters, clone.GetParams().OrderBy(item => item.Key).ToArray());
        Assert.DoesNotContain("@_p_2", cloneSql);
        Assert.False(clone.GetParams().ContainsKey("@_p_2"));
    }

    /// <summary>
    /// 测试目的：Union 应仅清理自身克隆分支的排序和分页，且后续修改输入 Builder 不得污染已组合查询。
    /// </summary>
    [Fact]
    public void Union_WhenInputBuilderHasOrderAndPage_ShouldKeepInputStateAndIsolateSubsequentChanges()
    {
        // Arrange
        const string expected = "(Select [a] \r\nFrom [Test] \r\n) \r\nUnion \r\n(Select [a] \r\nFrom [Test2] \r\n)";
        const string expectedInput = "Select [a],[c] \r\nFrom [Test2] \r\nOrder By [b] \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only";
        var union = _builder.New().Select("a").From("Test2").OrderBy("b").Skip(2).Take(3);

        // Act
        _builder.Select("a").From("Test").Union(union);
        union.Select("c");

        // Assert
        Assert.Equal(expected, _builder.ToSql());
        Assert.Equal(expectedInput, union.ToSql());
        Assert.Equal(2, union.GetParam("@_p_0"));
        Assert.Equal(3, union.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试目的：Intersect 组合查询应稳定重命名子查询参数，并按完整 SQL 顺序渲染交集操作。
    /// </summary>
    [Fact]
    public void Intersect_WhenQueriesHaveParameters_ShouldRenderExpectedSql()
    {
        // Arrange
        const string expected = "(Select [Id] \r\nFrom [Orders] \r\nWhere [Status]=@_p_0 \r\n) \r\nIntersect \r\n(Select [Id] \r\nFrom [ArchivedOrders] \r\nWhere [Status]=@_p_1 \r\n)";
        var intersect = _builder.New().Select("Id").From("ArchivedOrders").Where("Status", "archived");

        // Act
        _builder.Select("Id").From("Orders").Where("Status", "active").Intersect(intersect);

        // Assert
        Assert.Equal(expected, _builder.ToSql());
        Assert.Equal("active", _builder.GetParam("@_p_0"));
        Assert.Equal("archived", _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试目的：Except 组合查询应稳定重命名子查询参数，并按完整 SQL 顺序渲染差集操作。
    /// </summary>
    [Fact]
    public void Except_WhenQueriesHaveParameters_ShouldRenderExpectedSql()
    {
        // Arrange
        const string expected = "(Select [Id] \r\nFrom [Orders] \r\nWhere [Status]=@_p_0 \r\n) \r\nExcept \r\n(Select [Id] \r\nFrom [ArchivedOrders] \r\nWhere [Status]=@_p_1 \r\n)";
        var except = _builder.New().Select("Id").From("ArchivedOrders").Where("Status", "archived");

        // Act
        _builder.Select("Id").From("Orders").Where("Status", "active").Except(except);

        // Assert
        Assert.Equal(expected, _builder.ToSql());
        Assert.Equal("active", _builder.GetParam("@_p_0"));
        Assert.Equal("archived", _builder.GetParam("@_p_1"));
    }


    #endregion
}

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// Sql生成器测试 - 起始子句
/// </summary>
public partial class SqlBuilderTest
{
    #region Cte

    /// <summary>
    /// 测试CTE
    /// </summary>
    [Fact]
    public void Test_With_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("With [Test] ");
        result.AppendLine("As (Select [a],[b] ");
        result.AppendLine("From [Test2])");
        result.AppendLine("Select [a],[b] ");
        result.Append("From [Test]");

        //执行
        var builder2 = _builder.New().Select("a,b").From("Test2");
        _builder.Select("a,b").From("Test").With("Test", builder2);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 测试CTE - 两个CTE
    /// </summary>
    [Fact]
    public void Test_With_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("With [Test] ");
        result.AppendLine("As (Select [a],[b] ");
        result.AppendLine("From [Test2]),");
        result.AppendLine("[Test3] ");
        result.AppendLine("As (Select [a],[b] ");
        result.AppendLine("From [Test3])");
        result.AppendLine("Select [a],[b] ");
        result.Append("From [Test]");

        //执行
        var builder2 = _builder.New().Select("a,b").From("Test2");
        var builder3 = _builder.New().Select("a,b").From("Test3");
        _builder.Select("a,b").From("Test").With("Test", builder2).With("Test3", builder3);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 测试目的：注册 CTE 后修改输入 Builder 时，已组合查询应保持 CTE 注册时的独立快照。
    /// </summary>
    [Fact]
    public void With_WhenInputBuilderChangesAfterRegistration_ShouldKeepCteSnapshotIsolated()
    {
        // Arrange
        const string expected = "With [active_users] \r\nAs (Select [Id] \r\nFrom [Users])\r\nSelect [Id] \r\nFrom [active_users]";
        var cte = _builder.New().Select("Id").From("Users");

        // Act
        _builder.Select("Id").From("active_users").With("active_users", cte);
        cte.Select("Name");

        // Assert
        Assert.Equal(expected, _builder.ToSql());
        Assert.Equal("Select [Id],[Name] \r\nFrom [Users]", cte.ToSql());
    }

    /// <summary>
    /// 测试目的：延迟渲染的 CTE 连续参数与主查询冲突时，应分别重命名并在重复渲染时保持稳定。
    /// </summary>
    [Fact]
    public void With_WhenCteHasSequentialConflictingParameters_ShouldRenameEachTokenOnce()
    {
        // Arrange
        const string expected = "With [selected] \r\nAs (Select * \r\nFrom [Child] \r\nWhere [Name]=@_p_1 And [Age]=@_p_2)\r\nSelect * \r\nFrom [Parent] \r\nWhere [Id]=@_p_0";
        var cte = _builder.New().From("Child").Where("Name", "child-name").Where("Age", 18);

        // Act
        _builder.From("Parent").Where("Id", 1).With("selected", cte);
        var firstSql = _builder.ToSql();
        var secondSql = _builder.ToSql();

        // Assert
        Assert.Equal(expected, firstSql);
        Assert.Equal(firstSql, secondSql);
        Assert.Equal(1, _builder.GetParam("@_p_0"));
        Assert.Equal("child-name", _builder.GetParam("@_p_1"));
        Assert.Equal(18, _builder.GetParam("@_p_2"));
    }

    /// <summary>
    /// 测试目的：已渲染的 CTE 在克隆后应复用冻结的参数重命名，不遗留未引用参数。
    /// </summary>
    [Fact]
    public void With_WhenRenderedBeforeClone_ShouldPreserveSubqueryParameterNames()
    {
        // Arrange
        var cte = _builder.New().From("Child").Where("Name", "child-name");
        _builder.From("Parent").Where("Name", "parent-name").With("selected", cte);
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

    #endregion
}

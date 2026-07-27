using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// SQL Builder New 生命周期测试。
/// </summary>
public class BuilderNewLifecycleTest
{
    /// <summary>
    /// 测试 - 当来源包含参数时，New 应返回不包含来源参数的空参数管理器。
    /// </summary>
    [Fact]
    public void New_WhenSourceContainsParameters_ShouldReturnEmptyParameters()
    {
        // Arrange
        var source = new TestSqlBuilder();
        source.Select("*").From("Users").Where("Id", 1);

        // Act
        var fresh = source.New();

        // Assert
        Assert.Empty(fresh.GetParams());
        Assert.Single(source.GetParams());
    }

    /// <summary>
    /// 测试 - New Builder 新增参数不应污染来源 Builder。
    /// </summary>
    [Fact]
    public void New_WhenFreshBuilderAddsParameter_ShouldNotChangeSource()
    {
        // Arrange
        var source = new TestSqlBuilder();
        source.Select("*").From("Users").Where("Id", 1);
        var fresh = source.New();

        // Act
        fresh.Select("*").From("Orders").Where("OrderId", 2);

        // Assert
        Assert.Single(source.GetParams());
        Assert.Equal(1, source.GetParam("@_p_0"));
        Assert.Single(fresh.GetParams());
        Assert.Equal(2, fresh.GetParam("@_p_0"));
    }

    /// <summary>
    /// 测试 - 来源 Builder 在 New 后新增参数不应污染新 Builder。
    /// </summary>
    [Fact]
    public void New_WhenSourceAddsParameterAfterNew_ShouldNotChangeFreshBuilder()
    {
        // Arrange
        var source = new TestSqlBuilder();
        source.Select("*").From("Users").Where("Id", 1);
        var fresh = source.New();

        // Act
        source.Where("Status", 2);

        // Assert
        Assert.Equal(2, source.GetParams().Count);
        Assert.Empty(fresh.GetParams());
    }

    /// <summary>
    /// 测试 - New 的第一个参数应重新使用初始序号。
    /// </summary>
    [Fact]
    public void New_ShouldRestartParameterSequence()
    {
        // Arrange
        var source = new TestSqlBuilder();
        source.Select("*").From("Users").Where("Id", 1);
        var fresh = source.New();

        // Act
        fresh.Select("*").From("Orders").Where("OrderId", 2);

        // Assert
        Assert.Contains("@_p_0", fresh.GetParams().Keys);
        Assert.DoesNotContain("@_p_1", fresh.GetParams().Keys);
    }

    /// <summary>
    /// 测试 - New 不应复制别名、CTE、Union 或分页状态。
    /// </summary>
    [Fact]
    public void New_WhenSourceContainsQueryState_ShouldReturnEmptyState()
    {
        // Arrange
        var source = new TestSqlBuilder();
        var cte = source.New().Select("*").From("ArchivedUsers", "a");
        var union = source.New().Select("*").From("InactiveUsers", "i");
        source.Select("u.Id").From("Users", "u").Take(10).With("recent_users", cte).Union(union);

        // Act
        var fresh = (TestSqlBuilder)source.New();
        fresh.Select("o.Id").From("Orders", "u");

        // Assert
        Assert.Empty(fresh.CteItems);
        Assert.Empty(fresh.UnionItems);
        Assert.Equal(20, fresh.Pager.PageSize);
        Assert.Equal("Select [o].[Id] \r\nFrom [Orders] As [u]", fresh.ToSql());
    }

    /// <summary>
    /// 测试 - New 应保留来源 Builder 的具体类型。
    /// </summary>
    [Fact]
    public void New_ShouldReturnSameBuilderType()
    {
        // Arrange
        var source = new TestSqlBuilder();

        // Act
        var fresh = source.New();

        // Assert
        Assert.IsType<TestSqlBuilder>(fresh);
    }
}
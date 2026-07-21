using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// SQL Builder 克隆隔离测试。
/// </summary>
public class BuilderCloneIsolationTest
{
    /// <summary>
    /// 测试 - 修改克隆 Builder 的分页不应影响原 Builder。
    /// </summary>
    [Fact]
    public void Clone_WhenChangingPagination_ShouldNotChangeSourceBuilder()
    {
        // Arrange
        var source = new TestSqlBuilder();
        source.Take(10);
        var clone = (TestSqlBuilder)source.Clone();

        // Act
        clone.Take(20);

        // Assert
        Assert.Equal(10, source.Pager.PageSize);
        Assert.Equal(20, clone.Pager.PageSize);
        Assert.Equal(10, source.GetParam("@_p_0"));
        Assert.Equal(20, clone.GetParam("@_p_0"));
    }

    /// <summary>
    /// 测试 - 修改克隆 Builder 的 Join 不应影响原 Builder。
    /// </summary>
    [Fact]
    public void Clone_WhenAddingJoin_ShouldNotChangeSourceBuilder()
    {
        // Arrange
        var source = new TestSqlBuilder();
        source.From<Sample>("s");
        var clone = (TestSqlBuilder)source.Clone();

        // Act
        clone.LeftJoin<Sample2>("s2");

        // Assert
        Assert.Equal(string.Empty, source.JoinClause.ToSql());
        Assert.Equal("Left Join [Sample2] As [s2]", clone.JoinClause.ToSql());
    }

    /// <summary>
    /// 测试 - 修改克隆 Builder 的参数不应影响原 Builder。
    /// </summary>
    [Fact]
    public void Clone_WhenAddingParameter_ShouldNotChangeSourceBuilder()
    {
        // Arrange
        var source = new TestSqlBuilder();
        source.Where("Name", "source");
        var clone = (TestSqlBuilder)source.Clone();

        // Act
        clone.Where("Age", 18);

        // Assert
        Assert.Single(source.GetParams());
        Assert.Equal("source", source.GetParam("@_p_0"));
        Assert.Equal(2, clone.GetParams().Count);
        Assert.Equal(18, clone.GetParam("@_p_1"));
    }
}
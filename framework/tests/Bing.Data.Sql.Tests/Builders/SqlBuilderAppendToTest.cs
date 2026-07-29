using System.Text;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// SQL Builder AppendTo 合同测试。
/// </summary>
public class SqlBuilderAppendToTest
{
    /// <summary>
    /// 测试 - AppendTo 应输出与 ToSql 相同的 SQL。
    /// </summary>
    [Fact]
    public void AppendTo_WhenBuilderIsEmpty_ShouldRenderSameSqlAsToSql()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        builder.Count("u.Id", "UserCount", distinct: true).From("Users", "u").Where("u.Enabled", true);
        var result = new StringBuilder();

        // Act
        builder.AppendTo(result);

        // Assert
        Assert.Equal(builder.ToSql(), result.ToString());
    }

    /// <summary>
    /// 测试 - AppendTo 不应清空调用方已有内容。
    /// </summary>
    [Fact]
    public void AppendTo_WhenBuilderContainsPrefix_ShouldAppendWithoutClearing()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        builder.Select("u.Id").From("Users", "u");
        var result = new StringBuilder("Prefix:");

        // Act
        builder.AppendTo(result);

        // Assert
        Assert.Equal($"Prefix:{builder.ToSql()}", result.ToString());
    }

    /// <summary>
    /// 测试 - AppendTo 重复调用应重复追加 SQL 而不重置输出。
    /// </summary>
    [Fact]
    public void AppendTo_WhenCalledTwice_ShouldAppendTwice()
    {
        // Arrange
        var builder = new TestSqlBuilder();
        builder.Count(alias: "Total").From("Users");
        var result = new StringBuilder();
        var sql = builder.ToSql();

        // Act
        builder.AppendTo(result);
        builder.AppendTo(result);

        // Assert
        Assert.Equal($"{sql}{sql}", result.ToString());
    }

    /// <summary>
    /// 测试 - AppendTo 传入 null 时应抛出参数异常。
    /// </summary>
    [Fact]
    public void AppendTo_WhenArgumentIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var builder = new TestSqlBuilder();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => builder.AppendTo(null));

        // Assert
        Assert.Equal("builder", exception.ParamName);
    }

    /// <summary>
    /// 测试 - AppendTo 应保留子查询 SQL 与参数合并行为。
    /// </summary>
    [Fact]
    public void AppendTo_WhenSubqueryHasParameters_ShouldMergeParametersAndRenderExpectedSql()
    {
        // Arrange
        const string expected = "Select (Select Count(Distinct [Id]) As [Count] \r\nFrom [Audit] \r\nWhere [Enabled]=@_p_0) As [AuditCount] \r\nFrom [Users] \r\nWhere [Enabled]=@_p_1";
        var builder = new TestSqlBuilder();
        var subquery = builder.New().Count("Id", "Count", distinct: true).From("Audit").Where("Enabled", true);
        builder.Select(subquery, "AuditCount").From("Users").Where("Enabled", false);
        var result = new StringBuilder();

        // Act
        builder.AppendTo(result);

        // Assert
        Assert.Equal(expected, result.ToString());
        Assert.Equal(2, builder.GetParams().Count);
        Assert.Equal(true, builder.GetParam("@_p_0"));
        Assert.Equal(false, builder.GetParam("@_p_1"));
    }
}
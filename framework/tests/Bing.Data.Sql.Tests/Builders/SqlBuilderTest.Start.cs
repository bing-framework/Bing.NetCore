using System.Text;
using Xunit;

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

    #endregion
}

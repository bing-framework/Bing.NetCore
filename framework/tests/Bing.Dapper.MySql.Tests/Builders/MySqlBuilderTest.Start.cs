using Bing.Data.Sql;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// MySql Sql生成器测试 - 起始子句
/// </summary>
public partial class MySqlBuilderTest
{
    /// <summary>
    /// 测试CTE
    /// </summary>
    [Fact]
    public void Test_With_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("With Recursive `Test` ");
        result.AppendLine("As (Select `a`,`b` ");
        result.AppendLine("From `Test2`)");
        result.AppendLine("Select `a`,`b` ");
        result.Append("From `Test`");

        //执行
        var builder2 = _builder.New().Select("a,b").From("Test2");
        _builder.Select("a,b").From("Test").With("Test", builder2);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }
}

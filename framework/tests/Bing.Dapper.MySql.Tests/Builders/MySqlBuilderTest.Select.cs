using Bing.Data.Sql;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// MySql Sql生成器测试 - Select 子句
/// </summary>
public partial class MySqlBuilderTest
{
    /// <summary>
    /// 查询
    /// </summary>
    [Fact]
    public void Test_Select_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select `a` ");
        result.Append("From `t`");

        //执行
        _builder.Select("a").From("t");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 查询 - 表名带.符号
    /// </summary>
    [Fact]
    public void Test_Select_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select `c` ");
        result.Append("From `a.b`");

        //执行
        _builder.Select("c").From("a.b");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }
}

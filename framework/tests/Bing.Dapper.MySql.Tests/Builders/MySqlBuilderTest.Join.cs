using Bing.Dapper.Tests.Samples;
using Bing.Data.Sql;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// MySql Sql生成器测试 - Join 子句
/// </summary>
public partial class MySqlBuilderTest
{
    /// <summary>
    /// 测试 - 内连接
    /// </summary>
    [Fact]
    public void Test_Join_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select `a3`.`a`,`a1`.`b1`,`a2`.`b2` ");
        result.AppendLine("From `b` As `a2` ");
        result.Append("Join `t.c` As `a3` On `a2`.`d`=@_p_0");

        //执行
        _builder.Select("a,a1.b1,a2.b2", "a3")
            .From("b", "a2")
            .Join("t.c", "a3").On("a2.d", "e");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 测试 - 连接条件 - 属性表达式
    /// </summary>
    [Fact]
    public void Test_On_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select `a` ");
        result.AppendLine("From `Sample` As `b` ");
        result.Append("Join `Sample2` As `c` On `b`.`IntValue`<>`c`.`IntValue`");

        //执行
        _builder.Select("a")
            .From<Sample>("b")
            .Join<Sample2>("c").On<Sample, Sample2>(t => t.IntValue, t => t.IntValue, Operator.NotEqual);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }
}

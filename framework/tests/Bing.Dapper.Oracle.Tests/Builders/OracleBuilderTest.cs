using Bing.Dapper.Tests.Samples;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// Oracle Sql生成器测试
/// </summary>
public class OracleBuilderTest
{
    /// <summary>
    /// Oracle Sql生成器s
    /// </summary>
    private readonly OracleBuilder _builder;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public OracleBuilderTest() 
    {
        _builder = new OracleBuilder();
    }

    /// <summary>
    /// 设置条件 - 属性表达式
    /// </summary>
    [Fact]
    public void TestWhere()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select \"a\".\"Email\" ");
        result.AppendLine("From \"Sample\" \"a\" ");
        result.Append("Where \"a\".\"Email\"<>:p_0");

        //执行
        _builder.Select<Sample>(t => new object[] { t.Email })
            .From<Sample>("a")
            .Where<Sample>(t => t.Email, "abc", Operator.NotEqual);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Single(_builder.GetParams());
        Assert.Equal("abc", _builder.GetParam("p_0"));
    }
}

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
        result.AppendLine("Where [c]=@_p_1 ");
        result.AppendLine(") ");
        result.AppendLine("Union ");
        result.AppendLine("(Select [a],[b] ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [c]=@_p_0 ");
        result.Append(")");

        //执行
        var builder2 = _builder.New().Select("a,b").From("Test2").Where("c", 1);
        _builder.Select("a,b").From("Test").Where("c", 2).Union(builder2);
        _output.WriteLine(builder2.ToSql());
        _output.WriteLine(result.ToString());
        _output.WriteLine(_builder.ToSql());

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(1, _builder.GetParam("@_p_0"));
        Assert.Equal(2, _builder.GetParam("@_p_1"));
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
        result.AppendLine("Where [c]=@_p_1 ");
        result.AppendLine(") ");
        result.AppendLine("Union ");
        result.AppendLine("(Select [a],[b] ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [c]=@_p_0 ");
        result.AppendLine(") ");
        result.Append("Order By [a]");

        //执行
        var builder2 = _builder.New().Select("a,b").From("Test2").Where("c", 1);
        _builder.Select("a,b").From("Test").Where("c", 2).OrderBy("a").Union(builder2);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(1, _builder.GetParam("@_p_0"));
        Assert.Equal(2, _builder.GetParam("@_p_1"));
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
        result.AppendLine("Where [c]=@_p_1 ");
        result.AppendLine(") ");
        result.AppendLine("Union ");
        result.AppendLine("(Select [a],[b] ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [c]=@_p_0 ");
        result.AppendLine(") ");
        result.Append("Order By [a]");

        //执行
        var builder2 = _builder.New().Select("a,b").From("Test2").Where("c", 1).OrderBy("b");
        _builder.Select("a,b").From("Test").Where("c", 2).OrderBy("a").Union(builder2);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(1, _builder.GetParam("@_p_0"));
        Assert.Equal(2, _builder.GetParam("@_p_1"));
    }


    #endregion
}

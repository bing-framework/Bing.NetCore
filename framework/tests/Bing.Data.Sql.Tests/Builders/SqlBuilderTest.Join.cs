﻿using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// Sql生成器测试 - Join 子句
/// </summary>
public partial class SqlBuilderTest
{
    /// <summary>
    /// 内连接
    /// </summary>
    [Fact]
    public void Test_Join_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Join [c] As [d]");

        //执行
        _builder.Select("a")
            .From("b")
            .Join("c", "d");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 内连接 - 泛型
    /// </summary>
    [Fact]
    public void Test_Join_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Join [d].[Sample] As [c]");

        //执行
        _builder.Select("a")
            .From("b")
            .Join<Sample>("c", "d");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 添加Join子查询
    /// </summary>
    [Fact]
    public void Test_Join_3()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Join (Select * ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [Name]=@_p_0) As [t] ");
        result.Append("Where [Age]=@_p_1");

        //执行
        var builder2 = _builder.New().From("Test2").Where("Name", "a");
        _builder.From("Test").Join(builder2, "t").Where("Age", 1);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal("a", _builder.GetParam("@_p_0"));
        Assert.Equal(1, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 添加Join子查询 - 委托
    /// </summary>
    [Fact]
    public void Test_Join_4()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Join (Select * ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [Name]=@_p_0) As [t] ");
        result.Append("Where [Age]=@_p_1");

        //执行
        _builder.From("Test").Join(builder => builder.From("Test2").Where("Name", "a"), "t").Where("Age", 1);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal("a", _builder.GetParam("@_p_0"));
        Assert.Equal(1, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 内连接 - 添加原始Sql
    /// </summary>
    [Fact]
    public void Test_Join_5()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Join c");

        //执行
        _builder.Select("a")
            .From("b")
            .AppendJoin("c");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 内连接 - 添加原始Sql - 条件
    /// </summary>
    [Fact]
    public void Test_Join_6()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Join c");

        //执行
        _builder.Select("a")
            .From("b")
            .AppendJoin("d", false)
            .AppendJoin("c", true);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 左外连接
    /// </summary>
    [Fact]
    public void Test_LeftJoin_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Left Join [c] As [d]");

        //执行
        _builder.Select("a")
            .From("b")
            .LeftJoin("c", "d");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 左外连接 - 泛型
    /// </summary>
    [Fact]
    public void Test_LeftJoin_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Left Join [d].[Sample] As [c]");

        //执行
        _builder.Select("a")
            .From("b")
            .LeftJoin<Sample>("c", "d");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 左外连接子查询
    /// </summary>
    [Fact]
    public void Test_LeftJoin_3()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Left Join (Select * ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [Name]=@_p_0) As [t] ");
        result.Append("Where [Age]=@_p_1");

        //执行
        var builder2 = _builder.New().From("Test2").Where("Name", "a");
        _builder.From("Test").LeftJoin(builder2, "t").Where("Age", 1);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal("a", _builder.GetParam("@_p_0"));
        Assert.Equal(1, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 左外连接子查询 - 委托
    /// </summary>
    [Fact]
    public void Test_LeftJoin_4()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Left Join (Select * ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [Name]=@_p_0) As [t] ");
        result.Append("Where [Age]=@_p_1");

        //执行
        _builder.From("Test").LeftJoin(builder => builder.From("Test2").Where("Name", "a"), "t").Where("Age", 1);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal("a", _builder.GetParam("@_p_0"));
        Assert.Equal(1, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 左外连接 - 添加原始Sql
    /// </summary>
    [Fact]
    public void Test_LeftJoin_5()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Left Join c");

        //执行
        _builder.Select("a")
            .From("b")
            .AppendLeftJoin("c");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 左外连接 - 添加原始Sql - 条件
    /// </summary>
    [Fact]
    public void Test_LeftJoin_6()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Left Join c");

        //执行
        _builder.Select("a")
            .From("b")
            .AppendLeftJoin("d", false)
            .AppendLeftJoin("c", true);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 左连接 - lambda表达式
    /// </summary>
    [Fact]
    public void Test_LeftJoin_7()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a].[Email],[a].[BoolValue],[b].[Description],[b].[IntValue] ");
        result.AppendLine("From [Sample] As [a] ");
        result.Append("Left Join [Sample2] As [b] On [a].[Email]=[b].[StringValue] And [a].[IntValue]<>[b].[IntValue]");

        //执行
        _builder.Select<Sample>(t => new object[] { t.Email, t.BoolValue })
            .Select<Sample2>(t => new object[] { t.Description, t.IntValue })
            .From<Sample>("a")
            .LeftJoin<Sample2>("b").On<Sample, Sample2>((l, r) => l.Email == r.StringValue && l.IntValue != r.IntValue);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 右外连接
    /// </summary>
    [Fact]
    public void Test_RightJoin_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Right Join [c] As [d]");

        //执行
        _builder.Select("a")
            .From("b")
            .RightJoin("c", "d");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 右外连接 - 泛型
    /// </summary>
    [Fact]
    public void Test_RightJoin_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Right Join [d].[Sample] As [c]");

        //执行
        _builder.Select("a")
            .From("b")
            .RightJoin<Sample>("c", "d");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 右外连接子查询
    /// </summary>
    [Fact]
    public void Test_RightJoin_3()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Right Join (Select * ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [Name]=@_p_0) As [t] ");
        result.Append("Where [Age]=@_p_1");

        //执行
        var builder2 = _builder.New().From("Test2").Where("Name", "a");
        _builder.From("Test").RightJoin(builder2, "t").Where("Age", 1);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal("a", _builder.GetParam("@_p_0"));
        Assert.Equal(1, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 右外连接子查询 - 委托
    /// </summary>
    [Fact]
    public void Test_RightJoin_4()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Right Join (Select * ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [Name]=@_p_0) As [t] ");
        result.Append("Where [Age]=@_p_1");

        //执行
        _builder.From("Test").RightJoin(builder => builder.From("Test2").Where("Name", "a"), "t").Where("Age", 1);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal("a", _builder.GetParam("@_p_0"));
        Assert.Equal(1, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 右外连接 - 添加原始Sql
    /// </summary>
    [Fact]
    public void Test_RightJoin_5()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Right Join c");

        //执行
        _builder.Select("a")
            .From("b")
            .AppendRightJoin("c");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 右外连接 - 添加原始Sql - 条件
    /// </summary>
    [Fact]
    public void Test_RightJoin_6()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Right Join c");

        //执行
        _builder.Select("a")
            .From("b")
            .AppendRightJoin("d", false)
            .AppendRightJoin("c", true);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 连接条件
    /// </summary>
    [Fact]
    public void Test_On_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Join [c] As [d] On [b].[Id]<>@_p_0");

        //执行
        _builder.Select("a")
            .From("b")
            .Join("c", "d").On("b.Id", "c", Operator.NotEqual);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 连接条件 - 属性表达式
    /// </summary>
    [Fact]
    public void Test_On_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [Sample] As [b] ");
        result.Append("Join [Sample2] As [c] On [b].[IntValue]<>[c].[IntValue]");

        //执行
        _builder.Select("a")
            .From<Sample>("b")
            .Join<Sample2>("c").On<Sample, Sample2>(t => t.IntValue, t => t.IntValue, Operator.NotEqual);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 连接条件 - 布尔表达式
    /// </summary>
    [Fact]
    public void Test_On_3()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [Sample] As [b] ");
        result.Append("Join [Sample2] As [c] On [b].[IntValue]<>[c].[IntValue]");

        //执行
        _builder.Select("a")
            .From<Sample>("b")
            .Join<Sample2>("c").On<Sample, Sample2>((l, r) => l.IntValue != r.IntValue);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 连接条件 - 值为字面量
    /// </summary>
    [Fact]
    public void Test_On_4()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a],[b] ");
        result.AppendLine("From [Sample] As [s] ");
        result.Append("Left Join [Sample2] As [s2] On [s].[IntValue]=[s2].[IntValue] And [s].[StringValue]=@_p_0");

        //执行
        _builder.Select("a,b")
            .From<Sample>("s")
            .LeftJoin<Sample2>("s2").On<Sample, Sample2>((l, r) => l.IntValue == r.IntValue && l.StringValue == "a");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        _output.WriteLine(_builder.ToSql());
        Assert.Equal("a", _builder.GetParam("@_p_0"));
    }

    /// <summary>
    /// 连接条件 - 值为变量
    /// </summary>
    [Fact]
    public void Test_On_5()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a],[b] ");
        result.AppendLine("From [Sample] As [s] ");
        result.Append("Left Join [Sample2] As [s2] On [s].[IntValue]=[s2].[IntValue] And [s].[StringValue]=@_p_0");

        var a = "a";

        //执行
        _builder.Select("a,b")
            .From<Sample>("s")
            .LeftJoin<Sample2>("s2").On<Sample, Sample2>((l, r) => l.IntValue == r.IntValue && l.StringValue == a);
        _output.WriteLine(result.ToString());
        _output.WriteLine(_builder.ToSql());

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        _output.WriteLine(_builder.ToSql());
        Assert.Equal("a", _builder.GetParam("@_p_0"));
    }
}

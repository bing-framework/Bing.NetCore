﻿using System.Text;
using Bing.Data.Sql.Tests.Samples;
using Xunit;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// Sql生成器测试 - From 子句
/// </summary>
public partial class SqlBuilderTest
{
    /// <summary>
    /// 设置表
    /// </summary>
    [Fact]
    public void Test_From_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [c] ");
        result.Append("From [a] As [b]");

        //执行
        _builder.Select("c")
            .From("a", "b");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 设置表 - 泛型实体 - 别名 - 架构
    /// </summary>
    [Fact]
    public void Test_From_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [c] ");
        result.Append("From [b].[Sample] As [a]");

        //执行
        _builder.Select("c")
            .From<Sample>("a", "b");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 添加From子查询
    /// </summary>
    [Fact]
    public void Test_From_3()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.Append("From ");
        result.AppendLine("(Select Count(*) ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [Name]=@_p_0) As [test] ");
        result.Append("Where [Age]=@_p_1");

        //执行
        var builder2 = _builder.New().Count().From("Test2").Where("Name", "a");
        _builder.From(builder2, "test").Where("Age", 1);
        _output.WriteLine(_builder.ToSql());

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetSqlParams().Count);
        Assert.Equal("a", _builder.GetParamValue("@_p_0"));
        Assert.Equal(1, _builder.GetParamValue("@_p_1"));
    }

    /// <summary>
    /// 添加From子查询 - 委托
    /// </summary>
    [Fact]
    public void Test_From_4()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.Append("From ");
        result.AppendLine("(Select Count(*) ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [Name]=@_p_0) As [test] ");
        result.Append("Where [Age]=@_p_1");

        //执行
        _builder.From(builder => builder.Count().From("Test2").Where("Name", "a"), "test").Where("Age", 1);
        _output.WriteLine(_builder.ToSql());

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetSqlParams().Count);
        Assert.Equal("a", _builder.GetParamValue("@_p_0"));
        Assert.Equal(1, _builder.GetParamValue("@_p_1"));
    }

    /// <summary>
    /// 设置表 - 原始sql
    /// </summary>
    [Fact]
    public void Test_From_5()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [c] ");
        result.Append("From a");

        //执行
        _builder.Select("c")
            .AppendFrom("a");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 设置表 - 原始sql - 条件
    /// </summary>
    [Fact]
    public void Test_From_6()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [c] ");
        result.Append("From b");

        //执行
        _builder.Select("c")
            .AppendFrom("a", false)
            .AppendFrom("b", true);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }
}

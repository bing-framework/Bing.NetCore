﻿using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// Sql生成器测试 - OrderBy 子句
/// </summary>
public partial class SqlBuilderTest
{
    /// <summary>
    /// 测试排序
    /// </summary>
    [Fact]
    public void Test_OrderBy_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a].[Email] ");
        result.AppendLine("From [Sample] As [a] ");
        result.Append("Order By [c].[b] Desc");

        //执行
        _builder.Select<Sample>(t => t.Email)
            .From<Sample>("a")
            .OrderBy("b desc", "c");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 测试排序 - 属性表达式
    /// </summary>
    [Fact]
    public void Test_OrderBy_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a].[Email] ");
        result.AppendLine("From [Sample] As [a] ");
        result.Append("Order By [a].[Email] Desc");

        //执行
        _builder.Select<Sample>(t => t.Email)
            .From<Sample>("a")
            .OrderBy<Sample>(t => t.Email, true);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 测试排序
    /// </summary>
    [Fact]
    public void Test_AppendOrderBy_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a].[Email] ");
        result.AppendLine("From [Sample] As [a] ");
        result.Append("Order By b");

        //执行
        _builder.Select<Sample>(t => t.Email)
            .From<Sample>("a")
            .AppendOrderBy("b");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 测试排序 - 条件
    /// </summary>
    [Fact]
    public void Test_AppendOrderBy_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a].[Email] ");
        result.AppendLine("From [Sample] As [a] ");
        result.Append("Order By b");

        //执行
        _builder.Select<Sample>(t => t.Email)
            .From<Sample>("a")
            .AppendOrderBy("c", false)
            .AppendOrderBy("b", true);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }
}

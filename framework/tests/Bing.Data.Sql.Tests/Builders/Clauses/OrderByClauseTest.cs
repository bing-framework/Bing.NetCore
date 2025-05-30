﻿using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Clauses;

/// <summary>
/// 排序子句测试
/// </summary>
public class OrderByClauseTest
{
    /// <summary>
    /// 排序子句
    /// </summary>
    private OrderByClause _clause;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public OrderByClauseTest()
    {
        _clause = new OrderByClause(TestDialect.Instance, new EntityResolver(), new EntityAliasRegister());
    }

    /// <summary>
    /// 获取Sql语句
    /// </summary>
    private string GetSql()
    {
        return _clause.ToSql();
    }

    /// <summary>
    /// 默认输出空
    /// </summary>
    [Fact]
    public void Test_Default()
    {
        Assert.Null(GetSql());
    }

    /// <summary>
    /// 测试排序
    /// </summary>
    [Fact]
    public void Test_OrderBy_1()
    {
        _clause.OrderBy("a");
        Assert.Equal("Order By [a]", GetSql());
    }

    /// <summary>
    /// 测试排序 - 倒排序
    /// </summary>
    [Fact]
    public void Test_OrderBy_2()
    {
        _clause.OrderBy("a desc");
        Assert.Equal("Order By [a] Desc", GetSql());
    }

    /// <summary>
    /// 测试排序 - 倒排序 - 大写关键字，并添加空格
    /// </summary>
    [Fact]
    public void Test_OrderBy_3()
    {
        _clause.OrderBy(" A DESC ");
        Assert.Equal("Order By [A] Desc", GetSql());
    }

    /// <summary>
    /// 测试排序 - 列有前缀
    /// </summary>
    [Fact]
    public void Test_OrderBy_4()
    {
        _clause.OrderBy("a.b");
        Assert.Equal("Order By [a].[b]", GetSql());
    }

    /// <summary>
    /// 测试排序 - 列有前缀 - 倒排序
    /// </summary>
    [Fact]
    public void Test_OrderBy_5()
    {
        _clause.OrderBy("a.b desc");
        Assert.Equal("Order By [a].[b] Desc", GetSql());
    }

    /// <summary>
    /// 测试排序 - 多列排序
    /// </summary>
    [Fact]
    public void Test_OrderBy_6()
    {
        _clause.OrderBy("a.b desc,C");
        Assert.Equal("Order By [a].[b] Desc,[C]", GetSql());
    }

    /// <summary>
    /// 测试排序 - lambda支持
    /// </summary>
    [Fact]
    public void Test_OrderBy_7()
    {
        _clause.OrderBy<Sample>(t => t.Email);
        Assert.Equal("Order By [Email]", GetSql());
    }

    /// <summary>
    /// 测试排序 - lambda支持 - 倒排序
    /// </summary>
    [Fact]
    public void Test_OrderBy_8()
    {
        _clause.OrderBy<Sample>(t => t.Email, true);
        Assert.Equal("Order By [Email] Desc", GetSql());
    }

    /// <summary>
    /// 测试排序 - 测试实体解析
    /// </summary>
    [Fact]
    public void Test_OrderBy_9()
    {
        _clause = new OrderByClause(TestDialect.Instance, new TestEntityResolver(), new EntityAliasRegister());
        _clause.OrderBy<Sample>(t => t.Email, true);
        Assert.Equal("Order By [t_Email] Desc", GetSql());
    }

    /// <summary>
    /// 测试排序 - 测试实体别名注册
    /// </summary>
    [Fact]
    public void Test_OrderBy_10()
    {
        _clause = new OrderByClause(TestDialect.Instance, new TestEntityResolver(), new TestEntityAliasRegister());
        _clause.OrderBy<Sample>(t => t.Email, true);
        Assert.Equal("Order By [as_Sample].[t_Email] Desc", GetSql());
    }

    /// <summary>
    /// 测试排序
    /// </summary>
    [Fact]
    public void Test_OrderBy_11()
    {
        _clause.AppendSql("a");
        Assert.Equal("Order By a", GetSql());
    }

    /// <summary>
    /// 测试排序 - 多个OrderBy
    /// </summary>
    [Fact]
    public void Test_OrderBy_12()
    {
        _clause.OrderBy("a.b DESC");
        _clause.OrderBy<Sample>(t => t.Email, true);
        _clause.OrderBy("c,D.e desc");
        _clause.AppendSql("f");
        Assert.Equal("Order By [a].[b] Desc,[Email] Desc,[c],[D].[e] Desc,f", GetSql());
    }

    /// <summary>
    /// 测试排序 - 过滤列名相同的排序字段
    /// </summary>
    [Fact]
    public void Test_OrderBy_13()
    {
        _clause.OrderBy("Email");
        _clause.OrderBy<Sample>(t => t.Email, true);
        _clause.OrderBy("email");
        _clause.OrderBy("Email desc");
        _clause.OrderBy("a.Tel");
        _clause.OrderBy("A.Tel");
        Assert.Equal("Order By [Email],[a].[Tel]", GetSql());
    }

    /// <summary>
    /// 测试排序 - 过滤列名相同的排序字段 - 1个带别名，1个不带别名
    /// </summary>
    [Fact]
    public void Test_OrderBy_14()
    {
        _clause.OrderBy("a.Email");
        _clause.OrderBy("Email");
        Assert.Equal("Order By [a].[Email]", GetSql());
    }

    /// <summary>
    /// 测试排序 - 过滤列名相同的排序字段 - 2个带别名，都保留
    /// </summary>
    [Fact]
    public void Test_OrderBy_15()
    {
        _clause.OrderBy("a.Email");
        _clause.OrderBy("b.Email");
        Assert.Equal("Order By [a].[Email],[b].[Email]", GetSql());
    }

    /// <summary>
    /// 测试排序 - 过滤列名相同的排序字段 - 带别名并在后面添加，保留
    /// </summary>
    [Fact]
    public void Test_OrderBy_16()
    {
        _clause.OrderBy("Email");
        _clause.OrderBy("a.Email");
        Assert.Equal("Order By [Email],[a].[Email]", GetSql());
    }

    /// <summary>
    /// 测试排序 - 设置表别名
    /// </summary>
    [Fact]
    public void Test_OrderBy_17()
    {
        _clause.OrderBy("a", "b");
        Assert.Equal("Order By [b].[a]", GetSql());
    }

    /// <summary>
    /// 测试排序 - 设置表别名 - 倒排序
    /// </summary>
    [Fact]
    public void Test_OrderBy_18()
    {
        _clause.OrderBy("a desc", "b");
        Assert.Equal("Order By [b].[a] Desc", GetSql());
    }

    /// <summary>
    /// 测试排序 - 设置表别名 - 多列排序
    /// </summary>
    [Fact]
    public void Test_OrderBy_19()
    {
        _clause.OrderBy("a desc,c.D DESC,e,f asc,G Asc", "b");
        Assert.Equal("Order By [b].[a] Desc,[c].[D] Desc,[b].[e],[b].[f],[b].[G]", GetSql());
    }
}

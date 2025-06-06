﻿using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Clauses;

/// <summary>
/// From子句测试
/// </summary>
public class FromClauseTest
{
    /// <summary>
    /// From子句
    /// </summary>
    private FromClause _clause;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public FromClauseTest()
    {
        _clause = new FromClause(null, TestDialect.Instance, new EntityResolver(), new EntityAliasRegister(), null);
    }

    /// <summary>
    /// 获取Sql语句
    /// </summary>
    /// <returns></returns>
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
    /// 设置表
    /// </summary>
    [Fact]
    public void Test_From_1()
    {
        _clause.From("a");
        Assert.Equal("From [a]", GetSql());
    }

    /// <summary>
    /// 设置表 - 别名
    /// </summary>
    [Fact]
    public void Test_From_2()
    {
        _clause.From("a", "b");
        Assert.Equal("From [a] As [b]", GetSql());
    }

    /// <summary>
    /// 设置表 - 别名 - 架构
    /// </summary>
    [Fact]
    public void Test_From_3()
    {
        _clause.From("c.a", "b");
        Assert.Equal("From [c].[a] As [b]", GetSql());
    }

    /// <summary>
    /// 设置表 - 带方括号
    /// </summary>
    [Fact]
    public void Test_From_4()
    {
        _clause.From("[c].[a]", "b");
        Assert.Equal("From [c].[a] As [b]", GetSql());
    }

    /// <summary>
    /// 设置表 - 表名包含别名
    /// </summary>
    [Fact]
    public void Test_From_5()
    {
        _clause.From("a.b as t", "c");
        Assert.Equal("From [a].[b] As [t]", GetSql());
    }

    /// <summary>
    /// 设置表 - 泛型实体
    /// </summary>
    [Fact]
    public void Test_From_6()
    {
        _clause.From<Sample>();
        Assert.Equal("From [Sample]", GetSql());
    }

    /// <summary>
    /// 设置表 - 泛型实体 - 别名
    /// </summary>
    [Fact]
    public void Test_From_7()
    {
        _clause.From<Sample>("a");
        Assert.Equal("From [Sample] As [a]", GetSql());
    }

    /// <summary>
    /// 设置表 - 泛型实体 - 别名 -架构
    /// </summary>
    [Fact]
    public void Test_From_8()
    {
        _clause.From<Sample>("a", "b");
        Assert.Equal("From [b].[Sample] As [a]", GetSql());
    }

    /// <summary>
    /// 设置表 - 泛型实体 - 多次设置From - 后面的覆盖前面
    /// </summary>
    [Fact]
    public void Test_From_9()
    {
        _clause.From<Sample>("a");
        _clause.From<Sample>("b");
        Assert.Equal("From [Sample] As [b]", GetSql());
    }

    /// <summary>
    /// 设置表 - 原始Sql
    /// </summary>
    [Fact]
    public void Test_From_10()
    {
        _clause.AppendSql("a.b as c");
        Assert.Equal("From a.b as c", GetSql());
    }

    /// <summary>
    /// 设置表 - 多次设置From，最后一个生效
    /// </summary>
    [Fact]
    public void Test_From_11()
    {
        _clause.From<Sample>("a");
        _clause.AppendSql("b");
        _clause.From<Sample>("c");
        _clause.AppendSql("d");
        Assert.Equal("From d", GetSql());
    }

    /// <summary>
    /// 设置表 - 多次设置AppendSql，会拼到一起
    /// </summary>
    [Fact]
    public void Test_From_12()
    {
        _clause.AppendSql("a");
        _clause.AppendSql("b");
        Assert.Equal("From ab", GetSql());
    }

    /// <summary>
    /// 测试实体解析器
    /// </summary>
    [Fact]
    public void Test_From_13()
    {
        _clause = new FromClause(null, TestDialect.Instance, new TestEntityResolver(), new EntityAliasRegister(),
            null);
        _clause.From<Sample>();
        var result = _clause.ToSql();
        Assert.Equal("From [s].[t_Sample]", result);
    }

    /// <summary>
    /// 测试实体解析器 - 设置别名
    /// </summary>
    [Fact]
    public void Test_From_14()
    {
        _clause = new FromClause(null, TestDialect.Instance, new TestEntityResolver(), new EntityAliasRegister(),
            null);
        _clause.From<Sample>("a");
        var result = _clause.ToSql();
        Assert.Equal("From [s].[t_Sample] As [a]", result);
    }

    /// <summary>
    /// 测试复制副本
    /// </summary>
    [Fact]
    public void Test_Clone()
    {
        _clause.From("a", "b");
        var copy = _clause.Clone(null, null);
        Assert.Equal("From [a] As [b]", GetSql());
        Assert.Equal("From [a] As [b]", copy.ToSql());

        copy.From("c", "d");
        Assert.Equal("From [a] As [b]", GetSql());
        Assert.Equal("From [c] As [d]", copy.ToSql());
    }
}

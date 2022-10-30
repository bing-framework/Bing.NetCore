using System.Collections.Generic;
using Bing.Datas.Dapper.SqlServer;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Test.Integration.Samples;
using Bing.Data.Test.Integration.Sql.Builders.Samples;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Data.Test.Integration.Sql.Builders.SqlServer.Clauses;

/// <summary>
/// Select子句
/// </summary>
public class SelectClauseTest:TestBase
{
    /// <summary>
    /// Select子句
    /// </summary>
    private SelectClause _clause;

    /// <summary>
    /// 测试初始化
    /// </summary>
    /// <param name="output"></param>
    public SelectClauseTest(ITestOutputHelper output) : base(output)
    {
        _clause = new SelectClause(new SqlServerBuilder(), new SqlServerDialect(), new EntityResolver(), new EntityAliasRegister());
    }

    /// <summary>
    /// 获取Sql语句
    /// </summary>
    private string GetSql()
    {
        return _clause.ToSql();
    }

    /// <summary>
    /// 设置列 - 默认使用*
    /// </summary>
    [Fact]
    public void Test_Select_1()
    {
        Assert.Equal("Select *", GetSql());
    }

    /// <summary>
    /// 设置列
    /// </summary>
    [Fact]
    public void Test_Select_2()
    {
        _clause.Select("a");
        Assert.Equal("Select [a]", GetSql());
    }

    /// <summary>
    /// 设置列 - 设置表别名
    /// </summary>
    [Fact]
    public void Test_Select_3()
    {
        _clause.Select("a", "b");
        Assert.Equal("Select [b].[a]", _clause.ToSql());
    }

    /// <summary>
    /// 设置列 - 列带前缀
    /// </summary>
    [Fact]
    public void Test_Select_4()
    {
        _clause.Select("a.b");
        Assert.Equal("Select [a].[b]", _clause.ToSql());
    }

    /// <summary>
    /// 设置列 - 列具有中括号
    /// </summary>
    [Fact]
    public void Test_Select_5()
    {
        _clause.Select("[a]");
        Assert.Equal("Select [a]", GetSql());
    }

    /// <summary>
    /// 设置列 - 多列
    /// </summary>
    [Fact]
    public void Test_Select_6()
    {
        _clause.Select("a,[b]");
        Assert.Equal("Select [a],[b]", GetSql());
    }

    /// <summary>
    /// 设置列 - 多列 - 设置表别名
    /// </summary>
    [Fact]
    public void Test_Select_7()
    {
        _clause.Select("a,[b]", "c");
        Assert.Equal("Select [c].[a],[c].[b]", GetSql());
    }

    /// <summary>
    /// 设置列 - 多列 - 设置表别名 - 列带前缀
    /// </summary>
    [Fact]
    public void Test_Select_8()
    {
        _clause.Select("d.a,[b]", "c");
        Assert.Equal("Select [d].[a],[c].[b]", GetSql());
    }

    /// <summary>
    /// 设置列 - lambda表达式
    /// </summary>
    [Fact]
    public void Test_Select_9()
    {
        _clause.Select<Sample>(t => new object[] { t.Email, t.IntValue });
        Assert.Equal("Select [Email],[IntValue]", GetSql());
    }

    /// <summary>
    /// 设置列 - 多个Select
    /// </summary>
    [Fact]
    public void Test_Select_10()
    {
        _clause.Select("a");
        _clause.Select("b");
        Assert.Equal("Select [a],[b]", GetSql());
    }

    /// <summary>
    /// 设置列 - 每个Select使用不同的别名
    /// </summary>
    [Fact]
    public void Test_Select_11()
    {
        _clause.Select("a,b", "j");
        _clause.Select("c,d", "k");
        Assert.Equal("Select [j].[a],[j].[b],[k].[c],[k].[d]", GetSql());
    }

    /// <summary>
    /// 设置列 - 多个*
    /// </summary>
    [Fact]
    public void Test_Select_12()
    {
        _clause.Select("a.*,b.*");
        Assert.Equal("Select [a].*,[b].*", GetSql());
    }

    /// <summary>
    /// 设置列 - lambda表达式
    /// </summary>
    [Fact]
    public void Test_Select_13()
    {
        _clause.Select<Sample>(t => new object[] { t.Email, t.IntValue });
        _clause.Select<Sample2>(t => new object[] { t.Description, t.Display });
        Assert.Equal("Select [Email],[IntValue],[Description],[Display]", GetSql());
    }

    /// <summary>
    /// 设置列 - 设置列别名
    /// </summary>
    [Fact]
    public void Test_Select_14()
    {
        _clause.Select("t.a As e,[b],f.g", "k");
        _clause.Select("n");
        Assert.Equal("Select [t].[a] As [e],[k].[b],[f].[g],[n]", _clause.ToSql());
    }

    /// <summary>
    /// 设置列 - 设置列别名，增加了空格
    /// </summary>
    [Fact]
    public void Test_Select_15()
    {
        _clause.Select("t.a    As     [e]      ,        b aS          f ", "d");
        Assert.Equal("Select [t].[a] As [e],[d].[b] As [f]", GetSql());
    }

    /// <summary>
    /// 设置列 - 添加select子句，不进行修改
    /// </summary>
    [Fact]
    public void Test_Select_16()
    {
        _clause.AppendSql("a");
        Assert.Equal("Select a", GetSql());
    }

    /// <summary>
    /// 设置列
    /// </summary>
    [Fact]
    public void Test_Select_17()
    {
        _clause.Select("a.b,c,d", "o");
        _clause.AppendSql("e=1,");
        _clause.Select("f");
        _clause.AppendSql("g");
        _clause.Select("h");
        Assert.Equal("Select [a].[b],[o].[c],[o].[d],e=1,[f],g[h]", GetSql());
    }

    /// <summary>
    /// 测试实体解析器
    /// </summary>
    [Fact]
    public void Test_Select_18()
    {
        _clause = new SelectClause(new SqlServerBuilder(), new SqlServerDialect(), new TestEntityResolver(), new EntityAliasRegister());
        _clause.Select<Sample>(t => new object[] { t.Email, t.IntValue });
        Assert.Equal("Select [t_Email],[t_IntValue]", GetSql());
    }

    /// <summary>
    /// 测试实体别名注册器
    /// </summary>
    [Fact]
    public void Test_Select_19()
    {
        _clause = new SelectClause(new SqlServerBuilder(), new SqlServerDialect(), new TestEntityResolver(), new TestEntityAliasRegister());
        _clause.Select<Sample>(t => new object[] { t.Email, t.IntValue });
        var result = _clause.ToSql();
        Assert.Equal("Select [as_Sample].[t_Email],[as_Sample].[t_IntValue]", result);
    }

    /// <summary>
    /// 设置列 - lambda表达式 - 设置单个列名
    /// </summary>
    [Fact]
    public void Test_Select_20()
    {
        _clause.Select<Sample>(t => t.Email);
        Assert.Equal("Select [Email]", GetSql());
    }

    /// <summary>
    /// 设置列 - lambda表达式 - 设置单个列名和列别名
    /// </summary>
    [Fact]
    public void Test_Select_21()
    {
        _clause.Select<Sample>(t => t.Email, "e");
        Assert.Equal("Select [Email] As [e]", GetSql());
    }

    /// <summary>
    /// 设置列 - lambda表达式 - 以字典方式设置单个列名和列别名
    /// </summary>
    [Fact]
    public void Test_Select_22()
    {
        _clause.Select<Sample>(t => new Dictionary<object, string> { { t.Email, "e" } });
        Assert.Equal("Select [Email] As [e]", GetSql());
    }

    /// <summary>
    /// 设置列 - lambda表达式 - 以字典方式设置多个列名和列别名
    /// </summary>
    [Fact]
    public void Test_Select_23()
    {
        _clause.Select<Sample>(t => new Dictionary<object, string> { { t.Email, "e" }, { t.Url, "u" } });
        Assert.Equal("Select [Email] As [e],[Url] As [u]", GetSql());
    }

    /// <summary>
    /// 设置列 - lambda表达式 - 以字典方式设置多个列名和列别名 - 元数据解析
    /// </summary>
    [Fact]
    public void Test_Select_24()
    {
        _clause = new SelectClause(new SqlServerBuilder(), new SqlServerDialect(), new EntityResolver(new TestEntityMatedata()), new TestEntityAliasRegister());
        _clause.Select<Sample>(t => new Dictionary<object, string> { { t.Email, "e" }, { t.Url, "u" } });
        var result = _clause.ToSql();
        Assert.Equal("Select [as_Sample].[Sample_Email] As [e],[as_Sample].[Sample_Url] As [u]", result);
    }

    /// <summary>
    /// 设置列 - lambda表达式 - 将属性名映射为列别名
    /// </summary>
    [Fact]
    public void Test_Select_25()
    {
        _clause = new SelectClause(new SqlServerBuilder(), new SqlServerDialect(), new EntityResolver(new TestEntityMatedata()), new TestEntityAliasRegister());
        _clause.Select<Sample>(t => new object[] { t.Email, t.IntValue }, true);
        var result = _clause.ToSql();
        Assert.Equal("Select [as_Sample].[Sample_Email] As [Email],[as_Sample].[Sample_IntValue] As [IntValue]", result);
    }

    /// <summary>
    /// 设置列 - lambda表达式 - 将属性名映射为列别名 - 如果属性名和列名相同，不会生成As
    /// </summary>
    [Fact]
    public void Test_Select_26()
    {
        _clause = new SelectClause(new SqlServerBuilder(), new SqlServerDialect(), new EntityResolver(new TestEntityMatedata()), new TestEntityAliasRegister());
        _clause.Select<Sample>(t => new object[] { t.Email, t.DecimalValue }, true);
        var result = _clause.ToSql();
        Assert.Equal("Select [as_Sample].[Sample_Email] As [Email],[as_Sample].[DecimalValue]", result);
    }

    /// <summary>
    /// 复制副本
    /// </summary>
    [Fact]
    public void Test_Select_27()
    {
        _clause.Select("a");
        var copy = _clause.Clone(null, null);
        copy.Select("b");
        Assert.Equal("Select [a]", GetSql());
        Assert.Equal("Select [a],[b]", copy.ToSql());
    }

    /// <summary>
    /// 添加select子句
    /// </summary>
    [Fact]
    public void Test_AppendSql_1()
    {
        _clause.AppendSql("a");
        Assert.Equal("Select a", GetSql());
    }

    /// <summary>
    /// 添加select子句 - 带方括号
    /// </summary>
    [Fact]
    public void Test_AppendSql_2()
    {
        _clause.AppendSql("[a].[b]");
        Assert.Equal("Select [a].[b]", GetSql());
    }
}
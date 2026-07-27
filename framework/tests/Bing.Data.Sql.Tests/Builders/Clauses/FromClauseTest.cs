using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Tests.Samples;
using Bing.Data.Enums;

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
        _clause = new FromClause(TestSqlBuilder.CreateTestClauseContext());
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
    /// 测试目的：独立 schema 应由结构化表引用格式化。
    /// </summary>
    [Fact]
    public void Test_From_3()
    {
        _clause.From("c.a", "b");
        Assert.Equal("From [c].[a] As [b]", GetSql());
    }

    /// <summary>
    /// 测试目的：SQL Server 字符串表名应按既有规则拆分限定段。
    /// </summary>
    [Fact]
    public void Test_From_4()
    {
        _clause.From("c.a", "b");
        Assert.Equal("From [c].[a] As [b]", GetSql());
    }

    /// <summary>
    /// 测试目的：字符串中的别名应按既有规则解析。
    /// </summary>
    [Fact]
    public void Test_From_5()
    {
        _clause.From("a.b as t");
        Assert.Equal("From [a].[b] As [t]", GetSql());
    }

    /// <summary>
    /// 测试目的：字符串表名包含 SQL 语句分隔符时应被拒绝。
    /// </summary>
    [Fact]
    public void From_WhenTableContainsStatementSeparator_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _clause.From("a;DropTable"));
    }

    /// <summary>
    /// 测试目的：SQL Server 多段字符串表名应按既有规则拆分。
    /// </summary>
    [Fact]
    public void From_WhenTableContainsMultipleDots_ShouldFormatAsAtomicIdentifier()
    {
        _clause.From("profile.api.Event");
        Assert.Equal("From [profile].[api].[Event]", GetSql());
    }

    /// <summary>
    /// 测试目的：字符串表名包含函数结构时应被拒绝。
    /// </summary>
    [Fact]
    public void From_WhenTableContainsFunctionStructure_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _clause.From("a(b)"));
    }

    /// <summary>
    /// 测试目的：复合预加引号限定名必须被拒绝，调用方应改用独立 schema 参数。
    /// </summary>
    [Fact]
    public void From_WhenTableContainsQualifiedQuotedIdentifier_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _clause.From("[archive].[Order.Log2025]"));
    }

    /// <summary>
    /// 测试目的：字符串内的别名与显式别名冲突时应被拒绝，避免隐式覆盖。
    /// </summary>
    [Fact]
    public void From_WhenEmbeddedAliasConflictsWithExplicitAlias_ShouldThrowInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _clause.From("Order.Log2025 as log", "other"));
    }

    /// <summary>
    /// 测试目的：AppendFrom 原始 SQL 表达式应绕过结构化表名解析。
    /// </summary>
    [Fact]
    public void AppendFrom_ShouldPreserveSqlExpression()
    {
        _clause.AppendSql("(Select 1) As source");

        Assert.Equal("From (Select 1) As source", GetSql());
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
    /// 追加原始表表达式应保持调用方提供的内容。
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
        _clause = new FromClause(TestSqlBuilder.CreateTestClauseContext(
            entityResolver: new TestEntityResolver()));
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
        _clause = new FromClause(TestSqlBuilder.CreateTestClauseContext(
            entityResolver: new TestEntityResolver()));
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
        var copy = _clause.Clone(TestSqlBuilder.CreateTestClauseContext());
        Assert.Equal("From [a] As [b]", GetSql());
        Assert.Equal("From [a] As [b]", copy.ToSql());

        copy.From("c", "d");
        Assert.Equal("From [a] As [b]", GetSql());
        Assert.Equal("From [c] As [d]", copy.ToSql());
    }
}

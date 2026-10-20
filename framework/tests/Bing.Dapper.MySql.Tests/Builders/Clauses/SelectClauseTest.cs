using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Dapper.Tests.Builders.Clauses;

/// <summary>
/// Select子句测试
/// </summary>
public class SelectClauseTest
{
    /// <summary>
    /// Select子句
    /// </summary>
    private readonly SelectClause _clause;

    /// <summary>
    /// 初始化一个 <see cref="SelectClauseTest"/> 类型的实例。
    /// </summary>
    public SelectClauseTest()
    {
        _clause = (SelectClause)((ISqlQueryClauseAccessor)new MySqlBuilder()).SelectClause;
    }

    /// <summary>
    /// 获取Sql语句
    /// </summary>
    /// <returns>Select 子句生成的 SQL 文本。</returns>
    private string GetSql() => _clause.ToSql();

    /// <summary>
    /// 测试 - 添加Select子句
    /// </summary>
    [Fact]
    public void Test_AppendSql_1()
    {
        _clause.AppendSql("a");
        Assert.Equal("Select a", GetSql());
    }

    /// <summary>
    /// 测试 - 添加Select子句 - 带方括号
    /// </summary>
    [Fact]
    public void Test_AppendSql_2()
    {
        _clause.AppendSql("[a].[b]");
        Assert.Equal("Select `a`.`b`", GetSql());
    }
}

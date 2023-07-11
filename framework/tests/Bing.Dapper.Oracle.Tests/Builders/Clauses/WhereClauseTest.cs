using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Dapper.Tests.Builders.Clauses;

/// <summary>
/// Where子句测试
/// </summary>
public class WhereClauseTest
{
    /// <summary>
    /// Where子句
    /// </summary>
    private readonly WhereClause _clause;

    /// <summary>
    /// 初始化一个<see cref="WhereClauseTest"/>类型的实例
    /// </summary>
    public WhereClauseTest()
    {
        _clause = new WhereClause(null, OracleDialect.Instance, new EntityResolver(), new EntityAliasRegister(), new ParameterManager(OracleDialect.Instance));
    }

    /// <summary>
    /// 获取Sql语句
    /// </summary>
    private string GetSql() => _clause.ToSql();

    /// <summary>
    /// 设置条件
    /// </summary>
    [Fact]
    public void TestWhere_1()
    {
        _clause.Where("Name", "a");
        Assert.Equal("Where \"Name\"=:p_0", GetSql());
    }
}

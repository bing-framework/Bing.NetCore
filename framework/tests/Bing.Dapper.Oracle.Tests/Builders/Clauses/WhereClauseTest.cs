using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql;

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
        _clause = (WhereClause)((ISqlQueryClauseAccessor)new OracleBuilder()).WhereClause;
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

    /// <summary>
    /// 测试目的：Oracle 将空字符串视为 NULL，空值条件不得生成恒不匹配的空字符串比较。
    /// </summary>
    [Fact]
    public void IsEmptyAndNotEmpty_WhenOracleTreatsEmptyStringAsNull_ShouldUseNullChecks()
    {
        // Arrange
        var empty = new OracleBuilder().From("Users").IsEmpty("Name");
        var notEmpty = new OracleBuilder().From("Users").IsNotEmpty("Name");

        // Act
        var emptySql = empty.ToSql();
        var notEmptySql = notEmpty.ToSql();
        var cloneSql = empty.Clone().ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom \"Users\" \r\nWhere \"Name\" Is Null", emptySql);
        Assert.Equal("Select * \r\nFrom \"Users\" \r\nWhere \"Name\" Is Not Null", notEmptySql);
        Assert.Equal(emptySql, cloneSql);
    }
}

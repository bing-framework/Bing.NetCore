using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;

namespace Bing.Data.Sql.Tests.Builders.Clauses;

/// <summary>
/// Where子句测试
/// </summary>
public class WhereClauseTest
{
    /// <summary>
    /// 参数管理器
    /// </summary>
    private readonly IParameterManager _parameterManager;

    /// <summary>
    /// Where子句
    /// </summary>
    private readonly WhereClause _clause;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public WhereClauseTest()
    {
        _parameterManager = new ParameterManager(TestDialect.Instance);
        _clause = new WhereClause(new TestSqlBuilder(parameterManager: _parameterManager));
    }

    /// <summary>
    /// 获取结果
    /// </summary>
    private string GetResult(ISqlCondition condition)
    {
        var result = new StringBuilder();
        condition.AppendTo(result);
        return result.ToString();
    }

    /// <summary>
    /// 获取Sql语句
    /// </summary>
    private string GetSql(IWhereClause clause = null)
    {
        clause ??= _clause;
        var result = new StringBuilder();
        clause.AppendTo(result);
        return result.ToString();
    }

    #region  默认输出

    /// <summary>
    /// 默认输出
    /// </summary>
    [Fact]
    public void Test_Default()
    {
        Assert.Empty(GetSql());
    }

    #endregion

    #region Where(设置条件)

    /// <summary>
    /// 设置 - 相等条件 - 1个条件
    /// </summary>
    [Fact]
    public void Test_Where_1()
    {
        _clause.Where("Name", "a", Operator.Equal);
        Assert.Equal("Where [Name]=@_p_0", GetSql());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 设置 - 相等条件 - 带表别名
    /// </summary>
    [Fact]
    public void Test_Where_2()
    {
        _clause.Where("f.Name", "a", Operator.Equal);
        Assert.Equal("Where [f].[Name]=@_p_0", GetSql());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 设置 - 相等条件 - 2个条件
    /// </summary>
    [Fact]
    public void Test_Where_3()
    {
        _clause.Where("f.Name", "a", Operator.Equal);
        _clause.Where("s.Age", "b", Operator.Equal);
        Assert.Equal("Where [f].[Name]=@_p_0 And [s].[Age]=@_p_1", GetSql());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
        Assert.Equal("b", _parameterManager.GetValue("@_p_1"));
    }

    /// <summary>
    /// 测试 - 设置子查询条件
    /// </summary>
    [Fact]
    public void Test_Where_4()
    {
        var result = new StringBuilder();
        result.Append("Where [a].[b]=");
        result.AppendLine("(Select [a] ");
        result.Append("From [b])");

        var builder = new TestSqlBuilder().Select("a").From("b");
        _clause.Where("a.b", builder, Operator.Equal);
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试 - 设置子查询条件 - 内嵌表达式
    /// </summary>
    [Fact]
    public void Test_Where_5()
    {
        var result = new StringBuilder();
        result.Append("Where [a].[b]=");
        result.AppendLine("(Select [a] ");
        result.Append("From [b])");

        _clause.Where("a.b", t => t.Select("a").From("b"), Operator.Equal);
        Assert.Equal(result.ToString(), GetSql());
    }

    #endregion
}

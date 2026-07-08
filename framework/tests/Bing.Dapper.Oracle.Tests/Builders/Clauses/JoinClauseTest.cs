using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql;

namespace Bing.Dapper.Tests.Builders.Clauses;

/// <summary>
/// <see cref="OracleJoinClause"/> 单元测试
/// 验证 Oracle 方言下的 Join 子句生成行为（双引号标识符，:p_0 参数格式）
/// </summary>
public class OracleJoinClauseTest
{
    private readonly IParameterManager _parameterManager;
    private readonly OracleJoinClause _clause;

    public OracleJoinClauseTest()
    {
        _parameterManager = new ParameterManager(OracleDialect.Instance);
        var builder = new OracleBuilder();
        _clause = new OracleJoinClause(
            builder,
            OracleDialect.Instance,
            new EntityResolver(),
            new EntityAliasRegister(),
            _parameterManager,
            null);
    }

    private string GetSql() => _clause.ToSql();

    // ── Default ───────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：初始状态不设置任何 Join，ToSql 应返回空字符串，不抛异常。
    /// </summary>
    [Fact]
    public void Test_Default()
    {
        Assert.Empty(GetSql());
    }

    // ── Join ──────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：设置一个 Join 表，On 条件使用 Oracle 双引号和 :p_N 参数格式。
    /// </summary>
    [Fact]
    public void Test_Join_Basic()
    {
        // Arrange
        var result = new StringBuilder();
        result.Append("Join \"t\" ");
        result.Append("On \"a\".\"id\"=:p_0");

        // Act
        _clause.Join("t");
        _clause.On("a.id", "b");

        // Assert
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试目的：Join 时指定表别名，输出中应含别名。
    /// </summary>
    [Fact]
    public void Test_Join_WithAlias()
    {
        // Arrange
        var result = new StringBuilder();
        result.Append("Join \"t\" \"x\" ");
        result.Append("On \"x\".\"id\"=:p_0");

        // Act
        _clause.Join("t", "x");
        _clause.On("x.id", "b");

        // Assert
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试目的：On 中未先设置 Join，ToSql 应返回空（条件被忽略）。
    /// </summary>
    [Fact]
    public void Test_On_WithoutJoin_ShouldBeEmpty()
    {
        // Act
        _clause.On("a.id", "b");

        // Assert
        Assert.Empty(GetSql());
    }

    /// <summary>
    /// 测试目的：多个 On 条件用 And 连接。
    /// </summary>
    [Fact]
    public void Test_Join_MultipleOn()
    {
        // Arrange
        var result = new StringBuilder();
        result.Append("Join \"t\" ");
        result.Append("On \"a\".\"id\"=:p_0 And \"a\".\"code\"=:p_1");

        // Act
        _clause.Join("t");
        _clause.On("a.id", "b");
        _clause.On("a.code", "c");

        // Assert
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试目的：多个 Join 块各自独立，每块用换行分隔。
    /// </summary>
    [Fact]
    public void Test_MultipleJoins()
    {
        // Arrange
        var result = new StringBuilder();
        result.AppendLine("Join \"t1\" On \"a\".\"id\"=:p_0 ");
        result.Append("Join \"t2\" On \"b\".\"id\"=:p_1");

        // Act
        _clause.Join("t1");
        _clause.On("a.id", "v1");
        _clause.Join("t2");
        _clause.On("b.id", "v2");

        // Assert
        Assert.Equal(result.ToString(), GetSql());
    }

    // ── LeftJoin ──────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：LeftJoin 输出 "Left Join" 关键字，格式与 Oracle 方言一致。
    /// </summary>
    [Fact]
    public void Test_LeftJoin_Basic()
    {
        // Arrange
        var result = new StringBuilder();
        result.Append("Left Join \"t\" ");
        result.Append("On \"a\".\"id\"=:p_0");

        // Act
        _clause.LeftJoin("t");
        _clause.On("a.id", "b");

        // Assert
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试目的：RightJoin 输出 "Right Join" 关键字。
    /// </summary>
    [Fact]
    public void Test_RightJoin_Basic()
    {
        // Arrange
        var result = new StringBuilder();
        result.Append("Right Join \"t\" ");
        result.Append("On \"a\".\"id\"=:p_0");

        // Act
        _clause.RightJoin("t");
        _clause.On("a.id", "b");

        // Assert
        Assert.Equal(result.ToString(), GetSql());
    }

    // ── On Operator ──────────────────────────────────────────────

    /// <summary>
    /// 测试目的：On 条件支持不等于运算符，输出 &lt;&gt; 符号。
    /// </summary>
    [Fact]
    public void Test_On_WithNotEqualOperator()
    {
        // Arrange
        var result = new StringBuilder();
        result.Append("Join \"t\" ");
        result.Append("On \"a\".\"id\"<>:p_0");

        // Act
        _clause.Join("t");
        _clause.On("a.id", "b", Operator.NotEqual);

        // Assert
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试目的：On 条件支持小于运算符，输出 &lt; 符号。
    /// </summary>
    [Fact]
    public void Test_On_WithLessOperator()
    {
        // Arrange
        var result = new StringBuilder();
        result.Append("Join \"t\" ");
        result.Append("On \"a\".\"id\"<:p_0");

        // Act
        _clause.Join("t");
        _clause.On("a.id", "b", Operator.Less);

        // Assert
        Assert.Equal(result.ToString(), GetSql());
    }

    // ── AppendJoin ────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：AppendJoin 追加原始 SQL 片段，输出应原样保留。
    /// </summary>
    [Fact]
    public void Test_AppendJoin_Raw()
    {
        // Act
        _clause.AppendJoin("\"raw_table\" On 1=1");

        // Assert
        Assert.Equal("Join \"raw_table\" On 1=1", GetSql());
    }

    // ── Clear ─────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：通过 SqlBuilder.ClearJoin() 清空 Join 后，输出中不应保留 Join 片段。
    /// </summary>
    [Fact]
    public void Test_Clear_ShouldResetJoin()
    {
        // Arrange
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.Append("From \"User\"");

        var builder = new OracleBuilder();
        builder.Select("*").From("User").Join("t").On("a.id", "b");

        // Act
        builder.ClearJoin();

        // Assert
        Assert.Equal(result.ToString(), builder.ToSql());
    }
}

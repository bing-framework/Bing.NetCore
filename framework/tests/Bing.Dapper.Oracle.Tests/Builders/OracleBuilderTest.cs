using Bing.Dapper.Tests.Samples;
using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// Oracle Sql生成器测试
/// </summary>
public class OracleBuilderTest
{
    /// <summary>
    /// Oracle Sql生成器s
    /// </summary>
    private readonly OracleBuilder _builder;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public OracleBuilderTest() 
    {
        _builder = new OracleBuilder();
    }

    /// <summary>
    /// 设置条件 - 属性表达式
    /// </summary>
    [Fact]
    public void TestWhere()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select \"a\".\"Email\" ");
        result.AppendLine("From \"Sample\" \"a\" ");
        result.Append("Where \"a\".\"Email\"<>:p_0");

        //执行
        _builder.Select<Sample>(t => new object[] { t.Email })
            .From<Sample>("a")
            .Where<Sample>(t => t.Email, "abc", Operator.NotEqual);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Single(_builder.GetParams());
        Assert.Equal("abc", _builder.GetParam("p_0"));
    }

    /// <summary>
    /// 测试目的：Oracle 12c+ 分页应使用 Offset/Fetch Next 语法，并保持偏移量和限制参数的顺序。
    /// </summary>
    [Fact]
    public void Page_WhenSkipAndTakeAreSet_ShouldRenderOracleOffsetFetchSyntax()
    {
        // Arrange
        var builder = new OracleBuilder();

        // Act
        builder.Select("*").From("Sample").OrderBy("Id").Page(new Pager(2, 10, "Id"));

        // Assert
        Assert.Equal("Select * \r\nFrom \"Sample\" \r\nOrder By \"Id\" \r\nOffset :p_0 Rows Fetch Next :p_1 Rows Only",
            builder.ToSql());
        Assert.Equal("10", builder.GetParam("p_0"));
        Assert.Equal("10", builder.GetParam("p_1"));
    }

    /// <summary>
    /// 测试目的：Clone 后应保留 Oracle 分页语法和已绑定的分页参数。
    /// </summary>
    [Fact]
    public void Clone_WhenPageIsConfigured_ShouldKeepOracleOffsetFetchSyntaxAndParameters()
    {
        // Arrange
        _builder.Select("*").From("Sample").OrderBy("Id").Page(new Pager(2, 10, "Id"));

        // Act
        var clone = _builder.Clone();

        // Assert
        Assert.Equal(_builder.ToSql(), clone.ToSql());
        Assert.Equal("10", clone.GetParam("p_0"));
        Assert.Equal("10", clone.GetParam("p_1"));
    }

    /// <summary>
    /// 测试目的：New 后分页应使用新的参数管理器，并从 Oracle 参数序号零重新开始。
    /// </summary>
    [Fact]
    public void New_WhenPageIsConfigured_ShouldUseNewOraclePaginationParameters()
    {
        // Arrange
        _builder.Select("*").From("Sample").OrderBy("Id").Page(new Pager(2, 10, "Id"));

        // Act
        var fresh = _builder.New();
        fresh.Select("*").From("Sample").OrderBy("Id").Page(new Pager(1, 5, "Id"));

        // Assert
        Assert.Equal("Select * \r\nFrom \"Sample\" \r\nOrder By \"Id\" \r\nOffset :p_0 Rows Fetch Next :p_1 Rows Only",
            fresh.ToSql());
        Assert.Equal("0", fresh.GetParam("p_0"));
        Assert.Equal("5", fresh.GetParam("p_1"));
        Assert.Equal("10", _builder.GetParam("p_0"));
        Assert.Equal("10", _builder.GetParam("p_1"));
    }
}

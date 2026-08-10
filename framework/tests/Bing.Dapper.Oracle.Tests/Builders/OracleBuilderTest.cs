using Bing.Dapper.Tests.Samples;
using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;

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
        _builder = new OracleBuilder(new SqlBuilderServices(options: new SqlOptions
        {
            QueryCapabilities = new SqlQueryCapabilities { Pagination = SqlQueryCapabilityState.Supported }
        }));
    }

    /// <summary>
    /// 测试目的：未确认 Oracle 版本时，Offset/Fetch 分页必须在 SQL 渲染前被拒绝。
    /// </summary>
    [Fact]
    public void ToSql_WhenOraclePaginationVersionIsNotConfirmed_ShouldReject()
    {
        // Arrange
        var builder = new OracleBuilder();
        builder.Select("Id").From("Sample").OrderBy("Id").Page(new Pager(1, 10, "Id"));

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Provider bing.oracle 的当前查询能力配置不支持 分页。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Oracle 明确不支持 Except，选项配置不得重新启用该语法。
    /// </summary>
    [Fact]
    public void ToSql_WhenOracleExceptIsExplicitlyEnabled_ShouldStillReject()
    {
        // Arrange
        var builder = new OracleBuilder(new SqlBuilderServices(options: new SqlOptions
        {
            QueryCapabilities = new SqlQueryCapabilities { Except = SqlQueryCapabilityState.Supported }
        }));
        var archived = (OracleBuilder)builder.New();
        archived.Select("Id").From("ArchivedRows");
        builder.Select("Id").From("CurrentRows").Except(archived);

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Provider bing.oracle 的当前查询能力配置不支持 Except。", exception.Message);
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
    /// 测试目的：Oracle 空 In 与 Not In 集合必须保留明确常量语义且不创建参数。
    /// </summary>
    [Fact]
    public void InAndNotIn_WhenValuesAreEmpty_ShouldRenderConstantConditionsWithoutParameters()
    {
        // Arrange
        const string expectedIn = "Select * \r\nFrom \"Sample\" \r\nWhere 1 = 0";
        const string expectedNotIn = "Select * \r\nFrom \"Sample\" \r\nWhere 1 = 1";
        var inBuilder = new OracleBuilder().Select("*").From("Sample").In("Id", Array.Empty<object>());
        var notInBuilder = new OracleBuilder().Select("*").From("Sample").NotIn("Id", Array.Empty<object>());

        // Act
        var inSql = inBuilder.ToSql();
        var notInSql = notInBuilder.ToSql();

        // Assert
        Assert.Equal(expectedIn, inSql);
        Assert.Equal(expectedNotIn, notInSql);
        Assert.Empty(inBuilder.GetParams());
        Assert.Empty(notInBuilder.GetParams());
    }

    /// <summary>
    /// 测试目的：Oracle 12c+ 分页应使用 Offset/Fetch Next 语法，并保持偏移量和限制参数的顺序。
    /// </summary>
    [Fact]
    public void Page_WhenSkipAndTakeAreSet_ShouldRenderOracleOffsetFetchSyntax()
    {
        // Arrange
        var builder = (OracleBuilder)_builder.New();

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

    /// <summary>
    /// 测试目的：子查询参数冲突改名时，PostgreSQL 风格的双冒号类型转换不得被识别为 Oracle 冒号参数。
    /// </summary>
    [Fact]
    public void Join_WhenSubqueryContainsDoubleColonCast_ShouldRenameOnlyParameterToken()
    {
        // Arrange
        const string expected = "Select * \r\nFrom \"Parent\" \r\nJoin (Select * \r\nFrom \"Child\" \r\nWhere Payload::text=:p_0 And Note=':text' /* :text */) \"c\" \r\nWhere Id=:text";
        var subquery = new OracleBuilder().From("Child")
            .AppendWhere("Payload::text=:text And Note=':text' /* :text */")
            .AddParam("text", "child");

        // Act
        var sql = _builder.From("Parent").AppendWhere("Id=:text").AddParam("text", "outer").Join(subquery, "c").ToSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.Equal("outer", _builder.GetParam("text"));
        Assert.Equal("child", _builder.GetParam("p_0"));
    }

    /// <summary>
    /// 测试目的：Oracle 派生表 From 别名不得使用不受支持的 As 关键字。
    /// </summary>
    [Fact]
    public void From_WhenSourceIsSubquery_ShouldRenderOracleAliasWithoutAs()
    {
        // Arrange
        const string expected = "Select * \r\nFrom (Select \"Id\" \r\nFrom \"Child\") \"c\"";
        var subquery = new OracleBuilder().Select("Id").From("Child");

        // Act
        var sql = _builder.From(subquery, "c").ToSql();

        // Assert
        Assert.Equal(expected, sql);
    }
}

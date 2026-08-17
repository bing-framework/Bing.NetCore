using Bing.Data.Queries;
using Bing.Properties;
using Bing.Data.Sql.Tests.XUnitHelpers;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// Sql生成器测试 - 结束 子句
/// </summary>
public partial class SqlBuilderTest
{
    #region Page

    /// <summary>
    /// 验证分页时未设置排序字段，抛出异常
    /// </summary>
    [Fact]
    public void Test_Page_1()
    {
        var pager = new QueryParameter();
        _builder.From("a").Page(pager);
        AssertHelper.Throws<ArgumentException>(() => _builder.ToSql(), LibraryResource.OrderIsEmptyForPage);
    }

    /// <summary>
    /// 分页时设置了排序字段
    /// </summary>
    [Fact]
    public void Test_Page_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Order By [a] ");
        result.Append("Offset @_p_0 Rows Fetch Next @_p_1 Rows Only");

        //执行
        var pager = new QueryParameter { Order = "a" };
        _builder.From("Test").Page(pager);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(0, _builder.GetParam("@_p_0"));
        Assert.Equal(20, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试目的：Skip 和 Take 应输出完整分页 SQL 并保留精确参数。
    /// </summary>
    [Fact]
    public void SkipAndTake_ShouldRenderCompleteSqlAndBindParameters()
    {
        // Arrange
        var builder = _builder;

        // Act
        var sql = builder.From("Test")
            .OrderBy("Id")
            .Skip(5)
            .Take(10)
            .ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom [Test] \r\nOrder By [Id] \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only", sql);
        Assert.Equal(new[] { "@_p_0", "@_p_1" }, builder.GetParams().Keys);
        Assert.Equal(5, builder.GetParam("@_p_0"));
        Assert.Equal(10, builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试目的：已生成 SQL 的调试渲染应复用传入文本，并与无参重载保持相同输出。
    /// </summary>
    [Fact]
    public void ToDebugSql_WhenSqlIsProvided_ShouldReuseSqlAndPreserveOutput()
    {
        // Arrange
        var builder = _builder.From("Test").Where("Name", "O'Reilly");
        var sql = builder.ToSql();

        // Act
        var result = builder.ToDebugSql(sql);

        // Assert
        Assert.Equal("Select * \r\nFrom [Test] \r\nWhere [Name]='O'Reilly'", result);
        Assert.Equal(result, builder.ToDebugSql());
    }

    /// <summary>
    /// 测试目的：参数名与参数字面量包含正则替换字符时，调试渲染应保留字面值。
    /// </summary>
    [Fact]
    public void ToDebugSql_WhenParameterNameOrValueContainsRegexCharacters_ShouldPreserveLiteralValue()
    {
        // Arrange
        var builder = _builder.AppendSelect("*").AppendFrom("[Test]")
            .AppendWhere("[Name]=@name.value")
            .AddParam("name.value", "$value");
        var sql = builder.ToSql();

        // Act
        var result = builder.ToDebugSql(sql);

        // Assert
        Assert.Equal("Select * \r\nFrom [Test] \r\nWhere [Name]='$value'", result);
    }

    /// <summary>
    /// 测试目的：调试渲染应区分前缀参数，并且不得替换嵌入标识符中的参数文本。
    /// </summary>
    [Fact]
    public void ToDebugSql_WhenParametersHavePrefixes_ShouldReplaceOnlyStandaloneParameters()
    {
        // Arrange
        var builder = _builder.AppendSelect("*").AppendFrom("[Test]")
            .AppendWhere("[Code]=x@p And [P]=@p And [P1]=@p1 And [P10]=@p10 And [Tenant]=@Tenant And [TenantId]=@TenantId")
            .AddParam("p", 1)
            .AddParam("p1", 11)
            .AddParam("p10", 110)
            .AddParam("Tenant", "tenant")
            .AddParam("TenantId", "tenant-id");

        // Act
        var result = builder.ToDebugSql(builder.ToSql());

        // Assert
        Assert.Equal("Select * \r\nFrom [Test] \r\nWhere [Code]=x@p And [P]=1 And [P1]=11 And [P10]=110 And [Tenant]='tenant' And [TenantId]='tenant-id'", result);
    }

    /// <summary>
    /// 测试目的：调试 SQL 应遮蔽敏感参数值，且不得改写字符串字面量中的同名文本。
    /// </summary>
    [Fact]
    public void ToDebugSql_WhenParameterIsSensitive_ShouldRedactOnlyExecutableParameterToken()
    {
        // Arrange
        var builder = _builder.AppendSelect("*").AppendFrom("[Test]")
            .AppendWhere("[Token]=@ApiToken And [Note]='@ApiToken'")
            .AddParam("ApiToken", "super-secret-token");

        // Act
        var result = builder.ToDebugSql(builder.ToSql());

        // Assert
        Assert.Equal("Select * \r\nFrom [Test] \r\nWhere [Token]='<redacted>' And [Note]='@ApiToken'", result);
        Assert.DoesNotContain("super-secret-token", result);
    }

    /// <summary>
    /// 测试目的：调试 SQL 必须遮蔽所有支持的凭据别名，普通参数仍应保留其可诊断值。
    /// </summary>
    [Fact]
    public void ToDebugSql_WhenCredentialAliasesAreUsed_ShouldRedactSensitiveValues()
    {
        // Arrange
        var builder = _builder.AppendSelect("*").AppendFrom("[Test]")
            .AppendWhere("[Pwd]=@pwd And [Credential]=@ClientCredential And [Auth]=@Authorization And [Signature]=@Signature And [Name]=@Name")
            .AddParam("pwd", "database-password")
            .AddParam("ClientCredential", "client-credential")
            .AddParam("Authorization", "Bearer access-token")
            .AddParam("Signature", "request-signature")
            .AddParam("Name", "Bing");

        // Act
        var result = builder.ToDebugSql(builder.ToSql());

        // Assert
        Assert.Equal("Select * \r\nFrom [Test] \r\nWhere [Pwd]='<redacted>' And [Credential]='<redacted>' And [Auth]='<redacted>' And [Signature]='<redacted>' And [Name]='Bing'", result);
        Assert.DoesNotContain("database-password", result);
        Assert.DoesNotContain("client-credential", result);
        Assert.DoesNotContain("access-token", result);
        Assert.DoesNotContain("request-signature", result);
    }

    /// <summary>
    /// 测试 - 大量参数的调试 SQL 应在一次渲染中完整替换每个独立参数标记，并保留相邻参数名的边界。
    /// </summary>
    [Fact]
    public void ToDebugSql_WhenManyParametersArePresent_ShouldReplaceEveryStandaloneToken()
    {
        // Arrange
        const int parameterCount = 128;
        var conditions = Enumerable.Range(0, parameterCount)
            .Select(index => $"[Value{index}]=@p{index}");
        var builder = _builder.AppendSelect("*").AppendFrom("[Test]")
            .AppendWhere(string.Join(" And ", conditions));
        for (var index = 0; index < parameterCount; index++)
            builder.AddParam($"p{index}", index);

        // Act
        var result = builder.ToDebugSql(builder.ToSql());

        // Assert
        for (var index = 0; index < parameterCount; index++)
        {
            Assert.Contains($"[Value{index}]={index}", result, StringComparison.Ordinal);
            Assert.DoesNotContain($"@p{index}", result, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// 测试目的：传入空 SQL 时应明确拒绝，避免调试渲染出现不可诊断的空引用异常。
    /// </summary>
    [Fact]
    public void ToDebugSql_WhenSqlIsNull_ShouldThrowArgumentNullException()
    {
        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => _builder.ToDebugSql(null));

        // Assert
        Assert.Equal("sql", exception.ParamName);
    }

    #endregion
}

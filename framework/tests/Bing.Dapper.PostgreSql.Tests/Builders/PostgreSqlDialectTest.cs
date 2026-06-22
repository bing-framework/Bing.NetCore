using Bing.Data.Sql.Builders;
using Shouldly;
using Xunit;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// <see cref="PostgreSqlDialect"/> 单元测试
/// </summary>
public class PostgreSqlDialectTest
{
    private readonly IDialect _dialect = PostgreSqlDialect.Instance;

    // ═══════════════════════════════════════════════════════════
    // 标识符
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：OpeningIdentifier 应为双引号 '"'，符合 PostgreSQL ANSI SQL 规范。
    /// </summary>
    [Fact]
    public void OpeningIdentifier_ShouldBeDoubleQuote()
    {
        _dialect.OpeningIdentifier.ShouldBe('"');
    }

    /// <summary>
    /// 测试目的：ClosingIdentifier 应为双引号 '"'。
    /// </summary>
    [Fact]
    public void ClosingIdentifier_ShouldBeDoubleQuote()
    {
        _dialect.ClosingIdentifier.ShouldBe('"');
    }

    // ═══════════════════════════════════════════════════════════
    // SafeName
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：SafeName 对普通表名应用双引号包裹（区分大小写语义）。
    /// </summary>
    [Fact]
    public void SafeName_PlainName_ShouldWrapWithDoubleQuotes()
    {
        _dialect.SafeName("orders").ShouldBe("\"orders\"");
    }

    /// <summary>
    /// 测试目的：SafeName 对通配符 "*" 应保持不变。
    /// </summary>
    [Fact]
    public void SafeName_Wildcard_ShouldReturnAsIs()
    {
        _dialect.SafeName("*").ShouldBe("*");
    }

    /// <summary>
    /// 测试目的：SafeName 对空字符串应返回空字符串，不抛异常。
    /// </summary>
    [Fact]
    public void SafeName_EmptyString_ShouldReturnEmpty()
    {
        _dialect.SafeName(string.Empty).ShouldBe(string.Empty);
    }

    /// <summary>
    /// 测试目的：SafeName 对已有方括号包裹的名称，应剥去后改用双引号包裹。
    /// </summary>
    [Fact]
    public void SafeName_AlreadyBracketWrapped_ShouldRewrap()
    {
        _dialect.SafeName("[user_id]").ShouldBe("\"user_id\"");
    }

    /// <summary>
    /// 测试目的：SafeName 对已有反引号包裹的名称，应剥去后改用双引号包裹。
    /// </summary>
    [Fact]
    public void SafeName_BacktickWrapped_ShouldRewrap()
    {
        _dialect.SafeName("`created_at`").ShouldBe("\"created_at\"");
    }

    // ═══════════════════════════════════════════════════════════
    // GetPrefix（继承自 DialectBase）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：GetPrefix() 应返回基类默认值 "@"（PostgreSqlDialect 未覆盖该方法）。
    /// </summary>
    [Fact]
    public void GetPrefix_ShouldReturnAtSign()
    {
        _dialect.GetPrefix().ShouldBe("@");
    }

    // ═══════════════════════════════════════════════════════════
    // SupportSelectAs（继承自 DialectBase）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：SupportSelectAs() 应返回 true（PostgreSqlDialect 未覆盖，继承基类默认值）。
    /// </summary>
    [Fact]
    public void SupportSelectAs_ShouldReturnTrue()
    {
        _dialect.SupportSelectAs().ShouldBeTrue();
    }

    // ═══════════════════════════════════════════════════════════
    // GetParamValue（继承自 DialectBase）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：GetParamValue 对 bool 值应保持原始类型（PostgreSQL 原生支持 boolean 类型）。
    /// </summary>
    [Fact]
    public void GetParamValue_Bool_ShouldReturnOriginalValue()
    {
        _dialect.GetParamValue(true).ShouldBe(true);
        _dialect.GetParamValue(false).ShouldBe(false);
    }

    /// <summary>
    /// 测试目的：GetParamValue 对整数值应保持原始类型（PostgreSQL 原生支持整数）。
    /// </summary>
    [Fact]
    public void GetParamValue_Int_ShouldReturnOriginalValue()
    {
        _dialect.GetParamValue(42).ShouldBe(42);
    }

    /// <summary>
    /// 测试目的：GetParamValue 对 null 应返回 null（基类默认行为，与 Oracle 不同）。
    /// </summary>
    [Fact]
    public void GetParamValue_Null_ShouldReturnNull()
    {
        _dialect.GetParamValue(null).ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：GetParamValue 对字符串应返回相同字符串（原样透传）。
    /// </summary>
    [Fact]
    public void GetParamValue_String_ShouldReturnSameString()
    {
        _dialect.GetParamValue("hello").ShouldBe("hello");
    }

    // ═══════════════════════════════════════════════════════════
    // Instance
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：PostgreSqlDialect.Instance 应为 PostgreSqlDialect 类型，不为 null。
    /// </summary>
    [Fact]
    public void Instance_ShouldBePostgreSqlDialectType()
    {
        var instance = PostgreSqlDialect.Instance;
        instance.ShouldNotBeNull();
        instance.ShouldBeOfType<PostgreSqlDialect>();
    }
}

using Bing.Data.Sql.Builders;
using Shouldly;
using Xunit;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// <see cref="MySqlDialect"/> 单元测试
/// </summary>
public class MySqlDialectTest
{
    private readonly IDialect _dialect = MySqlDialect.Instance;

    // ═══════════════════════════════════════════════════════════
    // 标识符
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：OpeningIdentifier 应为反引号 '`'，符合 MySQL 标识符规范。
    /// </summary>
    [Fact]
    public void OpeningIdentifier_ShouldBeBacktick()
    {
        _dialect.OpeningIdentifier.ShouldBe('`');
    }

    /// <summary>
    /// 测试目的：ClosingIdentifier 应为反引号 '`'。
    /// </summary>
    [Fact]
    public void ClosingIdentifier_ShouldBeBacktick()
    {
        _dialect.ClosingIdentifier.ShouldBe('`');
    }

    // ═══════════════════════════════════════════════════════════
    // SafeName
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：SafeName 对普通名称应用反引号包裹，以适配 MySQL 保留字规避。
    /// </summary>
    [Fact]
    public void SafeName_PlainName_ShouldWrapWithBackticks()
    {
        _dialect.SafeName("user").ShouldBe("`user`");
    }

    /// <summary>
    /// 测试目的：SafeName 对通配符 "*" 应保持不变，不包裹。
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
    /// 测试目的：SafeName 对 null 应返回空字符串，不抛异常。
    /// </summary>
    [Fact]
    public void SafeName_Null_ShouldReturnEmpty()
    {
        _dialect.SafeName(null).ShouldBe(string.Empty);
    }

    /// <summary>
    /// 测试目的：SafeName 对已有方括号包裹的名称，应剥去后改用反引号包裹。
    /// </summary>
    [Fact]
    public void SafeName_AlreadyBracketWrapped_ShouldRewrapWithBacktick()
    {
        _dialect.SafeName("[order_id]").ShouldBe("`order_id`");
    }

    /// <summary>
    /// 测试目的：SafeName 对已有双引号包裹的名称，应剥去后改用反引号包裹。
    /// </summary>
    [Fact]
    public void SafeName_AlreadyDoubleQuoteWrapped_ShouldRewrapWithBacktick()
    {
        _dialect.SafeName("\"created_at\"").ShouldBe("`created_at`");
    }

    /// <summary>
    /// 测试目的：SafeName 对已有反引号包裹的名称，应剥去后重新包裹（幂等性）。
    /// </summary>
    [Fact]
    public void SafeName_AlreadyBacktickWrapped_ShouldBeIdempotent()
    {
        _dialect.SafeName("`table_name`").ShouldBe("``table_name``");
    }

    // ═══════════════════════════════════════════════════════════
    // GetPrefix（继承自 DialectBase）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：GetPrefix() 应返回 "@"（MySqlDialect 未覆盖，继承基类默认值）。
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
    /// 测试目的：SupportSelectAs() 应返回 true（MySqlDialect 未覆盖，继承基类默认值）。
    /// </summary>
    [Fact]
    public void SupportSelectAs_ShouldReturnTrue()
    {
        _dialect.SupportSelectAs().ShouldBeTrue();
    }

    // ═══════════════════════════════════════════════════════════
    // GenerateName（继承自 DialectBase）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：GenerateName(0) 应返回 "@_p_0"（继承基类默认格式）。
    /// </summary>
    [Fact]
    public void GenerateName_Index0_ShouldReturnAtP0()
    {
        _dialect.GenerateName(0).ShouldBe("@_p_0");
    }

    // ═══════════════════════════════════════════════════════════
    // GetParamValue（继承自 DialectBase）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：GetParamValue(bool) 应保持原始类型（基类直接透传）。
    /// </summary>
    [Fact]
    public void GetParamValue_Bool_ShouldReturnOriginalValue()
    {
        _dialect.GetParamValue(true).ShouldBe(true);
        _dialect.GetParamValue(false).ShouldBe(false);
    }

    /// <summary>
    /// 测试目的：GetParamValue(null) 应返回 null（基类直接透传，与 Oracle 不同）。
    /// </summary>
    [Fact]
    public void GetParamValue_Null_ShouldReturnNull()
    {
        _dialect.GetParamValue(null).ShouldBeNull();
    }

    // ═══════════════════════════════════════════════════════════
    // Instance
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：MySqlDialect.Instance 应为 MySqlDialect 类型，不为 null。
    /// </summary>
    [Fact]
    public void Instance_ShouldBeMySqlDialectType()
    {
        var instance = MySqlDialect.Instance;
        instance.ShouldNotBeNull();
        instance.ShouldBeOfType<MySqlDialect>();
    }
}

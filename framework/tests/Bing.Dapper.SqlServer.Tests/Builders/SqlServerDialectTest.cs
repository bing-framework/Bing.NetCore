using Bing.Data.Sql.Builders;
using Shouldly;
using Xunit;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// <see cref="SqlServerDialect"/> 单元测试
/// </summary>
public class SqlServerDialectTest
{
    private readonly IDialect _dialect = SqlServerDialect.Instance;

    // ═══════════════════════════════════════════════════════════
    // 标识符（默认方括号）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：OpeningIdentifier 应为方括号 '['，符合 SQL Server 标识符规范。
    /// </summary>
    [Fact]
    public void OpeningIdentifier_ShouldBeLeftBracket()
    {
        _dialect.OpeningIdentifier.ShouldBe('[');
    }

    /// <summary>
    /// 测试目的：ClosingIdentifier 应为方括号 ']'。
    /// </summary>
    [Fact]
    public void ClosingIdentifier_ShouldBeRightBracket()
    {
        _dialect.ClosingIdentifier.ShouldBe(']');
    }

    // ═══════════════════════════════════════════════════════════
    // SafeName
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：SafeName 对普通名称应用 [name] 包裹，适配 SQL Server 保留字规避。
    /// </summary>
    [Fact]
    public void SafeName_PlainName_ShouldWrapWithBrackets()
    {
        _dialect.SafeName("user").ShouldBe("[user]");
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
    /// 测试目的：SafeName 对 null 应返回空字符串，不抛 NullReferenceException。
    /// </summary>
    [Fact]
    public void SafeName_Null_ShouldReturnEmpty()
    {
        _dialect.SafeName(null!).ShouldBe(string.Empty);
    }

    /// <summary>
    /// 测试目的：SafeName 对已有方括号包裹的名称应先剥离再重新包裹，保持幂等。
    /// </summary>
    [Fact]
    public void SafeName_AlreadyBracketed_ShouldBeIdempotent()
    {
        _dialect.SafeName("[order]").ShouldBe("[order]");
    }

    /// <summary>
    /// 测试目的：SafeName 对含双引号包裹的名称应正确转换为方括号格式。
    /// </summary>
    [Fact]
    public void SafeName_DoubleQuoted_ShouldConvertToBrackets()
    {
        _dialect.SafeName("\"order\"").ShouldBe("[order]");
    }

    /// <summary>
    /// 测试目的：SafeName 对含空格的名称应用方括号包裹。
    /// </summary>
    [Fact]
    public void SafeName_NameWithSpace_ShouldWrapWithBrackets()
    {
        _dialect.SafeName("order item").ShouldBe("[order item]");
    }

    // ═══════════════════════════════════════════════════════════
    // 参数前缀
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：GetPrefix() 应返回 "@"，符合 SQL Server 参数命名约定。
    /// </summary>
    [Fact]
    public void GetPrefix_ShouldReturnAt()
    {
        _dialect.GetPrefix().ShouldBe("@");
    }

    // ═══════════════════════════════════════════════════════════
    // 参数名生成
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：GenerateName(0) 应生成 "@_p_0"。
    /// </summary>
    [Fact]
    public void GenerateName_Zero_ShouldReturnExpected()
    {
        _dialect.GenerateName(0).ShouldBe("@_p_0");
    }

    /// <summary>
    /// 测试目的：GenerateName(99) 应生成 "@_p_99"，验证多位数序号格式正确。
    /// </summary>
    [Fact]
    public void GenerateName_LargeIndex_ShouldReturnExpected()
    {
        _dialect.GenerateName(99).ShouldBe("@_p_99");
    }

    // ═══════════════════════════════════════════════════════════
    // SelectAs 支持
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：SupportSelectAs() 应返回 true，SQL Server 支持 SELECT 列别名。
    /// </summary>
    [Fact]
    public void SupportSelectAs_ShouldReturnTrue()
    {
        _dialect.SupportSelectAs().ShouldBeTrue();
    }

    // ═══════════════════════════════════════════════════════════
    // GetParamValue 透传
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：GetParamValue 对布尔值 true 应原样透传（SQL Server 支持 bit 映射）。
    /// </summary>
    [Fact]
    public void GetParamValue_Bool_ShouldPassThrough()
    {
        _dialect.GetParamValue(true).ShouldBe(true);
    }

    /// <summary>
    /// 测试目的：GetParamValue 对 null 应原样透传。
    /// </summary>
    [Fact]
    public void GetParamValue_Null_ShouldReturnNull()
    {
        _dialect.GetParamValue(null).ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：GetParamValue 对整数值应原样透传。
    /// </summary>
    [Fact]
    public void GetParamValue_Int_ShouldPassThrough()
    {
        _dialect.GetParamValue(42).ShouldBe(42);
    }

    // ═══════════════════════════════════════════════════════════
    // Instance 单例
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Instance 每次访问应返回 SqlServerDialect 类型的非 null 实例。
    /// </summary>
    [Fact]
    public void Instance_ShouldReturnSqlServerDialect()
    {
        var instance = SqlServerDialect.Instance;
        instance.ShouldNotBeNull();
        instance.ShouldBeOfType<SqlServerDialect>();
    }
}

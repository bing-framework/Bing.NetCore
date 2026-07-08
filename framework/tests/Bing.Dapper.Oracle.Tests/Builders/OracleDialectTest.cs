using Bing.Data.Sql.Builders;
using Shouldly;
using Xunit;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// <see cref="OracleDialect"/> 单元测试
/// </summary>
public class OracleDialectTest
{
    private readonly IDialect _dialect = OracleDialect.Instance;

    // ═══════════════════════════════════════════════════════════
    // 标识符
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：OpeningIdentifier 应为双引号 '"'，符合 Oracle 标准。
    /// </summary>
    [Fact]
    public void OpeningIdentifier_ShouldBeDoubleQuote()
    {
        _dialect.OpeningIdentifier.ShouldBe('"');
    }

    /// <summary>
    /// 测试目的：ClosingIdentifier 应为双引号 '"'，符合 Oracle 标准。
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
    /// 测试目的：SafeName 对普通标识符应用双引号包裹，适配 Oracle 风格。
    /// </summary>
    [Fact]
    public void SafeName_PlainName_ShouldWrapWithDoubleQuotes()
    {
        _dialect.SafeName("TableName").ShouldBe("\"TableName\"");
    }

    /// <summary>
    /// 测试目的：SafeName 对通配符 "*" 应保持不变（不包裹引号）。
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
    /// 测试目的：SafeName 对已有方括号包裹的名称，应剥去方括号后再用双引号包裹。
    /// </summary>
    [Fact]
    public void SafeName_AlreadyBracketWrapped_ShouldRewrap()
    {
        _dialect.SafeName("[ColName]").ShouldBe("\"ColName\"");
    }

    /// <summary>
    /// 测试目的：SafeName 对已有反引号包裹的名称，应剥去反引号后再用双引号包裹。
    /// </summary>
    [Fact]
    public void SafeName_AlreadyBacktickWrapped_ShouldRewrap()
    {
        _dialect.SafeName("`ColName`").ShouldBe("\"ColName\"");
    }

    // ═══════════════════════════════════════════════════════════
    // GetPrefix
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：GetPrefix() 应返回 ":"，Oracle 绑定变量用冒号前缀。
    /// </summary>
    [Fact]
    public void GetPrefix_ShouldReturnColon()
    {
        _dialect.GetPrefix().ShouldBe(":");
    }

    // ═══════════════════════════════════════════════════════════
    // SupportSelectAs
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：SupportSelectAs() 应返回 false，Oracle Select 子句不支持 AS 关键字（直接空格）。
    /// </summary>
    [Fact]
    public void SupportSelectAs_ShouldReturnFalse()
    {
        _dialect.SupportSelectAs().ShouldBeFalse();
    }

    // ═══════════════════════════════════════════════════════════
    // GenerateName
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：GenerateName(0) 应返回 ":p_0"，使用冒号前缀。
    /// </summary>
    [Fact]
    public void GenerateName_Index0_ShouldReturnColonP0()
    {
        _dialect.GenerateName(0).ShouldBe(":p_0");
    }

    /// <summary>
    /// 测试目的：GenerateName(5) 应返回 ":p_5"。
    /// </summary>
    [Fact]
    public void GenerateName_Index5_ShouldReturnColonP5()
    {
        _dialect.GenerateName(5).ShouldBe(":p_5");
    }

    // ═══════════════════════════════════════════════════════════
    // GetParamName
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：GetParamName 对以 ":" 开头的名称，应剥去前缀冒号后返回。
    /// </summary>
    [Fact]
    public void GetParamName_WithColonPrefix_ShouldStripColon()
    {
        _dialect.GetParamName(":p_0").ShouldBe("p_0");
    }

    /// <summary>
    /// 测试目的：GetParamName 对不含冒号前缀的名称，应直接返回原名。
    /// </summary>
    [Fact]
    public void GetParamName_WithoutColonPrefix_ShouldReturnAsIs()
    {
        _dialect.GetParamName("p_0").ShouldBe("p_0");
    }

    // ═══════════════════════════════════════════════════════════
    // GetParamValue
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：GetParamValue(null) 应返回空字符串，而不是 null，避免绑定时报错。
    /// </summary>
    [Fact]
    public void GetParamValue_WhenNull_ShouldReturnEmptyString()
    {
        _dialect.GetParamValue(null).ShouldBe(string.Empty);
    }

    /// <summary>
    /// 测试目的：GetParamValue(true) 应返回整数 1（Oracle 无 bool 类型，用 1/0 替代）。
    /// </summary>
    [Fact]
    public void GetParamValue_BoolTrue_ShouldReturn1()
    {
        _dialect.GetParamValue(true).ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：GetParamValue(false) 应返回整数 0。
    /// </summary>
    [Fact]
    public void GetParamValue_BoolFalse_ShouldReturn0()
    {
        _dialect.GetParamValue(false).ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：GetParamValue(int16 值) 应转换为字符串（Oracle ODP 参数需显式字符串）。
    /// </summary>
    [Fact]
    public void GetParamValue_Int16_ShouldReturnString()
    {
        _dialect.GetParamValue((short)42).ShouldBe("42");
    }

    /// <summary>
    /// 测试目的：GetParamValue(int32 值) 应转换为字符串。
    /// </summary>
    [Fact]
    public void GetParamValue_Int32_ShouldReturnString()
    {
        _dialect.GetParamValue(100).ShouldBe("100");
    }

    /// <summary>
    /// 测试目的：GetParamValue(int64 值) 应转换为字符串。
    /// </summary>
    [Fact]
    public void GetParamValue_Int64_ShouldReturnString()
    {
        _dialect.GetParamValue(9999999999L).ShouldBe("9999999999");
    }

    /// <summary>
    /// 测试目的：GetParamValue(float 值) 应转换为字符串。
    /// </summary>
    [Fact]
    public void GetParamValue_Single_ShouldReturnString()
    {
        var result = _dialect.GetParamValue(3.14f);
        result.ShouldBeOfType<string>();
    }

    /// <summary>
    /// 测试目的：GetParamValue(double 值) 应转换为字符串。
    /// </summary>
    [Fact]
    public void GetParamValue_Double_ShouldReturnString()
    {
        var result = _dialect.GetParamValue(3.14d);
        result.ShouldBeOfType<string>();
    }

    /// <summary>
    /// 测试目的：GetParamValue(decimal 值) 应转换为字符串。
    /// </summary>
    [Fact]
    public void GetParamValue_Decimal_ShouldReturnString()
    {
        _dialect.GetParamValue(9.99m).ShouldBe("9.99");
    }

    /// <summary>
    /// 测试目的：GetParamValue(string 值) 应转换为字符串（$"{value}" 格式）。
    /// </summary>
    [Fact]
    public void GetParamValue_String_ShouldReturnSameString()
    {
        _dialect.GetParamValue("hello").ShouldBe("hello");
    }

    /// <summary>
    /// 测试目的：GetParamValue(Guid 值) 应转换为字符串（走 default 分支）。
    /// </summary>
    [Fact]
    public void GetParamValue_Guid_ShouldReturnGuidString()
    {
        var guid = Guid.NewGuid();
        _dialect.GetParamValue(guid).ShouldBe(guid.ToString());
    }

    // ═══════════════════════════════════════════════════════════
    // Instance 唯一性
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：每次访问 OracleDialect.Instance 应返回新实例（非单例，但均为 OracleDialect 类型）。
    /// </summary>
    [Fact]
    public void Instance_ShouldBeOracleDialectType()
    {
        var instance = OracleDialect.Instance;
        instance.ShouldNotBeNull();
        instance.ShouldBeOfType<OracleDialect>();
    }
}

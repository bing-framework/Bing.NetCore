using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Shouldly;
using Xunit;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// <see cref="PostgreSqlParamLiteralsResolver"/> 单元测试。
/// 验证各类型参数的字面值输出：null、bool、整型/浮点型（无引号）、默认类型（有单引号）。
/// 不依赖数据库连接，纯逻辑单元测试。
/// </summary>
public class PostgreSqlParamLiteralsResolverTest
{
    private readonly IParamLiteralsResolver _resolver = PostgreSqlParamLiteralsResolver.Instance;

    // ─── null ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：null 值应返回 PostgreSQL 空字符串字面值 "''"。
    /// </summary>
    [Fact]
    public void GetParamLiterals_NullValue_ShouldReturnEmptyStringLiteral()
    {
        // Act & Assert
        _resolver.GetParamLiterals(null).ShouldBe("''");
    }

    // ─── boolean ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：bool true 应返回 PostgreSQL 布尔字面值 "true"（无引号）。
    /// </summary>
    [Fact]
    public void GetParamLiterals_BoolTrue_ShouldReturnLowercaseTrue()
    {
        // Act & Assert
        _resolver.GetParamLiterals(true).ShouldBe("true");
    }

    /// <summary>
    /// 测试目的：bool false 应返回 PostgreSQL 布尔字面值 "false"（无引号）。
    /// </summary>
    [Fact]
    public void GetParamLiterals_BoolFalse_ShouldReturnLowercaseFalse()
    {
        // Act & Assert
        _resolver.GetParamLiterals(false).ShouldBe("false");
    }

    // ─── 整数类型 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：int32 应直接返回数字字符串，不加单引号。
    /// </summary>
    [Fact]
    public void GetParamLiterals_Int32_ShouldReturnRawNumber()
    {
        // Act & Assert
        _resolver.GetParamLiterals(42).ShouldBe("42");
    }

    /// <summary>
    /// 测试目的：int16 (short) 应直接返回数字字符串，不加单引号。
    /// </summary>
    [Fact]
    public void GetParamLiterals_Int16_ShouldReturnRawNumber()
    {
        // Arrange
        short value = 100;

        // Act & Assert
        _resolver.GetParamLiterals(value).ShouldBe("100");
    }

    /// <summary>
    /// 测试目的：int64 (long) 应直接返回数字字符串，不加单引号。
    /// </summary>
    [Fact]
    public void GetParamLiterals_Int64_ShouldReturnRawNumber()
    {
        // Act & Assert
        _resolver.GetParamLiterals(9999L).ShouldBe("9999");
    }

    /// <summary>
    /// 测试目的：负整数也应直接输出（无引号）。
    /// </summary>
    [Fact]
    public void GetParamLiterals_NegativeInt_ShouldReturnRawNumber()
    {
        // Act & Assert
        _resolver.GetParamLiterals(-1).ShouldBe("-1");
    }

    // ─── 浮点/精度类型（验证无引号，不验证具体格式以避免文化依赖）─────────────

    /// <summary>
    /// 测试目的：float (Single) 应不带引号输出，属于数值分支。
    /// </summary>
    [Fact]
    public void GetParamLiterals_Float_ShouldNotWrapWithSingleQuotes()
    {
        // Act
        var result = _resolver.GetParamLiterals(3.14f);

        // Assert：数值类型不应有单引号包围
        result.ShouldNotStartWith("'");
        result.ShouldNotEndWith("'");
    }

    /// <summary>
    /// 测试目的：double 应不带引号输出，属于数值分支。
    /// </summary>
    [Fact]
    public void GetParamLiterals_Double_ShouldNotWrapWithSingleQuotes()
    {
        // Act
        var result = _resolver.GetParamLiterals(1.5d);

        // Assert
        result.ShouldNotStartWith("'");
        result.ShouldNotEndWith("'");
    }

    /// <summary>
    /// 测试目的：decimal 应不带引号输出，属于数值分支。
    /// </summary>
    [Fact]
    public void GetParamLiterals_Decimal_ShouldNotWrapWithSingleQuotes()
    {
        // Act
        var result = _resolver.GetParamLiterals(9.99m);

        // Assert
        result.ShouldNotStartWith("'");
        result.ShouldNotEndWith("'");
    }

    // ─── 默认分支（字符串/Guid/DateTime 等） ─────────────────────────────────

    /// <summary>
    /// 测试目的：字符串类型属于 default 分支，应被单引号包围。
    /// </summary>
    [Fact]
    public void GetParamLiterals_String_ShouldWrapWithSingleQuotes()
    {
        // Act
        var result = _resolver.GetParamLiterals("hello");

        // Assert
        result.ShouldBe("'hello'");
    }

    /// <summary>
    /// 测试目的：空字符串应被单引号包围（"''"），区别于 null 的 "''"（但结果相同）。
    /// </summary>
    [Fact]
    public void GetParamLiterals_EmptyString_ShouldWrapWithSingleQuotes()
    {
        // Act
        var result = _resolver.GetParamLiterals(string.Empty);

        // Assert
        result.ShouldBe("''");
    }

    /// <summary>
    /// 测试目的：Guid 属于 default 分支，应被单引号包围。
    /// </summary>
    [Fact]
    public void GetParamLiterals_Guid_ShouldWrapWithSingleQuotes()
    {
        // Arrange
        var guid = new Guid("12345678-1234-1234-1234-123456789abc");

        // Act
        var result = _resolver.GetParamLiterals(guid);

        // Assert
        result.ShouldStartWith("'");
        result.ShouldEndWith("'");
        result.ShouldContain("12345678");
    }

    /// <summary>
    /// 测试目的：DateTime 属于 default 分支，应被单引号包围。
    /// </summary>
    [Fact]
    public void GetParamLiterals_DateTime_ShouldWrapWithSingleQuotes()
    {
        // Arrange
        var dt = new DateTime(2024, 6, 15);

        // Act
        var result = _resolver.GetParamLiterals(dt);

        // Assert
        result.ShouldStartWith("'");
        result.ShouldEndWith("'");
    }

    // ─── Instance 访问 ────────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：Instance 属性应返回 IParamLiteralsResolver 实现，功能可用。
    /// </summary>
    [Fact]
    public void Instance_ShouldReturnFunctionalResolver()
    {
        // Arrange & Act
        var inst = PostgreSqlParamLiteralsResolver.Instance;

        // Assert
        inst.ShouldNotBeNull();
        // 验证基本功能可用（null→空字符串字面值）
        inst.GetParamLiterals(null).ShouldBe("''");
    }

    /// <summary>
    /// 测试目的：Instance 应返回可安全共享的同一无状态解析器实例。
    /// </summary>
    [Fact]
    public void Instance_CalledTwice_ShouldReturnSameInstance()
    {
        // Arrange & Act
        var first = PostgreSqlParamLiteralsResolver.Instance;
        var second = PostgreSqlParamLiteralsResolver.Instance;

        // Assert
        ReferenceEquals(first, second).ShouldBeTrue();
    }
}

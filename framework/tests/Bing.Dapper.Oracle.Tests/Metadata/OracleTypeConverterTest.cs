using Bing.Data.Metadata;
using Shouldly;
using Xunit;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// <see cref="OracleTypeConverter"/> 单元测试。
/// 当前实现中除空输入返回 null 外，所有类型均抛出 NotImplementedException（待实现）。
/// 测试目的是锁定现有边界行为，防止意外改动破坏空输入语义。
/// </summary>
public class OracleTypeConverterTest
{
    private readonly OracleTypeConverter _converter = new();

    // ═══════════════════════════════════════════════════════════
    // 边界：空输入 → null（已实现）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：dataType 为 null 时应返回 null，不抛异常。
    /// </summary>
    [Fact]
    public void ToDbType_Null_ShouldReturnNull()
    {
        _converter.ToDbType(null).ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：dataType 为空字符串时应返回 null。
    /// </summary>
    [Fact]
    public void ToDbType_Empty_ShouldReturnNull()
    {
        _converter.ToDbType(string.Empty).ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：dataType 为空白字符串时应返回 null。
    /// </summary>
    [Fact]
    public void ToDbType_Whitespace_ShouldReturnNull()
    {
        _converter.ToDbType("   ").ShouldBeNull();
    }

    // ═══════════════════════════════════════════════════════════
    // 未实现类型 → NotImplementedException（占位）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：传入任意非空类型时应抛出 NotImplementedException（当前 Oracle 实现尚未完成）。
    /// </summary>
    [Theory]
    [InlineData("varchar2")]
    [InlineData("number")]
    [InlineData("date")]
    [InlineData("clob")]
    [InlineData("blob")]
    public void ToDbType_AnyNonEmptyType_ShouldThrowNotImplementedException(string dataType)
    {
        Should.Throw<NotImplementedException>(() => _converter.ToDbType(dataType));
    }
}

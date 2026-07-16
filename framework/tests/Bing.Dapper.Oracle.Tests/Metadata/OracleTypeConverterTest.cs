using Bing.Data.Metadata;
using System.Data;
using Shouldly;
using Xunit;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// <see cref="OracleTypeConverter"/> 单元测试。
/// 测试常见 Oracle 类型映射与未知类型的明确错误语义。
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

    /// <summary>
    /// 测试目的：常见 Oracle 数据类型应映射为对应的 DbType。
    /// </summary>
    [Theory]
    [InlineData("varchar2", DbType.String)]
    [InlineData("number", DbType.Decimal)]
    [InlineData("date", DbType.DateTime)]
    [InlineData("timestamp with time zone", DbType.DateTimeOffset)]
    [InlineData("clob", DbType.String)]
    [InlineData("blob", DbType.Binary)]
    [InlineData("xmltype", DbType.Xml)]
    public void ToDbType_WhenDataTypeIsSupported_ShouldReturnMappedDbType(string dataType, DbType expected)
    {
        // Act
        var result = _converter.ToDbType(dataType);

        // Assert
        result.ShouldBe(expected);
    }

    /// <summary>
    /// 测试目的：未知 Oracle 数据类型应抛出明确的不支持异常。
    /// </summary>
    [Fact]
    public void ToDbType_WhenDataTypeIsUnknown_ShouldThrowNotSupportedException()
    {
        // Act
        var exception = Should.Throw<NotSupportedException>(() => _converter.ToDbType("unsupported_type"));

        // Assert
        exception.Message.ShouldContain("unsupported_type");
    }
}

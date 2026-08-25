using System.Data;
using System.Globalization;
using Bing.Data.Metadata;
using Shouldly;
using Xunit;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// <see cref="PostgreSqlTypeConverter"/> 单元测试。
/// 验证每个 PostgreSQL 数据类型字符串都能正确映射为 DbType，以及边界/负例行为。
/// </summary>
public class PostgreSqlTypeConverterTest
{
    private readonly PostgreSqlTypeConverter _converter = new();

    // ═══════════════════════════════════════════════════════════
    // 边界：空输入
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
    // 负例：未知类型抛出 NotSupportedException
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：未知类型应抛出带 Provider 和类型信息的 NotSupportedException。
    /// </summary>
    [Fact]
    public void ToDbType_UnknownType_ShouldThrowNotSupportedException()
    {
        var exception = Should.Throw<NotSupportedException>(() => _converter.ToDbType("pg_unknown_type"));
        exception.Message.ShouldContain("PostgreSQL");
        exception.Message.ShouldContain("pg_unknown_type");
        exception.Message.ShouldContain("扩展 PostgreSqlTypeConverter 映射");
    }

    // ═══════════════════════════════════════════════════════════
    // 大小写不敏感
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：类型名称大小写不敏感，"INT4" 应与 "int4" 映射相同。
    /// </summary>
    [Fact]
    public void ToDbType_CaseInsensitive_ShouldMatch()
    {
        _converter.ToDbType("INT4").ShouldBe(DbType.Int32);
        _converter.ToDbType("Int4").ShouldBe(DbType.Int32);
    }

    /// <summary>
    /// 测试目的：类型名称转换不得受当前区域文化影响，土耳其语环境中的 INT4 仍应映射为 Int32。
    /// </summary>
    [Fact]
    public void ToDbType_WhenCurrentCultureIsTurkish_ShouldUseInvariantTypeName()
    {
        // Arrange
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            // Act
            var result = _converter.ToDbType("INT4");

            // Assert
            result.ShouldBe(DbType.Int32);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // GUID 类型
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：uuid → DbType.Guid（PostgreSQL UUID 类型）。
    /// </summary>
    [Fact]
    public void ToDbType_Uuid_ShouldBeGuid()
    {
        _converter.ToDbType("uuid").ShouldBe(DbType.Guid);
    }

    // ═══════════════════════════════════════════════════════════
    // 字符串类型
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：varchar/text/json/jsonb/xml → DbType.String。
    /// </summary>
    [Theory]
    [InlineData("varchar")]
    [InlineData("text")]
    [InlineData("json")]
    [InlineData("jsonb")]
    [InlineData("xml")]
    public void ToDbType_StringTypes_ShouldBeString(string dataType)
    {
        _converter.ToDbType(dataType).ShouldBe(DbType.String);
    }

    // ═══════════════════════════════════════════════════════════
    // 布尔类型
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：bool → DbType.Boolean。
    /// </summary>
    [Fact]
    public void ToDbType_Bool_ShouldBeBoolean()
    {
        _converter.ToDbType("bool").ShouldBe(DbType.Boolean);
    }

    // ═══════════════════════════════════════════════════════════
    // 整数类型
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：char → DbType.Byte（PostgreSQL 中 char 映射为单字节）。
    /// </summary>
    [Fact]
    public void ToDbType_Char_ShouldBeByte()
    {
        _converter.ToDbType("char").ShouldBe(DbType.Byte);
    }

    /// <summary>
    /// 测试目的：int2 → DbType.Int16（2 字节整数）。
    /// </summary>
    [Fact]
    public void ToDbType_Int2_ShouldBeInt16()
    {
        _converter.ToDbType("int2").ShouldBe(DbType.Int16);
    }

    /// <summary>
    /// 测试目的：int4 → DbType.Int32（4 字节整数）。
    /// </summary>
    [Fact]
    public void ToDbType_Int4_ShouldBeInt32()
    {
        _converter.ToDbType("int4").ShouldBe(DbType.Int32);
    }

    /// <summary>
    /// 测试目的：int8 → DbType.Int64（8 字节整数）。
    /// </summary>
    [Fact]
    public void ToDbType_Int8_ShouldBeInt64()
    {
        _converter.ToDbType("int8").ShouldBe(DbType.Int64);
    }

    // ═══════════════════════════════════════════════════════════
    // 浮点/精确数值类型
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：float4 → DbType.Single（4 字节浮点）。
    /// </summary>
    [Fact]
    public void ToDbType_Float4_ShouldBeSingle()
    {
        _converter.ToDbType("float4").ShouldBe(DbType.Single);
    }

    /// <summary>
    /// 测试目的：float8 → DbType.Double（8 字节浮点）。
    /// </summary>
    [Fact]
    public void ToDbType_Float8_ShouldBeDouble()
    {
        _converter.ToDbType("float8").ShouldBe(DbType.Double);
    }

    /// <summary>
    /// 测试目的：numeric/decimal → DbType.Decimal（精确小数）。
    /// </summary>
    [Theory]
    [InlineData("numeric")]
    [InlineData("decimal")]
    public void ToDbType_DecimalTypes_ShouldBeDecimal(string dataType)
    {
        _converter.ToDbType(dataType).ShouldBe(DbType.Decimal);
    }

    // ═══════════════════════════════════════════════════════════
    // 日期时间类型
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：date → DbType.Date（仅日期，不含时间）。
    /// </summary>
    [Fact]
    public void ToDbType_Date_ShouldBeDate()
    {
        _converter.ToDbType("date").ShouldBe(DbType.Date);
    }

    /// <summary>
    /// 测试目的：time/timetz → DbType.Time（含时区与不含时区均映射为 Time）。
    /// </summary>
    [Theory]
    [InlineData("time")]
    [InlineData("timetz")]
    public void ToDbType_TimeTypes_ShouldBeTime(string dataType)
    {
        _converter.ToDbType(dataType).ShouldBe(DbType.Time);
    }

    /// <summary>
    /// 测试目的：timestamp/timestamptz → DbType.DateTime（含时区与不含时区均映射为 DateTime）。
    /// </summary>
    [Theory]
    [InlineData("timestamp")]
    [InlineData("timestamptz")]
    public void ToDbType_TimestampTypes_ShouldBeDateTime(string dataType)
    {
        _converter.ToDbType(dataType).ShouldBe(DbType.DateTime);
    }

    // ═══════════════════════════════════════════════════════════
    // 二进制类型
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：bytea → DbType.Binary（PostgreSQL 二进制数据类型）。
    /// </summary>
    [Fact]
    public void ToDbType_Bytea_ShouldBeBinary()
    {
        _converter.ToDbType("bytea").ShouldBe(DbType.Binary);
    }
}

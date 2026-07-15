using System.Data;
using Bing.Data.Metadata;
using Shouldly;
using Xunit;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// <see cref="SqlServerTypeConverter"/> 单元测试。
/// 验证每个 SQL Server 数据类型字符串都能正确映射为 DbType，以及边界/负例行为。
/// </summary>
public class SqlServerTypeConverterTest
{
    private readonly SqlServerTypeConverter _converter = new();

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
    // 负例：未知类型抛出 NotImplementedException
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：未知类型应抛出 NotImplementedException，便于发现未覆盖的类型映射。
    /// </summary>
    [Fact]
    public void ToDbType_UnknownType_ShouldThrowNotImplementedException()
    {
        Should.Throw<NotImplementedException>(() => _converter.ToDbType("unknown_type"));
    }

    // ═══════════════════════════════════════════════════════════
    // 大小写不敏感
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：类型名称大小写不敏感，"INT" 应与 "int" 映射相同。
    /// </summary>
    [Fact]
    public void ToDbType_CaseInsensitive_ShouldMatch()
    {
        _converter.ToDbType("INT").ShouldBe(DbType.Int32);
        _converter.ToDbType("Int").ShouldBe(DbType.Int32);
    }

    // ═══════════════════════════════════════════════════════════
    // GUID 类型
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：uniqueidentifier → DbType.Guid（SQL Server GUID 存储类型）。
    /// </summary>
    [Theory]
    [InlineData("uniqueidentifier")]
    public void ToDbType_UniqueIdentifier_ShouldBeGuid(string dataType)
    {
        _converter.ToDbType(dataType).ShouldBe(DbType.Guid);
    }

    // ═══════════════════════════════════════════════════════════
    // 字符串类型
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：nvarchar/text/ntext → DbType.String（Unicode 变长字符串）。
    /// </summary>
    [Theory]
    [InlineData("nvarchar")]
    [InlineData("text")]
    [InlineData("ntext")]
    public void ToDbType_UnicodeStringTypes_ShouldBeString(string dataType)
    {
        _converter.ToDbType(dataType).ShouldBe(DbType.String);
    }

    /// <summary>
    /// 测试目的：varchar → DbType.AnsiString（ANSI 变长字符串）。
    /// </summary>
    [Fact]
    public void ToDbType_Varchar_ShouldBeAnsiString()
    {
        _converter.ToDbType("varchar").ShouldBe(DbType.AnsiString);
    }

    /// <summary>
    /// 测试目的：char → DbType.AnsiStringFixedLength（ANSI 定长字符串）。
    /// </summary>
    [Fact]
    public void ToDbType_Char_ShouldBeAnsiStringFixedLength()
    {
        _converter.ToDbType("char").ShouldBe(DbType.AnsiStringFixedLength);
    }

    /// <summary>
    /// 测试目的：nchar → DbType.StringFixedLength（Unicode 定长字符串）。
    /// </summary>
    [Fact]
    public void ToDbType_Nchar_ShouldBeStringFixedLength()
    {
        _converter.ToDbType("nchar").ShouldBe(DbType.StringFixedLength);
    }

    // ═══════════════════════════════════════════════════════════
    // 布尔类型
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：bit → DbType.Boolean。
    /// </summary>
    [Fact]
    public void ToDbType_Bit_ShouldBeBoolean()
    {
        _converter.ToDbType("bit").ShouldBe(DbType.Boolean);
    }

    // ═══════════════════════════════════════════════════════════
    // 整数类型
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：tinyint → DbType.Byte（1 字节无符号整数）。
    /// </summary>
    [Fact]
    public void ToDbType_TinyInt_ShouldBeByte()
    {
        _converter.ToDbType("tinyint").ShouldBe(DbType.Byte);
    }

    /// <summary>
    /// 测试目的：smallint → DbType.Int16。
    /// </summary>
    [Fact]
    public void ToDbType_SmallInt_ShouldBeInt16()
    {
        _converter.ToDbType("smallint").ShouldBe(DbType.Int16);
    }

    /// <summary>
    /// 测试目的：int → DbType.Int32。
    /// </summary>
    [Fact]
    public void ToDbType_Int_ShouldBeInt32()
    {
        _converter.ToDbType("int").ShouldBe(DbType.Int32);
    }

    /// <summary>
    /// 测试目的：bigint → DbType.Int64。
    /// </summary>
    [Fact]
    public void ToDbType_BigInt_ShouldBeInt64()
    {
        _converter.ToDbType("bigint").ShouldBe(DbType.Int64);
    }

    // ═══════════════════════════════════════════════════════════
    // 浮点/精确数值类型
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：real → DbType.Single（4 字节浮点）。
    /// </summary>
    [Fact]
    public void ToDbType_Real_ShouldBeSingle()
    {
        _converter.ToDbType("real").ShouldBe(DbType.Single);
    }

    /// <summary>
    /// 测试目的：float → DbType.Double（8 字节浮点）。
    /// </summary>
    [Fact]
    public void ToDbType_Float_ShouldBeDouble()
    {
        _converter.ToDbType("float").ShouldBe(DbType.Double);
    }

    /// <summary>
    /// 测试目的：decimal/numeric/money/smallmoney → DbType.Decimal。
    /// </summary>
    [Theory]
    [InlineData("decimal")]
    [InlineData("numeric")]
    [InlineData("money")]
    [InlineData("smallmoney")]
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
    /// 测试目的：time → DbType.Time（仅时间，不含日期）。
    /// </summary>
    [Fact]
    public void ToDbType_Time_ShouldBeTime()
    {
        _converter.ToDbType("time").ShouldBe(DbType.Time);
    }

    /// <summary>
    /// 测试目的：datetime/smalldatetime → DbType.DateTime。
    /// </summary>
    [Theory]
    [InlineData("datetime")]
    [InlineData("smalldatetime")]
    public void ToDbType_DateTimeTypes_ShouldBeDateTime(string dataType)
    {
        _converter.ToDbType(dataType).ShouldBe(DbType.DateTime);
    }

    /// <summary>
    /// 测试目的：datetime2 → DbType.DateTime2（高精度日期时间）。
    /// </summary>
    [Fact]
    public void ToDbType_DateTime2_ShouldBeDateTime2()
    {
        _converter.ToDbType("datetime2").ShouldBe(DbType.DateTime2);
    }

    /// <summary>
    /// 测试目的：datetimeoffset → DbType.DateTimeOffset（含时区偏移）。
    /// </summary>
    [Fact]
    public void ToDbType_DateTimeOffset_ShouldBeDateTimeOffset()
    {
        _converter.ToDbType("datetimeoffset").ShouldBe(DbType.DateTimeOffset);
    }

    // ═══════════════════════════════════════════════════════════
    // 二进制类型
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：binary/varbinary/varbinary(max)/image/rowversion/timestamp → DbType.Binary。
    /// </summary>
    [Theory]
    [InlineData("binary")]
    [InlineData("varbinary")]
    [InlineData("varbinary(max)")]
    [InlineData("image")]
    [InlineData("rowversion")]
    [InlineData("timestamp")]
    public void ToDbType_BinaryTypes_ShouldBeBinary(string dataType)
    {
        _converter.ToDbType(dataType).ShouldBe(DbType.Binary);
    }

    // ═══════════════════════════════════════════════════════════
    // 其他类型
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：xml → DbType.Xml。
    /// </summary>
    [Fact]
    public void ToDbType_Xml_ShouldBeXml()
    {
        _converter.ToDbType("xml").ShouldBe(DbType.Xml);
    }

    /// <summary>
    /// 测试目的：sql_variant → DbType.Object（可变类型容器）。
    /// </summary>
    [Fact]
    public void ToDbType_SqlVariant_ShouldBeObject()
    {
        _converter.ToDbType("sql_variant").ShouldBe(DbType.Object);
    }
}

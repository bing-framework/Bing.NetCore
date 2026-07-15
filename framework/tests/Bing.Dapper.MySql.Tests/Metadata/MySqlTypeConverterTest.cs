using System.Data;
using Bing.Data.Metadata;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// 测试目的：验证 <see cref="MySqlTypeConverter"/> 将 MySql 数据类型字符串正确转换为 <see cref="DbType"/>
/// </summary>
public class MySqlTypeConverterTest
{
    private readonly MySqlTypeConverter _converter = new();

    #region Null / Whitespace

    /// <summary>
    /// 测试目的：传入 null 时应返回 null，不抛异常。
    /// </summary>
    [Fact]
    public void ToDbType_NullInput_ShouldReturnNull()
    {
        // Act
        var result = _converter.ToDbType(null);

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：传入空字符串时应返回 null。
    /// </summary>
    [Fact]
    public void ToDbType_EmptyInput_ShouldReturnNull()
    {
        // Act
        var result = _converter.ToDbType(string.Empty);

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：传入纯空白字符串时应返回 null。
    /// </summary>
    [Fact]
    public void ToDbType_WhitespaceInput_ShouldReturnNull()
    {
        // Act
        var result = _converter.ToDbType("   ");

        // Assert
        result.ShouldBeNull();
    }

    #endregion

    #region Unknown Type

    /// <summary>
    /// 测试目的：传入未知类型时应抛出 NotImplementedException，确保未映射类型不被静默忽略。
    /// </summary>
    [Fact]
    public void ToDbType_UnknownType_ShouldThrowNotImplementedException()
    {
        // Act & Assert
        Should.Throw<NotImplementedException>(() => _converter.ToDbType("unknown_type"));
    }

    /// <summary>
    /// 测试目的：大写输入应与小写等效（不区分大小写）。
    /// </summary>
    [Fact]
    public void ToDbType_UpperCaseInput_ShouldBeCaseInsensitive()
    {
        // Act
        var result = _converter.ToDbType("INT");

        // Assert
        result.ShouldBe(DbType.Int32);
    }

    #endregion

    #region String Types

    /// <summary>
    /// 测试目的：char 类型且 length=36 应映射为 Guid（MySQL 中常用 char(36) 存储 UUID）。
    /// </summary>
    [Fact]
    public void ToDbType_CharWithLength36_ShouldReturnGuid()
    {
        // Act
        var result = _converter.ToDbType("char", 36);

        // Assert
        result.ShouldBe(DbType.Guid);
    }

    /// <summary>
    /// 测试目的：char 类型且 length != 36 时应映射为 String。
    /// </summary>
    [Fact]
    public void ToDbType_CharWithOtherLength_ShouldReturnString()
    {
        // Act
        var result = _converter.ToDbType("char", 10);

        // Assert
        result.ShouldBe(DbType.String);
    }

    /// <summary>
    /// 测试目的：char 类型不传 length 时默认不为 36，应映射为 String。
    /// </summary>
    [Fact]
    public void ToDbType_CharWithNoLength_ShouldReturnString()
    {
        // Act
        var result = _converter.ToDbType("char");

        // Assert
        result.ShouldBe(DbType.String);
    }

    /// <summary>
    /// 测试目的：varchar 应映射为 String。
    /// </summary>
    [Fact]
    public void ToDbType_Varchar_ShouldReturnString()
    {
        // Act
        var result = _converter.ToDbType("varchar");

        // Assert
        result.ShouldBe(DbType.String);
    }

    /// <summary>
    /// 测试目的：text/tinytext/mediumtext/longtext 均应映射为 String。
    /// </summary>
    [Theory]
    [InlineData("text")]
    [InlineData("tinytext")]
    [InlineData("mediumtext")]
    [InlineData("longtext")]
    public void ToDbType_TextTypes_ShouldReturnString(string dataType)
    {
        // Act
        var result = _converter.ToDbType(dataType);

        // Assert
        result.ShouldBe(DbType.String);
    }

    #endregion

    #region Integer Types

    /// <summary>
    /// 测试目的：tinyint 且 length=1 时映射为 Boolean（MySQL 中 tinyint(1) 表示布尔）。
    /// </summary>
    [Fact]
    public void ToDbType_TinyIntWithLength1_ShouldReturnBoolean()
    {
        // Act
        var result = _converter.ToDbType("tinyint", 1);

        // Assert
        result.ShouldBe(DbType.Boolean);
    }

    /// <summary>
    /// 测试目的：tinyint 且 length != 1 时应映射为 Byte。
    /// </summary>
    [Fact]
    public void ToDbType_TinyIntWithOtherLength_ShouldReturnByte()
    {
        // Act
        var result = _converter.ToDbType("tinyint", 4);

        // Assert
        result.ShouldBe(DbType.Byte);
    }

    /// <summary>
    /// 测试目的：bit 应映射为 Boolean。
    /// </summary>
    [Fact]
    public void ToDbType_Bit_ShouldReturnBoolean()
    {
        // Act
        var result = _converter.ToDbType("bit");

        // Assert
        result.ShouldBe(DbType.Boolean);
    }

    /// <summary>
    /// 测试目的：smallint 应映射为 Int16。
    /// </summary>
    [Fact]
    public void ToDbType_SmallInt_ShouldReturnInt16()
    {
        // Act
        var result = _converter.ToDbType("smallint");

        // Assert
        result.ShouldBe(DbType.Int16);
    }

    /// <summary>
    /// 测试目的：int/integer/mediumint 均应映射为 Int32。
    /// </summary>
    [Theory]
    [InlineData("int")]
    [InlineData("integer")]
    [InlineData("mediumint")]
    public void ToDbType_Int32Types_ShouldReturnInt32(string dataType)
    {
        // Act
        var result = _converter.ToDbType(dataType);

        // Assert
        result.ShouldBe(DbType.Int32);
    }

    /// <summary>
    /// 测试目的：bigint 应映射为 Int64。
    /// </summary>
    [Fact]
    public void ToDbType_BigInt_ShouldReturnInt64()
    {
        // Act
        var result = _converter.ToDbType("bigint");

        // Assert
        result.ShouldBe(DbType.Int64);
    }

    #endregion

    #region Floating-Point Types

    /// <summary>
    /// 测试目的：float 应映射为 Single。
    /// </summary>
    [Fact]
    public void ToDbType_Float_ShouldReturnSingle()
    {
        // Act
        var result = _converter.ToDbType("float");

        // Assert
        result.ShouldBe(DbType.Single);
    }

    /// <summary>
    /// 测试目的：double 应映射为 Double。
    /// </summary>
    [Fact]
    public void ToDbType_Double_ShouldReturnDouble()
    {
        // Act
        var result = _converter.ToDbType("double");

        // Assert
        result.ShouldBe(DbType.Double);
    }

    /// <summary>
    /// 测试目的：decimal/numeric 均应映射为 Decimal。
    /// </summary>
    [Theory]
    [InlineData("decimal")]
    [InlineData("numeric")]
    public void ToDbType_DecimalTypes_ShouldReturnDecimal(string dataType)
    {
        // Act
        var result = _converter.ToDbType(dataType);

        // Assert
        result.ShouldBe(DbType.Decimal);
    }

    #endregion

    #region Date / Time Types

    /// <summary>
    /// 测试目的：date 应映射为 Date。
    /// </summary>
    [Fact]
    public void ToDbType_Date_ShouldReturnDate()
    {
        // Act
        var result = _converter.ToDbType("date");

        // Assert
        result.ShouldBe(DbType.Date);
    }

    /// <summary>
    /// 测试目的：time 应映射为 Time。
    /// </summary>
    [Fact]
    public void ToDbType_Time_ShouldReturnTime()
    {
        // Act
        var result = _converter.ToDbType("time");

        // Assert
        result.ShouldBe(DbType.Time);
    }

    /// <summary>
    /// 测试目的：datetime/timestamp 均应映射为 DateTime。
    /// </summary>
    [Theory]
    [InlineData("datetime")]
    [InlineData("timestamp")]
    public void ToDbType_DateTimeTypes_ShouldReturnDateTime(string dataType)
    {
        // Act
        var result = _converter.ToDbType(dataType);

        // Assert
        result.ShouldBe(DbType.DateTime);
    }

    #endregion

    #region Binary Types

    /// <summary>
    /// 测试目的：blob/tinyblob/mediumblob/longblob 均应映射为 Binary。
    /// </summary>
    [Theory]
    [InlineData("blob")]
    [InlineData("tinyblob")]
    [InlineData("mediumblob")]
    [InlineData("longblob")]
    public void ToDbType_BlobTypes_ShouldReturnBinary(string dataType)
    {
        // Act
        var result = _converter.ToDbType(dataType);

        // Assert
        result.ShouldBe(DbType.Binary);
    }

    #endregion
}

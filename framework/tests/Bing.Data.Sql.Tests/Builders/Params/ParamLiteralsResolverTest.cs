using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Tests.Builders.Params;

/// <summary>
/// 参数字面值解析器测试
/// </summary>
public class ParamLiteralsResolverTest
{
    #region 测试初始化

    /// <summary>
    /// 参数字面值解析器
    /// </summary>
    private readonly IParamLiteralsResolver _resolver;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public ParamLiteralsResolverTest()
    {
        _resolver = new ParamLiteralsResolver();
    }

    #endregion

    /// <summary>
    /// 测试当参数值为 null 时，返回空字符串
    /// </summary>
    [Fact]
    public void GetParamLiterals_NullValue_ReturnsEmptyString()
    {
        // Arrange
        object value = null;

        // Act
        var result = _resolver.GetParamLiterals(value);

        // Assert
        Assert.Equal("''", result);
    }

    /// <summary>
    /// 测试 boolean 类型的参数值，返回正确的字符串
    /// </summary>
    [Theory]
    [InlineData(true, "1")]
    [InlineData(false, "0")]
    public void GetParamLiterals_BooleanValue_ReturnsCorrectString(bool value, string expected)
    {
        // Act
        var result = _resolver.GetParamLiterals(value);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// 测试各种数值类型的参数值，返回正确的字符串
    /// </summary>
    [Theory]
    [InlineData((short)123, "123")]
    [InlineData(123, "123")]
    [InlineData((long)123, "123")]
    [InlineData(123.45f, "123.45")]
    [InlineData(123.45, "123.45")]
    public void GetParamLiterals_NumericValue_ReturnsCorrectString(object value, string expected)
    {
        // Act
        var result = _resolver.GetParamLiterals(value);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// 测试 decimal 类型的参数值，返回正确的字符串
    /// </summary>
    [Fact]
    public void GetParamLiterals_DecimalValue_ReturnsCorrectString()
    {
        // Arrange
        var value = 123.45M;

        // Act
        var result = _resolver.GetParamLiterals(value);

        // Assert
        Assert.Equal("123.45", result);
    }

    /// <summary>
    /// 测试 DateTime 类型的参数值，返回正确的字符串
    /// </summary>
    [Fact]
    public void GetParamLiterals_DateTimeValue_ReturnsCorrectString()
    {
        // Arrange
        var value = new DateTime(2023, 10, 1, 12, 30, 45);

        // Act
        var result = _resolver.GetParamLiterals(value);

        // Assert
        Assert.Equal("'2023-10-01 12:30:45'", result);
    }

    /// <summary>
    /// 测试 string 类型的参数值，返回正确的字符串
    /// </summary>
    [Fact]
    public void GetParamLiterals_StringValue_ReturnsCorrectString()
    {
        // Arrange
        var value = "test";

        // Act
        var result = _resolver.GetParamLiterals(value);

        // Assert
        Assert.Equal("'test'", result);
    }

    /// <summary>
    /// 测试其他类型的参数值，返回正确的字符串
    /// </summary>
    [Fact]
    public void GetParamLiterals_OtherValue_ReturnsCorrectString()
    {
        // Arrange
        var value = new object();

        // Act
        var result = _resolver.GetParamLiterals(value);

        // Assert
        Assert.Equal($"'{value}'", result);
    }

}

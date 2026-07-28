using Bing.Data.ObjectExtending;
using Bing.EntityFrameworkCore.ValueConverters;

namespace Bing.EntityFrameworkCore.Tests.ValueConverters;

/// <summary>
/// Entity Framework Core 值转换器单元测试。
/// </summary>
public class ValueConverterTest
{
    /// <summary>
    /// 测试目的：字符串转换器应去除前后空白，并保持 null 安全。
    /// </summary>
    [Fact]
    public void TrimStringValueConverter_WhenValueContainsWhitespaceOrNull_ShouldNormalizeExpectedValue()
    {
        // Arrange
        var converter = new TrimStringValueConverter();
        var convertToProvider = converter.ConvertToProviderExpression.Compile();

        // Act and Assert
        Assert.Equal("bing", convertToProvider("  bing  "));
        Assert.Equal(string.Empty, convertToProvider(null));
    }

    /// <summary>
    /// 测试目的：日期转换器处理 null 时应返回 null，避免持久化路径抛出异常。
    /// </summary>
    [Fact]
    public void DateTimeValueConverter_WhenValueIsNull_ShouldPreserveNull()
    {
        // Arrange
        var converter = new DateTimeValueConverter();
        var convertToProvider = converter.ConvertToProviderExpression.Compile();
        var convertFromProvider = converter.ConvertFromProviderExpression.Compile();

        // Act and Assert
        Assert.Null(convertToProvider(null));
        Assert.Null(convertFromProvider(null));
    }

    /// <summary>
    /// 测试目的：扩展属性转换器应将空 JSON 还原为空字典，并支持字典内容往返。
    /// </summary>
    [Fact]
    public void ExtraPropertiesValueConverter_WhenJsonEmptyOrDictionaryProvided_ShouldReturnExpectedDictionary()
    {
        // Arrange
        var converter = new ExtraPropertiesValueConverter();
        var convertToProvider = converter.ConvertToProviderExpression.Compile();
        var convertFromProvider = converter.ConvertFromProviderExpression.Compile();
        var properties = new ExtraPropertyDictionary { ["Name"] = "Bing" };

        // Act
        var json = convertToProvider(properties);
        var empty = convertFromProvider("{}");
        var result = convertFromProvider(json);

        // Assert
        Assert.Empty(empty);
        Assert.Equal("Bing", result.GetProperty<string>("Name"));
    }
}
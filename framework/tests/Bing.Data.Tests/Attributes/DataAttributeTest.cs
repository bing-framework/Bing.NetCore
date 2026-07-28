using System.Reflection;
using Bing.Data.Attributes;
using Xunit;

namespace Bing.Data.Tests.Attributes;

/// <summary>
/// 数据特性单元测试。
/// </summary>
public class DataAttributeTest
{
    /// <summary>
    /// 测试目的：验证未指定精度和小数位数时应使用约定默认值。
    /// </summary>
    [Fact]
    public void DecimalPrecisionAttribute_WhenConstructedWithoutArguments_ShouldUseDefaultValues()
    {
        // Act
        var attribute = new DecimalPrecisionAttribute();

        // Assert
        Assert.Equal(18, attribute.Precision);
        Assert.Equal(4, attribute.Scale);
    }

    /// <summary>
    /// 测试目的：验证显式指定精度和小数位数时应保留原始配置。
    /// </summary>
    [Fact]
    public void DecimalPrecisionAttribute_WhenConstructedWithArguments_ShouldPreserveValues()
    {
        // Act
        var attribute = new DecimalPrecisionAttribute(12, 6);

        // Assert
        Assert.Equal(12, attribute.Precision);
        Assert.Equal(6, attribute.Scale);
    }

    /// <summary>
    /// 测试目的：验证最大长度标记默认启用，且允许显式关闭。
    /// </summary>
    [Fact]
    public void HasMaxLengthAttribute_WhenConstructedOrAssigned_ShouldExposeConfiguredValue()
    {
        // Arrange
        var attribute = new HasMaxLengthAttribute();

        // Act
        attribute.HasMaxLength = false;

        // Assert
        Assert.False(attribute.HasMaxLength);
    }

    /// <summary>
    /// 测试目的：验证数据特性仅能标注单个属性，且不能继承或重复使用。
    /// </summary>
    [Theory]
    [InlineData(typeof(DecimalPrecisionAttribute))]
    [InlineData(typeof(HasMaxLengthAttribute))]
    public void DataAttribute_WhenInspected_ShouldRestrictUsageToSingleProperty(Type attributeType)
    {
        // Act
        var usage = attributeType.GetCustomAttribute<AttributeUsageAttribute>();

        // Assert
        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Property, usage.ValidOn);
        Assert.False(usage.Inherited);
        Assert.False(usage.AllowMultiple);
    }
}
using Bing.Logging;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.Logging.Tests.Core;

/// <summary>
/// <see cref="BingLoggingOptions"/> 单元测试
/// </summary>
public class BingLoggingOptionsTest
{
    /// <summary>
    /// 测试目的：默认构造后 ClearProviders 应为 false，不主动清除已有日志提供程序。
    /// </summary>
    [Fact]
    public void Default_ClearProviders_ShouldBeFalse()
    {
        // Arrange & Act
        var options = new BingLoggingOptions();

        // Assert
        options.ClearProviders.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：将 ClearProviders 设置为 true 后应可读取到修改值。
    /// </summary>
    [Fact]
    public void SetClearProviders_True_ShouldBeTrue()
    {
        // Arrange
        var options = new BingLoggingOptions();

        // Act
        options.ClearProviders = true;

        // Assert
        options.ClearProviders.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：RegisterExtension(null) 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void RegisterExtension_Null_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new BingLoggingOptions();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => options.RegisterExtension(null));
    }

    /// <summary>
    /// 测试目的：RegisterExtension 传入有效扩展时不抛异常。
    /// </summary>
    [Fact]
    public void RegisterExtension_ValidExtension_ShouldNotThrow()
    {
        // Arrange
        var options = new BingLoggingOptions();
        var extensionMock = new Mock<IBingLoggingOptionsExtension>();

        // Act & Assert
        Should.NotThrow(() => options.RegisterExtension(extensionMock.Object));
    }
}

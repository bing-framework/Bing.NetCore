using Bing.SecurityLog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.Security.Tests.SecurityLog;

/// <summary>
/// <see cref="DefaultSecurityLogManager"/> 及 <see cref="SimpleSecurityLogStore"/> 单元测试
/// </summary>
public class DefaultSecurityLogManagerAndSimpleStoreTest
{
    // ═══════════════════════════════════════════════════════════
    // DefaultSecurityLogManager — 辅助工厂
    // ═══════════════════════════════════════════════════════════

    private static DefaultSecurityLogManager CreateManager(
        bool isEnabled,
        string appName,
        Mock<ISecurityLogStore> mockStore)
    {
        var options = new BingSecurityLogOptions { IsEnabled = isEnabled, ApplicationName = appName };
        var mockOptions = new Mock<IOptions<BingSecurityLogOptions>>();
        mockOptions.Setup(o => o.Value).Returns(options);
        return new DefaultSecurityLogManager(mockOptions.Object, mockStore.Object);
    }

    // ═══════════════════════════════════════════════════════════
    // DefaultSecurityLogManager — 测试
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：当 IsEnabled = false 时，SaveAsync 应立即返回，不调用 SecurityLogStore.SaveAsync。
    /// </summary>
    [Fact]
    public async Task SaveAsync_WhenDisabled_ShouldNotCallStore()
    {
        // Arrange
        var mockStore = new Mock<ISecurityLogStore>();
        var manager = CreateManager(isEnabled: false, appName: "app", mockStore);

        // Act
        await manager.SaveAsync();

        // Assert
        mockStore.Verify(s => s.SaveAsync(It.IsAny<SecurityLogInfo>()), Times.Never);
    }

    /// <summary>
    /// 测试目的：当 IsEnabled = true 时，SaveAsync 应调用 SecurityLogStore.SaveAsync 恰好一次。
    /// </summary>
    [Fact]
    public async Task SaveAsync_WhenEnabled_ShouldCallStoreSaveOnce()
    {
        // Arrange
        var mockStore = new Mock<ISecurityLogStore>();
        mockStore.Setup(s => s.SaveAsync(It.IsAny<SecurityLogInfo>())).Returns(Task.CompletedTask);
        var manager = CreateManager(isEnabled: true, appName: "MyApp", mockStore);

        // Act
        await manager.SaveAsync();

        // Assert
        mockStore.Verify(s => s.SaveAsync(It.IsAny<SecurityLogInfo>()), Times.Once);
    }

    /// <summary>
    /// 测试目的：当 IsEnabled = true 且传入 saveAction 时，saveAction 应被调用，
    /// 且传入的 SecurityLogInfo.ApplicationName 应与选项一致。
    /// </summary>
    [Fact]
    public async Task SaveAsync_WhenEnabledWithAction_ShouldInvokeActionAndSetApplicationName()
    {
        // Arrange
        var mockStore = new Mock<ISecurityLogStore>();
        SecurityLogInfo capturedInfo = null;
        mockStore
            .Setup(s => s.SaveAsync(It.IsAny<SecurityLogInfo>()))
            .Callback<SecurityLogInfo>(info => capturedInfo = info)
            .Returns(Task.CompletedTask);
        var manager = CreateManager(isEnabled: true, appName: "TestApp", mockStore);

        // Act
        await manager.SaveAsync(info => info.Action = "Login");

        // Assert
        capturedInfo.ShouldNotBeNull();
        capturedInfo.ApplicationName.ShouldBe("TestApp");
        capturedInfo.Action.ShouldBe("Login");
    }

    /// <summary>
    /// 测试目的：当 IsEnabled = true 但不传 saveAction 时，应正常调用 Store，不会抛出 NRE。
    /// </summary>
    [Fact]
    public async Task SaveAsync_WhenEnabledWithNullAction_ShouldNotThrow()
    {
        // Arrange
        var mockStore = new Mock<ISecurityLogStore>();
        mockStore.Setup(s => s.SaveAsync(It.IsAny<SecurityLogInfo>())).Returns(Task.CompletedTask);
        var manager = CreateManager(isEnabled: true, appName: "app", mockStore);

        // Act & Assert — 不抛异常
        await Should.NotThrowAsync(async () => await manager.SaveAsync(null));
        mockStore.Verify(s => s.SaveAsync(It.IsAny<SecurityLogInfo>()), Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    // SimpleSecurityLogStore — 辅助工厂
    // ═══════════════════════════════════════════════════════════

    private static SimpleSecurityLogStore CreateStore(
        bool isEnabled,
        Mock<ILogger<SimpleSecurityLogStore>> mockLogger)
    {
        var options = new BingSecurityLogOptions { IsEnabled = isEnabled };
        var mockOptions = new Mock<IOptions<BingSecurityLogOptions>>();
        mockOptions.Setup(o => o.Value).Returns(options);
        return new SimpleSecurityLogStore(mockLogger.Object, mockOptions.Object);
    }

    // ═══════════════════════════════════════════════════════════
    // SimpleSecurityLogStore — 测试
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：当 IsEnabled = false 时，SaveAsync 应立即返回，不调用 Logger 任何方法。
    /// </summary>
    [Fact]
    public async Task SimpleStore_SaveAsync_WhenDisabled_ShouldNotLog()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SimpleSecurityLogStore>>();
        var store = CreateStore(isEnabled: false, mockLogger);

        // Act
        await store.SaveAsync(new SecurityLogInfo { ApplicationName = "app" });

        // Assert — Log 未被调用
        mockLogger.Verify(
            l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Never);
    }

    /// <summary>
    /// 测试目的：当 IsEnabled = true 时，SaveAsync 应调用 Logger.LogInformation 一次。
    /// </summary>
    [Fact]
    public async Task SimpleStore_SaveAsync_WhenEnabled_ShouldCallLogInformation()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SimpleSecurityLogStore>>();
        var store = CreateStore(isEnabled: true, mockLogger);

        // Act
        await store.SaveAsync(new SecurityLogInfo { ApplicationName = "app", Action = "Login" });

        // Assert
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    /// <summary>
    /// 测试目的：SecurityLogOptions.IsEnabled 应在 Store 中正确反映构造时的配置值。
    /// </summary>
    [Fact]
    public void SimpleStore_SecurityLogOptions_ShouldReflectConstructedIsEnabled()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SimpleSecurityLogStore>>();

        // Act
        var storeEnabled = CreateStore(isEnabled: true, mockLogger);
        var storeDisabled = CreateStore(isEnabled: false, mockLogger);

        // Assert
        storeEnabled.SecurityLogOptions.IsEnabled.ShouldBeTrue();
        storeDisabled.SecurityLogOptions.IsEnabled.ShouldBeFalse();
    }
}

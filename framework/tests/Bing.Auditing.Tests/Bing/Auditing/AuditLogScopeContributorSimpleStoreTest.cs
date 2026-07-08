using Bing.Auditing;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.Auditing.Tests;

/// <summary>
/// <see cref="AuditLogScope"/>、<see cref="AuditLogContributionContext"/>、
/// <see cref="AuditLogContributor"/>、<see cref="DisableAuditingAttribute"/>、
/// <see cref="SimpleLogAuditingStore"/> 单元测试。
/// </summary>
public class AuditLogScopeContributorSimpleStoreTest
{
    // ═══════════════════════════════════════════════════════════
    // AuditLogScope
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：构造器应将传入的 AuditLogInfo 正确赋给 Log 属性，确保作用域持有日志引用。
    /// </summary>
    [Fact]
    public void AuditLogScope_Constructor_ShouldSetLogProperty()
    {
        // Arrange
        var logInfo = new AuditLogInfo();

        // Act
        var scope = new AuditLogScope(logInfo);

        // Assert
        scope.Log.ShouldBeSameAs(logInfo);
    }

    /// <summary>
    /// 测试目的：AuditLogScope.Log 持有的 AuditLogInfo 应与原始实例完全相同（引用相等），
    /// 防止误复制导致修改不可见。
    /// </summary>
    [Fact]
    public void AuditLogScope_Log_ShouldBeReferenceEqual()
    {
        // Arrange
        var logInfo = new AuditLogInfo();
        logInfo.UserId = "u-001";

        // Act
        var scope = new AuditLogScope(logInfo);

        // Assert
        scope.Log.UserId.ShouldBe("u-001");
        ReferenceEquals(scope.Log, logInfo).ShouldBeTrue();
    }

    // ═══════════════════════════════════════════════════════════
    // AuditLogContributionContext
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：构造器应将 ServiceProvider 和 AuditInfo 正确赋值，
    /// 确保 Contributor 可访问这两个核心属性。
    /// </summary>
    [Fact]
    public void AuditLogContributionContext_Constructor_ShouldSetBothProperties()
    {
        // Arrange
        var mockSp = new Mock<IServiceProvider>();
        var logInfo = new AuditLogInfo();

        // Act
        var context = new AuditLogContributionContext(mockSp.Object, logInfo);

        // Assert
        context.ServiceProvider.ShouldBeSameAs(mockSp.Object);
        context.AuditInfo.ShouldBeSameAs(logInfo);
    }

    /// <summary>
    /// 测试目的：ServiceProvider 属性为只读，多次读取返回同一引用，不会被意外置换。
    /// </summary>
    [Fact]
    public void AuditLogContributionContext_ServiceProvider_ShouldBeImmutable()
    {
        // Arrange
        var mockSp = new Mock<IServiceProvider>();
        var context = new AuditLogContributionContext(mockSp.Object, new AuditLogInfo());

        // Act & Assert — 两次读取结果一致
        context.ServiceProvider.ShouldBeSameAs(mockSp.Object);
        context.ServiceProvider.ShouldBeSameAs(mockSp.Object);
    }

    // ═══════════════════════════════════════════════════════════
    // AuditLogContributor — 虚方法默认实现
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：PreContribute 和 PostContribute 的默认实现不应抛出任何异常，
    /// 子类可安全地选择性覆盖其中任一方法。
    /// </summary>
    [Fact]
    public void AuditLogContributor_DefaultMethods_ShouldNotThrow()
    {
        // Arrange
        var mockSp = new Mock<IServiceProvider>();
        var context = new AuditLogContributionContext(mockSp.Object, new AuditLogInfo());
        var contributor = new NoOpAuditLogContributor();

        // Act & Assert
        Should.NotThrow(() => contributor.PreContribute(context));
        Should.NotThrow(() => contributor.PostContribute(context));
    }

    /// <summary>
    /// 测试目的：子类覆盖 PreContribute 时，应仅执行子类逻辑，
    /// 验证虚方法可被正确重写。
    /// </summary>
    [Fact]
    public void AuditLogContributor_OverridePreContribute_ShouldBeInvoked()
    {
        // Arrange
        var mockSp = new Mock<IServiceProvider>();
        var logInfo = new AuditLogInfo();
        var context = new AuditLogContributionContext(mockSp.Object, logInfo);
        var contributor = new TrackingAuditLogContributor();

        // Act
        contributor.PreContribute(context);

        // Assert
        contributor.PreContributeCallCount.ShouldBe(1);
        contributor.PostContributeCallCount.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：子类覆盖 PostContribute 时，应仅执行子类逻辑，
    /// 验证 PreContribute 和 PostContribute 是独立的扩展点。
    /// </summary>
    [Fact]
    public void AuditLogContributor_OverridePostContribute_ShouldBeInvoked()
    {
        // Arrange
        var mockSp = new Mock<IServiceProvider>();
        var context = new AuditLogContributionContext(mockSp.Object, new AuditLogInfo());
        var contributor = new TrackingAuditLogContributor();

        // Act
        contributor.PostContribute(context);

        // Assert
        contributor.PostContributeCallCount.ShouldBe(1);
        contributor.PreContributeCallCount.ShouldBe(0);
    }

    // ═══════════════════════════════════════════════════════════
    // DisableAuditingAttribute
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：兼容层 DisableAuditingAttribute 应声明 Class/Method/Property 三种应用目标。
    /// </summary>
    [Fact]
    public void DisableAuditingAttribute_AttributeUsage_ShouldAllowClassMethodProperty()
    {
        // Arrange
        var attributeType = typeof(AuditLogInfo).Assembly.GetType("Bing.Auditing.DisableAuditingAttribute", throwOnError: true)!;

        // Assert
        var usage = (AttributeUsageAttribute)attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)[0];
        (usage.ValidOn & AttributeTargets.Class).ShouldBe(AttributeTargets.Class);
        (usage.ValidOn & AttributeTargets.Method).ShouldBe(AttributeTargets.Method);
        (usage.ValidOn & AttributeTargets.Property).ShouldBe(AttributeTargets.Property);
    }

    /// <summary>
    /// 测试目的：DisableAuditingAttribute 标记了 Obsolete，通过反射仍可正确读取。
    /// </summary>
    [Fact]
    public void DisableAuditingAttribute_ShouldBeObsolete()
    {
        // Arrange
        var attributeType = typeof(AuditLogInfo).Assembly.GetType("Bing.Auditing.DisableAuditingAttribute", throwOnError: true)!;

        // Act
        var obsolete = attributeType.GetCustomAttributes(typeof(ObsoleteAttribute), false);

        // Assert
        obsolete.ShouldNotBeEmpty();
    }

    // ═══════════════════════════════════════════════════════════
    // SimpleLogAuditingStore
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：默认构造后 Logger 为 NullLogger 实例，确保不依赖 DI 时也不会 NRE。
    /// </summary>
    [Fact]
    public async Task SimpleLogAuditingStore_Default_LoggerShouldNotBeNull()
    {
        // Arrange & Act
        var store = new SimpleLogAuditingStore();

        // Assert
        store.Logger.ShouldNotBeNull();
        // 可安全调用 SaveAsync，NullLogger 不会抛异常
        await Should.NotThrowAsync(async () => await store.SaveAsync(new AuditLogInfo()));
    }

    /// <summary>
    /// 测试目的：注入真实 Logger 后，SaveAsync 应调用 Logger.LogInformation 一次，
    /// 确保审计信息被写入日志。
    /// </summary>
    [Fact]
    public async Task SimpleLogAuditingStore_SaveAsync_ShouldCallLogInformationOnce()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SimpleLogAuditingStore>>();
        var store = new SimpleLogAuditingStore { Logger = mockLogger.Object };
        var auditInfo = new AuditLogInfo { UserId = "u-test", UserName = "tester" };

        // Act
        await store.SaveAsync(auditInfo);

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
    /// 测试目的：Logger 属性是可注入的（公开 setter），赋值后读取应返回新实例。
    /// </summary>
    [Fact]
    public void SimpleLogAuditingStore_Logger_ShouldBeMutableViaSetter()
    {
        // Arrange
        var store = new SimpleLogAuditingStore();
        var mockLogger = new Mock<ILogger<SimpleLogAuditingStore>>();

        // Act
        store.Logger = mockLogger.Object;

        // Assert
        store.Logger.ShouldBeSameAs(mockLogger.Object);
    }

    // ═══════════════════════════════════════════════════════════
    // 内部辅助类型
    // ═══════════════════════════════════════════════════════════

    /// <summary>空实现的 AuditLogContributor，用于测试默认虚方法不抛异常。</summary>
    private class NoOpAuditLogContributor : AuditLogContributor { }

    /// <summary>记录调用次数的 AuditLogContributor，用于验证方法覆盖。</summary>
    private class TrackingAuditLogContributor : AuditLogContributor
    {
        public int PreContributeCallCount { get; private set; }
        public int PostContributeCallCount { get; private set; }

        public override void PreContribute(AuditLogContributionContext context) => PreContributeCallCount++;
        public override void PostContribute(AuditLogContributionContext context) => PostContributeCallCount++;
    }

}

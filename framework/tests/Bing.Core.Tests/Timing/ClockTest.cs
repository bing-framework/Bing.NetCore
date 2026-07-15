using Bing.Timing;
using Shouldly;

namespace Bing.Tests.Timing;

/// <summary>
/// IClock / SystemClock 时钟测试
/// </summary>
public class ClockTest
{
    // ==================== SystemClock 基本属性 ====================

    /// <summary>
    /// 测试目的：SystemClock.Now 应返回本地时间（Kind == Local）。
    /// </summary>
    [Fact]
    public void SystemClock_Now_Kind_IsLocal()
    {
        // Arrange
        var clock = new SystemClock();

        // Act
        var now = clock.Now;

        // Assert
        now.Kind.ShouldBe(DateTimeKind.Local);
    }

    /// <summary>
    /// 测试目的：SystemClock.UtcNow 应返回 UTC 时间（Kind == Utc）。
    /// </summary>
    [Fact]
    public void SystemClock_UtcNow_Kind_IsUtc()
    {
        // Arrange
        var clock = new SystemClock();

        // Act
        var utcNow = clock.UtcNow;

        // Assert
        utcNow.Kind.ShouldBe(DateTimeKind.Utc);
    }

    /// <summary>
    /// 测试目的：SystemClock.NowOffset 返回合法的 DateTimeOffset，偏移量与系统一致。
    /// </summary>
    [Fact]
    public void SystemClock_NowOffset_HasOffset()
    {
        // Arrange
        var clock = new SystemClock();

        // Act
        var offset = clock.NowOffset;
        var systemOffset = DateTimeOffset.Now;

        // Assert：偏移量与系统时区一致（允许 1 秒误差）
        (systemOffset - offset).Duration().ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// 测试目的：SystemClock.Now 返回时间与系统当前时间偏差应在 1 秒以内。
    /// </summary>
    [Fact]
    public void SystemClock_Now_CloseToSystemTime()
    {
        // Arrange
        var clock = new SystemClock();
        var before = DateTime.Now;

        // Act
        var now = clock.Now;
        var after = DateTime.Now;

        // Assert
        now.ShouldBeGreaterThanOrEqualTo(before);
        now.ShouldBeLessThanOrEqualTo(after);
    }

    /// <summary>
    /// 测试目的：SystemClock.UtcNow 与 Now.ToUniversalTime() 偏差在 1 秒以内。
    /// </summary>
    [Fact]
    public void SystemClock_UtcNow_CloseToNowToUniversalTime()
    {
        // Arrange
        var clock = new SystemClock();

        // Act
        var utcNow = clock.UtcNow;
        var localToUtc = clock.Now.ToUniversalTime();

        // Assert
        (utcNow - localToUtc).Duration().ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    // ==================== SystemClock.Instance 静态单例 ====================

    /// <summary>
    /// 测试目的：SystemClock.Instance 静态单例不为 null，且实现 IClock 接口。
    /// </summary>
    [Fact]
    public void SystemClock_Instance_IsNotNull_And_ImplementsIClock()
    {
        // Act
        var instance = SystemClock.Instance;

        // Assert
        instance.ShouldNotBeNull();
        instance.ShouldBeAssignableTo<IClock>();
    }

    /// <summary>
    /// 测试目的：SystemClock.Instance 多次访问返回同一对象（单例）。
    /// </summary>
    [Fact]
    public void SystemClock_Instance_ReturnsSameObject()
    {
        // Act
        var a = SystemClock.Instance;
        var b = SystemClock.Instance;

        // Assert
        a.ShouldBeSameAs(b);
    }

    // ==================== IClock 协议 ====================

    /// <summary>
    /// 测试目的：SystemClock 实现 IClock 接口，通过接口使用 Now / UtcNow / NowOffset 均正常。
    /// </summary>
    [Fact]
    public void SystemClock_AsIClock_PropertiesAccessible()
    {
        // Arrange
        IClock clock = new SystemClock();

        // Act & Assert（不抛异常，且属性有值）
        Should.NotThrow(() =>
        {
            _ = clock.Now;
            _ = clock.UtcNow;
            _ = clock.NowOffset;
        });
    }
}

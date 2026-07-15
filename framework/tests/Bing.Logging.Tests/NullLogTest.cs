using Microsoft.Extensions.Logging;

namespace Bing.Logging.Tests;

/// <summary>
/// 日志操作测试 - 空日志（NullLog）
/// </summary>
public class NullLogTest
{
    #region NullLog 单例

    /// <summary>
    /// 测试目的：NullLog.Instance 不为 null，且多次访问返回同一实例。
    /// </summary>
    [Fact]
    public void Instance_ShouldNotBeNull_AndSingleton()
    {
        // Arrange & Act
        var instance1 = NullLog.Instance;
        var instance2 = NullLog.Instance;

        // Assert
        instance1.ShouldNotBeNull();
        instance1.ShouldBeSameAs(instance2);
    }

    /// <summary>
    /// 测试目的：NullLog&lt;T&gt;.Instance 不为 null，且实现 ILog&lt;T&gt; 接口。
    /// </summary>
    [Fact]
    public void GenericInstance_ShouldNotBeNull_AndImplementInterface()
    {
        // Arrange & Act
        var instance = NullLog<NullLogTest>.Instance;

        // Assert
        instance.ShouldNotBeNull();
        instance.ShouldBeAssignableTo<ILog<NullLogTest>>();
    }

    /// <summary>
    /// 测试目的：Log.Null 静态属性应为 NullLog 实例。
    /// </summary>
    [Fact]
    public void Log_Null_ShouldBeNullLogInstance()
    {
        // Arrange & Act
        var logNull = Log.Null;

        // Assert
        logNull.ShouldNotBeNull();
        logNull.ShouldBeSameAs(NullLog.Instance);
    }

    #endregion

    #region IsEnabled

    /// <summary>
    /// 测试目的：NullLog.IsEnabled 对所有日志级别均返回 false（空实现，不实际记录）。
    /// </summary>
    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    [InlineData(LogLevel.None)]
    public void IsEnabled_AllLevels_ShouldReturnFalse(LogLevel level)
    {
        // Arrange
        var log = NullLog.Instance;

        // Act & Assert
        log.IsEnabled(level).ShouldBeFalse();
    }

    #endregion

    #region 流式调用不抛异常

    /// <summary>
    /// 测试目的：NullLog 的所有 Log 级别方法均不抛出异常，返回值为自身（支持链式调用）。
    /// </summary>
    [Fact]
    public void LogMethods_ShouldNotThrow_AndReturnSelf()
    {
        // Arrange
        var log = NullLog.Instance;

        // Act & Assert
        Should.NotThrow(() =>
        {
            ILog result;
            result = log.LogTrace();
            result.ShouldBeSameAs(log);

            result = log.LogDebug();
            result.ShouldBeSameAs(log);

            result = log.LogInformation();
            result.ShouldBeSameAs(log);

            result = log.LogWarning();
            result.ShouldBeSameAs(log);

            result = log.LogError();
            result.ShouldBeSameAs(log);

            result = log.LogCritical();
            result.ShouldBeSameAs(log);
        });
    }

    /// <summary>
    /// 测试目的：NullLog 的流式设置方法（Message/Property/State/Exception/EventId）均返回自身不抛异常。
    /// </summary>
    [Fact]
    public void FluentSetters_ShouldNotThrow_AndReturnSelf()
    {
        // Arrange
        var log = NullLog.Instance;
        var ex = new Exception("test");
        var eventId = new Microsoft.Extensions.Logging.EventId(1, "TestEvent");

        // Act & Assert
        Should.NotThrow(() =>
        {
            var result = log
                .Message("hello {0}", 42)
                .Property("key", "value")
                .State(new { Name = "test" })
                .Exception(ex)
                .EventId(eventId);

            result.ShouldBeSameAs(log);
        });
    }

    /// <summary>
    /// 测试目的：NullLog.Exception(null) 不抛异常。
    /// </summary>
    [Fact]
    public void Exception_Null_ShouldNotThrow()
    {
        // Arrange
        var log = NullLog.Instance;

        // Act & Assert
        Should.NotThrow(() => log.Exception(null));
    }

    /// <summary>
    /// 测试目的：NullLog.BeginScope 返回可 Dispose 的对象（不抛异常，且 Dispose 不抛异常）。
    /// </summary>
    [Fact]
    public void BeginScope_ShouldReturnDisposable_AndDisposeNotThrow()
    {
        // Arrange
        var log = NullLog.Instance;

        // Act
        IDisposable scope = null;
        Should.NotThrow(() => scope = log.BeginScope("test-scope"));

        // Assert
        scope.ShouldNotBeNull();
        Should.NotThrow(() => scope.Dispose());
    }

    /// <summary>
    /// 测试目的：完整链式调用 NullLog 不抛异常（模拟真实调用模式）。
    /// </summary>
    [Fact]
    public void FullChain_ShouldNotThrow()
    {
        // Arrange
        var log = NullLog.Instance;

        // Act & Assert
        Should.NotThrow(() =>
        {
            log.Message("订单 {OrderId} 已创建", 123)
               .Property("UserId", "u-001")
               .State(new { OrderId = 123 })
               .Exception(new InvalidOperationException("测试"))
               .LogError();
        });
    }

    #endregion
}

using Bing.ExceptionHandling;
using Bing.Threading;
using Bing.Tracing;
using Microsoft.Extensions.Logging;

namespace Bing.Tests.Core;

/// <summary>
/// TraceIdContext、NoneCancellationTokenProvider、NullExceptionNotifier、
/// ExceptionNotificationContext 单元测试
/// </summary>
public class TracingAndCancellationTest
{
    // ════════════════════════════════════════════════════════════════
    // TraceIdContext
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：传入有效 traceId 时，TraceId 属性应与传入值一致。
    /// </summary>
    [Fact]
    public void TraceIdContext_WithValidTraceId_ShouldStoreTraceId()
    {
        // Arrange & Act
        var ctx = new TraceIdContext("abc-123");

        // Assert
        ctx.TraceId.ShouldBe("abc-123");
    }

    /// <summary>
    /// 测试目的：传入 null 或空字符串时，构造函数应自动生成一个 Guid 作为 TraceId。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TraceIdContext_WithNullOrEmpty_ShouldAutoGenerateTraceId(string traceId)
    {
        // Act
        var ctx = new TraceIdContext(traceId);

        // Assert
        ctx.TraceId.ShouldNotBeNullOrWhiteSpace();
        Guid.TryParse(ctx.TraceId, out _).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：四参数构造函数应正确保存所有字段。
    /// </summary>
    [Fact]
    public void TraceIdContext_FourParamCtor_ShouldStoreAllFields()
    {
        // Act
        var ctx = new TraceIdContext("t1", "r1", "p1", "c1");

        // Assert
        ctx.TraceId.ShouldBe("t1");
        ctx.RootId.ShouldBe("r1");
        ctx.ParentId.ShouldBe("p1");
        ctx.ChildId.ShouldBe("c1");
    }

    /// <summary>
    /// 测试目的：Current 静态属性应基于 AsyncLocal，在同一异步上下文内可读写。
    /// </summary>
    [Fact]
    public void TraceIdContext_Current_SetAndGet_ShouldReturnSameInstance()
    {
        // Arrange
        var ctx = new TraceIdContext("my-trace");
        var original = TraceIdContext.Current;

        try
        {
            // Act
            TraceIdContext.Current = ctx;

            // Assert
            TraceIdContext.Current.ShouldBeSameAs(ctx);
        }
        finally
        {
            TraceIdContext.Current = original;
        }
    }

    /// <summary>
    /// 测试目的：不同异步任务应拥有独立的 Current 值（AsyncLocal 隔离性验证）。
    /// </summary>
    [Fact]
    public async Task TraceIdContext_Current_ShouldBeIsolatedAcrossTasks()
    {
        // Arrange
        TraceIdContext.Current = null;
        var ctx = new TraceIdContext("parent");
        TraceIdContext.Current = ctx;

        // Act - 子任务设置独立的 Current
        TraceIdContext childCtx = null;
        await Task.Run(() =>
        {
            TraceIdContext.Current = new TraceIdContext("child");
            childCtx = TraceIdContext.Current;
        });

        // Assert - 父上下文不受子任务影响
        TraceIdContext.Current.TraceId.ShouldBe("parent");
        childCtx.TraceId.ShouldBe("child");

        // Cleanup
        TraceIdContext.Current = null;
    }

    // ════════════════════════════════════════════════════════════════
    // NoneCancellationTokenProvider
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Instance 不为 null，且是单例（两次访问同一对象）。
    /// </summary>
    [Fact]
    public void NoneCancellationTokenProvider_Instance_IsSingleton()
    {
        // Act
        var a = NoneCancellationTokenProvider.Instance;
        var b = NoneCancellationTokenProvider.Instance;

        // Assert
        a.ShouldNotBeNull();
        a.ShouldBeSameAs(b);
    }

    /// <summary>
    /// 测试目的：Token 应等于 CancellationToken.None（不可取消）。
    /// </summary>
    [Fact]
    public void NoneCancellationTokenProvider_Token_ShouldBeCancellationTokenNone()
    {
        // Act
        var token = NoneCancellationTokenProvider.Instance.Token;

        // Assert
        token.ShouldBe(CancellationToken.None);
        token.CanBeCanceled.ShouldBeFalse();
    }

    // ════════════════════════════════════════════════════════════════
    // NullExceptionNotifier
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Instance 不为 null，且实现 IExceptionNotifier。
    /// </summary>
    [Fact]
    public void NullExceptionNotifier_Instance_IsNotNullAndImplementsInterface()
    {
        NullExceptionNotifier.Instance.ShouldNotBeNull();
        NullExceptionNotifier.Instance.ShouldBeAssignableTo<IExceptionNotifier>();
    }

    /// <summary>
    /// 测试目的：NotifyAsync 应静默完成，不抛异常，返回已完成的 Task。
    /// </summary>
    [Fact]
    public async Task NullExceptionNotifier_NotifyAsync_ShouldCompleteWithoutThrowing()
    {
        // Arrange
        var ctx = new ExceptionNotificationContext(new InvalidOperationException("test"));

        // Act & Assert
        await Should.NotThrowAsync(() => NullExceptionNotifier.Instance.NotifyAsync(ctx));
    }

    // ════════════════════════════════════════════════════════════════
    // ExceptionNotificationContext
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：传入 null 异常时，构造函数应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void ExceptionNotificationContext_NullException_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ExceptionNotificationContext(null));
    }

    /// <summary>
    /// 测试目的：不传 logLevel 时，LogLevel 应由 exception.GetLogLevel() 自动推断（普通异常默认 Error）。
    /// </summary>
    [Fact]
    public void ExceptionNotificationContext_DefaultLogLevel_ShouldBeError()
    {
        // Arrange
        var ex = new InvalidOperationException("boom");

        // Act
        var ctx = new ExceptionNotificationContext(ex);

        // Assert
        ctx.Exception.ShouldBeSameAs(ex);
        ctx.LogLevel.ShouldBe(LogLevel.Error);
    }

    /// <summary>
    /// 测试目的：显式传入 LogLevel 时，应使用传入值而不是自动推断值。
    /// </summary>
    [Fact]
    public void ExceptionNotificationContext_ExplicitLogLevel_ShouldOverrideDefault()
    {
        // Arrange
        var ex = new InvalidOperationException("info");

        // Act
        var ctx = new ExceptionNotificationContext(ex, LogLevel.Warning);

        // Assert
        ctx.LogLevel.ShouldBe(LogLevel.Warning);
    }

    /// <summary>
    /// 测试目的：Handled 默认值应为 true，且可被显式设置为 false。
    /// </summary>
    [Fact]
    public void ExceptionNotificationContext_Handled_DefaultIsTrueAndCanBeChanged()
    {
        // Arrange & Act
        var ctxDefault = new ExceptionNotificationContext(new Exception("x"));
        var ctxFalse = new ExceptionNotificationContext(new Exception("y"), handled: false);

        // Assert
        ctxDefault.Handled.ShouldBeTrue();
        ctxFalse.Handled.ShouldBeFalse();
    }
}

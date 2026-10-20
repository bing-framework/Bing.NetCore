using Xunit;

namespace Bing.Caching.Tests;

/// <summary>
/// CacheFactoryExecution 超时、取消和清理异常测试。
/// </summary>
public class CacheFactoryExecutionTest
{
    /// <summary>
    /// 验证回源超时先返回，同时未配合的工厂仍可继续运行。
    /// </summary>
    [Fact]
    public async Task Timeout_ReturnsWhileUncooperativeFactoryStillRuns()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var tail = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        await Assert.ThrowsAsync<TimeoutException>(() => CacheFactoryExecution.InvokeAsync(_ => tail.Task,
            deadline.Token, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.False(tail.Task.IsCompleted);
        tail.SetException(new InvalidOperationException("late factory fault"));
        Assert.True(tail.Task.IsFaulted);
    }

    /// <summary>
    /// 验证调用方取消令牌原样传播到异常。
    /// </summary>
    [Fact]
    public async Task CallerCancellation_PreservesCancellationToken()
    {
        using var caller = new CancellationTokenSource();
        var factory = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = CacheFactoryExecution.InvokeAsync(_ => factory.Task, caller.Token, caller.Token);
        caller.Cancel();
        var error = await Assert.ThrowsAsync<OperationCanceledException>(() => work);
        Assert.Equal(caller.Token, error.CancellationToken);
        factory.SetResult(42);
    }

    /// <summary>
    /// 验证回源异常对象原样传播。
    /// </summary>
    [Fact]
    public async Task FactoryFault_IsPreserved()
    {
        var original = new InvalidOperationException("factory");
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CacheFactoryExecution.InvokeAsync<int>(_ => Task.FromException<int>(original),
                CancellationToken.None, CancellationToken.None));
        Assert.Same(original, actual);
    }

    /// <summary>
    /// 验证清理异常附加到原始异常，并在无原始异常时直接抛出。
    /// </summary>
    [Fact]
    public async Task CleanupFailure_IsAttachedToOriginalFault()
    {
        var primary = new InvalidOperationException("factory");
        var release = new IOException("release");
        CacheFactoryExecution.Cleanup(() => throw release, primary);
        Assert.Same(release, primary.Data["CacheCleanupException"]);
        await CacheFactoryExecution.CleanupAsync(() => Task.FromException(release), primary, TimeSpan.FromSeconds(1));
        Assert.Same(release, primary.Data["CacheCleanupException"]);
        Assert.Same(release, Assert.Throws<IOException>(() => CacheFactoryExecution.Cleanup(() => throw release, null)));
    }

    /// <summary>
    /// 验证清理超时有界返回并报告次级异常。
    /// </summary>
    [Fact]
    public async Task CleanupTimeout_IsBoundedAndReportsSecondaryFault()
    {
        var primary = new InvalidOperationException("factory");
        var tail = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await CacheFactoryExecution.CleanupAsync(() => tail.Task, primary, TimeSpan.FromMilliseconds(30))
            .WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsType<TimeoutException>(primary.Data["CacheCleanupException"]);
        tail.SetResult(true);
    }

    /// <summary>
    /// 验证协调计时器拒绝超过 int 毫秒范围的时限。
    /// </summary>
    [Fact]
    public void CoordinationOptions_RejectUnsupportedTimerRanges()
    {
        var tooLong = TimeSpan.FromMilliseconds((double)int.MaxValue + 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => new CacheCoordinationOptions(waitTimeout: tooLong));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CacheCoordinationOptions(retrievalTimeout: tooLong));
    }

    /// <summary>
    /// 验证正的亚毫秒协调时限向上取整到毫秒。
    /// </summary>
    [Fact]
    public void CoordinationOptions_RoundPositiveSubMillisecondDurationsUp()
    {
        var options = new CacheCoordinationOptions(TimeSpan.FromTicks(10001), TimeSpan.FromTicks(1),
            TimeSpan.FromTicks(1), TimeSpan.FromTicks(1));
        Assert.Equal(TimeSpan.FromMilliseconds(2), options.LeaseDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(1), options.RenewalInterval);
        Assert.Equal(TimeSpan.FromMilliseconds(1), options.WaitTimeout);
        Assert.Equal(TimeSpan.FromMilliseconds(1), options.RetrievalTimeout);
        Assert.Throws<ArgumentOutOfRangeException>(() => new CacheCoordinationOptions(
            TimeSpan.FromTicks(2), TimeSpan.FromTicks(1)));
    }
}

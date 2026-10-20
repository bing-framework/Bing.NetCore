using Bing.Caching;
using Xunit;

namespace Bing.Caching.Tests;

/// <summary>
/// CacheBatchAdaptive 批量读取拆分和失败传播测试。
/// </summary>
public class CacheBatchAdaptiveTest
{
    /// <summary>
    /// 验证同步批量读取在载荷超限时拆分请求并保留输入顺序。
    /// </summary>
    [Fact]
    public void Read_SplitsPayloadOversizeAndPreservesOrder()
    {
        var calls = 0;
        var result = CacheBatchAdaptive.Read(
            new[] { "a", "b", "c", "d" },
            keys =>
            {
                calls++;
                if (keys.Length > 1)
                    throw CacheBatchBudget.CreateLimitException(10, 4);
                return new[] { keys[0].ToUpperInvariant() };
            },
            (keys, values) => Assert.Equal(keys.Length, values.Length));

        Assert.Equal(new[] { "A", "B", "C", "D" }, result);
        Assert.Equal(7, calls);
    }

    /// <summary>
    /// 验证同步批量读取对非载荷异常不重试。
    /// </summary>
    [Fact]
    public void Read_DoesNotRetryNonPayloadFailures()
    {
        var calls = 0;
        var error = Assert.Throws<InvalidOperationException>(() => CacheBatchAdaptive.Read<string>(
            new[] { "a", "b" },
            keys =>
            {
                calls++;
                throw new InvalidOperationException("shape mismatch");
            },
            (_, _) => { }));

        Assert.Equal("shape mismatch", error.Message);
        Assert.Equal(1, calls);
    }

    /// <summary>
    /// 验证异步批量读取遇到单个超大值时直接失败，不重试。
    /// </summary>
    [Fact]
    public async Task ReadAsync_SingleOversizeValueFailsWithoutRetry()
    {
        var calls = 0;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CacheBatchAdaptive.ReadAsync(
            new[] { "large" },
            keys =>
            {
                calls++;
                return Task.FromResult(new[] { "too-large" });
            },
            (_, _) => throw CacheBatchBudget.CreateLimitException(9, 4),
            CancellationToken.None));

        Assert.True(CacheBatchBudget.IsPayloadLimit(error));
        Assert.Equal(1, calls);
    }

    /// <summary>
    /// 验证异步批量读取取消后不继续拆分重试。
    /// </summary>
    [Fact]
    public async Task ReadAsync_CancellationStopsBeforeRetry()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CacheBatchAdaptive.ReadAsync(
            new[] { "a", "b" },
            keys =>
            {
                calls++;
                cancellation.Cancel();
                return Task.FromResult(new[] { "oversize", "oversize" });
            },
            (_, _) => throw CacheBatchBudget.CreateLimitException(9, 4),
            cancellation.Token));

        Assert.Equal(1, calls);
    }
}

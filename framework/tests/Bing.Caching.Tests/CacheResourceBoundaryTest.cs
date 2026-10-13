using System.Text;
using Xunit;

namespace Bing.Caching.Tests;

/// <summary>
/// 缓存资源准入、批量载荷和扫描预算边界测试。
/// </summary>
public class CacheResourceBoundaryTest
{
    /// <summary>
    /// 验证不同键回源的资源队列有界且取消后可恢复。
    /// </summary>
    [Fact]
    public async Task Admission_BoundsDifferentKeysQueueAndRecoversAfterCancellation()
    {
        var budget = new CacheResourceOptions(1, 1, 64);
        using var holder = CacheFactoryAdmission.Enter(budget, TimeSpan.FromSeconds(1));
        using var cancellation = new CancellationTokenSource();
        var waiter = CacheFactoryAdmission.EnterAsync(budget, TimeSpan.FromSeconds(1), cancellation.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CacheFactoryAdmission.EnterAsync(budget,
            TimeSpan.FromSeconds(1), CancellationToken.None));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
        holder.Dispose();
        using var next = await CacheFactoryAdmission.EnterAsync(budget, TimeSpan.FromSeconds(1), CancellationToken.None);
    }

    /// <summary>
    /// 验证资源入队超时不会泄漏队列容量。
    /// </summary>
    [Fact]
    public async Task Admission_TimeoutDoesNotLeakQueueCapacity()
    {
        var budget = new CacheResourceOptions(1, 1);
        using var holder = CacheFactoryAdmission.Enter(budget, TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<TimeoutException>(() => CacheFactoryAdmission.EnterAsync(budget,
            TimeSpan.FromMilliseconds(20), CancellationToken.None));
        holder.Dispose();
        using var next = CacheFactoryAdmission.Enter(budget, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// 验证批量载荷按 UTF-8 字节计数并拒绝聚合超限。
    /// </summary>
    [Fact]
    public void PayloadBudget_CountsUtf8AndRejectsAggregateOversize()
    {
        CacheBatchBudget.Check(new[] { "中文", null }, 6);
        Assert.Throws<InvalidOperationException>(() => CacheBatchBudget.Check(new[] { "中文", "a" }, 6));
        CacheBatchBudget.Check(new[] { new byte[3], new byte[3] }, 6);
        Assert.Throws<InvalidOperationException>(() => CacheBatchBudget.Check(new[] { new byte[7] }, 6));
    }

    /// <summary>
    /// 验证带索引转换器的批量载荷仍遵守聚合限制。
    /// </summary>
    [Fact]
    public void PayloadBudget_IndexSelectorPreservesAggregateLimit()
    {
        CacheBatchBudget.Check(new[] { "中文", null },
            value => value == null ? null : Encoding.UTF8.GetBytes(value), 6);
        Assert.Throws<InvalidOperationException>(() => CacheBatchBudget.Check(
            new[] { "中文", "a" }, value => Encoding.UTF8.GetBytes(value), 6));
    }

    /// <summary>
    /// 验证扫描候选键预算达到上限后停止消费。
    /// </summary>
    [Fact]
    public void ScanBudget_StopsAfterConfiguredCandidateKeyCount()
    {
        var budget = new CacheScanBudget(2);

        budget.Consume();
        budget.Consume();

        var error = Assert.Throws<CacheScanLimitExceededException>(() => budget.Consume());

        Assert.Contains("MaxScanKeys", error.Message, StringComparison.Ordinal);
        Assert.Equal(2, error.Limit);
        Assert.Equal(2, error.Consumed);
        Assert.Empty(error.CompletedShards);
        Assert.Null(error.FailedShard);
        Assert.Equal(2, budget.Count);
    }

    /// <summary>
    /// 验证零候选键预算表示无限制。
    /// </summary>
    [Fact]
    public void ScanBudget_ZeroMeansUnlimited()
    {
        var budget = new CacheScanBudget(0);

        for (var index = 0; index < 1000; index++)
            budget.Consume();

        Assert.Equal(1000, budget.Count);
    }

    /// <summary>
    /// 验证扫描页预算达到上限后抛出对应异常。
    /// </summary>
    [Fact]
    public void ScanBudget_StopsAfterConfiguredPageCount()
    {
        var budget = new CacheScanBudget(0, 2);

        budget.ConsumePage();
        budget.ConsumePage();

        var error = Assert.Throws<CacheScanLimitExceededException>(() => budget.ConsumePage());

        Assert.Equal(CacheScanLimitKind.ScanPages, error.Kind);
        Assert.Contains("MaxScanPages", error.Message, StringComparison.Ordinal);
        Assert.Equal(2, error.Limit);
        Assert.Equal(2, error.Consumed);
        Assert.Equal(2, budget.PageCount);
        Assert.Equal(0, budget.Count);
    }

    /// <summary>
    /// 验证并发候选键和扫描页消费均不超过预算。
    /// </summary>
    [Fact]
    public async Task ScanBudget_ConcurrentCandidatesAndPagesRemainWithinLimits()
    {
        var candidateBudget = new CacheScanBudget(2);
        var candidateErrors = await Task.WhenAll(Enumerable.Range(0, 4).Select(async shard =>
        {
            using var scope = candidateBudget.EnterShard(shard);
            try
            {
                candidateBudget.Consume();
                await Task.Yield();
                candidateBudget.Consume();
                return null;
            }
            catch (CacheScanLimitExceededException error)
            {
                return error;
            }
        }));

        var candidateError = candidateErrors.First(error => error != null);
        Assert.NotNull(candidateError);
        Assert.Equal(2, candidateBudget.Count);
        Assert.Equal(2, candidateError.Consumed);
        Assert.InRange(candidateError.FailedShard.GetValueOrDefault(), 0, 3);

        var pageBudget = new CacheScanBudget(0, 2);
        var pageErrors = await Task.WhenAll(Enumerable.Range(0, 4).Select(async shard =>
        {
            using var scope = pageBudget.EnterShard(shard);
            try
            {
                pageBudget.ConsumePage();
                await Task.Yield();
                pageBudget.ConsumePage();
                return null;
            }
            catch (CacheScanLimitExceededException error)
            {
                return error;
            }
        }));

        var pageError = pageErrors.First(error => error != null);
        Assert.NotNull(pageError);
        Assert.Equal(2, pageBudget.PageCount);
        Assert.Equal(CacheScanLimitKind.ScanPages, pageError.Kind);
        Assert.Equal(2, pageError.Consumed);
        Assert.InRange(pageError.FailedShard.GetValueOrDefault(), 0, 3);
    }

    /// <summary>
    /// 验证资源配置拒绝负的扫描键上限。
    /// </summary>
    [Fact]
    public void ResourceOptions_RejectNegativeScanLimit()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CacheResourceOptions(maxScanKeys: -1));

        Assert.Equal("maxScanKeys", error.ParamName);
    }

    /// <summary>
    /// 验证资源配置拒绝负的扫描页上限。
    /// </summary>
    [Fact]
    public void ResourceOptions_RejectNegativeScanPageLimit()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CacheResourceOptions(maxScanPages: -1));

        Assert.Equal("maxScanPages", error.ParamName);
    }

    /// <summary>
    /// 验证失效消息字节预算默认无限制。
    /// </summary>
    [Fact]
    public void ResourceOptions_InvalidationMessageBudgetDefaultsToUnlimited()
    {
        var options = new CacheResourceOptions();

        Assert.Equal(0, options.MaxInvalidationMessageBytes);
    }

    /// <summary>
    /// 验证资源配置拒绝负的失效消息字节预算。
    /// </summary>
    [Fact]
    public void ResourceOptions_RejectsNegativeInvalidationMessageBudget()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CacheResourceOptions(maxInvalidationMessageBytes: -1));

        Assert.Equal("maxInvalidationMessageBytes", error.ParamName);
    }

    /// <summary>
    /// 验证删除命令预算必须为正数。
    /// </summary>
    [Fact]
    public void ResourceOptions_RejectNonPositiveDeleteCommandBudget()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CacheResourceOptions(maxBatchCommandBytes: 0));

        Assert.Equal("maxBatchCommandBytes", error.ParamName);
    }

    /// <summary>
    /// 验证删除批次按 UTF-8 字节拆分并保持键租约配对。
    /// </summary>
    [Fact]
    public void DeleteBatchBudget_SplitsByUtf8BytesAndKeepsLeasePairTogether()
    {
        var keys = new[] { "ascii", "订单", "long-key" };
        var batches = CacheDeleteBatchBudget.Partition(keys, 10, 24).ToArray();

        Assert.Equal(3, batches.Length);
        Assert.Equal(keys, batches.SelectMany(batch => batch));
        Assert.All(batches, batch =>
            Assert.All(batch, key => Assert.True(CacheDeleteBatchBudget.GetPairBytes(key) <= 24)));
    }

    /// <summary>
    /// 验证删除批次遵守数量上限并拒绝单个超大键。
    /// </summary>
    [Fact]
    public void DeleteBatchBudget_HonorsCountAndRejectsSingleOversizedKey()
    {
        var batches = CacheDeleteBatchBudget.Partition(new[] { "a", "b", "c" }, 2, 1024).ToArray();
        Assert.Equal(new[] { "a", "b" }, batches[0]);
        Assert.Equal(new[] { "c" }, batches[1]);

        var error = Assert.Throws<InvalidOperationException>(() =>
            CacheDeleteBatchBudget.Partition(new[] { "订单" }, 2, 10).ToArray());
        Assert.True(CacheDeleteBatchBudget.IsLimit(error));
    }
}

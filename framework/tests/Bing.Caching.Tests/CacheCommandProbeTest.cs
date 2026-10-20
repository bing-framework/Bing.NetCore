using Xunit;

namespace Bing.Caching.Tests;

/// <summary>
/// CacheCommandProbe 命令计数和并发统计测试。
/// </summary>
public class CacheCommandProbeTest
{
    /// <summary>
    /// 验证命令类型计数、并发峰值和 Unknown 分类兼容性。
    /// </summary>
    [Fact]
    public async Task Probe_TracksCallsByKindAndRestoresUnknownCompatibility()
    {
        using var outer = CacheCommandProbe.Begin();

        // The existing overload remains an Unknown command for compatibility.
        Assert.Equal(1, await CacheCommandProbe.InvokeAsync(() => Task.FromResult(1)));

        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var batch = CacheCommandProbe.InvokeAsync(CacheCommandKind.BatchRead, () => release.Task);
        var single = CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleRead, () => release.Task);
        var scan = CacheCommandProbe.InvokeAsync(CacheCommandKind.Scan, () => release.Task);

        Assert.Equal(4, outer.Calls);
        Assert.Equal(3, outer.Maximum);
        Assert.Equal(1, outer.GetCalls(CacheCommandKind.Unknown));
        Assert.Equal(1, outer.GetCalls(CacheCommandKind.BatchRead));
        Assert.Equal(1, outer.GetCalls(CacheCommandKind.SingleRead));
        Assert.Equal(1, outer.GetCalls(CacheCommandKind.Scan));
        var breakdown = outer.GetBreakdown();
        Assert.Equal(1, breakdown[nameof(CacheCommandKind.Unknown)]);
        Assert.Equal(1, breakdown[nameof(CacheCommandKind.BatchRead)]);
        Assert.Equal(1, breakdown[nameof(CacheCommandKind.SingleRead)]);
        Assert.Equal(1, breakdown[nameof(CacheCommandKind.Scan)]);

        using (var inner = CacheCommandProbe.Begin())
        {
            await Assert.ThrowsAsync<IOException>(() => CacheCommandProbe.InvokeAsync<int>(
                CacheCommandKind.BatchRead,
                () => throw new IOException("probe failure")));

            Assert.Equal(1, inner.Calls);
            Assert.Equal(1, inner.GetCalls(CacheCommandKind.BatchRead));
            Assert.Equal(0, inner.GetCalls(CacheCommandKind.Unknown));
            Assert.Equal(0, inner.Active);
        }

        // Disposing the nested scope restores the outer probe before its pending work completes.
        Assert.Equal(4, outer.Calls);
        Assert.Equal(1, outer.GetCalls(CacheCommandKind.Unknown));

        release.SetResult(7);
        Assert.Equal(new[] { 7, 7, 7 }, await Task.WhenAll(batch, single, scan));
        Assert.Equal(0, outer.Active);

        Assert.Equal(8, await CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleRead,
            () => Task.FromResult(8)));
        Assert.Equal(5, outer.Calls);
        Assert.Equal(2, outer.GetCalls(CacheCommandKind.SingleRead));
    }

    /// <summary>
    /// 验证重叠调用统计及嵌套作用域失败后的外层恢复。
    /// </summary>
    [Fact]
    public async Task Probe_TracksOverlappingCallsAndRestoresOuterScopeOnFailure()
    {
        using var outer = CacheCommandProbe.Begin();
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = CacheCommandProbe.InvokeAsync(() => release.Task);
        var second = CacheCommandProbe.InvokeAsync(() => release.Task);
        Assert.Equal(2, outer.Calls);
        Assert.Equal(2, outer.Maximum);
        using (var inner = CacheCommandProbe.Begin())
        {
            await Assert.ThrowsAsync<IOException>(() => CacheCommandProbe.InvokeAsync<int>(() => throw new IOException()));
            Assert.Equal(1, inner.Calls);
            Assert.Equal(0, inner.Active);
        }
        release.SetResult(7);
        await Task.WhenAll(first, second);
        Assert.Equal(0, outer.Active);
        Assert.Equal(3, await CacheCommandProbe.InvokeAsync(() => Task.FromResult(3)));
        Assert.Equal(3, outer.Calls);
    }

    /// <summary>
    /// 验证写入和删除命令分类及失败后的可用性。
    /// </summary>
    [Fact]
    public async Task Probe_TracksWriteAndDeleteKindsAndRecoversAfterFailures()
    {
        using var probe = CacheCommandProbe.Begin();

        // The legacy overload remains an Unknown command for existing callers.
        Assert.Equal(1, await CacheCommandProbe.InvokeAsync(() => Task.FromResult(1)));
        Assert.Equal(2, await CacheCommandProbe.InvokeAsync(
            CacheCommandKind.SingleWrite,
            () => Task.FromResult(2)));
        Assert.Equal(3, await CacheCommandProbe.InvokeAsync(
            CacheCommandKind.Delete,
            () => Task.FromResult(3)));

        await Assert.ThrowsAsync<IOException>(() => CacheCommandProbe.InvokeAsync<int>(
            CacheCommandKind.SingleWrite,
            () => throw new IOException("write failure")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => CacheCommandProbe.InvokeAsync<int>(
            CacheCommandKind.Delete,
            () => throw new InvalidOperationException("delete failure")));

        // Failed calls are counted and leave the probe usable for later calls.
        Assert.Equal(5, probe.Calls);
        Assert.Equal(0, probe.Active);
        Assert.Equal(1, probe.GetCalls(CacheCommandKind.Unknown));
        Assert.Equal(2, probe.GetCalls(CacheCommandKind.SingleWrite));
        Assert.Equal(2, probe.GetCalls(CacheCommandKind.Delete));

        var breakdown = probe.GetBreakdown();
        Assert.Equal(1, breakdown[nameof(CacheCommandKind.Unknown)]);
        Assert.Equal(2, breakdown[nameof(CacheCommandKind.SingleWrite)]);
        Assert.Equal(2, breakdown[nameof(CacheCommandKind.Delete)]);

        Assert.Equal(4, await CacheCommandProbe.InvokeAsync(
            CacheCommandKind.SingleWrite,
            () => Task.FromResult(4)));
        Assert.Equal(6, probe.Calls);
        Assert.Equal(3, probe.GetCalls(CacheCommandKind.SingleWrite));
        Assert.Equal(0, probe.Active);
    }

    /// <summary>
    /// 验证非泛型命令调用在失败后的恢复。
    /// </summary>
    [Fact]
    public async Task Probe_TracksNonGenericCommandsAndRestoresAfterFailure()
    {
        using var probe = CacheCommandProbe.Begin();

        await CacheCommandProbe.InvokeAsync(
            CacheCommandKind.SingleWrite,
            () => Task.CompletedTask);
        Assert.Equal(1, probe.Calls);
        Assert.Equal(1, probe.GetCalls(CacheCommandKind.SingleWrite));
        Assert.Equal(0, probe.Active);

        await Assert.ThrowsAsync<IOException>(() => CacheCommandProbe.InvokeAsync(
            CacheCommandKind.Delete,
            () => throw new IOException("delete failure")));

        Assert.Equal(2, probe.Calls);
        Assert.Equal(0, probe.Active);
        var breakdown = probe.GetBreakdown();
        Assert.Equal(1, breakdown[nameof(CacheCommandKind.SingleWrite)]);
        Assert.Equal(1, breakdown[nameof(CacheCommandKind.Delete)]);

        // A non-generic call remains usable after a failed command.
        await CacheCommandProbe.InvokeAsync(
            CacheCommandKind.Delete,
            () => Task.CompletedTask);
        Assert.Equal(3, probe.Calls);
        Assert.Equal(2, probe.GetCalls(CacheCommandKind.Delete));
        Assert.Equal(0, probe.Active);
    }

    /// <summary>
    /// 验证各命令类型的最大并发数和未使用类型的零值。
    /// </summary>
    [Fact]
    public async Task Probe_TracksMaximumConcurrencyPerKindAndLeavesUnusedKindsAtZero()
    {
        using var probe = CacheCommandProbe.Begin();
        var batchRelease = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var singleRelease = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        var batchCalls = new[]
        {
            CacheCommandProbe.InvokeAsync(CacheCommandKind.BatchRead, () => batchRelease.Task),
            CacheCommandProbe.InvokeAsync(CacheCommandKind.BatchRead, () => batchRelease.Task),
            CacheCommandProbe.InvokeAsync(CacheCommandKind.BatchRead, () => batchRelease.Task)
        };
        var singleCalls = new[]
        {
            CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleRead, () => singleRelease.Task),
            CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleRead, () => singleRelease.Task)
        };

        Assert.Equal(3, probe.GetMaximum(CacheCommandKind.BatchRead));
        Assert.Equal(2, probe.GetMaximum(CacheCommandKind.SingleRead));
        Assert.Equal(0, probe.GetMaximum(CacheCommandKind.Scan));
        Assert.Equal(0, probe.GetMaximum(CacheCommandKind.SingleWrite));
        Assert.Equal(0, probe.GetMaximum(CacheCommandKind.Delete));
        Assert.Equal(0, probe.GetMaximum(CacheCommandKind.Unknown));
        var maximumBreakdown = probe.GetMaximumBreakdown();
        Assert.Equal(3, maximumBreakdown[nameof(CacheCommandKind.BatchRead)]);
        Assert.Equal(2, maximumBreakdown[nameof(CacheCommandKind.SingleRead)]);
        Assert.DoesNotContain(nameof(CacheCommandKind.Scan), maximumBreakdown.Keys);

        batchRelease.SetResult(7);
        singleRelease.SetResult(8);
        Assert.Equal(new[] { 7, 7, 7 }, await Task.WhenAll(batchCalls));
        Assert.Equal(new[] { 8, 8 }, await Task.WhenAll(singleCalls));
        Assert.Equal(0, probe.Active);
        Assert.Equal(3, probe.GetMaximum(CacheCommandKind.BatchRead));
        Assert.Equal(2, probe.GetMaximum(CacheCommandKind.SingleRead));
    }

    /// <summary>
    /// 验证显式扫描页和页内条目的累计统计。
    /// </summary>
    [Fact]
    public async Task Probe_TracksExplicitScanPagesAndHiddenPageItems()
    {
        using var probe = CacheCommandProbe.Begin();

        CacheCommandProbe.RecordScanPage(3);
        CacheCommandProbe.RecordScanPage(0);
        CacheCommandProbe.RecordScanItem();

        Assert.Equal(2, probe.ScanPages);
        Assert.Equal(4, probe.ScanItems);
        await CacheCommandProbe.InvokeAsync(CacheCommandKind.Scan, () => Task.CompletedTask);
        Assert.Equal(1, probe.GetCalls(CacheCommandKind.Scan));
    }

    /// <summary>
    /// 验证未报告扫描页边界时保留未知页数。
    /// </summary>
    [Fact]
    public void Probe_ReportsUnknownScanPageBoundaryUntilProviderReportsOne()
    {
        using var probe = CacheCommandProbe.Begin();

        Assert.Equal(-1, probe.ScanPages);
        Assert.Equal(0, probe.ScanItems);
        CacheCommandProbe.RecordScanItem();
        Assert.Equal(-1, probe.ScanPages);
        Assert.Equal(1, probe.ScanItems);
    }

    /// <summary>
    /// 验证删除命令失败后最大并发统计能够恢复。
    /// </summary>
    [Fact]
    public async Task Probe_TracksMaximumConcurrencyAfterFailureAndRecovery()
    {
        using var probe = CacheCommandProbe.Begin();

        await Assert.ThrowsAsync<IOException>(() => CacheCommandProbe.InvokeAsync<int>(
            CacheCommandKind.Delete,
            () => throw new IOException("delete failure")));

        Assert.Equal(1, probe.GetCalls(CacheCommandKind.Delete));
        Assert.Equal(1, probe.GetMaximum(CacheCommandKind.Delete));
        Assert.Equal(0, probe.Active);

        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = CacheCommandProbe.InvokeAsync(CacheCommandKind.Delete, () => release.Task);
        var second = CacheCommandProbe.InvokeAsync(CacheCommandKind.Delete, () => release.Task);

        Assert.Equal(3, probe.GetCalls(CacheCommandKind.Delete));
        Assert.Equal(2, probe.GetMaximum(CacheCommandKind.Delete));
        Assert.Equal(2, probe.Active);

        release.SetResult(9);
        Assert.Equal(new[] { 9, 9 }, await Task.WhenAll(first, second));
        Assert.Equal(0, probe.Active);
        Assert.Equal(3, probe.GetCalls(CacheCommandKind.Delete));
        Assert.Equal(2, probe.GetMaximum(CacheCommandKind.Delete));
        Assert.Equal(0, probe.GetMaximum(CacheCommandKind.BatchRead));
    }
}

using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 验证 HybridRefillGeneration 对回填代次和并发闸门的控制。
/// </summary>
public sealed class HybridRefillGenerationTest
{
    /// <summary>
    /// 推进代次后拒绝使用旧代次的同步和异步回填。
    /// </summary>
    [Fact]
    public async Task AdvanceRejectsStaleSynchronousAndAsynchronousRefills()
    {
        var generation = new HybridRefillGeneration();
        var captured = generation.Capture();
        generation.Advance();
        var appliedSync = generation.TryApply(captured, () => { });
        var appliedAsync = await generation.TryApplyAsync(captured, () => Task.CompletedTask,
            CancellationToken.None);

        Assert.False(appliedSync);
        Assert.False(appliedAsync);
    }

    /// <summary>
    /// 异步回填持有代次闸门，直至回填操作完成。
    /// </summary>
    [Fact]
    public async Task AsyncRefillHoldsGenerationGateWhileApplying()
    {
        var generation = new HybridRefillGeneration();
        var captured = generation.Capture();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refill = generation.TryApplyAsync(captured, async () =>
        {
            started.SetResult(true);
            await release.Task;
        }, CancellationToken.None);

        await started.Task;
        var advance = Task.Run(generation.Advance);
        await Task.Delay(25);
        Assert.False(advance.IsCompleted);
        release.SetResult(true);
        Assert.True(await refill);
        await advance;
    }
}

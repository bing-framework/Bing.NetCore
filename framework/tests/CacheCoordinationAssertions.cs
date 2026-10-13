using Xunit;

namespace Bing.Caching.ContractTests;

/// <summary>
/// 各 Provider 复用的缓存协调边界断言。
/// </summary>
/// <remarks>
/// 覆盖回源资源限制、批量载荷拆分、相同键合并、显式修改优先和超时恢复。
/// </remarks>
public static class CacheCoordinationAssertions
{
    // 调用方配置 MaxConcurrentFactories=1、MaxQueuedFactories>=4、BatchSize=2、payload=32。
    /// <summary>
    /// 验证回源并发和批量载荷遵守资源限制。
    /// </summary>
    /// <remarks>
    /// 同时检查不同键的回源并发、超大批量载荷的失败边界以及列表和流式读取行为。
    /// </remarks>
    /// <param name="cache">待验证的缓存实现。</param>
    /// <param name="seed">可选的批量写入委托；为 null 时使用缓存实现的 SetAllAsync。</param>
    /// <param name="keys">用于回源、批量读取和清理的逻辑键；为 null 时生成随机键。</param>
    public static async Task VerifyResourceLimitsAsync(ICache cache, Func<Dictionary<string, string>, Task> seed = null,
        string[] keys = null)
    {
        keys ??= Enumerable.Range(0, 5).Select(_ => "resource:" + Guid.NewGuid().ToString("N")).ToArray();
        var active = 0;
        var maximum = 0;
        try
        {
            await Task.WhenAll(keys.Select(key => cache.GetAsync<int>(key, async token =>
            {
                var current = Interlocked.Increment(ref active);
                maximum = Math.Max(maximum, current);
                try { await Task.Delay(20, token); return 1; }
                finally { Interlocked.Decrement(ref active); }
            })));
            Assert.Equal(1, maximum);
            var values = new Dictionary<string, string>
            {
                [keys[0]] = "a", [keys[1]] = "b", [keys[2]] = new string('x', 64)
            };
            await (seed == null ? cache.SetAllAsync(values) : seed(values));
            Assert.Throws<InvalidOperationException>(() => cache.GetAll<string>(new[] { keys[2] }));
            await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAllAsync<string>(keys.Take(3)));
            await using var iterator = cache.GetAllStreamAsync<string>(keys.Take(3)).GetAsyncEnumerator();
            Assert.True(await iterator.MoveNextAsync());
            Assert.Equal("a", iterator.Current.Value);
            Assert.True(await iterator.MoveNextAsync());
            Assert.Equal("b", iterator.Current.Value);
            await Assert.ThrowsAsync<InvalidOperationException>(() => iterator.MoveNextAsync().AsTask());
        }
        finally { await cache.RemoveAllAsync(keys); }
    }

    /// <summary>
    /// 验证批量载荷超限时按键拆分读取。
    /// </summary>
    /// <remarks>
    /// 拆分后应保留列表和流式读取的请求顺序、值以及命中语义。
    /// </remarks>
    /// <param name="cache">待验证的缓存实现。</param>
    public static async Task VerifyAdaptivePayloadSplittingAsync(ICache cache)
    {
        var prefix = "adaptive-payload:" + Guid.NewGuid().ToString("N") + ":";
        var first = prefix + "first";
        var second = prefix + "second";
        var nullKey = prefix + "null";
        var missing = prefix + "missing";
        var values = new Dictionary<string, string>
        {
            [first] = new string('a', 16),
            [second] = new string('b', 16)
        };
        var requested = new[] { second, missing, nullKey, first, second };

        try
        {
            await cache.SetAllAsync(values);
            await cache.SetAsync<string>(nullKey, null);

            var expected = new[] { values[second], null, null, values[first], values[second] };
            var expectedHits = new[] { true, false, true, true, true };
            var list = await cache.GetAllAsync<string>(requested);
            Assert.Equal(expected, list);
            Assert.Equal(expected, cache.GetAll<string>(requested));

            var streamed = new List<CacheEntry<string>>(requested.Length);
            await foreach (var entry in cache.GetAllStreamAsync<string>(requested))
                streamed.Add(entry);

            Assert.Equal(requested, streamed.Select(entry => entry.Key));
            Assert.Equal(expected, streamed.Select(entry => entry.Value));
            Assert.Equal(expectedHits, streamed.Select(entry => entry.Hit));
        }
        finally
        {
            await cache.RemoveAllAsync(new[] { first, second, nullKey, missing });
        }
    }

    /// <summary>
    /// 验证相同键回源合并和租约丢失处理。
    /// </summary>
    /// <remarks>
    /// 相同键请求应共享一次回源；租约被删除后在途回源应取消且不得写回结果。
    /// </remarks>
    /// <param name="cache">待验证的缓存实现。</param>
    public static async Task VerifySameKeyMergeAndLeaseLossAsync(ICache cache)
    {
        var key = "merge:" + Guid.NewGuid().ToString("N");
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = cache.GetAsync<int>(key, _ => { started.SetResult(true); return result.Task; });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var follower = cache.GetAsync<int>(key, _ => throw new InvalidOperationException("不应重复回源"));
        try
        {
            result.SetResult(9);
            Assert.Equal(new[] { 9, 9 }, await Task.WhenAll(owner, follower).WaitAsync(TimeSpan.FromSeconds(5)));
            await cache.RemoveAsync(key);
            started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            result = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationToken factoryToken = default;
            owner = cache.GetAsync<int>(key, token => { factoryToken = token; started.SetResult(true); return result.Task; });
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await cache.RemoveAsync(key);
            await Assert.ThrowsAsync<TimeoutException>(() => owner.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.True(factoryToken.IsCancellationRequested);
            Assert.False(result.Task.IsCompleted);
            result.SetResult(1);
            Assert.False(await cache.ExistsAsync(key));
        }
        finally
        {
            result.TrySetResult(1);
            try { await owner; } catch { }
            try { await follower; } catch { }
            await cache.RemoveAsync(key);
        }
    }

    /// <summary>
    /// 验证显式写入和删除优先于旧回源提交。
    /// </summary>
    /// <remarks>
    /// 覆盖同步、异步及批量写入和删除操作，迟到回源结果不得覆盖显式修改。
    /// </remarks>
    /// <param name="cache">待验证的缓存实现。</param>
    public static async Task VerifyMutationsWinAsync(ICache cache)
    {
        for (var operation = 0; operation < 6; operation++)
        {
            var key = "race:" + Guid.NewGuid().ToString("N");
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var finish = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var retrieve = cache.GetAsync<int>(key, _ => { entered.SetResult(true); return finish.Task; });
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            try
            {
                switch (operation)
                {
                    case 0: cache.Set(key, 2); break;
                    case 1: await cache.SetAsync(key, 2); break;
                    case 2: cache.Remove(key); break;
                    case 3: await cache.RemoveAsync(key); break;
                    case 4: await cache.SetAllAsync(new Dictionary<string, int> { [key] = 2 }); break;
                    case 5: await cache.RemoveAllAsync(new[] { key }); break;
                }
                finish.SetResult(1);
                var error = await Record.ExceptionAsync(() => retrieve.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.NotNull(error);
                Assert.True(error is InvalidOperationException || error is TimeoutException, error.ToString());
                Assert.Equal(operation == 0 || operation == 1 || operation == 4 ? 2 : 0, await cache.GetAsync<int>(key));
            }
            finally
            {
                finish.TrySetResult(1);
                try { await retrieve; } catch { }
                await cache.RemoveAsync(key);
            }
        }
    }

    /// <summary>
    /// 验证回源超时后的协调状态恢复。
    /// </summary>
    /// <remarks>
    /// 首次回源超时后，后续回源应能够成功写入并返回新值。
    /// </remarks>
    /// <param name="cache">待验证的缓存实现。</param>
    public static async Task VerifyTimeoutAsync(ICache cache)
    {
        var key = "timeout:" + Guid.NewGuid().ToString("N");
        var factory = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        await Assert.ThrowsAsync<TimeoutException>(() => cache.GetAsync<int>(key, _ => factory.Task)
            .WaitAsync(TimeSpan.FromSeconds(3)));
        factory.SetResult(1);
        Assert.Equal(2, await cache.GetAsync<int>(key, _ => Task.FromResult(2)));
        await cache.RemoveAsync(key);
    }
}

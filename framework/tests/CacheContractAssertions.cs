using Bing.Caching;
using Xunit;

namespace Bing.Caching.ContractTests;

/// <summary>
/// 各 Provider 复用的缓存公共行为契约断言。
/// </summary>
public static class CacheContractAssertions
{
    /// <summary>
    /// 验证缓存实现的基础读写、批量、流式、扫描、取消和清理契约。
    /// </summary>
    /// <param name="cache">待验证的缓存实现。</param>
    public static async Task VerifyAsync(ICache cache)
    {
        var runPrefix = "contract:" + Guid.NewGuid().ToString("N") + ":";
        var firstKey = runPrefix + "first";
        var secondKey = runPrefix + "second";
        var zeroKey = runPrefix + "zero";

        cache.Set(firstKey, 1);
        cache.Set(firstKey, 2);
        Assert.Equal(2, cache.Get<int>(firstKey));
        Assert.False(cache.TrySet(firstKey, 3));
        Assert.True(cache.TrySet(secondKey, 4));
        Assert.Equal(4, cache.Get<int>(secondKey));

        await cache.SetAsync(zeroKey, 0);
        var called = false;
        Assert.Equal(0, await cache.GetAsync(zeroKey, _ =>
        {
            called = true;
            return Task.FromResult(5);
        }));
        Assert.False(called);

        Assert.Equal(new[] { 4, 0, 2, 4 },
            await cache.GetAllAsync<int>(new[] { secondKey, runPrefix + "missing", firstKey, secondKey }));
        Assert.Empty(await cache.GetAllAsync<int>(Array.Empty<string>()));

        var batchFirstKey = runPrefix + "batch:first";
        var batchSecondKey = runPrefix + "batch:second";
        cache.SetAll(new Dictionary<string, int>
        {
            [batchFirstKey] = 10,
            [batchSecondKey] = 20
        });
        cache.SetAll(new Dictionary<string, int>
        {
            [batchFirstKey] = 11,
            [batchSecondKey] = 21
        });
        Assert.Equal(new[] { 11, 21 }, cache.GetAll<int>(new[] { batchFirstKey, batchSecondKey }));

        var asyncBatchFirstKey = runPrefix + "batch:async:first";
        var asyncBatchSecondKey = runPrefix + "batch:async:second";
        await cache.SetAllAsync(new Dictionary<string, int>
        {
            [asyncBatchFirstKey] = 30,
            [asyncBatchSecondKey] = 40
        });
        await cache.SetAllAsync(new Dictionary<string, int>
        {
            [asyncBatchFirstKey] = 31,
            [asyncBatchSecondKey] = 41
        });
        Assert.Equal(new[] { 31, 41 },
            await cache.GetAllAsync<int>(new[] { asyncBatchFirstKey, asyncBatchSecondKey }));

        cache.SetAll(new Dictionary<string, int>());
        await cache.SetAllAsync(new Dictionary<string, int>());
        Assert.Equal(new[] { 11, 21 },
            await cache.GetAllAsync<int>(new[] { batchFirstKey, batchSecondKey }));

        var cancelledBatchKey = runPrefix + "batch:cancelled";
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                cache.SetAllAsync(new Dictionary<string, int> { [cancelledBatchKey] = 99 },
                    cancellationToken: cancelled.Token));
        }
        Assert.False(await cache.ExistsAsync(cancelledBatchKey));

        var streamNullKey = runPrefix + "stream:null";
        var streamMissingKey = runPrefix + "stream:missing";
        await cache.SetAsync<int?>(streamNullKey, null);
        var streamInput = new[] { firstKey, streamMissingKey, streamNullKey, firstKey };
        var streamEntries = new List<CacheEntry<int?>>();
        await foreach (var entry in cache.GetAllStreamAsync<int?>(streamInput))
            streamEntries.Add(entry);

        Assert.Equal(streamInput, streamEntries.Select(entry => entry.Key));
        Assert.Equal(new int?[] { 2, null, null, 2 }, streamEntries.Select(entry => entry.Value));
        Assert.Equal(new[] { true, false, true, true }, streamEntries.Select(entry => entry.Hit));

        var scanPrefix = runPrefix + "scan:";
        var scanHitKey = scanPrefix + "hit";
        var scanNullKey = scanPrefix + "null";
        var scanSiblingKey = runPrefix + "scan-sibling:sentinel";
        await cache.SetAsync(scanHitKey, 11);
        await cache.SetAsync<int?>(scanNullKey, null);
        await cache.SetAsync(scanSiblingKey, 99);

        var prefixValues = await cache.GetByPrefixAsync<int?>(scanPrefix);
        var prefixEntries = new List<CacheEntry<int?>>();
        await foreach (var entry in cache.GetByPrefixStreamAsync<int?>(scanPrefix))
            prefixEntries.Add(entry);

        Assert.Equal(2, prefixValues.Count);
        Assert.Equal(prefixValues.OrderBy(value => value).ToArray(),
            prefixEntries.Select(entry => entry.Value).OrderBy(value => value).ToArray());
        Assert.All(prefixEntries, entry => Assert.True(entry.Hit));
        Assert.Contains(prefixEntries, entry => entry.Key == scanHitKey && entry.Value == 11);
        Assert.Contains(prefixEntries, entry => entry.Key == scanNullKey && entry.Value == null);
        Assert.DoesNotContain(prefixEntries, entry => entry.Key == scanSiblingKey);
        Assert.DoesNotContain(prefixValues, value => value == 99);

        var specialPrefix = runPrefix + "特殊:{scan}*?:";
        var specialHitKey = specialPrefix + "\\中文";
        var specialNullKey = specialPrefix + "null";
        // 与目标前缀共享大部分文本，但在冒号前增加字面量差异，不能被目标前缀命中。
        var specialSiblingKey = runPrefix + "特殊:{scan}*?x:sibling";
        await cache.SetAsync(specialHitKey, 21);
        await cache.SetAsync<int?>(specialNullKey, null);
        await cache.SetAsync(specialSiblingKey, 99);

        var specialEntries = new List<CacheEntry<int?>>();
        await foreach (var entry in cache.GetByPrefixStreamAsync<int?>(specialPrefix))
            specialEntries.Add(entry);

        Assert.Equal(2, specialEntries.Count);
        Assert.Contains(specialEntries, entry => entry.Key == specialHitKey && entry.Value == 21 && entry.Hit);
        Assert.Contains(specialEntries, entry => entry.Key == specialNullKey && entry.Value == null && entry.Hit);
        Assert.DoesNotContain(specialEntries, entry => entry.Key == specialSiblingKey);
        await cache.RemoveAllAsync(new[] { specialHitKey, specialNullKey, specialSiblingKey });

        await cache.ClearAsync();
        Assert.False(cache.Exists(firstKey));
        Assert.False(cache.Exists(secondKey));
        Assert.False(cache.Exists(zeroKey));
    }
}

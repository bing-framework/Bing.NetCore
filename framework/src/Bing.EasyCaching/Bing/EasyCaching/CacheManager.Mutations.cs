using Bing.Caching;

namespace Bing.EasyCaching;

/// <inheritdoc />
public partial class CacheManager
{
    /// <summary>
    /// 按当前缓存拓扑写入单个物理键。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physical">物理缓存键。</param>
    /// <param name="value">待写入的值。</param>
    /// <param name="expiration">缓存有效期。</param>
    /// <param name="onlyMissing">是否仅在键不存在时写入。</param>
    /// <returns>成功写入时返回 <see langword="true"/>；键已存在时返回 <see langword="false"/>。</returns>
    private bool WriteValue<T>(string physical, T value, TimeSpan expiration, bool onlyMissing = false)
    {
        if (_coordinator != null)
        {
            MarkHybridMutation();
            var written = _coordinator.Write(physical, value, expiration, onlyMissing);
            if (written && _localProvider != null) ReadHybridBatch<T>(new[] { physical });
            return written;
        }
        if (onlyMissing) return ExecuteWithBusCheck(() => _provider.TrySet(physical, value, expiration));
        ExecuteWithBusCheck(() => _provider.Set(physical, value, expiration));
        return true;
    }

    /// <summary>
    /// 异步按当前缓存拓扑写入单个物理键。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physical">物理缓存键。</param>
    /// <param name="value">待写入的值。</param>
    /// <param name="expiration">缓存有效期。</param>
    /// <param name="onlyMissing">是否仅在键不存在时写入。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>写入成功状态。</returns>
    private async Task<bool> WriteValueAsync<T>(string physical, T value, TimeSpan expiration,
        bool onlyMissing, CancellationToken token)
    {
        if (_coordinator != null)
        {
            MarkHybridMutation();
            var written = await _coordinator.WriteAsync(physical, value, expiration, onlyMissing, token);
            if (written && _localProvider != null) await ReadHybridBatchAsync<T>(new[] { physical }, token);
            return written;
        }
        if (onlyMissing)
            return await ExecuteWithBusCheckAsync(() => CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleWrite,
                () => _provider.TrySetAsync(physical, value, expiration, token)));
        await ExecuteWithBusCheckAsync(() => CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleWrite,
            () => _provider.SetAsync(physical, value, expiration, token)));
        return true;
    }

    /// <summary>
    /// 删除单个物理缓存键。
    /// </summary>
    /// <param name="physical">物理缓存键。</param>
    private void DeleteValue(string physical)
    {
        if (_coordinator != null)
        {
            MarkHybridMutation();
            _coordinator.Remove(physical);
        }
        else ExecuteWithBusCheck(() => _provider.Remove(physical));
    }

    /// <summary>
    /// 异步删除单个物理缓存键。
    /// </summary>
    /// <param name="physical">物理缓存键。</param>
    /// <param name="token">取消令牌。</param>
    private async Task DeleteValueAsync(string physical, CancellationToken token)
    {
        if (_coordinator != null)
        {
            MarkHybridMutation();
            await _coordinator.RemoveAsync(physical, token);
            return;
        }
        await ExecuteWithBusCheckAsync(() => CacheCommandProbe.InvokeAsync(CacheCommandKind.Delete,
            () => _provider.RemoveAsync(physical, token)));
    }
}

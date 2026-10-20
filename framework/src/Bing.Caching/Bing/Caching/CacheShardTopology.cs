namespace Bing.Caching;

/// <summary>
/// 验证客户端分片清单中的 Redis 实例身份。
/// </summary>
public static class CacheShardTopology
{
    /// <summary>
    /// 从 INFO SERVER 响应读取 Redis 进程身份。
    /// </summary>
    /// <param name="info">INFO SERVER 命令响应。</param>
    /// <returns>Redis 进程身份。</returns>
    public static string GetServerId(string info)
    {
        if (string.IsNullOrWhiteSpace(info))
            throw new InvalidOperationException("无法读取 Redis 分片身份。");
        foreach (var line in info.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.StartsWith("run_id:", StringComparison.Ordinal)) continue;
            var id = line.Substring("run_id:".Length).Trim();
            if (id.Length != 0) return id;
        }
        throw new InvalidOperationException("Redis 分片身份响应不完整。");
    }

    /// <summary>
    /// 验证分片实例清单完整且无重复。
    /// </summary>
    /// <param name="serverInfo">各分片实例的 INFO SERVER 响应。</param>
    /// <param name="expectedCount">预期的分片实例数量。</param>
    public static void Validate(IEnumerable<string> serverInfo, int expectedCount)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var info in serverInfo)
        {
            if (!ids.Add(GetServerId(info)))
                throw new InvalidOperationException("Redis 分片扫描清单包含重复实例。");
        }
        if (ids.Count != expectedCount)
            throw new InvalidOperationException("Redis 分片扫描清单缺少实例。");
    }
}

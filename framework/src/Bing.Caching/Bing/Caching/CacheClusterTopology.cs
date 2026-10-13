namespace Bing.Caching;

/// <summary>
/// 验证 Redis Cluster 扫描节点清单。
/// </summary>
public static class CacheClusterTopology
{
    /// <summary>
    /// 根据 Redis CRC16 和 hash tag 规则计算键槽。
    /// </summary>
    /// <param name="key">待计算的键。</param>
    /// <returns>键对应的 Redis Cluster 槽位。</returns>
    public static int GetHashSlot(string key)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        var start = key.IndexOf('{');
        var hashStart = 0;
        var hashLength = key.Length;
        if (start >= 0)
        {
            var end = key.IndexOf('}', start + 1);
            if (end > start + 1)
            {
                hashStart = start + 1;
                hashLength = end - hashStart;
            }
        }

        // 物理键通常是 ASCII；直接按字符区间编码，避免每个分组键创建子字符串和 byte[]。
        // 对非 ASCII 和非法代理项保留 Encoding.UTF8 的替换语义。
        var crc = 0;
        var endIndex = hashStart + hashLength;
        for (var index = hashStart; index < endIndex; index++)
        {
            var value = key[index];
            if (value <= 0x7f)
            {
                crc = AppendCrcByte(crc, value);
                continue;
            }

            if (value <= 0x7ff)
            {
                crc = AppendCrcByte(crc, 0xc0 | (value >> 6));
                crc = AppendCrcByte(crc, 0x80 | (value & 0x3f));
                continue;
            }

            if (char.IsHighSurrogate(value) && index + 1 < endIndex &&
                char.IsLowSurrogate(key[index + 1]))
            {
                var codePoint = char.ConvertToUtf32(value, key[++index]);
                crc = AppendCrcByte(crc, 0xf0 | (codePoint >> 18));
                crc = AppendCrcByte(crc, 0x80 | ((codePoint >> 12) & 0x3f));
                crc = AppendCrcByte(crc, 0x80 | ((codePoint >> 6) & 0x3f));
                crc = AppendCrcByte(crc, 0x80 | (codePoint & 0x3f));
                continue;
            }

            // Encoding.UTF8 用 U+FFFD 替换孤立代理项。
            if (char.IsSurrogate(value))
                value = '\ufffd';
            crc = AppendCrcByte(crc, 0xe0 | (value >> 12));
            crc = AppendCrcByte(crc, 0x80 | ((value >> 6) & 0x3f));
            crc = AppendCrcByte(crc, 0x80 | (value & 0x3f));
        }
        return crc % 16384;
    }

    /// <summary>
    /// 将一个字节追加到 CRC16 校验值。
    /// </summary>
    /// <param name="crc">当前校验值。</param>
    /// <param name="value">待追加的字节值。</param>
    /// <returns>更新后的 CRC16 校验值。</returns>
    private static int AppendCrcByte(int crc, int value)
    {
        crc ^= value << 8;
        for (var bit = 0; bit < 8; bit++)
            crc = (crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1;
        return crc & 0xffff;
    }

    /// <summary>
    /// 从 CLUSTER NODES 响应读取在线主节点身份。
    /// </summary>
    /// <param name="nodes">CLUSTER NODES 命令响应。</param>
    /// <returns>在线主节点身份集合。</returns>
    public static HashSet<string> GetMasterIds(string nodes)
    {
        if (string.IsNullOrWhiteSpace(nodes))
            throw new InvalidOperationException("无法读取 Redis Cluster 拓扑。");
        var masters = new HashSet<string>(StringComparer.Ordinal);
        var coveredSlots = new bool[16384];
        foreach (var line in nodes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 8)
                throw new InvalidOperationException("Redis Cluster 拓扑响应不完整。");
            var flags = fields[2].Split(',');
            if (!flags.Contains("master")) continue;
            if (flags.Contains("fail") || flags.Contains("fail?")) continue;
            if (!string.Equals(fields[7], "connected", StringComparison.Ordinal))
                throw new InvalidOperationException("Redis Cluster 主节点连接状态异常。");
            masters.Add(fields[0]);
            for (var index = 8; index < fields.Length; index++)
            {
                if (fields[index].StartsWith("[", StringComparison.Ordinal)) continue;
                var range = fields[index].Split('-');
                if (range.Length > 2 || !int.TryParse(range[0], out var start) ||
                    (range.Length == 2 && !int.TryParse(range[1], out _)))
                    throw new InvalidOperationException("Redis Cluster 槽位信息无效。");
                var end = range.Length == 2 ? int.Parse(range[1]) : start;
                if (start < 0 || end >= coveredSlots.Length || end < start)
                    throw new InvalidOperationException("Redis Cluster 槽位范围无效。");
                for (var slot = start; slot <= end; slot++)
                {
                    if (coveredSlots[slot])
                        throw new InvalidOperationException("Redis Cluster 槽位归属重复。");
                    coveredSlots[slot] = true;
                }
            }
        }
        if (masters.Count == 0)
            throw new InvalidOperationException("Redis Cluster 没有可扫描的主节点。");
        if (coveredSlots.Any(covered => !covered))
            throw new InvalidOperationException("Redis Cluster 主节点未覆盖全部槽位。");
        return masters;
    }

    /// <summary>
    /// 生成规范化的槽位归属快照。
    /// </summary>
    /// <param name="nodes">CLUSTER NODES 命令响应。</param>
    /// <returns>按槽位顺序编码的主节点归属快照。</returns>
    /// <remarks>生成结果不受节点行顺序和槽位范围分段方式影响。</remarks>
    public static string GetSlotOwnershipSnapshot(string nodes)
    {
        var masters = GetMasterIds(nodes);
        var owners = new string[16384];
        foreach (var line in nodes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (!masters.Contains(fields[0])) continue;
            for (var index = 8; index < fields.Length; index++)
            {
                if (fields[index].StartsWith("[", StringComparison.Ordinal)) continue;
                var range = fields[index].Split('-');
                var start = int.Parse(range[0]);
                var end = range.Length == 2 ? int.Parse(range[1]) : start;
                for (var slot = start; slot <= end; slot++)
                    owners[slot] = fields[0];
            }
        }
        var snapshot = new System.Text.StringBuilder();
        string previous = null;
        for (var slot = 0; slot < owners.Length; slot++)
        {
            if (string.Equals(previous, owners[slot], StringComparison.Ordinal)) continue;
            snapshot.Append(slot).Append(':').Append(owners[slot]).Append(';');
            previous = owners[slot];
        }
        return snapshot.ToString();
    }

    /// <summary>
    /// 从 CLUSTER NODES 响应读取本机主节点身份。
    /// </summary>
    /// <param name="nodes">CLUSTER NODES 命令响应。</param>
    /// <returns>本机主节点身份。</returns>
    public static string GetLocalMasterId(string nodes)
    {
        if (string.IsNullOrWhiteSpace(nodes))
            throw new InvalidOperationException("无法读取 Redis Cluster 拓扑。");
        string localId = null;
        foreach (var line in nodes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 8)
                throw new InvalidOperationException("Redis Cluster 拓扑响应不完整。");
            var flags = fields[2].Split(',');
            if (!flags.Contains("myself"))
                continue;
            if (localId != null || !flags.Contains("master") || flags.Contains("fail") ||
                flags.Contains("fail?") || !string.Equals(fields[7], "connected", StringComparison.Ordinal))
                throw new InvalidOperationException("Redis Cluster 本机主节点身份无效。");
            localId = fields[0];
        }
        return localId ?? throw new InvalidOperationException("Redis Cluster 缺少本机主节点身份。");
    }

    /// <summary>
    /// 验证主节点报告的槽位归属一致。
    /// </summary>
    /// <param name="expected">预期的槽位归属快照。</param>
    /// <param name="nodes">待验证的 CLUSTER NODES 命令响应。</param>
    public static void EnsureSameView(string expected, string nodes)
    {
        if (!string.Equals(expected, GetSlotOwnershipSnapshot(nodes), StringComparison.Ordinal))
            throw new InvalidOperationException("Redis Cluster 主节点槽位视图不一致。");
    }

    /// <summary>
    /// 验证扫描期间槽位归属未发生变化。
    /// </summary>
    /// <param name="before">扫描开始前的槽位归属快照。</param>
    /// <param name="after">扫描完成后的槽位归属快照。</param>
    public static void EnsureUnchanged(string before, string after)
    {
        if (!string.Equals(before, after, StringComparison.Ordinal))
            throw new InvalidOperationException("Redis Cluster 扫描期间槽位归属发生变化。");
    }

    /// <summary>
    /// 读取覆盖全部槽位的在线主节点端点。
    /// </summary>
    /// <param name="nodes">CLUSTER NODES 命令响应。</param>
    /// <returns>在线主节点端点集合。</returns>
    public static HashSet<string> GetMasterEndpoints(string nodes)
    {
        var ids = GetMasterIds(nodes);
        var endpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in nodes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (!ids.Contains(fields[0]))
                continue;
            var endpoint = fields[1].Split('@')[0];
            if (string.IsNullOrWhiteSpace(endpoint) || !endpoints.Add(endpoint))
                throw new InvalidOperationException("Redis Cluster 主节点端点重复或无效。");
        }
        if (endpoints.Count != ids.Count)
            throw new InvalidOperationException("Redis Cluster 主节点端点清单不完整。");
        return endpoints;
    }

    /// <summary>
    /// 验证扫描节点清单与当前拓扑一致。
    /// </summary>
    /// <param name="nodes">当前 CLUSTER NODES 命令响应。</param>
    /// <param name="configuredNodeIds">配置的扫描节点身份集合。</param>
    /// <remarks>节点清单遗漏、重复或过期时抛出异常。</remarks>
    public static void Validate(string nodes, IEnumerable<string> configuredNodeIds)
    {
        var expected = GetMasterIds(nodes);
        var actual = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in configuredNodeIds)
        {
            if (string.IsNullOrWhiteSpace(id) || !actual.Add(id))
                throw new InvalidOperationException("Redis Cluster 扫描节点清单包含重复或无效节点。");
        }
        if (!actual.SetEquals(expected))
            throw new InvalidOperationException("Redis Cluster 扫描节点清单与当前主节点拓扑不一致。");
    }
}

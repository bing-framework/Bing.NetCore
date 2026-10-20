using Bing.Caching;
using Shouldly;
using System.Text;
using Xunit;

namespace Bing.Caching.Tests;

/// <summary>
/// 验证 Redis Cluster 拓扑解析与校验。
/// </summary>
public class CacheClusterTopologyTest
{
    /// <summary>
    /// 用于拓扑解析测试的 Redis 节点响应文本。
    /// </summary>
    private const string Nodes =
        "one 127.0.0.1:7001@17001 master - 0 1 1 connected 0-5000\n" +
        "two 127.0.0.1:7002@17002 master - 0 1 2 connected 5001-10000\n" +
        "three 127.0.0.1:7003@17003 master - 0 1 3 connected 10001-16383\n" +
        "old 127.0.0.1:7000@17000 master,fail - 0 1 0 disconnected\n" +
        "replica 127.0.0.1:7004@17004 slave one 0 1 4 connected\n";

    /// <summary>
    /// 验证拓扑校验要求每个活动主节点恰好出现一次。
    /// </summary>
    [Fact]
    public void Validate_RequiresEveryActiveMasterExactlyOnce()
    {
        Should.NotThrow(() => CacheClusterTopology.Validate(Nodes, new[] { "one", "two", "three" }));
        Should.Throw<InvalidOperationException>(() => CacheClusterTopology.Validate(Nodes, new[] { "one", "two" }));
        Should.Throw<InvalidOperationException>(() => CacheClusterTopology.Validate(Nodes,
            new[] { "one", "two", "two", "three" }));
        Should.Throw<InvalidOperationException>(() => CacheClusterTopology.Validate(Nodes,
            new[] { "one", "two", "old" }));
        var noFailover = Nodes.Replace("three 127.0.0.1:7003@17003 master - 0 1 3 connected 10001-16383",
            "three 127.0.0.1:7003@17003 master,fail - 0 1 3 disconnected 10001-16383");
        Should.Throw<InvalidOperationException>(() => CacheClusterTopology.Validate(noFailover,
            new[] { "one", "two" }));
    }

    /// <summary>
    /// 验证迁移和导入标记不改变稳定的主节点集合。
    /// </summary>
    [Fact]
    public void GetMasterIds_AllowsStableMigrationAndImportAnnotations()
    {
        var transientTopology = Nodes.Replace(
            "three 127.0.0.1:7003@17003 master - 0 1 3 connected 10001-16383",
            "three 127.0.0.1:7003@17003 master - 0 1 3 connected 10001-16383 [5001-<-two]")
            .Replace(
                "two 127.0.0.1:7002@17002 master - 0 1 2 connected 5001-10000",
                "two 127.0.0.1:7002@17002 master - 0 1 2 connected 5001-10000 [5001->-three]");

        CacheClusterTopology.GetMasterIds(transientTopology).ShouldBe(
            new HashSet<string>(new[] { "one", "two", "three" }, StringComparer.Ordinal));
        Should.NotThrow(() => CacheClusterTopology.Validate(
            transientTopology, new[] { "one", "two", "three" }));
    }

    /// <summary>
    /// 验证主节点槽位存在缺口时报告诊断异常。
    /// </summary>
    [Fact]
    public void GetMasterIds_RejectsSlotGapWithDiagnostic()
    {
        var gap = Nodes.Replace("5001-10000", "5001-9999");

        var exception = Should.Throw<InvalidOperationException>(() => CacheClusterTopology.GetMasterIds(gap));

        exception.Message.ShouldContain("未覆盖全部槽位");
    }

    /// <summary>
    /// 验证主节点槽位重复时报告诊断异常。
    /// </summary>
    [Fact]
    public void GetMasterIds_RejectsDuplicateSlotWithDiagnostic()
    {
        var duplicate = Nodes.Replace("5001-10000", "5000-10000");

        var exception = Should.Throw<InvalidOperationException>(() => CacheClusterTopology.GetMasterIds(duplicate));

        exception.Message.ShouldContain("槽位归属重复");
    }

    /// <summary>
    /// 验证主节点连接断开时报告诊断异常。
    /// </summary>
    [Fact]
    public void GetMasterIds_RejectsDisconnectedMasterWithDiagnostic()
    {
        var disconnected = Nodes.Replace(
            "one 127.0.0.1:7001@17001 master - 0 1 1 connected 0-5000",
            "one 127.0.0.1:7001@17001 master - 0 1 1 disconnected 0-5000");

        var exception = Should.Throw<InvalidOperationException>(() => CacheClusterTopology.GetMasterIds(disconnected));

        exception.Message.ShouldContain("连接状态异常");
    }

    /// <summary>
    /// 验证本地 master 标识与节点行顺序无关。
    /// </summary>
    [Fact]
    public void GetLocalMasterId_ReadsMyselfMasterRegardlessOfRowOrder()
    {
        var localMaster = Nodes.Replace(
            "one 127.0.0.1:7001@17001 master - 0 1 1 connected 0-5000",
            "one 127.0.0.1:7001@17001 myself,master - 0 1 1 connected 0-5000");
        var reordered = string.Join("\n", localMaster.Split('\n').Reverse());

        CacheClusterTopology.GetLocalMasterId(localMaster).ShouldBe("one");
        CacheClusterTopology.GetLocalMasterId(reordered).ShouldBe("one");
    }

    /// <summary>
    /// 验证本地 master 缺失、重复、角色错误或断开时失败。
    /// </summary>
    [Fact]
    public void GetLocalMasterId_RejectsMissingDuplicateSlaveAndDisconnectedMyself()
    {
        var localMaster = Nodes.Replace(
            "one 127.0.0.1:7001@17001 master - 0 1 1 connected 0-5000",
            "one 127.0.0.1:7001@17001 myself,master - 0 1 1 connected 0-5000");
        var duplicate = localMaster.Replace(
            "two 127.0.0.1:7002@17002 master - 0 1 2 connected 5001-10000",
            "two 127.0.0.1:7002@17002 myself,master - 0 1 2 connected 5001-10000");
        var slave = localMaster.Replace(
            "one 127.0.0.1:7001@17001 myself,master - 0 1 1 connected 0-5000",
            "one 127.0.0.1:7001@17001 myself,slave - 0 1 1 connected 0-5000");
        var disconnected = localMaster.Replace(
            "one 127.0.0.1:7001@17001 myself,master - 0 1 1 connected 0-5000",
            "one 127.0.0.1:7001@17001 myself,master - 0 1 1 disconnected 0-5000");

        Should.Throw<InvalidOperationException>(() => CacheClusterTopology.GetLocalMasterId(Nodes));
        Should.Throw<InvalidOperationException>(() => CacheClusterTopology.GetLocalMasterId(duplicate));
        Should.Throw<InvalidOperationException>(() => CacheClusterTopology.GetLocalMasterId(slave));
        Should.Throw<InvalidOperationException>(() => CacheClusterTopology.GetLocalMasterId(disconnected));
    }

    /// <summary>
    /// 验证只返回在线主节点端点。
    /// </summary>
    [Fact]
    public void GetMasterEndpoints_ExcludesFailedMasterAndReplica()
    {
        CacheClusterTopology.GetMasterEndpoints(Nodes).ShouldBe(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "127.0.0.1:7001", "127.0.0.1:7002", "127.0.0.1:7003"
        }, ignoreOrder: true);
        Should.Throw<InvalidOperationException>(() => CacheClusterTopology.GetMasterEndpoints(
            Nodes.Replace("127.0.0.1:7003@17003", "127.0.0.1:7002@17002")));
    }

    /// <summary>
    /// 验证槽位迁移检测和稳定主节点视图保持语义。
    /// </summary>
    [Fact]
    public void SlotOwnershipSnapshot_DetectsSameMasterSetMigration()
    {
        var before = CacheClusterTopology.GetSlotOwnershipSnapshot(Nodes);
        var after = Nodes.Replace("0-5000", "0-4999")
            .Replace("5001-10000", "5000-10000");

        CacheClusterTopology.GetMasterIds(after).ShouldBe(
            CacheClusterTopology.GetMasterIds(Nodes), ignoreOrder: true);
        Should.Throw<InvalidOperationException>(() => CacheClusterTopology.EnsureUnchanged(
            before, CacheClusterTopology.GetSlotOwnershipSnapshot(after)));

        var reordered = string.Join("\n", Nodes.Split('\n').Reverse());
        Should.NotThrow(() => CacheClusterTopology.EnsureUnchanged(
            before, CacheClusterTopology.GetSlotOwnershipSnapshot(reordered)));

        var splitRange = Nodes.Replace("0-5000", "0-2499 2500-5000");
        Should.NotThrow(() => CacheClusterTopology.EnsureUnchanged(
            before, CacheClusterTopology.GetSlotOwnershipSnapshot(splitRange)));

        var migrating = Nodes.Replace("5001-10000", "5001-10000 [5001->-three]");
        Should.NotThrow(() => CacheClusterTopology.EnsureUnchanged(
            before, CacheClusterTopology.GetSlotOwnershipSnapshot(migrating)));
    }

    /// <summary>
    /// 验证不同主节点视图被拒绝且相同视图的顺序变化可接受。
    /// </summary>
    [Fact]
    public void EnsureSameView_RejectsDivergentMasterViews()
    {
        var expected = CacheClusterTopology.GetSlotOwnershipSnapshot(Nodes);
        var divergent = Nodes.Replace("0-5000", "0-4999")
            .Replace("5001-10000", "5000-10000");

        Should.Throw<InvalidOperationException>(() =>
                CacheClusterTopology.EnsureSameView(expected, divergent))
            .Message.ShouldContain("槽位视图不一致");
        Should.NotThrow(() => CacheClusterTopology.EnsureSameView(expected,
            string.Join("\n", Nodes.Split('\n').Reverse())));
    }

    /// <summary>
    /// 验证 Redis 哈希标签和已知 CRC 槽位计算。
    /// </summary>
    [Fact]
    public void GetHashSlot_UsesRedisHashTagAndKnownCrcValue()
    {
        CacheClusterTopology.GetHashSlot("123456789").ShouldBe(12739);
        CacheClusterTopology.GetHashSlot("prefix{shared}:one")
            .ShouldBe(CacheClusterTopology.GetHashSlot("other{shared}:two"));
        CacheClusterTopology.GetHashSlot("prefix{}:one")
            .ShouldBe(CacheClusterTopology.GetHashSlot("prefix{}:one"));
        Should.Throw<ArgumentNullException>(() => CacheClusterTopology.GetHashSlot(null));
    }

    /// <summary>
    /// 验证 UTF-8 哈希槽位计算兼容 Unicode 和无效 UTF-16。
    /// </summary>
    [Fact]
    public void GetHashSlot_MatchesUtf8ReferenceForUnicodeAndInvalidUtf16()
    {
        var values = new[]
        {
            "",
            "plain-key",
            "prefix{shared}:one",
            "prefix{}:one",
            "prefix{订单}:one",
            "prefix{😀}:one",
            "prefix{\ud800}:one",
            "prefix{\udc00}:one",
            "prefix{a\ud800b}:one",
            "\u00e9{100%}:\\path"
        };

        foreach (var value in values)
            CacheClusterTopology.GetHashSlot(value).ShouldBe(ReferenceHashSlot(value), value);
    }

    /// <summary>
    /// 按 Redis 哈希标签和 UTF-8 CRC 计算参考槽位。
    /// </summary>
    /// <param name="key">逻辑缓存键。</param>
    /// <returns>键对应的 Redis Cluster 槽位编号。</returns>
    private static int ReferenceHashSlot(string key)
    {
        var start = key.IndexOf('{');
        if (start >= 0)
        {
            var end = key.IndexOf('}', start + 1);
            if (end > start + 1)
                key = key.Substring(start + 1, end - start - 1);
        }

        var crc = 0;
        foreach (var value in Encoding.UTF8.GetBytes(key))
        {
            crc ^= value << 8;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1;
            crc &= 0xffff;
        }
        return crc % 16384;
    }
}

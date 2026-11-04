using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bing.Auditing;
using Bing.Domain.ChangeTracking;
using Bing.Domain.Entities;

/// <summary>补充原生运行中的忽略、恢复与审计时间预设场景。</summary>
public static class ReviewIgnoreScenarios
{
    /// <summary>运行忽略分支和审计时间采集的 NativeAOT 断言。</summary>
    public static void Run()
    {
        VerifyIgnoredSubtreesAndPayloads();
        VerifyAuditTimestampDefaults();
    }

    private static void VerifyIgnoredSubtreesAndPayloads()
    {
        IgnoredSubtreeRoot.ChildReads = 0;
        IgnoredSubtreeRoot.CollectionReads = 0;
        IgnoredSubtreeNode.KeyReads = 0;

        var beforeItems = new ReviewCountingEnumerable<IgnoredSubtreeNode>(new[] { new IgnoredSubtreeNode(1) });
        var afterItems = new ReviewCountingEnumerable<IgnoredSubtreeNode>(new[] { new IgnoredSubtreeNode(2) });
        var before = new IgnoredSubtreeRoot
        {
            Visible = "before",
            IgnoredChild = new IgnoredSubtreeNode(3),
            IgnoredCollection = beforeItems
        };
        var after = new IgnoredSubtreeRoot
        {
            Visible = "after",
            IgnoredChild = new IgnoredSubtreeNode(4),
            IgnoredCollection = afterItems
        };
        var tracker = new ChangeGraphTracker();

        var changes = tracker.Compare(before, after).Changes;
        Require(changes.Count == 1 && changes[0].Path.Value == nameof(IgnoredSubtreeRoot.Visible) &&
                changes[0].Kind == ChangeKind.Modified &&
                changes[0].OldValue.GetScalar<string>() == "before" &&
                changes[0].NewValue.GetScalar<string>() == "after",
            "忽略子对象与集合后仅输出可见字段的完整差异");

        var added = tracker.Compare<IgnoredSubtreeRoot>(null!, after).Changes;
        Require(added.Count == 1 && added[0].Path.Value == string.Empty && added[0].Kind == ChangeKind.Added &&
                added[0].NewValue.Members.Count == 1 &&
                added[0].NewValue.Members.TryGetValue(nameof(IgnoredSubtreeRoot.Visible), out var addedVisible) &&
                addedVisible.GetScalar<string>() == "after" &&
                !added[0].NewValue.Members.ContainsKey(nameof(IgnoredSubtreeRoot.IgnoredChild)) &&
                !added[0].NewValue.Members.ContainsKey(nameof(IgnoredSubtreeRoot.IgnoredCollection)),
            "新增对象载荷只包含可见字段且不泄漏忽略子树");

        var removed = tracker.Compare<IgnoredSubtreeRoot>(before, null!).Changes;
        Require(removed.Count == 1 && removed[0].Path.Value == string.Empty && removed[0].Kind == ChangeKind.Removed &&
                removed[0].OldValue.Members.Count == 1 &&
                removed[0].OldValue.Members.TryGetValue(nameof(IgnoredSubtreeRoot.Visible), out var removedVisible) &&
                removedVisible.GetScalar<string>() == "before" &&
                !removed[0].OldValue.Members.ContainsKey(nameof(IgnoredSubtreeRoot.IgnoredChild)) &&
                !removed[0].OldValue.Members.ContainsKey(nameof(IgnoredSubtreeRoot.IgnoredCollection)),
            "删除对象载荷只包含可见字段且不泄漏忽略子树");

        Require(IgnoredSubtreeRoot.ChildReads == 0 && IgnoredSubtreeRoot.CollectionReads == 0 &&
                beforeItems.Enumerations == 0 && afterItems.Enumerations == 0 && IgnoredSubtreeNode.KeyReads == 0,
            "忽略父关系时子 getter、集合枚举和元素键读取次数均为零");
    }

    private static void VerifyAuditTimestampDefaults()
    {
        var timestamp = new DateTime(2026, 1, 1);
        AuditTimestampDefaultGraph.Reads = 0;
        var before = new AuditTimestampDefaultGraph
        {
            Name = "same",
            CreationTime = timestamp,
            LastModificationTime = timestamp,
            DeletionTime = timestamp
        };
        var after = new AuditTimestampDefaultGraph
        {
            Name = "same",
            CreationTime = timestamp.AddDays(1),
            LastModificationTime = timestamp.AddDays(1),
            DeletionTime = timestamp.AddDays(1)
        };

        var defaultTracker = new ChangeGraphTracker();
        Require(defaultTracker.Compare(before, after).Changes.Count == 0,
            "三类审计时间接口默认忽略");
        var added = defaultTracker.Compare<AuditTimestampDefaultGraph>(null!, after).Changes;
        Require(added.Count == 1 && added[0].Kind == ChangeKind.Added && added[0].Path.Value == string.Empty &&
                added[0].NewValue.Members.Count == 1 &&
                added[0].NewValue.Members.TryGetValue(nameof(AuditTimestampDefaultGraph.Name), out var name) &&
                name.GetScalar<string>() == "same" &&
                !added[0].NewValue.Members.ContainsKey(nameof(AuditTimestampDefaultGraph.IsDeleted)) &&
                !added[0].NewValue.Members.ContainsKey(nameof(AuditTimestampDefaultGraph.CreationTime)) &&
                !added[0].NewValue.Members.ContainsKey(nameof(AuditTimestampDefaultGraph.LastModificationTime)) &&
                !added[0].NewValue.Members.ContainsKey(nameof(AuditTimestampDefaultGraph.DeletionTime)),
            "默认审计忽略同时应用于新增对象载荷");
        Require(AuditTimestampDefaultGraph.Reads == 0,
            "默认忽略三类审计时间时没有调用属性 getter");

    }

    private static void Require(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException("Smoke 检查失败: " + description);
        Console.WriteLine("PASS | " + description);
    }
}

public partial class IgnoredSubtreeRoot : DomainObjectBase<IgnoredSubtreeRoot>
{
    public static int ChildReads;
    public static int CollectionReads;

    public string Visible { get; set; } = string.Empty;

    [ChangeIgnore]
    public IgnoredSubtreeNode IgnoredChild
    {
        get
        {
            Interlocked.Increment(ref ChildReads);
            return _ignoredChild;
        }
        set => _ignoredChild = value;
    }

    [ChangeIgnore]
    public IEnumerable<IgnoredSubtreeNode> IgnoredCollection
    {
        get
        {
            Interlocked.Increment(ref CollectionReads);
            return _ignoredCollection;
        }
        set => _ignoredCollection = value;
    }

    private IgnoredSubtreeNode _ignoredChild = null!;
    private IEnumerable<IgnoredSubtreeNode> _ignoredCollection = Array.Empty<IgnoredSubtreeNode>();
}

public sealed class IgnoredSubtreeNode : IKey<int>
{
    public static int KeyReads;
    private readonly int _id;
    public IgnoredSubtreeNode(int id) => _id = id;

    public int Id
    {
        get
        {
            Interlocked.Increment(ref KeyReads);
            return _id;
        }
    }
}

public sealed class ReviewCountingEnumerable<T> : IEnumerable<T>
{
    private readonly IReadOnlyList<T> _items;
    private int _enumerations;
    public ReviewCountingEnumerable(IReadOnlyList<T> items) => _items = items;
    public int Enumerations => Volatile.Read(ref _enumerations);

    public IEnumerator<T> GetEnumerator()
    {
        Interlocked.Increment(ref _enumerations);
        return _items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public partial class AuditTimestampDefaultGraph : DomainObjectBase<AuditTimestampDefaultGraph>, IHasCreationTime, IHasModificationTime, IHasDeletionTime
{
    public static int Reads;
    public string Name { get; set; } = string.Empty;
    private DateTime? _creationTime;
    private DateTime? _lastModificationTime;
    private DateTime? _deletionTime;

    public DateTime? CreationTime { get => Read(_creationTime); set => _creationTime = value; }
    public DateTime? LastModificationTime { get => Read(_lastModificationTime); set => _lastModificationTime = value; }
    public DateTime? DeletionTime { get => Read(_deletionTime); set => _deletionTime = value; }

    public bool IsDeleted { get; set; }

    private static DateTime? Read(DateTime? value)
    {
        Interlocked.Increment(ref Reads);
        return value;
    }
}

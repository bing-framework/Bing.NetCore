using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bing.Auditing;
using Bing.Domain.Entities;
using Xunit;

namespace Bing.Domain.ChangeTracking;

/// <summary>
/// 验证生成器消费者中的忽略、恢复和继承规则。
/// </summary>
public sealed class ChangeGraphIgnoreRemediationTest
{
    /// <summary>
    /// 验证硬忽略的标量及子树既不读取也不进入载荷。
    /// </summary>
    [Fact]
    public void HardIgnoredScalarAndChildSubtrees_AreNeverReadOrReturnedInPayloads()
    {
        IgnoredScalarGraph.Reads = 0;
        IgnoredBranchGraph.ChildReads = 0;
        IgnoredBranchGraph.CollectionReads = 0;
        IgnoredBranchNode.KeyReads = 0;

        var scalarBefore = new IgnoredScalarGraph { Visible = "old", IgnoredValue = "secret-old" };
        var scalarAfter = new IgnoredScalarGraph { Visible = "new", IgnoredValue = "secret-new" };
        var scalarTracker = new ChangeGraphTracker();
        var scalarSnapshot = scalarTracker.Capture(scalarBefore);
        scalarBefore.Visible = "changed-after-capture";
        var scalarSnapshotChange = Assert.Single(scalarTracker.CompareSnapshot(scalarSnapshot, scalarBefore).Changes);
        Assert.Equal("Visible", scalarSnapshotChange.Path.Value);
        Assert.Equal("old", scalarSnapshotChange.OldValue.GetScalar<string>());
        Assert.Equal("changed-after-capture", scalarSnapshotChange.NewValue.GetScalar<string>());
        var scalarChange = Assert.Single(scalarTracker.Compare(scalarBefore, scalarAfter).Changes);
        Assert.Equal("Visible", scalarChange.Path.Value);
        Assert.DoesNotContain("IgnoredValue", Assert.Single(scalarTracker.Compare<IgnoredScalarGraph>(null, scalarAfter).Changes).NewValue.Members.Keys);
        Assert.DoesNotContain("IgnoredValue", Assert.Single(scalarTracker.Compare<IgnoredScalarGraph>(scalarBefore, null).Changes).OldValue.Members.Keys);
        Assert.Equal(0, IgnoredScalarGraph.Reads);

        var beforeItems = new CountingEnumerable<IgnoredBranchNode>(new[] { new IgnoredBranchNode { Id = 1 } });
        var afterItems = new CountingEnumerable<IgnoredBranchNode>(new[] { new IgnoredBranchNode { Id = 2 } });
        var branchBefore = new IgnoredBranchGraph
        {
            Visible = "same",
            IgnoredChild = new IgnoredBranchNode { Id = 1 },
            IgnoredCollection = beforeItems
        };
        var branchAfter = new IgnoredBranchGraph
        {
            Visible = "same",
            IgnoredChild = new IgnoredBranchNode { Id = 2 },
            IgnoredCollection = afterItems
        };
        var branchTracker = new ChangeGraphTracker();
        Assert.Empty(branchTracker.Compare(branchBefore, branchAfter).Changes);
        var branchSnapshot = branchTracker.Capture(branchAfter);
        branchAfter.Visible = "changed-after-capture";
        var branchSnapshotChanges = branchTracker.CompareSnapshot(branchSnapshot, branchAfter);
        Assert.Equal(new[] { "Visible" }, branchSnapshotChanges.Changes.Select(change => change.Path.Value));
        var addedPayload = Assert.Single(branchTracker.Compare<IgnoredBranchGraph>(null, branchAfter).Changes).NewValue;
        var removedPayload = Assert.Single(branchTracker.Compare<IgnoredBranchGraph>(branchBefore, null).Changes).OldValue;

        Assert.False(addedPayload.Members.ContainsKey("IgnoredChild"));
        Assert.False(removedPayload.Members.ContainsKey("IgnoredChild"));
        Assert.Equal(0, IgnoredBranchGraph.ChildReads);
        Assert.Equal(0, IgnoredBranchGraph.CollectionReads);
        Assert.Equal(0, beforeItems.Enumerations);
        Assert.Equal(0, afterItems.Enumerations);
        Assert.Equal(0, IgnoredBranchNode.KeyReads);
    }

    /// <summary>
    /// 验证嵌套集合及字典条目自身忽略规则同时作用于差异和新增载荷。
    /// </summary>
    [Fact]
    public void NestedCollectionAndDictionaryEntries_OmitLocallyIgnoredMember()
    {
        var before = new NestedIgnorePathsGraph
        {
            Groups = new List<List<NestedIgnorePathNode>>
            {
                new List<NestedIgnorePathNode> { new NestedIgnorePathNode { Name = "old", Secret = "old-secret" } }
            },
            Map = new Dictionary<string, List<NestedIgnorePathNode>>
            {
                ["main"] = new List<NestedIgnorePathNode> { new NestedIgnorePathNode { Name = "old-map", Secret = "old-secret" } }
            }
        };
        var after = new NestedIgnorePathsGraph
        {
            Groups = new List<List<NestedIgnorePathNode>>
            {
                new List<NestedIgnorePathNode> { new NestedIgnorePathNode { Name = "new", Secret = "new-secret" } }
            },
            Map = new Dictionary<string, List<NestedIgnorePathNode>>
            {
                ["main"] = new List<NestedIgnorePathNode> { new NestedIgnorePathNode { Name = "new-map", Secret = "new-secret" } }
            }
        };
        var tracker = new ChangeGraphTracker();

        var modified = tracker.Compare(before, after);
        Assert.Equal(new[] { "Groups[0][0].Name", "Map{main}[0].Name" }, modified.Changes.Select(change => change.Path.Value));
        Assert.All(modified.Changes, change => Assert.Equal(ChangeKind.Modified, change.Kind));

        var added = tracker.Compare(new NestedIgnorePathsGraph
        {
            Groups = new List<List<NestedIgnorePathNode>>(),
            Map = new Dictionary<string, List<NestedIgnorePathNode>>()
        }, after);
        var groupPayload = Assert.Single(added.Changes, change => change.Path.Value == "Groups[0]").NewValue;
        var groupNode = Assert.Single(groupPayload.Items).Value;
        Assert.False(groupNode.Members.ContainsKey("Secret"));
        var mapPayload = Assert.Single(added.Changes, change => change.Path.Value == "Map{main}").NewValue;
        var mapNode = Assert.Single(mapPayload.Items).Value;
        Assert.False(mapNode.Members.ContainsKey("Secret"));

        var removed = tracker.Compare(after, new NestedIgnorePathsGraph
        {
            Groups = new List<List<NestedIgnorePathNode>>(),
            Map = new Dictionary<string, List<NestedIgnorePathNode>>()
        });
        var removedGroupPayload = Assert.Single(removed.Changes, change => change.Path.Value == "Groups[0]").OldValue;
        Assert.False(Assert.Single(removedGroupPayload.Items).Value.Members.ContainsKey("Secret"));
        var removedMapPayload = Assert.Single(removed.Changes, change => change.Path.Value == "Map{main}").OldValue;
        Assert.False(Assert.Single(removedMapPayload.Items).Value.Members.ContainsKey("Secret"));
    }

    /// <summary>
    /// 验证节点局部忽略规则作用于对象及各类容器条目。
    /// </summary>
    [Fact]
    public void EntityLocalIgnore_AppliesToObjectListDictionaryAndNestedCollectionEntries()
    {
        var before = BuildLocalIgnoreRoot("old", "old-secret");
        var after = BuildLocalIgnoreRoot("new", "new-secret");
        var tracker = new ChangeGraphTracker();

        var changed = tracker.Compare(before, after);

        Assert.Equal(new[]
        {
            "Child.Name", "Items[0].Name", "Map{main}.Name", "Nested[0][0].Name"
        }, changed.Changes.Select(change => change.Path.Value));
        Assert.All(changed.Changes, change => Assert.Equal(ChangeKind.Modified, change.Kind));

        var added = tracker.Compare<LocalIgnoreGraph>(null, after);
        AssertAllValuesOmitMember(Assert.Single(added.Changes).NewValue, "Secret");
        var removed = tracker.Compare<LocalIgnoreGraph>(before, null);
        AssertAllValuesOmitMember(Assert.Single(removed.Changes).OldValue, "Secret");
    }

    /// <summary>
    /// 验证重写成员继承基类忽略规则，隐藏成员独立参与比较。
    /// </summary>
    [Fact]
    public void OverrideAndHiddenProperties_ResolveIgnoreRulesBySymbol()
    {
        var before = new InheritedIgnoreGraph
        {
            Override = new OverrideIgnoreNode { Value = "old-override" },
            Hidden = new HiddenIgnoreNode { Value = "old-hidden" },
            Inherited = new InheritedIgnoreNode { Value = "old-inherited" }
        };
        var after = new InheritedIgnoreGraph
        {
            Override = new OverrideIgnoreNode { Value = "new-override" },
            Hidden = new HiddenIgnoreNode { Value = "new-hidden" },
            Inherited = new InheritedIgnoreNode { Value = "new-inherited" }
        };

        var result = new ChangeGraphTracker().Compare(before, after);

        Assert.Equal(new[] { "Hidden.Value" }, result.Changes.Select(change => change.Path.Value));
        Assert.Equal(new[] { "new-hidden" }, result.Changes.Select(change => change.NewValue.GetScalar<string>()));
    }

    /// <summary>
    /// 创建包含局部忽略成员的测试对象图。
    /// </summary>
    /// <param name="name">测试对象的名称属性值。</param>
    /// <param name="secret">应被局部忽略规则排除的属性值。</param>
    /// <returns>包含指定普通及忽略成员值的对象图。</returns>
    private static LocalIgnoreGraph BuildLocalIgnoreRoot(string name, string secret) => new LocalIgnoreGraph
    {
        Child = new LocalIgnoreNode { Name = name, Secret = secret },
        Items = new List<LocalIgnoreNode> { new LocalIgnoreNode { Name = name, Secret = secret } },
        Map = new Dictionary<string, LocalIgnoreNode> { ["main"] = new LocalIgnoreNode { Name = name, Secret = secret } },
        Nested = new List<List<LocalIgnoreNode>>
        {
            new List<LocalIgnoreNode> { new LocalIgnoreNode { Name = name, Secret = secret } }
        }
    };

    /// <summary>
    /// 递归验证变更载荷不包含指定成员。
    /// </summary>
    /// <param name="value">需要递归校验的变更载荷。</param>
    /// <param name="member">不应出现在载荷中的成员名称。</param>
    private static void AssertAllValuesOmitMember(ChangeValue value, string member)
    {
        Assert.NotNull(value);
        Assert.DoesNotContain(member, value.Members?.Keys ?? Array.Empty<string>());
        foreach (var child in value.Members?.Values ?? Array.Empty<ChangeValue>())
            AssertAllValuesOmitMember(child, member);
        foreach (var item in value.Items ?? Array.Empty<ChangeValueItem>())
            AssertAllValuesOmitMember(item.Value, member);
    }

    /// <summary>
    /// 统计枚举次数的可枚举集合包装器。
    /// </summary>
    /// <typeparam name="T">枚举序列中的元素类型。</typeparam>
    private sealed class CountingEnumerable<T> : IEnumerable<T>
    {
        /// <summary>
        /// 保存计数枚举器包装的元素列表。
        /// </summary>
        private readonly IReadOnlyList<T> _items;

        /// <summary>
        /// 初始化一个 <see cref="CountingEnumerable{T}" /> 类型的实例。
        /// </summary>
        /// <param name="items">当前实例处理或保存的项集合。</param>
        public CountingEnumerable(IEnumerable<T> items) => _items = items.ToList();

        /// <summary>
        /// 获取枚举序列的累计次数。
        /// </summary>
        public int Enumerations { get; private set; }

        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator()
        {
            Enumerations++;
            return _items.GetEnumerator();
        }

        /// <inheritdoc />
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

/// <summary>
/// 验证忽略标量属性的对象图模型。
/// </summary>
public partial class IgnoredScalarGraph : DomainObjectBase<IgnoredScalarGraph>
{
    /// <summary>
    /// 记录标量图属性的读取次数。
    /// </summary>
    public static int Reads;

    /// <summary>
    /// 获取或设置参与变更捕获的可见值。
    /// </summary>
    public string Visible { get; set; }

    /// <summary>
    /// 获取或设置应被忽略的标量值。
    /// </summary>
    /// <remarks>读取会记录访问次数并引发异常，设置操作不保存值。</remarks>
    [ChangeIgnore]
    public string IgnoredValue
    {
        get
        {
            Interlocked.Increment(ref Reads);
            throw new InvalidOperationException("Ignored scalar getter was executed.");
        }
        set { }
    }
}

/// <summary>
/// 验证忽略对象与集合分支的对象图模型。
/// </summary>
public partial class IgnoredBranchGraph : DomainObjectBase<IgnoredBranchGraph>
{
    /// <summary>
    /// 保存被忽略的集合成员。
    /// </summary>
    private IEnumerable<IgnoredBranchNode> _ignoredCollection;
    /// <summary>
    /// 保存被忽略的子节点成员。
    /// </summary>
    private IgnoredBranchNode _ignoredChild;

    /// <summary>
    /// 记录子节点成员的读取次数。
    /// </summary>
    public static int ChildReads;
    /// <summary>
    /// 记录集合成员的读取次数。
    /// </summary>
    public static int CollectionReads;
    /// <summary>
    /// 获取或设置参与变更捕获的可见值。
    /// </summary>
    public string Visible { get; set; }

    /// <summary>
    /// 获取或设置应被忽略的子节点。
    /// </summary>
    /// <remarks>读取会递增子节点访问计数。</remarks>
    [ChangeIgnore]
    public IgnoredBranchNode IgnoredChild
    {
        get
        {
            Interlocked.Increment(ref ChildReads);
            return _ignoredChild;
        }
        set => _ignoredChild = value;
    }

    /// <summary>
    /// 获取或设置应被忽略的节点集合。
    /// </summary>
    /// <remarks>读取会递增集合访问计数。</remarks>
    [ChangeIgnore]
    public IEnumerable<IgnoredBranchNode> IgnoredCollection
    {
        get
        {
            Interlocked.Increment(ref CollectionReads);
            return _ignoredCollection;
        }
        set => _ignoredCollection = value;
    }
}

/// <summary>
/// 记录主键读取行为的忽略分支节点。
/// </summary>

public sealed class IgnoredBranchNode : IKey<int>
{
    /// <summary>
    /// 保存被忽略分支节点的标识。
    /// </summary>
    private int _id;

    /// <summary>
    /// 记录节点键属性的读取次数。
    /// </summary>
    public static int KeyReads;
    /// <inheritdoc />
    /// <remarks>读取会递增主键访问计数。</remarks>
    public int Id
    {
        get
        {
            Interlocked.Increment(ref KeyReads);
            return _id;
        }
        set => _id = value;
    }
}

/// <summary>
/// 验证嵌套集合路径忽略规则的对象图模型。
/// </summary>
public partial class NestedIgnorePathsGraph : DomainObjectBase<NestedIgnorePathsGraph>
{
    /// <summary>
    /// 获取或设置嵌套节点分组。
    /// </summary>
    public List<List<NestedIgnorePathNode>> Groups { get; set; }
    /// <summary>
    /// 获取或设置按键索引的节点字典。
    /// </summary>
    public Dictionary<string, List<NestedIgnorePathNode>> Map { get; set; }
}

/// <summary>
/// 包含路径忽略目标的嵌套节点。
/// </summary>
public sealed class NestedIgnorePathNode
{
    /// <summary>
    /// 获取或设置名称。
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// 获取或设置用于忽略规则测试的私密值。
    /// </summary>
    [ChangeIgnore]
    public string Secret { get; set; }
}

/// <summary>
/// 验证节点局部忽略规则的对象图模型。
/// </summary>
public partial class LocalIgnoreGraph : DomainObjectBase<LocalIgnoreGraph>
{
    /// <summary>
    /// 获取或设置子节点。
    /// </summary>
    public LocalIgnoreNode Child { get; set; }
    /// <summary>
    /// 获取或设置节点集合。
    /// </summary>
    public List<LocalIgnoreNode> Items { get; set; }
    /// <summary>
    /// 获取或设置按键索引的节点字典。
    /// </summary>
    public Dictionary<string, LocalIgnoreNode> Map { get; set; }
    /// <summary>
    /// 获取或设置嵌套节点集合。
    /// </summary>
    public List<List<LocalIgnoreNode>> Nested { get; set; }
}

/// <summary>
/// 通过成员特性忽略私密值的节点。
/// </summary>

public sealed class LocalIgnoreNode
{
    /// <summary>
    /// 获取或设置名称。
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 获取或设置用于忽略规则测试的私密值。
    /// </summary>
    [ChangeIgnore]
    public string Secret { get; set; }
}

/// <summary>
/// 声明可重写忽略属性的节点基类。
/// </summary>

public class BaseIgnoreNode
{
    /// <summary>
    /// 获取或设置成员值。
    /// </summary>
    [ChangeIgnore]
    public virtual string Value { get; set; }
}

/// <summary>
/// 重新纳入基类忽略属性的重写节点。
/// </summary>

public sealed class OverrideIgnoreNode : BaseIgnoreNode
{
    /// <inheritdoc />
    public override string Value { get; set; }
}

/// <summary>
/// 隐藏基类忽略属性的节点。
/// </summary>

public sealed class HiddenIgnoreNode : BaseIgnoreNode
{
    /// <summary>
    /// 获取或设置成员值。
    /// </summary>
    public new string Value { get; set; }
}

/// <summary>
/// 继承基类忽略规则的节点。
/// </summary>

public sealed class InheritedIgnoreNode : BaseIgnoreNode
{
}

/// <summary>
/// 验证重写、隐藏及继承忽略规则的对象图模型。
/// </summary>
public partial class InheritedIgnoreGraph : DomainObjectBase<InheritedIgnoreGraph>
{
    /// <summary>
    /// 获取或设置重写基类属性的节点。
    /// </summary>
    public OverrideIgnoreNode Override { get; set; }
    /// <summary>
    /// 获取或设置隐藏基类属性的节点。
    /// </summary>
    public HiddenIgnoreNode Hidden { get; set; }
    /// <summary>
    /// 获取或设置继承基类属性的节点。
    /// </summary>
    public InheritedIgnoreNode Inherited { get; set; }
}

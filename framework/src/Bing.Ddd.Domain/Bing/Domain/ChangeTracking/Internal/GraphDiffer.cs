using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.Domain.ChangeTracking;

/// <summary>
/// 比较变更图快照并生成结构化差异的组件。
/// </summary>
internal sealed class GraphDiffer
{
    /// <summary>
    /// 保存图比较共享的遍历预算。
    /// </summary>
    private readonly GraphTraversalBudget _budget;
    /// <summary>
    /// 指示是否忽略调用方标量/键比较器，使用领域默认相等语义。
    /// </summary>
    private readonly bool _ignoreOverrides;
    /// <summary>
    /// 保存当前图比较递归深度。
    /// </summary>
    private int _depth;
    /// <summary>
    /// 保存正在比较的容器对，避免循环引用导致无限递归。
    /// </summary>
    private readonly HashSet<Tuple<GraphValue, GraphValue>> _activeContainers = new HashSet<Tuple<GraphValue, GraphValue>>();
    /// <summary>
    /// 保存正在比较的公共容器。
    /// </summary>
    private readonly HashSet<GraphValue> _activePublicContainers = new HashSet<GraphValue>();
    /// <summary>
    /// 初始化一个 <see cref="GraphDiffer" /> 类型的实例。
    /// </summary>
    /// <param name="options">用于初始化实例的配置选项。</param>
    /// <param name="budget">限制对象图遍历范围的预算。</param>
    private GraphDiffer(ChangeGraphOptions options, GraphTraversalBudget budget, bool ignoreOverrides)
    {
        _budget = budget ?? new GraphTraversalBudget(options);
        _ignoreOverrides = ignoreOverrides;
    }

    /// <summary>
    /// 比较两个内部图值并生成变更结果。
    /// </summary>
    /// <param name="before">比较前的内部图值。</param>
    /// <param name="after">比较后的内部图值。</param>
    /// <param name="options">控制比较规则的选项。</param>
    /// <param name="budget">可选的图遍历预算；为 null 时使用默认预算。</param>
    /// <returns>按配置生成的变更结果。</returns>
    internal static ChangeGraphResult Diff(GraphValue before, GraphValue after, ChangeGraphOptions options,
        GraphTraversalBudget budget = null, bool ignoreOverrides = false) =>
        new GraphDiffer(options, budget, ignoreOverrides).Run(before, after, options);

    /// <summary>
    /// 遍历并排序图值差异以生成变更结果。
    /// </summary>
    /// <param name="before">比较前的内部图值。</param>
    /// <param name="after">比较后的内部图值。</param>
    /// <param name="options">控制比较规则的选项。</param>
    /// <returns>按路径及变更类型排序的只读变更项集合。</returns>
    private ChangeGraphResult Run(GraphValue before, GraphValue after, ChangeGraphOptions options)
    {
        var changes = new List<ChangeEntry>();
        DiffValue(before, after, new ChangePath(string.Empty), null, changes, new HashSet<GraphNodePair>(), options, _ignoreOverrides);
        changes.Sort((x, y) =>
        {
            var path = x.Path.CompareTo(y.Path);
            return path != 0 ? path : x.Kind.CompareTo(y.Kind);
        });
        return new ChangeGraphResult(changes.AsReadOnly());
    }

    /// <summary>
    /// 比较图值并将发现的差异加入变更集合。
    /// </summary>
    /// <param name="before">比较前的图值。</param>
    /// <param name="after">比较后的图值。</param>
    /// <param name="path">当前比较路径。</param>
    /// <param name="description">当前成员说明。</param>
    /// <param name="changes">接收差异项的集合。</param>
    /// <param name="active">当前递归路径上的对象节点对集合。</param>
    /// <param name="options">控制相等和键比较规则的选项。</param>
    private void DiffValue(GraphValue before, GraphValue after, ChangePath path, string description,
        ICollection<ChangeEntry> changes, ISet<GraphNodePair> active, ChangeGraphOptions options,
        bool domainRules)
    {
        _budget.Visit(path.Value, _depth);
        _depth++;
        try
        {
            if (before == null || before.Kind == ChangeValueKind.Null)
            {
                if (after == null || after.Kind == ChangeValueKind.Null)
                    return;
                changes.Add(new ChangeEntry(path, ChangeKind.Added, description, ToPublic(before), ToPublic(after)));
                return;
            }
            if (after == null || after.Kind == ChangeValueKind.Null)
            {
                changes.Add(new ChangeEntry(path, ChangeKind.Removed, description, ToPublic(before), ToPublic(after)));
                return;
            }
            if (before.Kind == ChangeValueKind.Scalar && after.Kind == ChangeValueKind.Scalar)
            {
                var equal = domainRules
                    ? Equals(before.ScalarValue, after.ScalarValue)
                    : options.ScalarsEqual(before.ScalarValue, after.ScalarValue);
                if (before.ScalarValue == null || after.ScalarValue == null ||
                    before.ScalarValue.GetType() != after.ScalarValue.GetType())
                {
                    if (!equal)
                        changes.Add(new ChangeEntry(path, ChangeKind.Replaced, description, ToPublic(before), ToPublic(after)));
                }
                else if (!equal)
                    changes.Add(new ChangeEntry(path, ChangeKind.Modified, description, ToPublic(before), ToPublic(after)));
                return;
            }
            if (before.Kind != after.Kind)
            {
                changes.Add(new ChangeEntry(path, ChangeKind.Replaced, description, ToPublic(before), ToPublic(after)));
                return;
            }
            if (before.Kind == ChangeValueKind.Object)
            {
                // 领域实体节点使用实体规则；普通结构节点继续使用其配置的 comparer。
                var nodeDomainRules = before.Node.IsDomainEntity;
                if (!string.Equals(before.Node.ModelId, after.Node.ModelId, StringComparison.Ordinal) ||
                    (before.Node.Key == null) != (after.Node.Key == null) ||
                    (before.Node.Key != null && !options.KeyComparer(false, nodeDomainRules).Equals(before.Node.Key, after.Node.Key)))
                {
                    changes.Add(new ChangeEntry(path, ChangeKind.Replaced, description, ToPublic(before), ToPublic(after)));
                    return;
                }
                var pair = new GraphNodePair(before.Node, after.Node);
                if (!active.Add(pair))
                    return;
                var names = new SortedSet<string>(before.Node.Members.Keys, StringComparer.Ordinal);
                names.UnionWith(after.Node.Members.Keys);
                foreach (var name in names)
                {
                    before.Node.Members.TryGetValue(name, out var oldMember);
                    after.Node.Members.TryGetValue(name, out var newMember);
                    var childPath = path.Member(name);
                    if (oldMember == null)
                    {
                        changes.Add(new ChangeEntry(childPath, ChangeKind.Added, newMember.Description,
                            ToPublic(null), ToPublic(newMember.Value)));
                    }
                    else if (newMember == null)
                    {
                        changes.Add(new ChangeEntry(childPath, ChangeKind.Removed, oldMember.Description,
                            ToPublic(oldMember.Value), ToPublic(null)));
                    }
                    else
                    {
                        DiffValue(oldMember.Value, newMember.Value, childPath, newMember.Description ?? oldMember.Description, changes, active, options, nodeDomainRules);
                    }
                }
                active.Remove(pair);
                return;
            }
            if (before.Kind == ChangeValueKind.Collection || before.Kind == ChangeValueKind.Set || before.Kind == ChangeValueKind.Dictionary)
            {
                var containers = Tuple.Create(before, after);
                if (!_activeContainers.Add(containers)) return;
                try
                {
                    if (before.Kind == ChangeValueKind.Dictionary)
                        DiffDictionary(before, after, path, description, changes, active, options, domainRules);
                    else
                        DiffCollection(before, after, path, description, changes, active, options, domainRules);
                }
                finally { _activeContainers.Remove(containers); }
                return;
            }
            changes.Add(new ChangeEntry(path, ChangeKind.Replaced, description, ToPublic(before), ToPublic(after)));
        }
        finally { _depth--; }
    }

    /// <summary>
    /// 比较两个序列或集合并记录项的增删改动。
    /// </summary>
    /// <param name="before">比较前的集合图值。</param>
    /// <param name="after">比较后的集合图值。</param>
    /// <param name="path">当前集合路径。</param>
    /// <param name="description">集合成员说明。</param>
    /// <param name="changes">接收差异项的集合。</param>
    /// <param name="active">当前递归路径上的对象节点对集合。</param>
    /// <param name="options">控制键比较和移动跟踪的选项。</param>
    private void DiffCollection(GraphValue before, GraphValue after, ChangePath path, string description,
        ICollection<ChangeEntry> changes, ISet<GraphNodePair> active, ChangeGraphOptions options,
        bool domainRules)
    {
        if (before.Keyed != after.Keyed)
        {
            changes.Add(new ChangeEntry(path, ChangeKind.Replaced, description, ToPublic(before), ToPublic(after)));
            return;
        }
        if (!before.Keyed)
        {
            var length = Math.Min(before.Items.Count, after.Items.Count);
            for (var i = 0; i < length; i++)
                DiffValue(before.Items[i].Value, after.Items[i].Value, path.At(i), description, changes, active, options, domainRules);
            for (var i = length; i < before.Items.Count; i++)
                changes.Add(new ChangeEntry(path.At(i), ChangeKind.Removed, description,
                    ToPublic(before.Items[i].Value), ToPublic(null), i, null));
            for (var i = length; i < after.Items.Count; i++)
                changes.Add(new ChangeEntry(path.At(i), ChangeKind.Added, description,
                    ToPublic(null), ToPublic(after.Items[i].Value), null, i));
            return;
        }
        // 进入领域实体集合时恢复实体自身身份语义；普通结构集合保留自身 comparer。
        var ignoreKeyOverrides = domainRules;
        foreach (var candidate in before.Items)
        {
            if (candidate.Value?.Node != null && candidate.Value.Node.IsDomainEntity)
            {
                ignoreKeyOverrides = true;
                break;
            }
        }
        var oldMap = before.Items.ToDictionary(x => x.Key, x => x, options.KeyComparer(before.Kind == ChangeValueKind.Set, ignoreKeyOverrides));
        var newMap = after.Items.ToDictionary(x => x.Key, x => x, options.KeyComparer(after.Kind == ChangeValueKind.Set, ignoreKeyOverrides));
        foreach (var oldItem in before.Items)
        {
            if (!newMap.ContainsKey(oldItem.Key))
                changes.Add(new ChangeEntry(path.ForKey(oldItem.Key), ChangeKind.Removed,
                    description, ToPublic(oldItem.Value), ToPublic(null), oldItem.Index, null));
        }
        foreach (var newItem in after.Items)
        {
            if (!oldMap.TryGetValue(newItem.Key, out var oldItem))
            {
                changes.Add(new ChangeEntry(path.ForKey(newItem.Key), ChangeKind.Added,
                    description, ToPublic(null), ToPublic(newItem.Value), null, newItem.Index));
                continue;
            }
            // 标量 Set 的值就是身份，等价身份没有第二份独立的可修改载荷。
            if (before.Kind != ChangeValueKind.Set || oldItem.Value.Kind != ChangeValueKind.Scalar || newItem.Value.Kind != ChangeValueKind.Scalar)
                DiffValue(oldItem.Value, newItem.Value, path.ForKey(newItem.Key), description, changes, active, options, domainRules);
        }
        if (before.Kind == ChangeValueKind.Collection && options.TrackMoves)
            AddMovedEntries(before, after, oldMap, newMap, path, description, changes);
    }

    /// <summary>
    /// 根据相同键项的索引变化记录集合移动项。
    /// </summary>
    /// <param name="before">比较前的集合图值。</param>
    /// <param name="after">比较后的集合图值。</param>
    /// <param name="oldMap">比较前按键索引的集合项。</param>
    /// <param name="newMap">比较后按键索引的集合项。</param>
    /// <param name="path">当前集合路径。</param>
    /// <param name="description">集合成员说明。</param>
    /// <param name="changes">接收差异项的集合。</param>
    private void AddMovedEntries(GraphValue before, GraphValue after,
        IDictionary<object, GraphCollectionItem> oldMap, IDictionary<object, GraphCollectionItem> newMap,
        ChangePath path, string description, ICollection<ChangeEntry> changes)
    {
        var common = after.Items.Where(item => oldMap.ContainsKey(item.Key)).ToList();
        if (common.Count < 2)
            return;
        var oldIndexes = common.Select(item => oldMap[item.Key].Index).ToList();
        var tails = new List<int>();
        var predecessors = Enumerable.Repeat(-1, common.Count).ToArray();
        for (var index = 0; index < oldIndexes.Count; index++)
        {
            var low = 0;
            var high = tails.Count;
            while (low < high)
            {
                var middle = low + ((high - low) / 2);
                if (oldIndexes[tails[middle]] < oldIndexes[index])
                    low = middle + 1;
                else
                    high = middle;
            }
            if (low > 0)
                predecessors[index] = tails[low - 1];
            if (low == tails.Count)
                tails.Add(index);
            else
                tails[low] = index;
        }

        var anchors = new HashSet<int>();
        var cursor = tails.Count == 0 ? -1 : tails[tails.Count - 1];
        while (cursor >= 0)
        {
            anchors.Add(cursor);
            cursor = predecessors[cursor];
        }
        for (var index = 0; index < common.Count; index++)
        {
            if (anchors.Contains(index))
                continue;
            var item = common[index];
            var oldItem = oldMap[item.Key];
            if (oldItem.Index == item.Index)
                continue;
            changes.Add(new ChangeEntry(
                path.ForKey(item.Key),
                ChangeKind.Moved,
                description,
                ToPublic(oldItem.Value),
                ToPublic(item.Value),
                oldItem.Index,
                item.Index));
        }
    }

    /// <summary>
    /// 比较两个字典并记录键值项的增删和修改。
    /// </summary>
    /// <param name="before">比较前的字典图值。</param>
    /// <param name="after">比较后的字典图值。</param>
    /// <param name="path">当前字典路径。</param>
    /// <param name="description">字典成员说明。</param>
    /// <param name="changes">接收差异项的集合。</param>
    /// <param name="active">当前递归路径上的对象节点对集合。</param>
    /// <param name="options">控制键比较规则的选项。</param>
    private void DiffDictionary(GraphValue before, GraphValue after, ChangePath path, string description,
        ICollection<ChangeEntry> changes, ISet<GraphNodePair> active, ChangeGraphOptions options,
        bool domainRules)
    {
        var oldMap = before.Items.ToDictionary(x => x.Key, x => x, options.KeyComparer(before.Kind == ChangeValueKind.Set, domainRules));
        var newMap = after.Items.ToDictionary(x => x.Key, x => x, options.KeyComparer(after.Kind == ChangeValueKind.Set, domainRules));
        foreach (var oldItem in before.Items)
        {
            if (!newMap.ContainsKey(oldItem.Key))
                changes.Add(new ChangeEntry(path.ForKey(oldItem.Key, true), ChangeKind.Removed,
                    description, ToPublic(oldItem.Value), ToPublic(null), oldItem.Index, null));
        }
        foreach (var newItem in after.Items)
        {
            var itemPath = path.ForKey(newItem.Key, true);
            if (!oldMap.TryGetValue(newItem.Key, out var oldItem))
            {
                changes.Add(new ChangeEntry(itemPath, ChangeKind.Added, description,
                    ToPublic(null), ToPublic(newItem.Value), null, newItem.Index));
                continue;
            }
            DiffValue(oldItem.Value, newItem.Value, itemPath, description, changes, active, options, domainRules);
        }
    }

    /// <summary>
    /// 将内部图值转换为公开变更值。
    /// </summary>
    /// <param name="value">要转换的内部图值。</param>
    /// <returns>转换得到的公开变更值；value 为 null 时表示缺失值。</returns>
    private ChangeValue ToPublic(GraphValue value)
    {
        if (value == null)
            return new ChangeValue(ChangeValueKind.Missing, null);
        return ToPublic(value, new HashSet<GraphNode>());
    }

    /// <summary>
    /// 将内部图值转换为公开变更值。
    /// </summary>
    /// <param name="value">要转换的内部图值。</param>
    /// <param name="active">当前递归路径上的对象节点集合。</param>
    /// <returns>转换得到的公开变更值；循环对象以引用值表示。</returns>
    private ChangeValue ToPublic(GraphValue value, ISet<GraphNode> active)
    {
        _budget.Visit("$payload", _depth);
        _depth++;
        try
        {
            if (value == null)
                return new ChangeValue(ChangeValueKind.Missing, null);
            if (value.Kind == ChangeValueKind.Scalar || value.Kind == ChangeValueKind.Null)
                return new ChangeValue(value.Kind, value.Kind == ChangeValueKind.Scalar ? value.ScalarValue : null);
            if (value.Kind == ChangeValueKind.Object)
            {
                if (!active.Add(value.Node))
                {
                    var reference = new Dictionary<string, ChangeValue>(StringComparer.Ordinal)
                    {
                        ["$ref"] = new ChangeValue(ChangeValueKind.Scalar, value.Node.NodeId)
                    };
                    return new ChangeValue(ChangeValueKind.Object,
                        new System.Collections.ObjectModel.ReadOnlyDictionary<string, ChangeValue>(reference), referenceId: value.Node.NodeId);
                }
                try
                {
                    var members = new Dictionary<string, ChangeValue>(StringComparer.Ordinal);
                    foreach (var member in value.Node.Members)
                        members[member.Key] = ToPublic(member.Value.Value, active);
                    return new ChangeValue(ChangeValueKind.Object,
                        new System.Collections.ObjectModel.ReadOnlyDictionary<string, ChangeValue>(members), nodeId: value.Node.NodeId);
                }
                finally
                {
                    active.Remove(value.Node);
                }
            }
            if (!_activePublicContainers.Add(value))
                return new ChangeValue(value.Kind, Array.AsReadOnly(Array.Empty<ChangeValueItem>()), referenceId: value.NodeId);
            try
            {
                var items = new List<ChangeValueItem>();
                foreach (var item in value.Items ?? Array.Empty<GraphCollectionItem>())
                    items.Add(new ChangeValueItem(item.Key, item.Index, ToPublic(item.Value, active)));
                return new ChangeValue(value.Kind,
                    new System.Collections.ObjectModel.ReadOnlyCollection<ChangeValueItem>(items), nodeId: value.NodeId);
            }
            finally { _activePublicContainers.Remove(value); }
        }
        finally { _depth--; }
    }
}

/// <summary>
/// 保存变更图中待比较的节点对。
/// </summary>
internal struct GraphNodePair : IEquatable<GraphNodePair>
{
    /// <summary>
    /// 保存比较中的旧节点。
    /// </summary>
    private readonly GraphNode _old;
    /// <summary>
    /// 保存比较中的新节点。
    /// </summary>
    private readonly GraphNode _new;

    /// <summary>
    /// 初始化一个 <see cref="GraphNodePair" /> 类型的实例。
    /// </summary>
    /// <param name="oldNode">对象图差异比较前的节点。</param>
    /// <param name="newNode">对象图差异比较后的节点。</param>
    internal GraphNodePair(GraphNode oldNode, GraphNode newNode)
    {
        _old = oldNode;
        _new = newNode;
    }

    /// <inheritdoc />
    public bool Equals(GraphNodePair other) => ReferenceEquals(_old, other._old) && ReferenceEquals(_new, other._new);
    /// <inheritdoc />
    public override bool Equals(object obj) => obj is GraphNodePair other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() => (System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_old) * 397) ^ System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_new);
}

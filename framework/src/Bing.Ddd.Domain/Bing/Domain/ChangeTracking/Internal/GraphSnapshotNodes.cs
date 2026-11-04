using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.Domain.ChangeTracking;

/// <summary>
/// 对象图捕获产生的冻结节点。
/// </summary>
internal sealed class GraphNode
{
    /// <summary>
    /// 初始化一个 <see cref="GraphNode" /> 类型的实例。
    /// </summary>
    /// <param name="modelId">用于区分对象图模型定义的标识。</param>
    /// <param name="key">对象在容器内的键。</param>
    /// <param name="nodeId">当前节点的标识。</param>
    /// <param name="isDomainEntity">该节点是否为领域实体模型。</param>
    internal GraphNode(string modelId, object key, int nodeId, bool isDomainEntity)
    {
        ModelId = modelId;
        NodeId = nodeId;
        Key = key;
        IsDomainEntity = isDomainEntity;
        Members = new Dictionary<string, GraphMember>(StringComparer.Ordinal);
    }

    /// <summary>
    /// 获取对象图模型的稳定标识。
    /// </summary>
    internal string ModelId { get; }
    /// <summary>
    /// 获取当前捕获中的对象图节点编号。
    /// </summary>
    internal int NodeId { get; }
    /// <summary>
    /// 获取节点的稳定身份键。
    /// </summary>
    internal object Key { get; }
    /// <summary>
    /// 获取该节点是否为领域实体模型，用于隔离调用方比较器覆盖。
    /// </summary>
    internal bool IsDomainEntity { get; }
    /// <summary>
    /// 获取按成员名保存的冻结值。
    /// </summary>
    internal Dictionary<string, GraphMember> Members { get; }
}

/// <summary>
/// 对象图节点的冻结成员值。
/// </summary>
internal sealed class GraphMember
{
    /// <summary>
    /// 初始化一个 <see cref="GraphMember" /> 类型的实例。
    /// </summary>
    /// <param name="value">用于初始化实例的数据值。</param>
    /// <param name="description">当前对象的说明文本。</param>
    internal GraphMember(GraphValue value, string description)
    {
        Value = value;
        Description = description;
    }

    /// <summary>
    /// 获取已经冻结的成员值。
    /// </summary>
    internal GraphValue Value { get; }
    /// <summary>
    /// 获取成员的显示说明。
    /// </summary>
    internal string Description { get; }
}

/// <summary>
/// 对象图集合的冻结元素与位置。
/// </summary>
internal sealed class GraphCollectionItem
{
    /// <summary>
    /// 初始化一个 <see cref="GraphCollectionItem" /> 类型的实例。
    /// </summary>
    /// <param name="key">对象在容器内的键。</param>
    /// <param name="value">用于初始化实例的数据值。</param>
    /// <param name="index">当前值在所属序列中的索引。</param>
    internal GraphCollectionItem(object key, GraphValue value, int index)
    {
        Key = key;
        Value = value;
        Index = index;
    }

    /// <summary>
    /// 获取集合元素的稳定身份键。
    /// </summary>
    internal object Key { get; }
    /// <summary>
    /// 获取已经冻结的集合元素值。
    /// </summary>
    internal GraphValue Value { get; }
    /// <summary>
    /// 获取捕获时的集合下标。
    /// </summary>
    internal int Index { get; }
}

/// <summary>
/// 对象图内部使用的冻结标量、对象或集合值。
/// </summary>
internal sealed class GraphValue
{
    /// <summary>
    /// 初始化一个 <see cref="GraphValue" /> 类型的实例。
    /// </summary>
    /// <param name="kind">当前对象或操作的种类。</param>
    /// <param name="scalar">对象图中捕获的标量值。</param>
    /// <param name="node">当前对象图中的节点。</param>
    /// <param name="items">当前实例处理或保存的项集合。</param>
    /// <param name="keyed">指示当前对象值是否按键访问。</param>
    private GraphValue(ChangeValueKind kind, object scalar, GraphNode node, IReadOnlyList<GraphCollectionItem> items, bool keyed)
    {
        Kind = kind;
        ScalarValue = scalar;
        Node = node;
        Items = items;
        Keyed = keyed;
    }

    /// <summary>
    /// 获取表示空值的共享对象图值。
    /// </summary>
    internal static GraphValue Null { get; } = new GraphValue(ChangeValueKind.Null, null, null, null, false);
    /// <summary>
    /// 获取当前冻结值的结构种类。
    /// </summary>
    internal ChangeValueKind Kind { get; }
    /// <summary>
    /// 获取冻结的标量内容。
    /// </summary>
    internal object ScalarValue { get; }
    /// <summary>
    /// 获取冻结的对象图节点。
    /// </summary>
    internal GraphNode Node { get; }
    /// <summary>
    /// 获取冻结的集合或字典条目。
    /// </summary>
    internal IReadOnlyList<GraphCollectionItem> Items { get; }
    /// <summary>
    /// 获取集合元素是否具有稳定身份键。
    /// </summary>
    internal bool Keyed { get; }
    /// <summary>
    /// 获取或设置当前值在本次快照中的节点编号。
    /// </summary>
    internal int? NodeId { get; set; }
    /// <summary>
    /// 设置图值节点标识并返回该图值。
    /// </summary>
    /// <param name="nodeId">要设置的节点标识。</param>
    /// <returns>已更新节点标识的当前图值实例。</returns>
    internal GraphValue WithNodeId(int nodeId) { NodeId = nodeId; return this; }

    /// <summary>
    /// 创建经身份键校验的标量图值。
    /// </summary>
    /// <param name="value">标量值；为 null 时创建空值图节点。</param>
    /// <returns>对应的标量图值或空值图值。</returns>
    internal static GraphValue Scalar(object value)
    {
        GraphCaptureContext.ValidateKey(value, "$scalar");
        return value == null ? Null : new GraphValue(ChangeValueKind.Scalar, value, null, null, false);
    }
    /// <summary>
    /// 根据对象节点创建对象图值。
    /// </summary>
    /// <param name="value">对象图节点；为 null 时创建空值图值。</param>
    /// <returns>对应的对象图值或空值图值。</returns>
    internal static GraphValue Object(GraphNode value) => value == null ? Null : new GraphValue(ChangeValueKind.Object, null, value, null, false);
    /// <summary>
    /// 根据集合项创建集合图值。
    /// </summary>
    /// <param name="items">集合项及其索引和键。</param>
    /// <param name="keyed">集合项是否按键关联。</param>
    /// <returns>包含指定集合项的集合图值。</returns>
    internal static GraphValue Collection(IReadOnlyList<GraphCollectionItem> items, bool keyed) => new GraphValue(ChangeValueKind.Collection, null, null, items, keyed);
    /// <summary>
    /// 根据键值项创建字典图值。
    /// </summary>
    /// <param name="items">字典键值项及其索引。</param>
    /// <returns>包含指定键值项的字典图值。</returns>
    internal static GraphValue Dictionary(IReadOnlyList<GraphCollectionItem> items) => new GraphValue(ChangeValueKind.Dictionary, null, null, items, true);
    /// <summary>
    /// 将当前图值转换为集合图值。
    /// </summary>
    /// <returns>Kind 为 Set 的图值；当前图值为 null 时返回原实例。</returns>
    internal GraphValue AsSet() => Kind == ChangeValueKind.Null
        ? this
        : new GraphValue(ChangeValueKind.Set, ScalarValue, Node, Items, Keyed) { NodeId = NodeId };
}

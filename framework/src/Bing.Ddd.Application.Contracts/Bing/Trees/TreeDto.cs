// ReSharper disable once CheckNamespace
namespace Bing.Trees;

/// <summary>
/// 表示包含强类型子节点集合的树形数据传输对象。
/// </summary>
/// <typeparam name="TNode">树节点类型。</typeparam>
public class TreeDto<TNode> : TreeDto where TNode : TreeDto<TNode>
{
    /// <summary>
    /// 获取或设置子节点集合。
    /// </summary>
    public List<TNode> Children { get; set; }

    /// <summary>
    /// 初始化一个 <see cref="TreeDto{TNode}"/> 类型的实例。
    /// </summary>
    public TreeDto() => Children = new List<TNode>();
}

/// <summary>
/// 表示包含展示和选择状态的树形数据传输对象。
/// </summary>
public class TreeDto : TreeDtoBase
{
    /// <summary>
    /// 获取或设置节点的显示文本。
    /// </summary>
    public virtual string Text { get; set; }

    /// <summary>
    /// 获取或设置节点图标。
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// 获取或设置是否禁用节点复选框。
    /// </summary>
    public bool? DisableCheckbox { get; set; }

    /// <summary>
    /// 获取或设置节点是否可选。
    /// </summary>
    /// <remarks>
    /// 默认值为 <see langword="true"/>。
    /// </remarks>
    public bool? Selectable { get; set; } = true;

    /// <summary>
    /// 获取或设置节点复选框是否选中。
    /// </summary>
    public bool? Checked { get; set; }

    /// <summary>
    /// 获取或设置节点是否被选中。
    /// </summary>
    public bool? Selected { get; set; }

    /// <summary>
    /// 获取或设置节点是否为叶节点。
    /// </summary>
    public bool? Leaf { get; set; }
}

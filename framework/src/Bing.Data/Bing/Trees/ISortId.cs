namespace Bing.Trees;

/// <summary>
/// 定义具有可选排序号的树节点契约。
/// </summary>
public interface ISortId
{
    /// <summary>
    /// 获取或设置同级节点的排序号；为空时不指定排序号。
    /// </summary>
    int? SortId { get; set; }
}
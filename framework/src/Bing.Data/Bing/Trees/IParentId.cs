namespace Bing.Trees;

/// <summary>
/// 定义具有父节点标识的树节点契约。
/// </summary>
/// <typeparam name="TParentId">父节点标识类型。</typeparam>
public interface IParentId<TParentId>
{
    /// <summary>
    /// 获取或设置父节点标识。
    /// </summary>
    TParentId ParentId { get; set; }
}
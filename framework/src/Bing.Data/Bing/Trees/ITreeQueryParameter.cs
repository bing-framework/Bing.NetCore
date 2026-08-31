using Bing.Data.Queries;

namespace Bing.Trees;

/// <summary>
/// 定义支持父节点、层级、路径和启用状态过滤的树形查询参数。
/// </summary>
/// <typeparam name="TParentId">父节点标识类型。</typeparam>
public interface ITreeQueryParameter<TParentId> : IQueryParameter
{
    /// <summary>
    /// 获取或设置父节点标识。
    /// </summary>
    TParentId ParentId { get; set; }

    /// <summary>
    /// 获取或设置节点层级；为空时不按层级过滤。
    /// </summary>
    int? Level { get; set; }

    /// <summary>
    /// 获取或设置物化路径过滤条件。
    /// </summary>
    string Path { get; set; }

    /// <summary>
    /// 获取或设置启用状态过滤条件；为空时不按状态过滤。
    /// </summary>
    bool? Enabled { get; set; }

    /// <summary>
    /// 判断当前参数是否包含可用于查询的有效条件。
    /// </summary>
    /// <returns>存在有效查询条件时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    bool IsSearch();
}

/// <summary>
/// 使用可空 GUID 父节点标识的树形查询参数契约。
/// </summary>
public interface ITreeQueryParameter : ITreeQueryParameter<Guid?>
{
}

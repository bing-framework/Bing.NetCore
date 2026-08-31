using System.ComponentModel.DataAnnotations;
using Bing.Data.Queries;
using Bing.Extensions;

namespace Bing.Trees;

/// <summary>
/// 提供支持父节点、层级、路径和启用状态过滤的树形查询参数实现。
/// </summary>
/// <typeparam name="TParentId">父节点标识类型。</typeparam>
public class TreeQueryParameter<TParentId> : QueryParameter, ITreeQueryParameter<TParentId>
{
    /// <summary>
    /// 获取或设置父节点标识。
    /// </summary>
    public TParentId ParentId { get; set; }

    /// <summary>
    /// 获取或设置节点层级；为空时不按层级过滤。
    /// </summary>
    public int? Level { get; set; }

    /// <summary>
    /// 保存经过规范化的物化路径查询条件。
    /// </summary>
    private string _path = string.Empty;

    /// <summary>
    /// 获取或设置物化路径查询条件；获取时会去除首尾空白，空值按空字符串返回。
    /// </summary>
    public string Path
    {
        get => _path == null ? string.Empty : _path.Trim();
        set => _path = value;
    }

    /// <summary>
    /// 获取或设置启用状态查询条件；为空时不按状态过滤。
    /// </summary>
    [Display(Name = "启用")]
    public bool? Enabled { get; set; }

    /// <summary>
    /// 初始化一个 <see cref="TreeQueryParameter{TParentId}"/> 实例，并设置默认排序字段。
    /// </summary>
    protected TreeQueryParameter() => Order = "SortId";

    /// <summary>
    /// 判断当前参数是否包含可用于查询的有效条件。
    /// </summary>
    /// <returns>存在有效查询条件时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public virtual bool IsSearch()
    {
        var items = Reflection.Reflections.GetPublicProperties(this);
        return items.Any(t => IsSearchProperty(t.Text, t.Value));
    }

    /// <summary>
    /// 判断指定属性是否应作为查询条件。
    /// </summary>
    /// <param name="name">属性名称。</param>
    /// <param name="value">属性值。</param>
    /// <returns>属性值非空且不是分页或排序控制属性时返回 <see langword="true"/>。</returns>
    protected virtual bool IsSearchProperty(string name, object value)
    {
        if (value.SafeString().IsEmpty())
            return false;
        switch (name.SafeString().ToLower())
        {
            case "order":
            case "pagesize":
            case "page":
            case "totalcount":
            case "istotalcountknown":
                return false;
        }
        return true;
    }
}

/// <summary>
/// 树型查询参数
/// </summary>
public class TreeQueryParameter : TreeQueryParameter<Guid?>, ITreeQueryParameter { }

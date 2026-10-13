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
    /// <inheritdoc />
    public TParentId ParentId { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// 为空时不按层级过滤。
    /// </remarks>
    public int? Level { get; set; }

    /// <summary>
    /// 保存经过规范化的物化路径查询条件。
    /// </summary>
    private string _path = string.Empty;

    /// <inheritdoc />
    /// <remarks>
    /// 获取时会去除首尾空白，空值按空字符串返回。
    /// </remarks>
    public string Path
    {
        get => _path == null ? string.Empty : _path.Trim();
        set => _path = value;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 为空时不按状态过滤。
    /// </remarks>
    [Display(Name = "启用")]
    public bool? Enabled { get; set; }

    /// <summary>
    /// 初始化一个 <see cref="TreeQueryParameter{TParentId}"/> 类型的实例。
    /// </summary>
    protected TreeQueryParameter() => Order = "SortId";

    /// <inheritdoc />
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
/// 提供父节点标识为可空 GUID 的树形查询参数。
/// </summary>
public class TreeQueryParameter : TreeQueryParameter<Guid?>, ITreeQueryParameter { }

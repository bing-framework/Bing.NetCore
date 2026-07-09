using System.Collections.ObjectModel;
using System.Linq.Expressions;
using Bing.Expressions;
using Bing.Extensions;

namespace Bing.Data.Sql;

/// <summary>
/// Sql 参数映射
/// </summary>
public interface ISqlParameterMap
{
    /// <summary>
    /// 获取参数映射项集合
    /// </summary>
    /// <returns>参数映射项集合</returns>
    IReadOnlyCollection<SqlParameterMapItem> GetItems();
}

/// <summary>
/// Sql 参数映射项
/// </summary>
public class SqlParameterMapItem
{
    /// <summary>
    /// 参数名
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 实体类型
    /// </summary>
    public Type EntityType { get; set; }

    /// <summary>
    /// 属性名
    /// </summary>
    public string PropertyName { get; set; }

    /// <summary>
    /// 参数值
    /// </summary>
    public object Value { get; set; }
}

/// <summary>
/// Sql 参数映射
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public class SqlParameterMap<TEntity> : ISqlParameterMap where TEntity : class
{
    /// <summary>
    /// 参数映射项字典
    /// </summary>
    private readonly IDictionary<string, SqlParameterMapItem> _items =
        new Dictionary<string, SqlParameterMapItem>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 添加参数映射
    /// </summary>
    /// <param name="name">参数名</param>
    /// <param name="property">实体属性表达式</param>
    /// <param name="value">参数值</param>
    /// <returns>Sql 参数映射</returns>
    public SqlParameterMap<TEntity> Add(string name, Expression<Func<TEntity, object>> property, object value = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));
        property.CheckNull(nameof(property));
        _items[name] = new SqlParameterMapItem
        {
            Name = name,
            EntityType = typeof(TEntity),
            PropertyName = Lambdas.GetLastName(property),
            Value = value
        };
        return this;
    }

    /// <summary>
    /// 获取参数映射项集合
    /// </summary>
    /// <returns>参数映射项集合</returns>
    public IReadOnlyCollection<SqlParameterMapItem> GetItems() =>
        new ReadOnlyCollection<SqlParameterMapItem>(_items.Values.ToList());
}
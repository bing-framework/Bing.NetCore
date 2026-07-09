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

    /// <summary>
    /// 是否已显式提供参数值
    /// </summary>
    public bool HasExplicitValue { get; set; }

    /// <summary>
    /// 参数值是否已成功解析
    /// </summary>
    public bool ValueResolved { get; set; }
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
    /// 参数源对象
    /// </summary>
    private object _source;

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
            Value = value,
            HasExplicitValue = value != null,
            ValueResolved = value != null
        };
        return this;
    }

    /// <summary>
    /// 映射参数名与实体属性
    /// </summary>
    /// <param name="name">参数名</param>
    /// <param name="property">实体属性表达式</param>
    /// <returns>Sql 参数映射</returns>
    public SqlParameterMap<TEntity> Map(string name, Expression<Func<TEntity, object>> property) =>
        Add(name, property);

    /// <summary>
    /// 绑定参数源对象
    /// </summary>
    /// <param name="source">参数源对象</param>
    /// <returns>Sql 参数映射</returns>
    public SqlParameterMap<TEntity> UseSource(object source)
    {
        _source = source;
        return this;
    }

    /// <summary>
    /// 获取参数映射项集合
    /// </summary>
    /// <returns>参数映射项集合</returns>
    public IReadOnlyCollection<SqlParameterMapItem> GetItems() =>
        new ReadOnlyCollection<SqlParameterMapItem>(_items.Values.Select(ResolveItem).ToList());

    /// <summary>
    /// 解析参数映射项
    /// </summary>
    /// <param name="item">参数映射项</param>
    /// <returns>解析后的参数映射项</returns>
    private SqlParameterMapItem ResolveItem(SqlParameterMapItem item)
    {
        var result = new SqlParameterMapItem
        {
            Name = item.Name,
            EntityType = item.EntityType,
            PropertyName = item.PropertyName,
            Value = item.Value,
            HasExplicitValue = item.HasExplicitValue,
            ValueResolved = item.ValueResolved
        };
        if (item.HasExplicitValue)
            return result;
        if (TryGetValue(_source, item.Name, out var value) || TryGetValue(_source, item.PropertyName, out value))
        {
            result.Value = value;
            result.ValueResolved = true;
            return result;
        }

        result.ValueResolved = false;
        return result;
    }

    /// <summary>
    /// 尝试获取参数值
    /// </summary>
    /// <param name="source">参数源对象</param>
    /// <param name="name">参数名</param>
    /// <param name="value">参数值</param>
    /// <returns>是否获取成功</returns>
    private static bool TryGetValue(object source, string name, out object value)
    {
        value = null;
        if (source == null || string.IsNullOrWhiteSpace(name))
            return false;
        if (source is IReadOnlyDictionary<string, object> readOnlyDictionary)
            return TryGetValue(readOnlyDictionary, name, out value);
        if (source is IDictionary<string, object> dictionary)
            return TryGetValue(dictionary, name, out value);
        var property = source.GetType().GetProperty(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.IgnoreCase);
        if (property == null || property.CanRead == false)
            return false;
        value = property.GetValue(source);
        return true;
    }

    /// <summary>
    /// 尝试从字典获取参数值
    /// </summary>
    /// <param name="dictionary">字典</param>
    /// <param name="name">参数名</param>
    /// <param name="value">参数值</param>
    /// <returns>是否获取成功</returns>
    private static bool TryGetValue(IEnumerable<KeyValuePair<string, object>> dictionary, string name, out object value)
    {
        foreach (var item in dictionary)
        {
            if (string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase) == false)
                continue;
            value = item.Value;
            return true;
        }

        value = null;
        return false;
    }
}
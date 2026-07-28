using System.Collections;
using System.Reflection;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql;

/// <summary>
/// 默认 SQL 参数源解析器
/// </summary>
public sealed class DefaultSqlParameterSourceResolver : ISqlParameterSourceResolver
{
    /// <summary>
    /// 参数名称规范化器
    /// </summary>
    private readonly ISqlParameterNameNormalizer _nameNormalizer;

    /// <summary>
    /// 初始化一个<see cref="DefaultSqlParameterSourceResolver"/>类型的实例
    /// </summary>
    /// <param name="nameNormalizer">参数名称规范化器</param>
    public DefaultSqlParameterSourceResolver(ISqlParameterNameNormalizer nameNormalizer = null) =>
        _nameNormalizer = nameNormalizer ?? new DefaultSqlParameterNameNormalizer();

    /// <inheritdoc />
    public bool CanResolve(object source) => source != null && source is not ISqlParameterMap;

    /// <inheritdoc />
    public bool TryResolve(object source, string parameterName, out object value)
    {
        value = null;
        if (CanResolve(source) == false || string.IsNullOrWhiteSpace(parameterName))
            return false;
        var normalizedName = _nameNormalizer.Normalize(parameterName);
        if (source is IEnumerable<SqlParam> sqlParameters)
        {
            var parameter = sqlParameters.FirstOrDefault(t => t != null &&
                string.Equals(_nameNormalizer.Normalize(t.Name), normalizedName, StringComparison.OrdinalIgnoreCase));
            if (parameter == null)
                return false;
            value = parameter.Value;
            return true;
        }
        if (source is IReadOnlyDictionary<string, object> readOnlyDictionary)
            return TryResolve(readOnlyDictionary, normalizedName, out value);
        if (source is IDictionary<string, object> dictionary)
            return TryResolve(dictionary, normalizedName, out value);
        if (source is IDictionary nonGenericDictionary)
            return TryResolve(nonGenericDictionary, normalizedName, out value);
        var property = source.GetType().GetProperty(normalizedName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property == null || property.CanRead == false || property.GetIndexParameters().Length != 0)
            return false;
        value = property.GetValue(source);
        return true;
    }

    /// <summary>
    /// 从泛型字典解析参数
    /// </summary>
    /// <param name="source">参数字典</param>
    /// <param name="parameterName">标准参数名称</param>
    /// <param name="value">参数值</param>
    /// <returns>找到参数时返回 true</returns>
    private bool TryResolve(IEnumerable<KeyValuePair<string, object>> source, string parameterName, out object value)
    {
        foreach (var item in source)
        {
            if (string.Equals(_nameNormalizer.Normalize(item.Key), parameterName, StringComparison.OrdinalIgnoreCase) == false)
                continue;
            value = item.Value;
            return true;
        }
        value = null;
        return false;
    }

    /// <summary>
    /// 从非泛型字典解析参数
    /// </summary>
    /// <param name="source">参数字典</param>
    /// <param name="parameterName">标准参数名称</param>
    /// <param name="value">参数值</param>
    /// <returns>找到参数时返回 true</returns>
    private bool TryResolve(IDictionary source, string parameterName, out object value)
    {
        foreach (DictionaryEntry item in source)
        {
            if (item.Key is not string name ||
                string.Equals(_nameNormalizer.Normalize(name), parameterName, StringComparison.OrdinalIgnoreCase) == false)
                continue;
            value = item.Value;
            return true;
        }
        value = null;
        return false;
    }
}
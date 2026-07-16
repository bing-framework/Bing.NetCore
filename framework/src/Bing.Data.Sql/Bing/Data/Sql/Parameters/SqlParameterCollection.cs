using System.Collections;
using System.Data;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql;

/// <summary>
/// 可枚举的 SQL 参数集合。
/// </summary>
/// <remarks>
/// <para>集合统一规范化参数名称，并保留参数方向、类型和 Provider 元数据。</para>
/// <para>可直接传给数据访问实现，避免映射参数在转换过程中丢失输出参数信息。</para>
/// </remarks>
public sealed class SqlParameterCollection : IReadOnlyCollection<SqlParam>
{
    /// <summary>
    /// 参数名称规范化器。
    /// </summary>
    private readonly ISqlParameterNameNormalizer _nameNormalizer;

    /// <summary>
    /// 按标准名称存储的参数。
    /// </summary>
    private readonly IDictionary<string, SqlParam> _parameters;

    /// <summary>
    /// 初始化一个<see cref="SqlParameterCollection"/>类型的实例。
    /// </summary>
    /// <param name="nameNormalizer">参数名称规范化器。</param>
    public SqlParameterCollection(ISqlParameterNameNormalizer nameNormalizer = null)
    {
        _nameNormalizer = nameNormalizer ?? new DefaultSqlParameterNameNormalizer();
        _parameters = new Dictionary<string, SqlParam>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 参数数量。
    /// </summary>
    public int Count => _parameters.Count;

    /// <summary>
    /// 添加或替换参数。
    /// </summary>
    /// <param name="parameter">参数元数据。</param>
    /// <returns>当前参数集合。</returns>
    /// <exception cref="ArgumentNullException">参数为空或参数名称为空时抛出。</exception>
    public SqlParameterCollection Add(SqlParam parameter)
    {
        if (parameter == null)
            throw new ArgumentNullException(nameof(parameter));
        var name = _nameNormalizer.Normalize(parameter.Name);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(parameter), "SQL 参数名称不能为空。");
        _parameters[name] = parameter;
        return this;
    }

    /// <summary>
    /// 添加或替换输入参数。
    /// </summary>
    /// <param name="name">参数名称，支持 Provider 前缀。</param>
    /// <param name="value">参数值。</param>
    /// <param name="dbType">数据库通用类型。</param>
    /// <param name="size">参数长度。</param>
    /// <param name="precision">数值有效位数。</param>
    /// <param name="scale">数值小数位数。</param>
    /// <returns>当前参数集合。</returns>
    public SqlParameterCollection Add(string name, object value, DbType? dbType = null, int? size = null,
        byte? precision = null, byte? scale = null) =>
        Add(new SqlParam(name, value, dbType, ParameterDirection.Input, size, precision, scale)
        {
            OriginalValue = value
        });

    /// <summary>
    /// 添加或替换输出参数。
    /// </summary>
    /// <param name="name">参数名称，支持 Provider 前缀。</param>
    /// <param name="dbType">数据库通用类型。</param>
    /// <param name="size">参数长度。</param>
    /// <returns>当前参数集合。</returns>
    public SqlParameterCollection AddOutput(string name, DbType? dbType = null, int? size = null) =>
        Add(new SqlParam(name, null, dbType, ParameterDirection.Output, size));

    /// <summary>
    /// 获取参数枚举器。
    /// </summary>
    /// <returns>参数枚举器。</returns>
    public IEnumerator<SqlParam> GetEnumerator() => _parameters.Values.GetEnumerator();

    /// <summary>
    /// 获取非泛型参数枚举器。
    /// </summary>
    /// <returns>非泛型参数枚举器。</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
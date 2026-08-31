using System.Collections.ObjectModel;
using Bing.Data.Sql;
using Bing.Extensions;

namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// 参数管理器
/// </summary>
public class ParameterManager : IAdvancedParameterManager
{
    #region 字段

    /// <summary>
    /// 参数名称规范化器。
    /// </summary>
    private static readonly DefaultSqlParameterNameNormalizer ParameterNameNormalizer = new();

    /// <summary>
    /// 参数集合
    /// </summary>
    private readonly IDictionary<string, object> _params;

    /// <summary>
    /// 增强参数集合
    /// </summary>
    private readonly IDictionary<string, SqlParam> _sqlParams;

    /// <summary>
    /// 参数索引
    /// </summary>
    private int _paramIndex;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化一个<see cref="ParameterManager"/>类型的实例
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    public ParameterManager(IDialect dialect)
    {
        Dialect = dialect;
        _paramIndex = 0;
        _params = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        _sqlParams = new Dictionary<string, SqlParam>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 初始化一个<see cref="ParameterManager"/>类型的实例
    /// </summary>
    /// <param name="parameterManager">参数管理器</param>
    protected ParameterManager(ParameterManager parameterManager)
    {
        Dialect = parameterManager.Dialect;
        _paramIndex = parameterManager._paramIndex;
        _params = new Dictionary<string, object>(parameterManager._params, StringComparer.OrdinalIgnoreCase);
        _sqlParams = new Dictionary<string, SqlParam>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in parameterManager._sqlParams)
            _sqlParams.Add(parameter.Key, CloneSqlParam(parameter.Value, parameter.Key, parameter.Value.Value));
    }

    /// <summary>
    /// 使用参数快照恢复当前参数状态。
    /// </summary>
    /// <param name="source">参数快照。</param>
    internal void RestoreFrom(ParameterManager source)
    {
        if (source == null)
            return;
        _paramIndex = source._paramIndex;
        _params.Clear();
        foreach (var parameter in source._params)
            _params[parameter.Key] = parameter.Value;
        _sqlParams.Clear();
        foreach (var parameter in source._sqlParams)
            _sqlParams[parameter.Key] = CloneSqlParam(parameter.Value, parameter.Key, parameter.Value.Value);
    }

    #endregion

    #region 属性

    /// <summary>
    /// Sql方言
    /// </summary>
    protected IDialect Dialect { get; }

    /// <inheritdoc />
    public int Count => _params.Count;

    #endregion

    #region GenerateName(创建参数名)

    /// <inheritdoc />
    public virtual string GenerateName()
    {
        string result;
        do
        {
            result = Dialect.GenerateName(_paramIndex);
            _paramIndex += 1;
        } while (_params.ContainsKey(result) || _sqlParams.ContainsKey(result));
        return result;
    }

    #endregion

    #region NormalizeName(标准化参数名)

    /// <inheritdoc />
    public virtual string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;
        name = ParameterNameNormalizer.Normalize(name);
        return string.IsNullOrWhiteSpace(name) ? string.Empty : $"{Dialect.GetPrefix()}{name}";
    }

    

    #endregion

    #region Add(添加参数)

    /// <inheritdoc />
    public void Add(string name, object value, Operator? @operator = null)
    {
        name = NormalizeName(name);
        if (string.IsNullOrWhiteSpace(name))
            return;
        value = Dialect.GetParamValue(value);
        value = GetValue(value, @operator);
        if (_params.ContainsKey(name))
            _params.Remove(name);
        _params.Add(name, value);
        AddSqlParam(CreateLegacySqlParam(name, value));
    }

    /// <summary>
    /// 添加增强参数
    /// </summary>
    /// <param name="parameter">Sql 参数</param>
    public void Add(SqlParam parameter)
    {
        if (parameter == null)
            return;
        var name = NormalizeName(parameter.Name);
        if (string.IsNullOrWhiteSpace(name))
            return;
        var value = Dialect.GetParamValue(parameter.Value);
        if (_params.ContainsKey(name))
            _params.Remove(name);
        _params.Add(name, value);
        AddSqlParam(CloneSqlParam(parameter, name, value));
    }

    /// <summary>
    /// 添加增强参数到内部字典
    /// </summary>
    /// <param name="parameter">Sql 参数</param>
    private void AddSqlParam(SqlParam parameter)
    {
        if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name))
            return;
        if (_sqlParams.ContainsKey(parameter.Name))
            _sqlParams.Remove(parameter.Name);
        _sqlParams.Add(parameter.Name, parameter);
    }

    /// <summary>
    /// 创建旧参数链路的增强参数
    /// </summary>
    /// <param name="name">参数名</param>
    /// <param name="value">参数值</param>
    /// <returns>Sql 参数</returns>
    private SqlParam CreateLegacySqlParam(string name, object value)
    {
        return new SqlParam(name, value)
        {
            Source = SqlParameterSource.Basic,
            MetadataLevel = SqlParameterMetadataLevel.Weak
        };
    }

    /// <summary>
    /// 克隆增强参数并应用标准化后的名称和值
    /// </summary>
    /// <param name="parameter">源参数</param>
    /// <param name="name">标准化参数名</param>
    /// <param name="value">转换后的参数值</param>
    /// <returns>Sql 参数</returns>
    private SqlParam CloneSqlParam(SqlParam parameter, string name, object value)
    {
        return new SqlParam(name, value, parameter.DbType, parameter.Direction, parameter.Size, parameter.Precision,
            parameter.Scale)
        {
            EntityType = parameter.EntityType,
            OriginalValue = parameter.OriginalValue,
            PropertyName = parameter.PropertyName,
            ColumnName = parameter.ColumnName,
            DatabaseType = parameter.DatabaseType,
            ProviderTypeName = parameter.ProviderTypeName,
            Source = parameter.Source,
            MetadataLevel = parameter.MetadataLevel,
            StorageKind = parameter.StorageKind,
            ConverterKind = parameter.ConverterKind,
            CustomConverterName = parameter.CustomConverterName
        };
    }

    /// <summary>
    /// 获取值
    /// </summary>
    /// <param name="value">参数值</param>
    /// <param name="operator">运算符</param>
    /// <returns>根据运算符应用通配符后的参数值。</returns>
    private object GetValue(object value, Operator? @operator)
    {
        if (string.IsNullOrWhiteSpace(value.SafeString()))
            return value;
        switch (@operator)
        {
            case Operator.Contains:
                return $"%{value}%";

            case Operator.Starts:
                return $"{value}%";

            case Operator.Ends:
                return $"%{value}";

            default:
                return value;
        }
    }

    #endregion

    #region GetParams(获取参数列表)

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object> GetParams() =>
        new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(_params, StringComparer.OrdinalIgnoreCase));

    /// <inheritdoc />
    public IReadOnlyDictionary<string, SqlParam> GetSqlParams()
    {
        var result = new Dictionary<string, SqlParam>(_sqlParams.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in _sqlParams)
            result.Add(parameter.Key, CloneSqlParam(parameter.Value, parameter.Key, parameter.Value.Value));
        return new ReadOnlyDictionary<string, SqlParam>(result);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object> ExportValues() =>
        new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(_params, StringComparer.OrdinalIgnoreCase));

    #endregion

    #region Contains(是否包含参数)

    /// <inheritdoc />
    public virtual bool Contains(string name)
    {
        name = NormalizeName(name);
        return string.IsNullOrWhiteSpace(name) == false && _params.ContainsKey(name);
    }

    #endregion

    #region GetValue(获取参数值)

    /// <inheritdoc />
    public virtual object GetValue(string name)
    {
        name = NormalizeName(name);
        return string.IsNullOrWhiteSpace(name) == false && _params.ContainsKey(name) ? _params[name] : null;
    }

    #endregion

    #region Clear(清空参数)

    /// <summary>
    /// 清空参数
    /// </summary>
    public virtual void Clear()
    {
        _paramIndex = 0;
        _params.Clear();
        _sqlParams.Clear();
    }

    #endregion

    #region Clone(克隆)

    /// <summary>
    /// 克隆
    /// </summary>
    /// <returns>当前参数管理器的独立副本。</returns>
    public virtual IParameterManager Clone() => new ParameterManager(this);

    /// <summary>
    /// 创建保留当前方言配置的空参数管理器。
    /// </summary>
    /// <returns>不包含参数和值的独立参数管理器。</returns>
    public virtual IParameterManager CreateEmpty() => new ParameterManager(Dialect);

    #endregion
}

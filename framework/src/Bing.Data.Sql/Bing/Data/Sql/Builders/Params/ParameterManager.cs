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
    /// 初始化一个 <see cref="ParameterManager"/> 类型的实例。
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
    /// 初始化一个 <see cref="ParameterManager"/> 类型的实例。
    /// </summary>
    /// <param name="parameterManager">参数管理器</param>
    protected ParameterManager(ParameterManager parameterManager)
    {
        Dialect = parameterManager.Dialect;
        _paramIndex = parameterManager._paramIndex;
        _params = new Dictionary<string, object>(parameterManager._params, StringComparer.OrdinalIgnoreCase);
        // 内部描述符在加入时复制，之后只替换整项；GetSqlParams 仍返回描述符副本。
        // 各克隆保留独立字典，安全复用内部只读项，避免宽子查询合并逐项复制元数据。
        _sqlParams = new Dictionary<string, SqlParam>(parameterManager._sqlParams, StringComparer.OrdinalIgnoreCase);
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

    /// <inheritdoc />
    public void Restore(SqlParam parameter)
    {
        if (parameter == null)
            return;
        var name = NormalizeName(parameter.Name);
        if (!_params.ContainsKey(name))
            return;
        _params[name] = parameter.Value;
        _sqlParams[name] = CloneSqlParam(parameter, name, parameter.Value);
    }

    /// <summary>写入由同一方言转换过的参数快照，不再重复转换。</summary>
    internal void AddSnapshot(SqlParam parameter, bool preserveMetadata)
    {
        var name = NormalizeName(parameter.Name);
        if (string.IsNullOrWhiteSpace(name))
            return;
        _params[name] = parameter.Value;
        _sqlParams[name] = preserveMetadata
            ? CloneSqlParam(parameter, name, parameter.Value)
            : CreateLegacySqlParam(name, parameter.Value);
    }

    /// <summary>仅在使用同一方言实例时共享已转换参数。</summary>
    internal bool SharesDialectWith(ParameterManager other) => ReferenceEquals(Dialect, other.Dialect);

    /// <inheritdoc />
    public bool Remove(string name)
    {
        name = NormalizeName(name);
        if (!_params.Remove(name))
            return false;
        _sqlParams.Remove(name);
        // 清理分页后重用可用名称，与 Clear 后重建参数的既有行为一致。
        _paramIndex = 0;
        return true;
    }

    /// <summary>
    /// 仅暂存本次子查询新增的参数，复用当前字典进行冲突检查，避免复制全部历史参数。
    /// 调用期间目标管理器保持不变；生成名序列只在局部推进，与原 Clone 预检合同一致。
    /// </summary>
    internal void PrepareSubqueryMerge(IReadOnlyDictionary<string, object> source,
        IReadOnlyDictionary<string, SqlParam> metadata, IDictionary<string, string> names,
        bool useMetadata, int maxCount, string providerKey, ISet<string> distinctNames = null,
        bool alreadyConverted = false)
    {
        var pending = new ParameterManager(Dialect) { _paramIndex = _paramIndex };
        foreach (var parameter in source)
        {
            if (!names.TryGetValue(parameter.Key, out var targetName))
            {
                targetName = parameter.Key;
                var normalized = NormalizeName(targetName);
                var exists = pending._params.TryGetValue(normalized, out var value) ||
                    _params.TryGetValue(normalized, out value);
                var metadataDiffers = exists && useMetadata && metadata != null &&
                    metadata.TryGetValue(parameter.Key, out var sourceParameter) &&
                    (pending._sqlParams.TryGetValue(normalized, out var existingParameter) ||
                     _sqlParams.TryGetValue(normalized, out existingParameter)) &&
                    (!SqlQueryShapeTemplate.HasSameContract(existingParameter, sourceParameter) ||
                     !SqlQueryShapeTemplate.HasSameConversionState(existingParameter, sourceParameter));
                if (exists && (distinctNames?.Contains(parameter.Key) == true ||
                    !Equals(value, parameter.Value) || metadataDiffers))
                {
                    do { targetName = pending.GenerateName(); }
                    while (_params.ContainsKey(targetName));
                }
                names[parameter.Key] = targetName;
            }
            var name = NormalizeName(targetName);
            if (string.IsNullOrWhiteSpace(name) || _params.ContainsKey(name) || pending._params.ContainsKey(name))
                continue;
            var count = _params.Count + pending.Count;
            if (count >= maxCount)
                throw new InvalidOperationException(
                    $"SQL Provider '{providerKey}' 的参数数量超出上限。当前参数数量: {count}；尝试添加后数量: {count + 1}；最大参数数量: {maxCount}。");
            if (alreadyConverted)
            {
                var snapshot = metadata != null && metadata.TryGetValue(parameter.Key, out var sourceParameter)
                    ? CloneSqlParam(sourceParameter, targetName, sourceParameter.Value)
                    : CreateLegacySqlParam(targetName, parameter.Value);
                pending.AddSnapshot(snapshot, useMetadata);
            }
            // 不同转换上下文和原始片段仍由目标方言处理。
            else if (useMetadata && metadata != null && metadata.TryGetValue(parameter.Key, out var sqlParameter))
                pending.Add(CloneSqlParam(sqlParameter, targetName, sqlParameter.Value));
            else
                pending.Add(targetName, parameter.Value);
        }
    }

    #endregion

    #region 属性

    /// <summary>
    /// 获取Sql方言
    /// </summary>
    protected IDialect Dialect { get; }

    /// <summary>冻结参数派生沿用实际存储方言，不持有可变参数容器。</summary>
    internal IDialect ParameterDialect => Dialect;

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

    /// <summary>预览缺失的分页名称，已有 offset 不占用新名称，不推进当前容器游标。</summary>
    internal void PreviewPageNames(out string offset, out string limit, string existingOffset = null)
    {
        var index = _paramIndex;
        string Next()
        {
            string name;
            do { name = Dialect.GenerateName(index++); }
            while (_params.ContainsKey(name) || _sqlParams.ContainsKey(name));
            return name;
        }
        offset = string.IsNullOrWhiteSpace(existingOffset) ? Next() : existingOffset;
        do { limit = Next(); }
        while (string.Equals(limit, offset, StringComparison.OrdinalIgnoreCase));
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

    /// <inheritdoc />
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
            IsProviderValue = parameter.IsProviderValue,
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

    /// <summary>仅供框架内部比较只读参数合同，避免复制整个参数集合。</summary>
    internal bool TryGetStoredSqlParam(string name, out SqlParam parameter) =>
        _sqlParams.TryGetValue(NormalizeName(name), out parameter);

    /// <summary>按已规范化的描述符名称检查参数是否仍为同一项。</summary>
    internal bool ContainsStoredSqlParam(SqlParam parameter) =>
        _sqlParams.TryGetValue(parameter.Name, out var current) && ReferenceEquals(current, parameter);

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

    /// <inheritdoc />
    public virtual void Clear()
    {
        _paramIndex = 0;
        _params.Clear();
        _sqlParams.Clear();
    }

    #endregion

    #region Clone(克隆)

    /// <inheritdoc />
    public virtual IParameterManager Clone() => new ParameterManager(this);

    /// <inheritdoc />
    /// <remarks>
    /// 创建保留当前方言配置的空参数管理器。
    /// </remarks>
    public virtual IParameterManager CreateEmpty() => new ParameterManager(Dialect);

    #endregion
}

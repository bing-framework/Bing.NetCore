using System.Collections.ObjectModel;
using System.Data;
using Bing.Extensions;

namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// 参数管理器
/// </summary>
public class ParameterManager : IParameterManager
{
    #region 字段

    /// <summary>
    /// Sql方言
    /// </summary>
    protected readonly IDialect Dialect;

    /// <summary>
    /// 参数集合
    /// </summary>
    private readonly IDictionary<string, object> _params;

    /// <summary>
    /// 参数索引
    /// </summary>
    private int _paramIndex;

    /// <summary>
    /// 参数集合
    /// </summary>
    private readonly IDictionary<string, SqlParam> _sqlParams;

    /// <summary>
    /// 动态参数集合
    /// </summary>
    private readonly List<object> _dynamicParams;

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
        _params = new Dictionary<string, object>();
        _sqlParams = new Dictionary<string, SqlParam>();
        _dynamicParams = new List<object>();
    }

    /// <summary>
    /// 初始化一个<see cref="ParameterManager"/>类型的实例
    /// </summary>
    /// <param name="manager">参数管理器</param>
    protected ParameterManager(ParameterManager manager)
    {
        Dialect = manager.Dialect;
        _paramIndex = manager._paramIndex;
        _params = new Dictionary<string, object>(manager._params);
        _sqlParams = new Dictionary<string, SqlParam>(manager._sqlParams);
        _dynamicParams = new List<object>(manager._dynamicParams);
    }

    #endregion

    #region GenerateName(创建参数名)

    /// <inheritdoc />
    public virtual string GenerateName()
    {
        var result = Dialect.GenerateName(_paramIndex);
        _paramIndex += 1;
        return result;
    }

    #endregion

    #region NormalizeName(标准化参数名)

    /// <inheritdoc />
    public virtual string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name;
        name = name.Trim();
        if (name.StartsWith(Dialect.GetPrefix()))
            return name;
        return $"{Dialect.GetPrefix()}{name}";
    }

    /// <summary>
    /// 添加动态参数
    /// </summary>
    /// <param name="param">动态参数</param>
    public virtual void AddDynamicParams(object param)
    {
        if (param == null)
            return;
        _dynamicParams.Add(param);
    }

    #endregion

    #region Add(添加参数)

    /// <inheritdoc />
    public void Add(string name, object value, Operator? @operator = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        name = NormalizeName(name);
        value = Dialect.GetParamValue(value);
        if (_params.ContainsKey(name))
            _params.Remove(name);
        _params.Add(name, GetValue(value, @operator));
    }

    /// <summary>
    /// 添加参数，如果参数已存在则替换
    /// </summary>
    /// <param name="name">参数名</param>
    /// <param name="value">参数值</param>
    /// <param name="dbType">参数类型</param>
    /// <param name="direction">参数方向</param>
    /// <param name="size">字段长度</param>
    /// <param name="precision">数值有效位数</param>
    /// <param name="scale">数值小数位数</param>
    public void AddSqlParam(string name, object value = null, DbType? dbType = null, ParameterDirection? direction = null,
        int? size = null, byte? precision = null, byte? scale = null)
    {
        if(string.IsNullOrWhiteSpace(name))
            return;
        name = NormalizeName(name);
        if (_sqlParams.ContainsKey(name))
            _sqlParams.Remove(name);
        var param = new SqlParam(name, value, dbType, direction, size, precision, scale);
        _sqlParams.Add(name, param);
    }

    /// <summary>
    /// 获取动态参数列表
    /// </summary>
    public IReadOnlyList<object> GetDynamicParams() => _dynamicParams;

    /// <summary>
    /// 获取值
    /// </summary>
    /// <param name="value">参数值</param>
    /// <param name="operator">运算符</param>
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

    /// <summary>
    /// 获取参数列表
    /// </summary>
    public IReadOnlyDictionary<string, object> GetParams() => new ReadOnlyDictionary<string, object>(_params);

    /// <summary>
    /// 获取参数列表
    /// </summary>
    public IReadOnlyList<SqlParam> GetSqlParams() => _sqlParams.Values.ToList();

    #endregion

    #region Contains(是否包含参数)

    /// <inheritdoc />
    public virtual bool Contains(string name)
    {
        name = NormalizeName(name);
        return _params.ContainsKey(name);
    }

    /// <summary>
    /// 是否包含参数
    /// </summary>
    /// <param name="name">参数名</param>
    public virtual bool ContainsSqlParam(string name)
    {
        name = NormalizeName(name);
        return _sqlParams.ContainsKey(name);
    }

    /// <summary>
    /// 获取参数
    /// </summary>
    /// <param name="name">参数名</param>
    public virtual SqlParam GetParam(string name)
    {
        name = NormalizeName(name);
        return _sqlParams.ContainsKey(name) ? _sqlParams[name] : null;
    }

    #endregion

    #region GetValue(获取参数值)

    /// <inheritdoc />
    public virtual object GetValue(string name)
    {
        name = NormalizeName(name);
        return _params.ContainsKey(name) ? _params[name] : null;
    }

    /// <summary>
    /// 获取参数值
    /// </summary>
    /// <param name="name">参数名</param>
    public object GetParamValue(string name)
    {
        name = NormalizeName(name);
        return _sqlParams.ContainsKey(name) ? _sqlParams[name].Value : null;
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
    public IParameterManager Clone() => new ParameterManager(this);

    #endregion
}

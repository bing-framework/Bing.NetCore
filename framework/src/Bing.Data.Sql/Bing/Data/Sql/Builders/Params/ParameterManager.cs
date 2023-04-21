using System.Collections.ObjectModel;
using Bing.Extensions;

namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// 参数管理器
/// </summary>
public class ParameterManager : IParameterManager
{
    #region 字段

    /// <summary>
    /// 参数集合
    /// </summary>
    private readonly IDictionary<string, object> _params;

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
        _params = new Dictionary<string, object>();
    }

    /// <summary>
    /// 初始化一个<see cref="ParameterManager"/>类型的实例
    /// </summary>
    /// <param name="parameterManager">参数管理器</param>
    protected ParameterManager(ParameterManager parameterManager)
    {
        Dialect = parameterManager.Dialect;
        _paramIndex = parameterManager._paramIndex;
        _params = new Dictionary<string, object>(parameterManager._params);
    }

    #endregion

    #region 属性

    /// <summary>
    /// Sql方言
    /// </summary>
    protected IDialect Dialect { get; }

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

    #endregion

    #region Contains(是否包含参数)

    /// <inheritdoc />
    public virtual bool Contains(string name)
    {
        name = NormalizeName(name);
        return _params.ContainsKey(name);
    }

    #endregion

    #region GetValue(获取参数值)

    /// <inheritdoc />
    public virtual object GetValue(string name)
    {
        name = NormalizeName(name);
        return _params.ContainsKey(name) ? _params[name] : null;
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
    }

    #endregion

    #region Clone(克隆)

    /// <summary>
    /// 克隆
    /// </summary>
    public IParameterManager Clone() => new ParameterManager(this);

    #endregion
}

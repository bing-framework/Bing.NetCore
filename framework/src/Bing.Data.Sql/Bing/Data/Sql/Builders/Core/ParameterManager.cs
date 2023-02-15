﻿using System.Collections.ObjectModel;
using Bing.Extensions;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 参数管理器
/// </summary>
public class ParameterManager : IParameterManager
{
    /// <summary>
    /// 参数集合
    /// </summary>
    private readonly IDictionary<string, object> _params;

    /// <summary>
    /// 参数索引
    /// </summary>
    private int _paramIndex;

    /// <summary>
    /// Sql方言
    /// </summary>
    private readonly IDialect _dialect;

    /// <summary>
    /// 初始化一个<see cref="ParameterManager"/>类型的实例
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    public ParameterManager(IDialect dialect)
    {
        _params = new Dictionary<string, object>();
        _paramIndex = 0;
        _dialect = dialect;
    }

    /// <summary>
    /// 初始化一个<see cref="ParameterManager"/>类型的实例
    /// </summary>
    /// <param name="parameterManager">参数管理器</param>
    protected ParameterManager(ParameterManager parameterManager)
    {
        _params = new Dictionary<string, object>(parameterManager._params);
        _paramIndex = parameterManager._paramIndex;
        _dialect = parameterManager._dialect;
    }

    /// <summary>
    /// 创建参数名
    /// </summary>
    public string GenerateName()
    {
        var result = _dialect.GenerateName(_paramIndex);
        _paramIndex += 1;
        return result;
    }

    /// <summary>
    /// 获取参数列表
    /// </summary>
    public IReadOnlyDictionary<string, object> GetParams() => new ReadOnlyDictionary<string, object>(_params);

    /// <summary>
    /// 添加参数，如果参数已存在则替换
    /// </summary>
    /// <param name="name">参数名</param>
    /// <param name="value">参数值</param>
    /// <param name="operator">运算符</param>
    public void Add(string name, object value, Operator? @operator = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        name = _dialect.GetParamName(name);
        value = _dialect.GetParamValue(value);
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

    /// <summary>
    /// 克隆
    /// </summary>
    public IParameterManager Clone() => new ParameterManager(this);

    /// <summary>
    /// 清空参数
    /// </summary>
    public void Clear()
    {
        _paramIndex = 0;
        _params.Clear();
    }
}
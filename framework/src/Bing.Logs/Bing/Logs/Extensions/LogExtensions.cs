﻿using System.Text;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Logs.Contents;
using Bing.Logs.Properties;

// ReSharper disable once CheckNamespace
namespace Bing.Logs;

/// <summary>
/// 日志扩展
/// </summary>
public static partial class LogExtensions
{
    #region BusinessId(设置业务编号)

    /// <summary>
    /// 设置业务编号
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="businessId">业务编号</param>
    public static ILog BusinessId(this ILog log, string businessId)
    {
        return log.Set<LogContent>(content =>
        {
            if (string.IsNullOrWhiteSpace(content.BusinessId) == false)
                content.BusinessId += ",";
            content.BusinessId += businessId;
        });
    }

    #endregion

    #region Module(设置模块)

    /// <summary>
    /// 设置模块
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="module">业务编号</param>
    public static ILog Module(this ILog log, string module) => log.Set<LogContent>(content => content.Module = module);

    #endregion

    #region Class(设置类名)

    /// <summary>
    /// 设置类名
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="class">类名</param>
    public static ILog Class(this ILog log, string @class) => log.Set<LogContent>(content => content.Class = @class);

    #endregion

    #region Method(设置方法)

    /// <summary>
    /// 设置方法
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="method">方法</param>
    public static ILog Method(this ILog log, string method) => log.Set<LogContent>(content => content.Method = method);

    #endregion

    #region Params(设置参数)

    /// <summary>
    /// 设置参数
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="value">参数值</param>
    public static ILog Params(this ILog log, string value) => log.Set<LogContent>(content => content.AppendLine(content.Params, value));

    /// <summary>
    /// 设置参数
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="name">参数名</param>
    /// <param name="value">参数值</param>
    /// <param name="type">参数类型</param>
    public static ILog Params(this ILog log, string name, string value, string type = null)
    {
        return log.Set<LogContent>(content =>
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                content.AppendLine(content.Params,
                    $"{LogResource.ParameterName}: {name}, {LogResource.ParameterValue}: {value}");
                return;
            }
            content.AppendLine(content.Params,
                $"{LogResource.ParameterType}: {type}, {LogResource.ParameterName}: {name}, {LogResource.ParameterValue}: {value}");
        });
    }

    /// <summary>
    /// 设置参数
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="dictionary">字典</param>
    public static ILog Params(this ILog log, IDictionary<string, object> dictionary)
    {
        if (dictionary == null || dictionary.Count == 0)
            return log;
        foreach (var item in dictionary)
            Params(log, item.Key, item.Value.SafeString());
        return log;
    }

    #endregion

    #region Caption(设置标题)

    /// <summary>
    /// 设置标题
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="caption">标题</param>
    public static ILog Caption(this ILog log, string caption) => log.Set<LogContent>(content => content.Caption = caption);

    /// <summary>
    /// 追加标题
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="caption">标题</param>
    public static ILog AppendCaption(this ILog log, string caption) => log.Set<LogContent>(content => content.Caption = $"{content.Caption}{caption}");

    #endregion

    #region Sql(设置Sql)

    /// <summary>
    /// 设置Sql语句
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="value">值</param>
    public static ILog Sql(this ILog log, string value) => log.Set<LogContent>(content => content.Sql.AppendLine(value));

    /// <summary>
    /// 设置Sql参数
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="value">值</param>
    public static ILog SqlParams(this ILog log, string value) => log.Set<LogContent>(content => content.AppendLine(content.SqlParams, value));

    /// <summary>
    /// 设置Sql参数
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="list">键值对列表</param>
    public static ILog SqlParams(this ILog log, IEnumerable<KeyValuePair<string, object>> list)
    {
        if (list == null)
            return log;
        var dictionary = list.ToList();
        if (dictionary.Count == 0)
            return log;
        var result = new StringBuilder();
        foreach (var item in dictionary)
            result.AppendLine($"    {item.Key} : {GetParamLiterals(item.Value)} : {item.Value?.GetType()},");
        return SqlParams(log, result.ToString().RemoveEnd($",{Common.Line}"));
    }

    /// <summary>
    /// 获取参数字面值
    /// </summary>
    /// <param name="value">参数值</param>
    private static string GetParamLiterals(object value)
    {
        if (value == null)
            return "''";
        switch (value.GetType().Name.ToLower())
        {
            case "boolean":
                return Conv.ToBool(value) ? "1" : "0";
            case "int16":
            case "int32":
            case "int64":
            case "single":
            case "double":
            case "decimal":
                return value.SafeString();
            default:
                return $"'{value}'";
        }
    }

    #endregion

    #region Excption(设置异常)

    /// <summary>
    /// 设置异常
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="exception">异常</param>
    /// <param name="errorCode">错误码</param>
    public static ILog Exception(this ILog log, Exception exception, string errorCode = "")
    {
        if (exception == null)
            return log;
        return log.Set<LogContent>(content =>
        {
            content.Exception = exception;
            content.ErrorCode = errorCode;
        });
    }

    /// <summary>
    /// 设置异常
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="exception">异常</param>
    public static ILog Exception(this ILog log, Warning exception)
    {
        if (exception == null)
            return log;
        return Exception(log, exception, exception.Code);
    }

    #endregion

    #region AddExtraProperty(设置扩展属性)

    /// <summary>
    /// 添加扩展属性
    /// </summary>
    /// <param name="log">日志内容</param>
    /// <param name="name">名称</param>
    /// <param name="value">值</param>
    public static ILog AddExtraProperty(this ILog log, string name, object value) => log.Set<LogContent>(content => content.AddExtraProperty(name, value));

    #endregion

}

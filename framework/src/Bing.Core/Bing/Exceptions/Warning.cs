﻿using Bing.ExceptionHandling;
using Bing.Properties;
using Microsoft.Extensions.Logging;

namespace Bing.Exceptions;

/// <summary>
/// 应用程序异常
/// </summary>
[Serializable]
public class Warning : Exception, IHasErrorCode, IHasHttpStatusCode
{
    #region 构造函数

    /// <summary>
    /// 初始化一个<see cref="Warning"/>类型的实例
    /// </summary>
    /// <param name="message">错误消息</param>
    public Warning(string message) : this(message, null)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="Warning"/>类型的实例
    /// </summary>
    /// <param name="exception">异常</param>
    public Warning(Exception exception) : this(null, null, exception)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="Warning"/>类型的实例
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="code">错误码</param>
    /// <param name="exception">内部异常</param>
    /// <param name="httpStatusCode">Http状态码</param>
    public Warning(string message, string code = null, Exception exception = null, int httpStatusCode = default)
        : base(message ?? "", exception)
    {
        Code = code;
        HttpStatusCode = httpStatusCode;
    }

    #endregion

    #region 属性

    /// <summary>
    /// 错误码
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Http状态码
    /// </summary>
    public int HttpStatusCode { get; set; }

    #endregion

    /// <summary>
    /// 获取错误消息
    /// </summary>
    /// <param name="isProduction">是否生产环境</param>
    public virtual string GetMessage(bool isProduction = true) => GetMessage(this);

    /// <summary>
    /// 获取错误消息
    /// </summary>
    /// <param name="ex">异常</param>
    public static string GetMessage(Exception ex)
    {
        var result = new StringBuilder();
        var list = GetExceptions(ex);
        foreach (var exception in list)
            AppendMessage(result, exception);
        return result.ToString().Trim(Environment.NewLine.ToCharArray());
    }

    /// <summary>
    /// 添加异常消息
    /// </summary>
    /// <param name="result">字符串拼接器</param>
    /// <param name="exception">异常</param>
    private static void AppendMessage(StringBuilder result, Exception exception)
    {
        if (exception == null)
            return;
        result.AppendLine(exception.Message);
    }

    /// <summary>
    /// 获取异常列表
    /// </summary>
    public IList<Exception> GetExceptions() => GetExceptions(this);

    /// <summary>
    /// 获取异常列表
    /// </summary>
    /// <param name="ex">异常</param>
    public static IList<Exception> GetExceptions(Exception ex)
    {
        var result = new List<Exception>();
        AddException(result, ex);
        return result;
    }

    /// <summary>
    /// 添加内部异常
    /// </summary>
    /// <param name="result">异常列表</param>
    /// <param name="exception">异常</param>
    private static void AddException(List<Exception> result, Exception exception)
    {
        if (exception == null)
            return;
        result.Add(exception);
        AddException(result, exception.InnerException);
    }

    /// <summary>
    /// 获取友好提示
    /// </summary>
    /// <param name="level">日志级别</param>
    public string GetPrompt(LogLevel level)
    {
        if (level == LogLevel.Error)
            return R.SystemError;
        return Message;
    }
}

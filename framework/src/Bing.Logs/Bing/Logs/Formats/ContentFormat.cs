﻿using System;
using System.Linq;
using System.Text;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.Logs.Abstractions;
using Bing.Logs.Contents;
using Bing.Logs.Properties;

namespace Bing.Logs.Formats;

/// <summary>
/// 内容格式化器
/// </summary>
public class ContentFormat : ILogFormat
{
    /// <summary>
    /// 格式化
    /// </summary>
    /// <param name="logContent">日志内容</param>
    public string Format(ILogContent logContent)
    {
        if (!(logContent is LogContent))
            return string.Empty;
        return Format((LogContent)logContent);
    }

    /// <summary>
    /// 内容格式化器实例
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public static readonly ILogFormat Instance = new ContentFormat();

    /// <summary>
    /// 格式化
    /// </summary>
    /// <param name="content">日志内容</param>
    protected virtual string Format(LogContent content)
    {
        int line = 1;
        var result = new StringBuilder();
        Line1(result, content, ref line);
        Line2(result, content, ref line);
        Line3(result, content, ref line);
        Line4(result, content, ref line);
        Line5(result, content, ref line);
        Line6(result, content, ref line);
        Line7(result, content, ref line);
        Line8(result, content, ref line);
        Line9(result, content, ref line);
        Line10(result, content, ref line);
        Line11(result, content, ref line);
        Line12(result, content, ref line);
        Line13(result, content, ref line);
        Line14(result, content, ref line);
        Line15(result, content, ref line);
        Finish(result);
        return result.ToString();
    }

    /// <summary>
    /// 添加行
    /// </summary>
    /// <param name="result">拼接器</param>
    /// <param name="content">日志内容</param>
    /// <param name="action">操作</param>
    /// <param name="line">行号</param>
    protected void AppendLine(StringBuilder result, LogContent content, Action<StringBuilder, LogContent> action, ref int line)
    {
        Append(result, content, action, ref line);
        result.AppendLine();
    }

    /// <summary>
    /// 添加行
    /// </summary>
    /// <param name="result">拼接器</param>
    /// <param name="content">日志内容</param>
    /// <param name="action">操作</param>
    /// <param name="line">行号</param>
    protected void Append(StringBuilder result, LogContent content, Action<StringBuilder, LogContent> action, ref int line)
    {
        result.AppendFormat("{0}. ", line++);
        action(result, content);
    }

    /// <summary>
    /// 添加日志内容
    /// </summary>
    /// <param name="result">拼接器</param>
    /// <param name="caption">标题</param>
    /// <param name="value">值</param>
    protected void Append(StringBuilder result, string caption, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        result.AppendFormat("{0}: {1}   ", caption, value);
    }

    /// <summary>
    /// 第1行
    /// </summary>
    /// <param name="result">拼接器</param>
    /// <param name="content">日志内容</param>
    /// <param name="line">行号</param>
    protected void Line1(StringBuilder result, LogContent content, ref int line)
    {
        AppendLine(result, content, (r, c) =>
        {
            r.AppendFormat("{0}: {1} >> ", c.Level, c.LogName);
            r.AppendFormat("{0}: {1}    ", LogResource.LogId, c.LogId);
            r.AppendFormat("{0}: {1}    ", LogResource.OperationTime, c.OperationTime);
            if (string.IsNullOrWhiteSpace(c.Duration))
                return;
            r.AppendFormat("{0}: {1}    ", LogResource.Duration, c.Duration);
        }, ref line);
    }

    /// <summary>
    /// 第2行
    /// </summary>
    /// <param name="result">拼接器</param>
    /// <param name="content">日志内容</param>
    /// <param name="line">行号</param>
    protected void Line2(StringBuilder result, LogContent content, ref int line)
    {
        AppendLine(result, content, (r, c) =>
        {
            Append(r, "IP", c.Ip);
            Append(r, LogResource.Host, c.Host);
            Append(r, LogResource.ThreadId, c.ThreadId);
        }, ref line);
    }

    /// <summary>
    /// 第3行
    /// </summary>
    /// <param name="result">拼接器</param>
    /// <param name="content">日志内容</param>
    /// <param name="line">行号</param>
    protected void Line3(StringBuilder result, LogContent content, ref int line)
    {
        if (string.IsNullOrWhiteSpace(content.Browser))
            return;
        AppendLine(result, content, (r, c) => Append(r, LogResource.Browser, c.Browser), ref line);
    }

    /// <summary>
    /// 第4行
    /// </summary>
    /// <param name="result">拼接器</param>
    /// <param name="content">日志内容</param>
    /// <param name="line">行号</param>
    protected void Line4(StringBuilder result, LogContent content, ref int line)
    {
        if (string.IsNullOrWhiteSpace(content.Url))
            return;
        AppendLine(result, content, (r, c) => r.Append("Url: " + c.Url), ref line);
    }

    /// <summary>
    /// 第5行
    /// </summary>
    /// <param name="result">拼接器</param>
    /// <param name="content">日志内容</param>
    /// <param name="line">行号</param>
    protected void Line5(StringBuilder result, LogContent content, ref int line)
    {
        if (string.IsNullOrWhiteSpace(content.UserId) &&
            string.IsNullOrWhiteSpace(content.Operator) &&
            string.IsNullOrWhiteSpace(content.Role))
            return;
        AppendLine(result, content, (r, c) =>
        {
            Append(r, LogResource.UserId, c.UserId);
            Append(r, LogResource.Operator, c.Operator);
            Append(r, LogResource.Role, c.Role);
        }, ref line);
    }

    /// <summary>
    /// 第6行
    /// </summary>
    /// <param name="result">拼接器</param>
    /// <param name="content">日志内容</param>
    /// <param name="line">行号</param>
    protected void Line6(StringBuilder result, LogContent content, ref int line)
    {
        if (string.IsNullOrWhiteSpace(content.BusinessId) && 
            string.IsNullOrWhiteSpace(content.Tenant) &&
            string.IsNullOrWhiteSpace(content.Application) && 
            string.IsNullOrWhiteSpace(content.Module))
            return;
        AppendLine(result, content, (r, c) =>
        {
            Append(r, LogResource.BusinessId, c.BusinessId);
            Append(r, LogResource.Tenant, c.Tenant);
            Append(r, LogResource.Application, c.Application);
            Append(r, LogResource.Module, c.Module);
        }, ref line);
    }

    /// <summary>
    /// 第7行
    /// </summary>
    /// <param name="result">拼接器</param>
    /// <param name="content">日志内容</param>
    /// <param name="line">行号</param>
    protected void Line7(StringBuilder result, LogContent content, ref int line)
    {
        if (string.IsNullOrWhiteSpace(content.Class) && string.IsNullOrWhiteSpace(content.Method))
            return;
        AppendLine(result, content, (r, c) =>
        {
            Append(r, LogResource.Class, c.Class);
            Append(r, LogResource.Method, c.Method);
        }, ref line);
    }

    /// <summary>
    /// 第8行
    /// </summary>
    /// <param name="result">拼接器</param>
    /// <param name="content">日志内容</param>
    /// <param name="line">行号</param>
    protected void Line8(StringBuilder result, LogContent content, ref int line)
    {
        if (content.Params.Length == 0)
            return;
        Append(result, content, (r, c) =>
        {
            r.AppendLine($"{LogResource.Params}:");
            r.Append(c.Params);
        }, ref line);
    }

    /// <summary>
    /// 第9行
    /// </summary>
    /// <param name="result">拼接器</param>
    /// <param name="content">日志内容</param>
    /// <param name="line">行号</param>
    protected void Line9(StringBuilder result, LogContent content, ref int line)
    {
        if (string.IsNullOrWhiteSpace(content.Caption))
            return;
        AppendLine(result, content, (r, c) =>
        {
            r.AppendFormat("{0}: {1}", LogResource.Caption, c.Caption);
        }, ref line);
    }

    /// <summary>
    /// 第10行
    /// </summary>
    /// <param name="result">拼接器</param>
    /// <param name="content">日志内容</param>
    /// <param name="line">行号</param>
    protected void Line10(StringBuilder result, LogContent content, ref int line)
    {
        if (content.Content.Length == 0)
            return;
        Append(result, content, (r, c) =>
        {
            r.AppendLine($"{LogResource.Content}");
            r.Append(c.Content);
        }, ref line);
    }

    /// <summary>
    /// 第11行
    /// </summary>
    /// <param name="result">拼接器</param>
    /// <param name="content">日志内容</param>
    /// <param name="line">行号</param>
    protected void Line11(StringBuilder result, LogContent content, ref int line)
    {
        if (content.Sql.Length == 0)
            return;
        Append(result, content, (r, c) =>
        {
            r.AppendLine($"{LogResource.Sql}:");
            r.Append(c.Sql);
        }, ref line);
    }

    /// <summary>
    /// 第12行
    /// </summary>
    /// <param name="result">拼接器</param>
    /// <param name="content">日志内容</param>
    /// <param name="line">行号</param>
    protected void Line12(StringBuilder result, LogContent content, ref int line)
    {
        if (content.SqlParams.Length == 0)
            return;
        Append(result, content, (r, c) =>
        {
            r.AppendLine($"{LogResource.SqlParams}:");
            r.Append(c.SqlParams);
        }, ref line);
    }

    /// <summary>
    /// 第13行
    /// </summary>
    /// <param name="result">拼接器</param>
    /// <param name="content">日志内容</param>
    /// <param name="line">行号</param>
    protected void Line13(StringBuilder result, LogContent content, ref int line)
    {
        if (content.Exception == null)
            return;
        AppendLine(result, content, (r, c) =>
        {
            r.AppendLine($"{LogResource.Exception}: {GetExceptionTypes(c.Exception)} {GetErrorCode(c.ErrorCode)}");
            r.Append($"   {Warning.GetMessage(c.Exception)}");
        }, ref line);
    }

    /// <summary>
    /// 获取异常类型列表
    /// </summary>
    /// <param name="exception">异常</param>
    private string GetExceptionTypes(Exception exception) => Warning.GetExceptions(exception).Select(x => x.GetType()).Join();

    /// <summary>
    /// 获取错误码
    /// </summary>
    /// <param name="errorCode">错误码</param>
    private string GetErrorCode(string errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
            return string.Empty;
        return $"-- {LogResource.ErrorCode}: {errorCode}";
    }

    /// <summary>
    /// 第14行
    /// </summary>
    /// <param name="result">拼接器</param>
    /// <param name="content">日志内容</param>
    /// <param name="line">行号</param>
    protected void Line14(StringBuilder result, LogContent content, ref int line)
    {
        if (content.Exception == null)
            return;
        AppendLine(result, content, (r, c) =>
        {
            r.AppendLine($"{LogResource.StackTrace}:");
            r.Append(c.Exception.StackTrace);
        }, ref line);
    }

    /// <summary>
    /// 第15行
    /// </summary>
    /// <param name="result">拼接器</param>
    /// <param name="content">日志内容</param>
    /// <param name="line">行号</param>
    protected void Line15(StringBuilder result, LogContent content, ref int line)
    {
        if (!content.ExtraProperties.Any())
            return;
        AppendLine(result, content, (r, c) =>
        {
            r.AppendLine($"{LogResource.ExtraProperties}:");
            foreach (var keyValue in content.ExtraProperties)
                r.AppendLine($"{keyValue.Key}: {keyValue.Value}");
        }, ref line);
    }

    /// <summary>
    /// 结束
    /// </summary>
    /// <param name="result">拼接器</param>
    protected void Finish(StringBuilder result)
    {
        for (var i = 0; i < 125; i++) 
            result.Append("-");
        result.AppendLine();
    }
}

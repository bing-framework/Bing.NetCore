﻿using System.Text;
using Bing.Extensions;
using Bing.Logs.Abstractions;

// ReSharper disable once CheckNamespace
namespace Bing.Logs;

/// <summary>
/// 日志内容 扩展
/// </summary>
public static partial class LogContentExtensions
{
    /// <summary>
    /// 追加内容
    /// </summary>
    /// <param name="content">日志内容</param>
    /// <param name="result">拼接字符串</param>
    /// <param name="value">值</param>
    public static void Append(this ILogContent content, StringBuilder result, string value)
    {
        if (value.IsEmpty())
            return;
        result.Append(value);
    }

    /// <summary>
    /// 追加内容并换行
    /// </summary>
    /// <param name="content">日志内容</param>
    /// <param name="result">拼接字符串</param>
    /// <param name="value">值</param>
    public static void AppendLine(this ILogContent content, StringBuilder result, string value)
    {
        content.Append(result, value);
        result.AppendLine();
    }

    /// <summary>
    /// 设置内容并换行
    /// </summary>
    /// <param name="content">日志内容</param>
    /// <param name="value">值</param>
    public static void Content(this ILogContent content, string value) => content.AppendLine(content.Content, value);

    /// <summary>
    /// 添加扩展属性
    /// </summary>
    /// <param name="content">日志内容</param>
    /// <param name="name">名称</param>
    /// <param name="value">值</param>
    public static void AddExtraProperty(this ILogContent content, string name, object value) => content.ExtraProperties[name] = value;
}

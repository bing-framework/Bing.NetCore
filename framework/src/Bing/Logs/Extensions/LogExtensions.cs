﻿using Bing.Extensions;
using Bing.Logs.Abstractions;

// ReSharper disable once CheckNamespace
namespace Bing.Logs;

/// <summary>
/// 日志操作 扩展
/// </summary>
public static partial class LogExtensions
{
    #region Content(设置内容)

    /// <summary>
    /// 设置内容
    /// </summary>
    /// <param name="log">日志操作</param>
    public static ILog Content(this ILog log) => log.Set<ILogContent>(content => content.Content(""));

    /// <summary>
    /// 设置内容
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="value">值</param>
    public static ILog Content(this ILog log, string value) => log.Set<ILogContent>(content => content.Content(value));

    /// <summary>
    /// 设置内容
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="dictionary">字典</param>
    public static ILog Content(this ILog log, IDictionary<string, object> dictionary)
    {
        if (dictionary == null)
            return log;
        return Content(log, dictionary.ToDictionary(t => t.Key, t => t.Value.SafeString()));
    }

    /// <summary>
    /// 设置内容
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="dictionary">字典</param>
    public static ILog Content(this ILog log, IDictionary<string, string> dictionary)
    {
        if (dictionary == null)
            return log;
        foreach (var keyValue in dictionary)
            log.Set<ILogContent>(content => content.Content($"{keyValue.Key} : {keyValue.Value}"));
        return log;
    }

    #endregion

    #region Tag(设置标签)

    /// <summary>
    /// 设置标签
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="tag">标签</param>
    public static ILog Tag(this ILog log, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return log;
        log.Set<ILogContent>(content => content.Tags.Add(tag));
        return log;
    }

    /// <summary>
    /// 设置标签
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="tags">标签</param>
    public static ILog Tags(this ILog log, params string[] tags)
    {
        if (tags == null)
            return log;
        log.Set<ILogContent>(content => content.Tags.AddRange(tags));
        return log;
    }

    #endregion
}

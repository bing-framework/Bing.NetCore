﻿using System.Reflection;

namespace Bing.Localization;

/// <summary>
/// 本地化资源类型
/// </summary>
public class LocalizedTypeAttribute : Attribute
{
    /// <summary>
    /// 初始化一个<see cref="LocalizedTypeAttribute"/>类型的实例
    /// </summary>
    /// <param name="type">本地化资源类型</param>
    public LocalizedTypeAttribute(string type) => Type = type;

    /// <summary>
    /// 本地化资源类型
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// 获取本地化资源类型，如果未设置则返回类型名
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    public static string GetResourceType<T>() => GetResourceType(typeof(T));

    /// <summary>
    /// 获取本地化资源类型，如果未设置则返回类型名
    /// </summary>
    /// <param name="type">类型</param>
    public static string GetResourceType(Type type)
    {
        if (type == null)
            return null;
        var attribute = type.GetCustomAttribute<LocalizedTypeAttribute>();
        return attribute == null || string.IsNullOrWhiteSpace(attribute.Type) ? type.Name : attribute.Type;
    }
}

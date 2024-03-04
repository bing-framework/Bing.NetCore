﻿namespace Bing.Caching;

/// <summary>
/// 缓存键
/// </summary>
public class CacheKey
{
    /// <summary>
    /// 缓存键
    /// </summary>
    private string _key;

    /// <summary>
    /// 初始化一个<see cref="CacheKey"/>类型的实例
    /// </summary>
    public CacheKey() { }

    /// <summary>
    /// 初始化一个<see cref="CacheKey"/>类型的实例
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="parameters">缓存键参数</param>
    public CacheKey(string key, params object[] parameters) => _key = string.Format(key, parameters);

    /// <summary>
    /// 缓存键
    /// </summary>
    public string Key
    {
        get => ToString();
        set => _key = value;
    }

    /// <summary>
    /// 缓存键前缀
    /// </summary>
    public string Prefix { get; set; }

    /// <summary>
    /// 获取缓存键
    /// </summary>
    public override string ToString() => $"{Prefix}{_key}";
}

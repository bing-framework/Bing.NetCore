using Bing.Helpers;
using Bing.Text;

namespace Bing.Caching;

/// <summary>
/// 缓存名称 特性
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct)]
public class CacheNameAttribute : Attribute
{
    /// <summary>
    /// 初始化一个<see cref="CacheNameAttribute"/>类型的实例
    /// </summary>
    /// <param name="name">名称</param>
    public CacheNameAttribute(string name)
    {
        Check.NotNull(name, nameof(name));
        Name = name;
    }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 获取给定类型的缓存名称
    /// </summary>
    /// <typeparam name="TCacheItem">缓存项的类型</typeparam>
    /// <returns>
    /// 如果指定类型上存在<see cref="CacheNameAttribute"/>特性，则返回该特性指定的缓存名称；
    /// 否则，返回类型的全名去除"CacheItem"后缀后的结果。
    /// </returns>
    public static string GetCacheName<TCacheItem>() => GetCacheName(typeof(TCacheItem));

    /// <summary>
    /// 获取给定类型的缓存名称
    /// </summary>
    /// <param name="cacheItemType">缓存项的类型</param>
    /// <returns>
    /// 如果指定类型上存在<see cref="CacheNameAttribute"/>特性，则返回该特性指定的缓存名称；
    /// 否则，返回类型的全名去除"CacheItem"后缀后的结果。
    /// </returns>
    public static string GetCacheName(Type cacheItemType)
    {
        var cacheNameAttribute = cacheItemType
            .GetCustomAttributes(true)
            .OfType<CacheNameAttribute>()
            .FirstOrDefault();
        if (cacheNameAttribute != null)
            return cacheNameAttribute.Name;
        return cacheItemType.FullName!.RemoveEnd("CacheItem")!;
    }
}

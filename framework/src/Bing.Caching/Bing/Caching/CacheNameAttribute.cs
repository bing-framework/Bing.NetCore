using Bing.Helpers;
using Bing.Text;

namespace Bing.Caching;

/// <summary>
/// 为缓存项类型指定缓存名称覆盖值。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct)]
public class CacheNameAttribute : Attribute
{
    /// <summary>
    /// 使用指定缓存名称初始化 <see cref="CacheNameAttribute"/> 的实例。
    /// </summary>
    /// <param name="name">覆盖默认类型名推导规则的缓存名称。</param>
    public CacheNameAttribute(string name)
    {
        Check.NotNull(name, nameof(name));
        Name = name;
    }

    /// <summary>
    /// 获取应用于目标缓存项类型的缓存名称覆盖值。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 获取指定缓存项类型的最终缓存名称。
    /// </summary>
    /// <typeparam name="TCacheItem">要解析缓存名称的缓存项类型。</typeparam>
    /// <returns>
    /// 指定类型存在 <see cref="CacheNameAttribute"/> 时返回该特性指定的名称；否则返回移除 <c>CacheItem</c> 后缀后的类型全名。
    /// </returns>
    public static string GetCacheName<TCacheItem>() => GetCacheName(typeof(TCacheItem));

    /// <summary>
    /// 获取指定缓存项类型的最终缓存名称。
    /// </summary>
    /// <param name="cacheItemType">要解析缓存名称的缓存项类型。</param>
    /// <returns>
    /// 指定类型存在 <see cref="CacheNameAttribute"/> 时返回该特性指定的名称；否则返回移除 <c>CacheItem</c> 后缀后的类型全名。
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

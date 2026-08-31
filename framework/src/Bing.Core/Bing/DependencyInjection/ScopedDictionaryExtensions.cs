namespace Bing.DependencyInjection;

/// <summary>
/// 作用域字典(<see cref="ScopedDictionary"/>) 扩展
/// </summary>
public static class ScopedDictionaryExtensions
{
    /// <summary>
    /// 从Scoped字典获取指定类型的值
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="dictionary">字典</param>
    /// <param name="key">键名</param>
    /// <returns>键存在且值可转换为 <typeparamref name="T"/> 时返回该值，否则返回默认值。</returns>
    public static T GetValue<T>(this ScopedDictionary dictionary, string key) where T : class
    {
        if (dictionary.TryGetValue(key, out var obj))
            return obj as T;
        return default;
    }
}

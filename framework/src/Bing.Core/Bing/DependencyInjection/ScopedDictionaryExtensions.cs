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
    public static T GetValue<T>(this ScopedDictionary dictionary, string key) where T : class
    {
        if (dictionary.TryGetValue(key, out var obj))
            return obj as T;
        return default;
    }
}

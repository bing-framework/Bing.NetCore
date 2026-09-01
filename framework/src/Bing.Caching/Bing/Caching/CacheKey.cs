namespace Bing.Caching;

/// <summary>
/// 表示可由前缀和格式化参数组成的缓存键，并负责生成最终物理键文本。
/// </summary>
public class CacheKey
{
    /// <summary>
    /// 存储不包含 <see cref="Prefix"/> 的缓存键文本。
    /// </summary>
    private string _key;

    /// <summary>
    /// 初始化一个 <see cref="CacheKey"/> 类型的实例。
    /// </summary>
    public CacheKey() { }

    /// <summary>
    /// 初始化一个 <see cref="CacheKey"/> 类型的实例。
    /// </summary>
    /// <param name="key">包含 <see cref="string.Format(string,object[])"/> 占位符的缓存键格式字符串。</param>
    /// <param name="parameters">用于填充缓存键格式字符串的参数；参数格式化遵循当前区域性和 <see cref="string.Format(string,object[])"/> 的规则。</param>
    /// <remarks>参数在构造时格式化并保存为键文本。</remarks>
    public CacheKey(string key, params object[] parameters) => _key = string.Format(key, parameters);

    /// <summary>
    /// 获取或设置包含前缀的最终缓存键；读取时返回 <see cref="Prefix"/> 与内部键文本的拼接结果，写入时仅替换内部键文本。
    /// </summary>
    public string Key
    {
        get => ToString();
        set => _key = value;
    }

    /// <summary>
    /// 获取或设置直接拼接到最终缓存键前的命名空间前缀；调用方负责提供所需的分隔符。
    /// </summary>
    public string Prefix { get; set; }

    /// <summary>
    /// 返回由前缀和缓存键文本直接拼接形成的最终缓存键，不会自动添加分隔符或再次格式化参数。
    /// </summary>
    /// <returns>可用于缓存实现的最终缓存键。</returns>
    public override string ToString() => $"{Prefix}{_key}";
}

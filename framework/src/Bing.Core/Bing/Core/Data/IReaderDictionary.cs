namespace Bing.Core.Data;

/// <summary>
/// 读取字典
/// </summary>
/// <typeparam name="TKey">键类型</typeparam>
/// <typeparam name="TValue">值类型</typeparam>
public interface IReaderDictionary<in TKey, out TValue>
{
    /// <summary>
    /// 获取指定键对应的值。
    /// </summary>
    /// <param name="key">键</param>
    /// <returns>与指定键关联的值。</returns>
    TValue Reader(TKey key);
}

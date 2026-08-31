namespace Bing.Core.Data;

/// <summary>
/// 定义按键读取值的能力。
/// </summary>
/// <typeparam name="TKey">用于读取的键类型。</typeparam>
/// <typeparam name="TValue">读取结果的值类型。</typeparam>
public interface IGetable<in TKey, out TValue>
{
    /// <summary>
    /// 根据键读取值。
    /// </summary>
    /// <param name="key">要读取的键。</param>
    /// <returns>与键关联的值。</returns>
    TValue Get(TKey key);
}

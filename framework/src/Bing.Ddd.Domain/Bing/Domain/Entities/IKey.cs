namespace Bing.Domain.Entities;

/// <summary>
/// 定义具有只读标识的对象。
/// </summary>
/// <typeparam name="TKey">可协变返回的标识类型。</typeparam>
public interface IKey<out TKey>
{
    /// <summary>
    /// 获取对象标识。
    /// </summary>
    TKey Id { get; }
}
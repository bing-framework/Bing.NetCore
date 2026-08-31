namespace Bing.DependencyInjection;

/// <summary>
/// 提供可写对象槽位的 <see cref="IObjectAccessor{T}"/> 默认实现。
/// </summary>
/// <typeparam name="T">保存对象的类型。</typeparam>
public class ObjectAccessor<T> : IObjectAccessor<T>
{
    /// <summary>
    /// 获取或设置当前对象值。
    /// </summary>
    public T Value { get; set; }

    /// <summary>
    /// 初始化 <see cref="ObjectAccessor{T}"/> 的空实例。
    /// </summary>
    public ObjectAccessor() { }

    /// <summary>
    /// 使用初始对象值初始化 <see cref="ObjectAccessor{T}"/> 的实例。
    /// </summary>
    /// <param name="value">要保存的初始对象值。</param>
    public ObjectAccessor(T value) => Value = value;
}

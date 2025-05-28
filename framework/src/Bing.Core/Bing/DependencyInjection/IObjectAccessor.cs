namespace Bing.DependencyInjection;

/// <summary>
/// 对象访问器
/// </summary>
/// <typeparam name="T">类型</typeparam>
public interface IObjectAccessor<out T>
{
    /// <summary>
    /// 值
    /// </summary>
    T Value { get; }
}

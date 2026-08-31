namespace Bing.DependencyInjection;

/// <summary>
/// 定义只读访问指定对象的访问器。
/// </summary>
/// <typeparam name="T">可协变返回的对象类型。</typeparam>
public interface IObjectAccessor<out T>
{
    /// <summary>
    /// 获取当前对象值；引用类型尚未设置时可能为 <c>null</c>。
    /// </summary>
    T Value { get; }
}

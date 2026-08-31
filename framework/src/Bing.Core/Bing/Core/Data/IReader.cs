namespace Bing.Core.Data;

/// <summary>
/// 定义读取数据对象的能力。
/// </summary>
/// <typeparam name="T">读取数据的类型。</typeparam>
public interface IReader<out T>
{
    /// <summary>
    /// 读取数据对象。
    /// </summary>
    /// <returns>由实现提供的数据对象。</returns>
    T Reader();
}

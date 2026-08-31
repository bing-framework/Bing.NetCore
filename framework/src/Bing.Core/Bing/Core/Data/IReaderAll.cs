namespace Bing.Core.Data;

/// <summary>
/// 定义读取全部项目的能力。
/// </summary>
/// <typeparam name="T">项目类型。</typeparam>
public interface IReaderAll<T>
{
    /// <summary>
    /// 读取全部项目。
    /// </summary>
    /// <returns>由实现提供的项目列表。</returns>
    IList<T> ReaderAll();
}

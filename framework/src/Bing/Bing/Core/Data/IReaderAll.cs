namespace Bing.Core.Data;

/// <summary>
/// 读取全部
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public interface IReaderAll<T>
{
    /// <summary>
    /// 读取全部
    /// </summary>
    IList<T> ReaderAll();
}

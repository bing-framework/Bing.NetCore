namespace Bing.Core.Data;

/// <summary>
/// 读取
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public interface IReader<out T>
{
    /// <summary>
    /// 读取
    /// </summary>
    T Reader();
}

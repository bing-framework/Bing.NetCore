using Microsoft.Extensions.Logging;

namespace Bing.Logging;

/// <summary>
/// 初始化日志记录器
/// </summary>
/// <typeparam name="T">类型</typeparam>
public interface IInitLogger<out T> : ILogger<T>
{
    /// <summary>
    /// 获取条目列表
    /// </summary>
    public List<BingInitLogEntry> Entries { get; }
}

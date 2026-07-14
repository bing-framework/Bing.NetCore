using System.Collections.ObjectModel;

namespace Bing.Logging;

/// <summary>
/// 业务日志上下文
/// </summary>
public sealed class BusinessLogContext
{
    /// <summary>
    /// 初始化一个<see cref="BusinessLogContext"/>类型的实例
    /// </summary>
    public BusinessLogContext(
        string businessTraceId = null,
        IEnumerable<string> tags = null,
        IDictionary<string, object> data = null)
    {
        BusinessTraceId = businessTraceId;
        Tags = new ReadOnlyCollection<string>((tags ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList());
        Data = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(data ?? new Dictionary<string, object>()));
    }

    /// <summary>
    /// 业务跟踪标识
    /// </summary>
    public string BusinessTraceId { get; }

    /// <summary>
    /// 标签
    /// </summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>
    /// 扩展数据
    /// </summary>
    public IReadOnlyDictionary<string, object> Data { get; }
}
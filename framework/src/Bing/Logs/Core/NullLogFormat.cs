using Bing.Logs.Abstractions;

namespace Bing.Logs.Core;

/// <summary>
/// 空日志格式器
/// </summary>
public class NullLogFormat : ILogFormat
{
    /// <summary>
    /// 空日志格式器实例
    /// </summary>
    public static ILogFormat Instance { get; } = new NullLogFormat();

    /// <summary>
    /// 初始化一个<see cref="NullLogFormat"/>类型的实例
    /// </summary>
    private NullLogFormat() { }

    /// <summary>
    /// 格式化
    /// </summary>
    /// <param name="content">日志内容</param>
    public string Format(ILogContent content) => "";
}

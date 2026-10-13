namespace Bing.Logging.Core.Callers;

/// <summary>
/// 日志调用者信息
/// </summary>
public readonly struct LogCallerInfo : ILogCallerInfo
{
    /// <summary>
    /// 初始化一个 <see cref="LogCallerInfo"/> 类型的实例。
    /// </summary>
    /// <param name="memberName">成员名称（方法名）</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="lineNumber">行号</param>
    public LogCallerInfo(string memberName, string filePath = null, int lineNumber = 0)
    {
        MemberName = memberName;
        FilePath = filePath;
        LineNumber = lineNumber;
    }

    /// <inheritdoc />
    public string MemberName { get; }

    /// <inheritdoc />
    public string FilePath { get; }

    /// <inheritdoc />
    public int LineNumber { get; }

    /// <inheritdoc />
    public dynamic ToParams() => new { MemberName, FilePath, LineNumber };
}
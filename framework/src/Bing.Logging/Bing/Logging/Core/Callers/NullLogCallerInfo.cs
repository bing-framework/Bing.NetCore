namespace Bing.Logging.Core.Callers;

/// <summary>
/// 空日志调用者信息
/// </summary>
public struct NullLogCallerInfo : ILogCallerInfo
{
    /// <summary>
    /// 空日志调用者信息实例
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public static readonly NullLogCallerInfo Instance = new NullLogCallerInfo();

    /// <inheritdoc />
    public string MemberName => null;

    /// <inheritdoc />
    public string FilePath => null;

    /// <inheritdoc />
    public int LineNumber => 0;

    /// <inheritdoc />
    public dynamic ToParams() => null;
}
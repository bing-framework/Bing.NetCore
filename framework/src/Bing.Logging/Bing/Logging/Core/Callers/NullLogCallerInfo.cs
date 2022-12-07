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

    /// <summary>
    /// 成员名称（方法名）
    /// </summary>
    public string MemberName => null;

    /// <summary>
    /// 文件路径
    /// </summary>
    public string FilePath => null;

    /// <summary>
    /// 行号
    /// </summary>
    public int LineNumber => 0;

    /// <summary>
    /// 转换为参数
    /// </summary>
    public dynamic ToParams() => null;
}
namespace Bing.Logging.Core.Callers;

/// <summary>
/// 日志调用者信息
/// </summary>
public interface ILogCallerInfo
{
    /// <summary>
    /// 成员名称（方法名）
    /// </summary>
    string MemberName { get; }

    /// <summary>
    /// 文件路径
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// 行号
    /// </summary>
    int LineNumber { get; }

    /// <summary>
    /// 转换为参数
    /// </summary>
    dynamic ToParams();
}
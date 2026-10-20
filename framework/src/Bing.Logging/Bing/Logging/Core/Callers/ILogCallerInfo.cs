namespace Bing.Logging.Core.Callers;

/// <summary>
/// 日志调用者信息
/// </summary>
public interface ILogCallerInfo
{
    /// <summary>
    /// 获取成员名称（方法名）
    /// </summary>
    string MemberName { get; }

    /// <summary>
    /// 获取文件路径
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// 获取行号
    /// </summary>
    int LineNumber { get; }

    /// <summary>
    /// 转换为参数
    /// </summary>
    /// <returns>包含调用成员名称、文件路径和行号的动态参数对象。</returns>
    dynamic ToParams();
}
namespace Bing.Auditing;

/// <summary>
/// 审计序列化器
/// </summary>
public interface IAuditSerializer
{
    /// <summary>
    /// 将审计日志序列化为字符串。
    /// </summary>
    /// <param name="obj">对象</param>
    /// <returns>对象序列化后的文本。</returns>
    string Serialize(object obj);
}

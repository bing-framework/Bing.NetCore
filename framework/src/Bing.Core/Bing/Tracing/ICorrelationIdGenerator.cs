namespace Bing.Tracing;

/// <summary>
/// 关联标识生成器
/// </summary>
public interface ICorrelationIdGenerator
{
    /// <summary>
    /// 创建关联标识
    /// </summary>
    string Create();
}
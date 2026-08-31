namespace Bing.Domain.Entities;

/// <summary>
/// 为实体提供乐观并发控制版本。
/// </summary>
public interface IVersion
{
    /// <summary>
    /// 获取或设置用于乐观并发检查的版本值。
    /// </summary>
    byte[] Version { get; set; }
}

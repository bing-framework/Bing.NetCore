using Bing.Data;

namespace Bing.Auditing;

/// <summary>
/// 定义支持软删除并记录删除时间的审计契约。
/// </summary>
public interface IHasDeletionTime : ISoftDelete
{
    /// <summary>
    /// 获取或设置实体的删除时间；未删除时为 <c>null</c>。
    /// </summary>
    DateTime? DeletionTime { get; set; }
}

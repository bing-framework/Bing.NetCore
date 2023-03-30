namespace Bing.Data;

/// <summary>
/// 逻辑删除
/// </summary>
public interface ISoftDelete
{
    /// <summary>
    /// 是否已删除
    /// </summary>
    bool IsDeleted { get; set; }
}

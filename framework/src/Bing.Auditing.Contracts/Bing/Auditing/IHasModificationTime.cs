namespace Bing.Auditing;

/// <summary>
/// 定义具有可空最后修改时间的审计契约。
/// </summary>
public interface IHasModificationTime
{
    /// <summary>
    /// 获取或设置实体最后一次修改的时间；尚未修改或未填充时为 <c>null</c>。
    /// </summary>
    DateTime? LastModificationTime { get; set; }
}

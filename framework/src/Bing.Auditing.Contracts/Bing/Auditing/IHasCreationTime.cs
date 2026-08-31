namespace Bing.Auditing;

/// <summary>
/// 定义具有可空创建时间的审计契约。
/// </summary>
public interface IHasCreationTime
{
    /// <summary>
    /// 获取或设置实体的创建时间；尚未创建或未填充时为 <c>null</c>。
    /// </summary>
    DateTime? CreationTime { get; set; }
}

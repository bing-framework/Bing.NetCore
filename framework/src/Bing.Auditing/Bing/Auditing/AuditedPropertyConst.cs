namespace Bing.Auditing;

/// <summary>
/// 定义审计字段映射默认使用的可配置属性名称。
/// </summary>
public static class AuditedPropertyConst
{
    /// <summary>
    /// 获取或设置创建人名称对应的默认属性名。
    /// </summary>
    /// <remarks>
    /// 默认值为 <c>Creator</c>。
    /// </remarks>
    public static string Creator { get; set; } = "Creator";

    /// <summary>
    /// 获取或设置创建人标识对应的默认属性名。
    /// </summary>
    /// <remarks>
    /// 默认值为 <c>CreatorId</c>。
    /// </remarks>
    public static string CreatorId { get; set; } = "CreatorId";

    /// <summary>
    /// 获取或设置创建时间对应的默认属性名。
    /// </summary>
    /// <remarks>
    /// 默认值为 <c>CreationTime</c>。
    /// </remarks>
    public static string CreationTime { get; set; } = "CreationTime";

    /// <summary>
    /// 获取或设置最后修改人名称对应的默认属性名。
    /// </summary>
    /// <remarks>
    /// 默认值为 <c>LastModifier</c>。
    /// </remarks>
    public static string Modifier { get; set; } = "LastModifier";

    /// <summary>
    /// 获取或设置最后修改人标识对应的默认属性名。
    /// </summary>
    /// <remarks>
    /// 默认值为 <c>LastModifierId</c>。
    /// </remarks>
    public static string ModifierId { get; set; } = "LastModifierId";

    /// <summary>
    /// 获取或设置最后修改时间对应的默认属性名。
    /// </summary>
    /// <remarks>
    /// 默认值为 <c>LastModificationTime</c>。
    /// </remarks>
    public static string ModificationTime { get; set; } = "LastModificationTime";

    /// <summary>
    /// 获取或设置乐观并发版本字段对应的默认属性名。
    /// </summary>
    /// <remarks>
    /// 默认值为 <c>Version</c>。
    /// </remarks>
    public static string Version { get; set; } = "Version";
}
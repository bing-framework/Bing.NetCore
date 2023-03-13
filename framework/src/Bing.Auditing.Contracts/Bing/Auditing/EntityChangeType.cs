using System.ComponentModel;

namespace Bing.Auditing;

/// <summary>
/// 实体变更类型
/// </summary>
public enum EntityChangeType
{
    /// <summary>
    /// 创建
    /// </summary>
    [Description("创建")]
    Created = 0,

    /// <summary>
    /// 更新
    /// </summary>
    [Description("更新")]
    Updated = 1,

    /// <summary>
    /// 删除
    /// </summary>
    [Description("删除")]
    Deleted = 2
}

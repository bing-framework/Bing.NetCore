using System.ComponentModel;

namespace Bing.Auditing;

/// <summary>
/// 标识实体在审计范围内发生的持久化变更类型。
/// </summary>
public enum EntityChangeType
{
    /// <summary>
    /// 实体在本次操作中创建。
    /// </summary>
    [Description("创建")]
    Created = 0,

    /// <summary>
    /// 实体在本次操作中更新。
    /// </summary>
    [Description("更新")]
    Updated = 1,

    /// <summary>
    /// 实体在本次操作中删除。
    /// </summary>
    [Description("删除")]
    Deleted = 2
}

namespace Bing.Auditing;

/// <summary>
/// 定义实体审计字段的填充操作。
/// </summary>
public interface IAuditPropertySetter
{
    /// <summary>
    /// 设置创建审计字段。
    /// </summary>
    /// <param name="targetObject">目标实体。</param>
    void SetCreationProperties(object targetObject);

    /// <summary>
    /// 设置修改审计字段。
    /// </summary>
    /// <param name="targetObject">目标实体。</param>
    void SetModificationProperties(object targetObject);

    /// <summary>
    /// 设置删除审计字段。
    /// </summary>
    /// <param name="targetObject">目标实体。</param>
    void SetDeletionProperties(object targetObject);
}

namespace Bing.Auditing;

/// <summary>
/// 审计属性设置器
/// </summary>
public interface IAuditPropertySetter
{
    /// <summary>
    /// 设置创建属性
    /// </summary>
    /// <param name="targetObject">目标对象</param>
    void SetCreationProperties(object targetObject);

    /// <summary>
    /// 设置修改属性
    /// </summary>
    /// <param name="targetObject">目标对象</param>
    void SetModificationProperties(object targetObject);

    /// <summary>
    /// 设置删除属性
    /// </summary>
    /// <param name="targetObject">目标对象</param>
    void SetDeletionProperties(object targetObject);
}
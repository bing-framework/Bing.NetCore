using Bing.Auditing;

namespace Bing.Data;

/// <summary>
/// 逻辑删除(<see cref="ISoftDelete"/>) 扩展
/// </summary>
public static partial class SoftDeleteExtensions
{
    /// <summary>
    /// 是否空对象或是否已删除
    /// </summary>
    /// <param name="entity">逻辑删除实体</param>
    public static bool IsNullOrDeleted(this ISoftDelete entity) => entity == null || entity.IsDeleted;

    /// <summary>
    /// 取消删除。将<see cref="ISoftDelete.IsDeleted"/>设为false，并且将<see cref="IDeletionAuditedObject"/>属性设置为空
    /// </summary>
    /// <param name="entity">逻辑删除实体</param>
    public static void UnDelete(this ISoftDelete entity)
    {
        entity.IsDeleted = false;
        if (entity is IDeletionAuditedObject deletionAuditedEntity)
        {
            deletionAuditedEntity.DeletionTime = null;
            deletionAuditedEntity.DeleterId = null;
        }
    }
}
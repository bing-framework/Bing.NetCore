using Bing.Auditing;

namespace Bing.Domains.Entities
{
    /// <summary>
    /// 实体扩展
    /// </summary>
    public static partial class EntityExtensions
    {
        /// <summary>
        /// 是否空对象或是否已删除
        /// </summary>
        /// <param name="entity">逻辑删除实体</param>
        public static bool IsNullOrDeleted(this IDelete entity) => entity == null || entity.IsDeleted;

        /// <summary>
        /// 取消删除。将<see cref="IDelete.IsDeleted"/>设为false，并且将<see cref="IDeletionAuditedObject"/>属性设置为空
        /// </summary>
        /// <param name="entity">逻辑删除实体</param>
        public static void UnDelete(this IDelete entity)
        {
            entity.IsDeleted = false;
            if (entity is IDeletionAuditedObject deletionAuditedEntity)
            {
                deletionAuditedEntity.DeletionTime = null;
                deletionAuditedEntity.DeleterId = null;
            }
        }
    }
}

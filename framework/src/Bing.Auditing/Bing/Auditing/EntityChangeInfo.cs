using Bing.Data.ObjectExtending;

namespace Bing.Auditing;

/// <summary>
/// 实体变更信息
/// </summary>
[Serializable]
public class EntityChangeInfo : IHasExtraProperties
{
    /// <summary>
    /// 变更时间
    /// </summary>
    /// <remarks>当实体被改变的时间。</remarks>
    public DateTime ChangeTime { get; set; }

    /// <summary>
    /// 变更类型
    /// </summary>
    public EntityChangeInfo ChangeType { get; set; }

    /// <summary>
    /// 实体租户ID
    /// </summary>
    /// <remarks>实体所属的租户ID。</remarks>
    public string EntityTenantId { get; set; }

    /// <summary>
    /// 实体ID
    /// </summary>
    /// <remarks>变更实体的ID</remarks>
    public string EntityId { get; set; }

    /// <summary>
    /// 实体类型全名称
    /// </summary>
    /// <remarks>实体类型的完整命名空间。</remarks>
    public string EntityTypeFullName { get; set; }

    /// <summary>
    /// 实体变更属性列表
    /// </summary>
    public List<EntityPropertyChangeInfo> PropertyChanges { get; set; }

    /// <summary>
    /// 合并
    /// </summary>
    /// <param name="changeInfo">实体变更信息</param>
    public virtual void Merge(EntityChangeInfo changeInfo)
    {
        foreach (var propertyChange in changeInfo.PropertyChanges)
        {
            var existingChange = PropertyChanges.FirstOrDefault(p => p.PropertyName == propertyChange.PropertyName);
            if (existingChange == null)
                PropertyChanges.Add(propertyChange);
            else
                existingChange.NewValue = propertyChange.NewValue;
        }
    }
}

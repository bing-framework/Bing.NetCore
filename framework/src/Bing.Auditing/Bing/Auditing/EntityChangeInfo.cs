using Bing.Data.ObjectExtending;

namespace Bing.Auditing;

/// <summary>
/// 表示一次实体变更及其属性级变更明细。
/// </summary>
[Serializable]
public class EntityChangeInfo : IHasExtraProperties
{
    /// <summary>
    /// 获取或设置实体变更发生的时间。
    /// </summary>
    /// <remarks>用于记录实体变更事件的时间点。</remarks>
    public DateTime ChangeTime { get; set; }

    /// <summary>
    /// 获取或设置实体变更类型。
    /// </summary>
    public EntityChangeInfo ChangeType { get; set; }

    /// <summary>
    /// 获取或设置发生变更的实体所属租户标识。
    /// </summary>
    /// <remarks>非多租户实体或无法解析租户时可为空。</remarks>
    public string EntityTenantId { get; set; }

    /// <summary>
    /// 获取或设置发生变更的实体标识文本。
    /// </summary>
    /// <remarks>以字符串保存，以兼容不同实体标识类型。</remarks>
    public string EntityId { get; set; }

    /// <summary>
    /// 获取或设置发生变更的实体类型全名。
    /// </summary>
    /// <remarks>包含命名空间的 CLR 类型名称，用于审计记录中的类型识别。</remarks>
    public string EntityTypeFullName { get; set; }

    /// <summary>
    /// 获取或设置该实体的属性级变更列表。
    /// </summary>
    public List<EntityPropertyChangeInfo> PropertyChanges { get; set; }

    /// <summary>
    /// 将另一份同一实体的属性变更合并到当前记录。
    /// </summary>
    /// <param name="changeInfo">要合并的实体变更信息；同名属性已存在时使用其最新值。</param>
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

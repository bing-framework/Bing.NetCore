using Bing.Extensions;

namespace Bing.Auditing;

/// <summary>
/// 初始化实体的删除时间、删除人和删除人标识。
/// </summary>
public sealed class DeletionAuditedInitializer
{
    /// <summary>
    /// 待初始化的实体对象。
    /// </summary>
    private readonly object _entity;

    /// <summary>
    /// 删除人的字符串标识。
    /// </summary>
    private readonly string _userId;

    /// <summary>
    /// 删除人的名称。
    /// </summary>
    private readonly string _userName;

    /// <summary>
    /// 使用实体和删除人审计信息初始化一个 <see cref="DeletionAuditedInitializer"/> 实例。
    /// </summary>
    /// <param name="entity">待初始化的实体对象。</param>
    /// <param name="userId">删除人的字符串标识。</param>
    /// <param name="userName">删除人的名称。</param>
    private DeletionAuditedInitializer(object entity, string userId, string userName)
    {
        _entity = entity;
        _userId = userId;
        _userName = userName;
    }

    /// <summary>
    /// 初始化实体的删除审计信息。
    /// </summary>
    /// <param name="entity">待初始化的实体对象；为空时不执行任何操作。</param>
    /// <param name="userId">删除人的字符串标识。</param>
    /// <param name="userName">删除人的名称。</param>
    public static void Init(object entity, string userId, string userName) => new DeletionAuditedInitializer(entity, userId, userName).Init();

    /// <summary>
    /// 按删除审计契约初始化实体的删除时间、人员名称和人员标识。
    /// </summary>
    public void Init()
    {
        if (_entity == null)
            return;
        InitDeletionTime();
        InitDeleter();
        InitDeleterId();
    }

    /// <summary>
    /// 初始化删除时间
    /// </summary>
    private void InitDeletionTime()
    {
        if (_entity is IHasDeletionTime result)
            result.DeletionTime = DateTime.Now;
    }

    /// <summary>
    /// 初始化删除人
    /// </summary>
    private void InitDeleter()
    {
        if (string.IsNullOrWhiteSpace(_userName))
            return;
        if (_entity is IHasDeleter result)
            result.Deleter = _userName;
    }

    /// <summary>
    /// 初始化删除人标识
    /// </summary>
    private void InitDeleterId()
    {
        if (string.IsNullOrWhiteSpace(_userId))
            return;
        switch (_entity)
        {
            case IDeletionAuditedObject<Guid> _:
                InitGuid();
                return;

            case IDeletionAuditedObject<Guid?> _:
                InitNullableGuid();
                return;

            case IDeletionAuditedObject<int> _:
                InitInt();
                return;

            case IDeletionAuditedObject<int?> _:
                InitNullableInt();
                return;

            case IDeletionAuditedObject<string> _:
                InitString();
                return;

            case IDeletionAuditedObject<long> _:
                InitLong();
                return;

            case IDeletionAuditedObject<long?> _:
                InitNullableLong();
                return;
        }
    }

    /// <summary>
    /// 初始化Guid
    /// </summary>
    private void InitGuid()
    {
        var result = (IDeletionAuditedObject<Guid>)_entity;
        result.DeleterId = _userId.ToGuid();
    }

    /// <summary>
    /// 初始化可空Guid
    /// </summary>
    private void InitNullableGuid()
    {
        var result = (IDeletionAuditedObject<Guid?>)_entity;
        result.DeleterId = _userId.ToGuidOrNull();
    }

    /// <summary>
    /// 初始化int
    /// </summary>
    private void InitInt()
    {
        var result = (IDeletionAuditedObject<int>)_entity;
        result.DeleterId = _userId.ToInt();
    }

    /// <summary>
    /// 初始化可空int
    /// </summary>
    private void InitNullableInt()
    {
        var result = (IDeletionAuditedObject<int?>)_entity;
        result.DeleterId = _userId.ToIntOrNull();
    }

    /// <summary>
    /// 初始化Long
    /// </summary>
    private void InitLong()
    {
        var result = (IDeletionAuditedObject<long>)_entity;
        result.DeleterId = _userId.ToLong();
    }

    /// <summary>
    /// 初始化可空Long
    /// </summary>
    private void InitNullableLong()
    {
        var result = (IDeletionAuditedObject<long?>)_entity;
        result.DeleterId = _userId.ToLongOrNull();
    }

    /// <summary>
    /// 初始化字符串
    /// </summary>
    private void InitString()
    {
        var result = (IDeletionAuditedObject<string>)_entity;
        result.DeleterId = _userId.SafeString();
    }
}

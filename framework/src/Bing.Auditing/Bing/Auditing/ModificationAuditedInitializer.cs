using Bing.Extensions;

namespace Bing.Auditing;

/// <summary>
/// 初始化实体的最后修改时间、修改人和修改人标识。
/// </summary>
public sealed class ModificationAuditedInitializer
{
    /// <summary>
    /// 待初始化的实体对象。
    /// </summary>
    private readonly object _entity;

    /// <summary>
    /// 修改人的字符串标识。
    /// </summary>
    private readonly string _userId;

    /// <summary>
    /// 修改人的名称。
    /// </summary>
    private readonly string _userName;

    /// <summary>
    /// 指定的最后修改时间；为空时使用当前时间。
    /// </summary>
    private readonly DateTime? _dateTime;

    /// <summary>
    /// 使用实体和修改人审计信息初始化一个 <see cref="ModificationAuditedInitializer"/> 实例。
    /// </summary>
    /// <param name="entity">待初始化的实体对象。</param>
    /// <param name="userId">修改人的字符串标识。</param>
    /// <param name="userName">修改人的名称。</param>
    /// <param name="dateTime">指定的最后修改时间；为空时使用当前时间。</param>
    private ModificationAuditedInitializer(object entity, string userId, string userName, DateTime? dateTime)
    {
        _entity = entity;
        _userId = userId;
        _userName = userName;
        _dateTime = dateTime;
    }

    /// <summary>
    /// 初始化实体的修改审计信息。
    /// </summary>
    /// <param name="entity">待初始化的实体对象；为空时不执行任何操作。</param>
    /// <param name="userId">修改人的字符串标识。</param>
    /// <param name="userName">修改人的名称。</param>
    public static void Init(object entity, string userId, string userName) => new ModificationAuditedInitializer(entity, userId, userName, null).Init();

    /// <summary>
    /// 使用指定时间初始化实体的修改审计信息。
    /// </summary>
    /// <param name="entity">待初始化的实体对象；为空时不执行任何操作。</param>
    /// <param name="userId">修改人的字符串标识。</param>
    /// <param name="userName">修改人的名称。</param>
    /// <param name="dateTime">最后修改时间；为空时使用当前时间。</param>
    public static void Init(object entity, string userId, string userName, DateTime? dateTime) => new ModificationAuditedInitializer(entity, userId, userName, dateTime).Init();

    /// <summary>
    /// 按修改审计契约初始化实体的时间、人员名称和人员标识。
    /// </summary>
    public void Init()
    {
        if (_entity == null)
            return;
        InitLastModificationTime();
        InitLastModifier();
        InitLastModifierId();
    }

    /// <summary>
    /// 初始化修改时间
    /// </summary>
    private void InitLastModificationTime()
    {
        if (_entity is IHasModificationTime result)
            result.LastModificationTime = _dateTime.HasValue ? _dateTime.SafeValue() : DateTime.Now;
    }

    /// <summary>
    /// 初始化修改人
    /// </summary>
    private void InitLastModifier()
    {
        if (string.IsNullOrWhiteSpace(_userName))
            return;
        if (_entity is IHasModifier result)
            result.LastModifier = _userName;
    }

    /// <summary>
    /// 初始化修改人标识
    /// </summary>
    private void InitLastModifierId()
    {
        if (string.IsNullOrWhiteSpace(_userId))
            return;
        switch (_entity)
        {
            case IModificationAuditedObject<Guid> _:
                InitGuid();
                return;

            case IModificationAuditedObject<Guid?> _:
                InitNullableGuid();
                return;

            case IModificationAuditedObject<int> _:
                InitInt();
                return;

            case IModificationAuditedObject<int?> _:
                InitNullableInt();
                return;

            case IModificationAuditedObject<string> _:
                InitString();
                return;

            case IModificationAuditedObject<long> _:
                InitLong();
                return;

            case IModificationAuditedObject<long?> _:
                InitNullableLong();
                return;
        }
    }

    /// <summary>
    /// 初始化Guid
    /// </summary>
    private void InitGuid()
    {
        var result = (IModificationAuditedObject<Guid>)_entity;
        result.LastModifierId = _userId.ToGuid();
    }

    /// <summary>
    /// 初始化可空Guid
    /// </summary>
    private void InitNullableGuid()
    {
        var result = (IModificationAuditedObject<Guid?>)_entity;
        result.LastModifierId = _userId.ToGuidOrNull();
    }

    /// <summary>
    /// 初始化int
    /// </summary>
    private void InitInt()
    {
        var result = (IModificationAuditedObject<int>)_entity;
        result.LastModifierId = _userId.ToInt();
    }

    /// <summary>
    /// 初始化可空int
    /// </summary>
    private void InitNullableInt()
    {
        var result = (IModificationAuditedObject<int?>)_entity;
        result.LastModifierId = _userId.ToIntOrNull();
    }

    /// <summary>
    /// 初始化Long
    /// </summary>
    private void InitLong()
    {
        var result = (IModificationAuditedObject<long>)_entity;
        result.LastModifierId = _userId.ToLong();
    }

    /// <summary>
    /// 初始化可空Long
    /// </summary>
    private void InitNullableLong()
    {
        var result = (IModificationAuditedObject<long?>)_entity;
        result.LastModifierId = _userId.ToLongOrNull();
    }

    /// <summary>
    /// 初始化字符串
    /// </summary>
    private void InitString()
    {
        var result = (IModificationAuditedObject<string>)_entity;
        result.LastModifierId = _userId.SafeString();
    }
}

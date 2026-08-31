using Bing.Extensions;

namespace Bing.Auditing;

/// <summary>
/// 初始化实体的创建时间、创建人和创建人标识。
/// </summary>
public sealed class CreationAuditedInitializer
{
    /// <summary>
    /// 待初始化的实体对象。
    /// </summary>
    private readonly object _entity;

    /// <summary>
    /// 创建人的字符串标识。
    /// </summary>
    private readonly string _userId;

    /// <summary>
    /// 创建人的名称。
    /// </summary>
    private readonly string _userName;

    /// <summary>
    /// 指定的创建时间；为空时使用当前时间。
    /// </summary>
    private readonly DateTime? _dateTime;

    /// <summary>
    /// 使用实体和创建人审计信息初始化一个 <see cref="CreationAuditedInitializer"/> 实例。
    /// </summary>
    /// <param name="entity">待初始化的实体对象。</param>
    /// <param name="userId">创建人的字符串标识。</param>
    /// <param name="userName">创建人的名称。</param>
    /// <param name="dateTime">指定的创建时间；为空时使用当前时间。</param>
    private CreationAuditedInitializer(object entity, string userId, string userName, DateTime? dateTime)
    {
        _entity = entity;
        _userId = userId;
        _userName = userName;
        _dateTime = dateTime;
    }

    /// <summary>
    /// 初始化实体的创建审计信息。
    /// </summary>
    /// <param name="entity">待初始化的实体对象；为空时不执行任何操作。</param>
    /// <param name="userId">创建人的字符串标识。</param>
    /// <param name="userName">创建人的名称。</param>
    public static void Init(object entity, string userId, string userName) => new CreationAuditedInitializer(entity, userId, userName, null).Init();

    /// <summary>
    /// 使用指定时间初始化实体的创建审计信息。
    /// </summary>
    /// <param name="entity">待初始化的实体对象；为空时不执行任何操作。</param>
    /// <param name="userId">创建人的字符串标识。</param>
    /// <param name="userName">创建人的名称。</param>
    /// <param name="dateTime">创建时间；为空时使用当前时间。</param>
    public static void Init(object entity, string userId, string userName, DateTime? dateTime) => new CreationAuditedInitializer(entity, userId, userName,dateTime).Init();

    /// <summary>
    /// 按创建审计契约初始化实体的时间、人员名称和人员标识。
    /// </summary>
    public void Init()
    {
        if (_entity == null)
            return;
        InitCreationTime();
        InitCreator();
        InitCreatorId();
    }

    /// <summary>
    /// 初始化创建时间
    /// </summary>
    private void InitCreationTime()
    {
        if (_entity is IHasCreationTime result)
            result.CreationTime = _dateTime.HasValue ? _dateTime.SafeValue() : DateTime.Now;
    }

    /// <summary>
    /// 初始化创建人
    /// </summary>
    private void InitCreator()
    {
        if (string.IsNullOrWhiteSpace(_userName))
            return;
        if (_entity is IHasCreator result)
            result.Creator = _userName;
    }

    /// <summary>
    /// 初始化创建人标识
    /// </summary>
    private void InitCreatorId()
    {
        if (string.IsNullOrWhiteSpace(_userId))
            return;
        switch (_entity)
        {
            case ICreationAuditedObject<Guid> _:
                InitGuid();
                return;

            case ICreationAuditedObject<Guid?> _:
                InitNullableGuid();
                return;

            case ICreationAuditedObject<int> _:
                InitInt();
                return;

            case ICreationAuditedObject<int?> _:
                InitNullableInt();
                return;

            case ICreationAuditedObject<string> _:
                InitString();
                return;

            case ICreationAuditedObject<long> _:
                InitLong();
                return;

            case ICreationAuditedObject<long?> _:
                InitNullableLong();
                return;
        }
    }

    /// <summary>
    /// 初始化Guid
    /// </summary>
    private void InitGuid()
    {
        var result = (ICreationAuditedObject<Guid>)_entity;
        result.CreatorId = _userId.ToGuid();
    }

    /// <summary>
    /// 初始化可空Guid
    /// </summary>
    private void InitNullableGuid()
    {
        var result = (ICreationAuditedObject<Guid?>)_entity;
        result.CreatorId = _userId.ToGuidOrNull();
    }

    /// <summary>
    /// 初始化int
    /// </summary>
    private void InitInt()
    {
        var result = (ICreationAuditedObject<int>)_entity;
        result.CreatorId = _userId.ToInt();
    }

    /// <summary>
    /// 初始化可空int
    /// </summary>
    private void InitNullableInt()
    {
        var result = (ICreationAuditedObject<int?>)_entity;
        result.CreatorId = _userId.ToIntOrNull();
    }

    /// <summary>
    /// 初始化Long
    /// </summary>
    private void InitLong()
    {
        var result = (ICreationAuditedObject<long>)_entity;
        result.CreatorId = _userId.ToLong();
    }

    /// <summary>
    /// 初始化可空Long
    /// </summary>
    private void InitNullableLong()
    {
        var result = (ICreationAuditedObject<long?>)_entity;
        result.CreatorId = _userId.ToLongOrNull();
    }

    /// <summary>
    /// 初始化字符串
    /// </summary>
    private void InitString()
    {
        var result = (ICreationAuditedObject<string>)_entity;
        result.CreatorId = _userId.SafeString();
    }
}

using Bing.DependencyInjection;
using Bing.Extensions;
using Bing.Timing;
using Bing.Users;

namespace Bing.Auditing;

/// <summary>
/// 为支持审计接口的对象填充创建、修改和删除属性。
/// </summary>
public class AuditPropertySetter : IAuditPropertySetter, ITransientDependency
{
    /// <summary>
    /// 使用当前用户和时钟服务初始化 <see cref="AuditPropertySetter"/> 的实例。
    /// </summary>
    /// <param name="currentUser">提供当前用户标识和名称的服务。</param>
    /// <param name="clock">提供当前时间的服务，可在测试中替换为测试时钟。</param>
    public AuditPropertySetter(ICurrentUser currentUser, IClock clock)
    {
        CurrentUser = currentUser;
        Clock = clock;
    }

    /// <summary>
    /// 获取提供当前审计主体信息的用户服务。
    /// </summary>
    protected ICurrentUser CurrentUser { get; }

    /// <summary>
    /// 获取提供审计时间戳的时钟服务。
    /// </summary>
    protected IClock Clock { get; }

    /// <summary>
    /// 为目标对象填充尚未设置的创建时间、创建人标识和创建人名称。
    /// </summary>
    /// <param name="targetObject">要填充创建审计属性的对象；为 <c>null</c> 时不执行任何操作。</param>
    public virtual void SetCreationProperties(object targetObject)
    {
        if (targetObject == null)
            return;
        SetCreationTime(targetObject);
        SetCreatorId(targetObject);
        SetCreator(targetObject);
    }

    /// <summary>
    /// 为目标对象填充最后修改时间、最后修改人标识和最后修改人名称。
    /// </summary>
    /// <param name="targetObject">要填充修改审计属性的对象；为 <c>null</c> 时不执行任何操作。</param>
    public virtual void SetModificationProperties(object targetObject)
    {
        if (targetObject == null)
            return;
        SetLastModificationTime(targetObject);
        SetLastModifierId(targetObject);
        SetLastModifier(targetObject);
    }

    /// <summary>
    /// 为目标对象填充删除时间、删除人标识和删除人名称。
    /// </summary>
    /// <param name="targetObject">要填充删除审计属性的对象；为 <c>null</c> 时不执行任何操作。</param>
    public virtual void SetDeletionProperties(object targetObject)
    {
        if (targetObject == null)
            return;
        SetDeletionTime(targetObject);
        SetDeleterId(targetObject);
        SetDeleter(targetObject);
    }

    /// <summary>
    /// 为支持创建时间的对象设置尚未赋值的创建时间。
    /// </summary>
    /// <param name="targetObject">可能实现 <see cref="IHasCreationTime"/> 的目标对象。</param>
    protected virtual void SetCreationTime(object targetObject)
    {
        if (targetObject is not IHasCreationTime objectWithCreationTime)
            return;
        objectWithCreationTime.CreationTime ??= Clock.Now;
    }

    /// <summary>
    /// 为支持创建审计的对象设置尚未赋值的创建人标识。
    /// </summary>
    /// <remarks>仅在当前用户标识有效且目标对象的创建人标识为默认值时写入；根据目标审计接口的泛型标识类型进行转换。</remarks>
    /// <param name="targetObject">可能实现创建审计接口的目标对象。</param>
    protected virtual void SetCreatorId(object targetObject)
    {
        if (string.IsNullOrWhiteSpace(CurrentUser.UserId))
            return;
        switch (targetObject)
        {
            case ICreationAuditedObject<Guid> userIdWithGuid:
                if (userIdWithGuid.CreatorId != default && userIdWithGuid.CreatorId != Guid.Empty)
                    return;
                userIdWithGuid.CreatorId = CurrentUser.UserId.ToGuid();
                return;

            case ICreationAuditedObject<Guid?> userIdWithNullableGuid:
                if (userIdWithNullableGuid.CreatorId.HasValue
                    && userIdWithNullableGuid.CreatorId.Value != default
                    && userIdWithNullableGuid.CreatorId.Value != Guid.Empty)
                    return;
                userIdWithNullableGuid.CreatorId = CurrentUser.UserId.ToGuidOrNull();
                return;

            case ICreationAuditedObject<int> userIdWithInt:
                if (userIdWithInt.CreatorId != default)
                    return;
                userIdWithInt.CreatorId = CurrentUser.UserId.ToInt();
                return;

            case ICreationAuditedObject<int?> userIdWithNullableInt:
                if (userIdWithNullableInt.CreatorId.HasValue
                    && userIdWithNullableInt.CreatorId.Value != default)
                    return;
                userIdWithNullableInt.CreatorId = CurrentUser.UserId.ToIntOrNull();
                return;

            case ICreationAuditedObject<string> userIdWithString:
                if (!string.IsNullOrWhiteSpace(userIdWithString.CreatorId) && userIdWithString.CreatorId != default)
                    return;
                userIdWithString.CreatorId = CurrentUser.UserId.SafeString();
                return;

            case ICreationAuditedObject<long> userIdWithLong:
                if (userIdWithLong.CreatorId != default)
                    return;
                userIdWithLong.CreatorId = CurrentUser.UserId.ToLong();
                return;

            case ICreationAuditedObject<long?> userIdWithNullableLong:
                if (userIdWithNullableLong.CreatorId.HasValue
                    && userIdWithNullableLong.CreatorId.Value != default)
                    return;
                userIdWithNullableLong.CreatorId = CurrentUser.UserId.ToLongOrNull();
                return;
        }
    }

    /// <summary>
    /// 设置创建人
    /// </summary>
    /// <param name="targetObject">目标对象</param>
    protected virtual void SetCreator(object targetObject)
    {
        var userName = GetUserName();
        if (string.IsNullOrWhiteSpace(userName))
            return;
        if (targetObject is IHasCreator objectWithCreator)
            objectWithCreator.Creator = userName;
    }

    /// <summary>
    /// 设置修改时间
    /// </summary>
    /// <param name="targetObject">目标对象</param>
    protected virtual void SetLastModificationTime(object targetObject)
    {
        if (targetObject is IHasModificationTime objectWithModificationTime)
            objectWithModificationTime.LastModificationTime = Clock.Now;
    }

    /// <summary>
    /// 按目标审计接口的标识类型设置最后修改人标识。
    /// </summary>
    /// <param name="targetObject">可能实现修改审计接口的目标对象。</param>
    protected virtual void SetLastModifierId(object targetObject)
    {
        if (string.IsNullOrWhiteSpace(CurrentUser.UserId))
            return;
        switch (targetObject)
        {
            case IModificationAuditedObject<Guid> userIdWithGuid:
                userIdWithGuid.LastModifierId = CurrentUser.UserId.ToGuid();
                return;

            case IModificationAuditedObject<Guid?> userIdWithNullableGuid:
                userIdWithNullableGuid.LastModifierId = CurrentUser.UserId.ToGuidOrNull();
                return;

            case IModificationAuditedObject<int> userIdWithInt:
                userIdWithInt.LastModifierId = CurrentUser.UserId.ToInt();
                return;

            case IModificationAuditedObject<int?> userIdWithNullableInt:
                userIdWithNullableInt.LastModifierId = CurrentUser.UserId.ToIntOrNull();
                return;

            case IModificationAuditedObject<string> userIdWithString:
                userIdWithString.LastModifierId = CurrentUser.UserId.SafeString();
                return;

            case IModificationAuditedObject<long> userIdWithLong:
                userIdWithLong.LastModifierId = CurrentUser.UserId.ToLong();
                return;

            case IModificationAuditedObject<long?> userIdWithNullableLong:
                userIdWithNullableLong.LastModifierId = CurrentUser.UserId.ToLongOrNull();
                return;
        }
    }

    /// <summary>
    /// 设置修改人
    /// </summary>
    /// <param name="targetObject">目标对象</param>
    protected virtual void SetLastModifier(object targetObject)
    {
        var userName = GetUserName();
        if (string.IsNullOrWhiteSpace(userName))
            return;
        if (targetObject is IHasModifier objectWithModifier)
            objectWithModifier.LastModifier = userName;
    }

    /// <summary>
    /// 设置删除时间
    /// </summary>
    /// <param name="targetObject">目标对象</param>
    protected virtual void SetDeletionTime(object targetObject)
    {
        if (targetObject is IHasDeletionTime objectWithDeletionTime)
            objectWithDeletionTime.DeletionTime = Clock.Now;
    }

    /// <summary>
    /// 为支持删除审计的对象设置尚未赋值的删除人标识。
    /// </summary>
    /// <remarks>仅在当前用户标识有效且目标对象的删除人标识为默认值时写入；根据目标审计接口的泛型标识类型进行转换。</remarks>
    /// <param name="targetObject">可能实现删除审计接口的目标对象。</param>
    protected virtual void SetDeleterId(object targetObject)
    {
        if (string.IsNullOrWhiteSpace(CurrentUser.UserId))
            return;
        switch (targetObject)
        {
            case IDeletionAuditedObject<Guid> userIdWithGuid:
                if (userIdWithGuid.DeleterId != default && userIdWithGuid.DeleterId != Guid.Empty)
                    return;
                userIdWithGuid.DeleterId = CurrentUser.UserId.ToGuid();
                return;

            case IDeletionAuditedObject<Guid?> userIdWithNullableGuid:
                if (userIdWithNullableGuid.DeleterId.HasValue
                    && userIdWithNullableGuid.DeleterId.Value != default
                    && userIdWithNullableGuid.DeleterId.Value != Guid.Empty)
                    return;
                userIdWithNullableGuid.DeleterId = CurrentUser.UserId.ToGuidOrNull();
                return;

            case IDeletionAuditedObject<int> userIdWithInt:
                if (userIdWithInt.DeleterId != default)
                    return;
                userIdWithInt.DeleterId = CurrentUser.UserId.ToInt();
                return;

            case IDeletionAuditedObject<int?> userIdWithNullableInt:
                if (userIdWithNullableInt.DeleterId.HasValue
                    && userIdWithNullableInt.DeleterId.Value != default)
                    return;
                userIdWithNullableInt.DeleterId = CurrentUser.UserId.ToIntOrNull();
                return;

            case IDeletionAuditedObject<string> userIdWithString:
                if (!string.IsNullOrWhiteSpace(userIdWithString.DeleterId) && userIdWithString.DeleterId != default)
                    return;
                userIdWithString.DeleterId = CurrentUser.UserId.SafeString();
                return;

            case IDeletionAuditedObject<long> userIdWithLong:
                if (userIdWithLong.DeleterId != default)
                    return;
                userIdWithLong.DeleterId = CurrentUser.UserId.ToLong();
                return;

            case IDeletionAuditedObject<long?> userIdWithNullableLong:
                if (userIdWithNullableLong.DeleterId.HasValue
                    && userIdWithNullableLong.DeleterId.Value != default)
                    return;
                userIdWithNullableLong.DeleterId = CurrentUser.UserId.ToLongOrNull();
                return;
        }
    }

    /// <summary>
    /// 设置删除人
    /// </summary>
    /// <param name="targetObject">目标对象</param>
    protected virtual void SetDeleter(object targetObject)
    {
        var userName = GetUserName();
        if (string.IsNullOrWhiteSpace(userName))
            return;
        if (targetObject is IHasDeleter objectWithDeleter)
            objectWithDeleter.Deleter = userName;
    }

    /// <summary>
    /// 获取用于审计字段的当前用户显示名称。
    /// </summary>
    /// <returns>当前用户全名；全名为空时返回用户名。</returns>
    protected virtual string GetUserName()
    {
        var name = CurrentUser.GetFullName();
        return string.IsNullOrEmpty(name) ? CurrentUser.GetUserName() : name;
    }
}

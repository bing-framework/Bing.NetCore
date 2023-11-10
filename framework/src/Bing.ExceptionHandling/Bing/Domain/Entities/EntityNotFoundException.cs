namespace Bing.Domain.Entities;

/// <summary>
/// 找不到实体异常
/// </summary>
public class EntityNotFoundException : BingException
{
    /// <summary>
    /// 实体类型
    /// </summary>
    public Type EntityType { get; set; }

    /// <summary>
    /// 实体标识
    /// </summary>
    public object Id { get; set; }

    /// <summary>
    /// 初始化一个<see cref="EntityNotFoundException"/>类型的实例
    /// </summary>
    public EntityNotFoundException()
    {
    }

    /// <summary>
    /// 初始化一个<see cref="EntityNotFoundException"/>类型的实例
    /// </summary>
    /// <param name="entityType">实体类型</param>
    public EntityNotFoundException(Type entityType)
        : this(entityType, null, null)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="EntityNotFoundException"/>类型的实例
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="id">实体标识</param>
    public EntityNotFoundException(Type entityType, object id)
        : this(entityType, id, null)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="EntityNotFoundException"/>类型的实例
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="id">实体标识</param>
    /// <param name="innerException">内部异常</param>
    public EntityNotFoundException(Type entityType, object id, Exception innerException)
        : base(
            id == null
                ? $"There is no such an entity given id. Entity type: {entityType.FullName}"
                : $"There is no such an entity. Entity type: {entityType.FullName}, id: {id}",
            innerException)
    {
        EntityType = entityType;
        Id = id;
    }

    /// <summary>
    /// 初始化一个<see cref="EntityNotFoundException"/>类型的实例
    /// </summary>
    /// <param name="message">错误消息</param>
    public EntityNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="EntityNotFoundException"/>类型的实例
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="innerException">内部异常</param>
    public EntityNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

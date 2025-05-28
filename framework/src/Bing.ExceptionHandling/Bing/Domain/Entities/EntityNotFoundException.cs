namespace Bing.Domain.Entities;

/// <summary>
/// 实体未找到异常
/// </summary>
public class EntityNotFoundException : BingException
{
    /// <summary>
    /// 标识
    /// </summary>
    private const string FLAG = "__ENTITY_NOT_FOUND_FLG";

    /// <summary>
    /// 默认错误消息
    /// </summary>
    private const string DEFAULT_ERROR_MSG = "The specified entity could not be found.";

    /// <summary>
    /// 默认错误码
    /// </summary>
    private const long ERROR_CODE = 1010;

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
    public EntityNotFoundException() : this(DEFAULT_ERROR_MSG)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="EntityNotFoundException"/>类型的实例
    /// </summary>
    /// <param name="entityType">实体类型</param>
    public EntityNotFoundException(Type entityType) : this(entityType, null, null, null)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="EntityNotFoundException"/>类型的实例
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="id">实体标识</param>
    public EntityNotFoundException(Type entityType, object id) : this(entityType, id, null, null)
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
    /// <param name="entityType">实体类型</param>
    /// <param name="id">实体标识</param>
    /// <param name="message">错误消息</param>
    public EntityNotFoundException(Type entityType, object id, string message)
        : this(entityType, id, message, null)
    {
        EntityType = entityType;
        Id = id;
    }

    /// <summary>
    /// 初始化一个<see cref="EntityNotFoundException"/>类型的实例
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="id">实体标识</param>
    /// <param name="message">错误消息</param>
    /// <param name="innerException">内部异常</param>
    private EntityNotFoundException(Type entityType, object id, string message, Exception innerException)
        : base(ERROR_CODE, GetMessage(entityType, id, message), FLAG, innerException)
    {
        EntityType = entityType;
        Id = id;
    }

    /// <summary>
    /// 获取消息
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="id">实体标识</param>
    /// <param name="message">自定义消息</param>
    private static string GetMessage(Type entityType, object id, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return id == null
                ? $"There is no such an entity given id. Entity type: {entityType.FullName}"
                : $"There is no such an entity. Entity type: {entityType.FullName}, id: {id}";
        return message;
    }

    /// <summary>
    /// 初始化一个<see cref="EntityNotFoundException"/>类型的实例
    /// </summary>
    /// <param name="message">错误消息</param>
    public EntityNotFoundException(string message)
        : base(ERROR_CODE, message, FLAG)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="EntityNotFoundException"/>类型的实例
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="innerException">内部异常</param>
    public EntityNotFoundException(string message, Exception innerException)
        : base(ERROR_CODE, message, FLAG, innerException)
    {
    }
}

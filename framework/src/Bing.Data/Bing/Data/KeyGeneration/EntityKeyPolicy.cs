using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Bing.Data.KeyGeneration;

/// <summary>
/// 标识实体主键的生成方。
/// </summary>
/// <remarks>
/// 键的 CLR 类型不会自动决定其生成策略。
/// </remarks>
public enum EntityKeyGenerationKind
{
    /// <summary>
    /// 由调用方在写入前提供主键。
    /// </summary>
    Assigned,
    /// <summary>
    /// 由客户端生成 GUID 主键。
    /// </summary>
    ClientGuid,
    /// <summary>
    /// 由客户端雪花算法生成主键。
    /// </summary>
    ClientSnowflake,
    /// <summary>
    /// 由数据库 Identity 或等效机制生成主键。
    /// </summary>
    DatabaseIdentity
}

/// <summary>
/// 按实体冻结的单列主键生成契约。
/// </summary>
/// <remarks>按实体冻结的单列主键契约，访问器支持领域实体受保护的 setter。</remarks>
public sealed class EntityKeyPolicy
{
    /// <summary>
    /// 读取实体键值的委托。
    /// </summary>
    private readonly Func<object, object> _get;
    /// <summary>
    /// 写入实体键值的委托。
    /// </summary>
    private readonly Action<object, object> _set;
    /// <summary>
    /// 获取主键契约对应的实体类型。
    /// </summary>
    /// <remarks>实体类型。</remarks>
    public Type EntityType { get; }
    /// <summary>
    /// 获取实体的单列主键属性。
    /// </summary>
    /// <remarks>主键属性。</remarks>
    public PropertyInfo KeyProperty { get; }
    /// <summary>
    /// 获取实体主键的 CLR 类型。
    /// </summary>
    /// <remarks>主键类型。</remarks>
    public Type KeyType { get; }
    /// <summary>获取主键属性名称；静态访问器策略不需要 PropertyInfo。</summary>
    public string KeyPropertyName { get; }
    /// <summary>获取策略是否使用显式静态访问器。</summary>
    public bool IsGenerated { get; }
    private readonly object _defaultValue;
    /// <summary>
    /// 获取实体主键的生成策略。
    /// </summary>
    /// <remarks>生成策略。</remarks>
    public EntityKeyGenerationKind Kind { get; }
    /// <summary>
    /// 获取客户端主键生成器的注册名称。
    /// </summary>
    /// <remarks>命名客户端生成器。</remarks>
    public string GeneratorName { get; }
    /// <summary>
    /// 初始化一个 <see cref="EntityKeyPolicy" /> 类型的实例。
    /// </summary>
    /// <param name="entityType">实体的 CLR 类型。</param>
    /// <param name="keyProperty">作为实体主键的属性信息。</param>
    /// <param name="kind">当前对象或操作的种类。</param>
    /// <param name="generatorName">注册的标识生成器名称。</param>
    /// <remarks>创建不可变策略；只接受可读写的实例主键属性。</remarks>
    public EntityKeyPolicy(Type entityType, PropertyInfo keyProperty, EntityKeyGenerationKind kind, string generatorName = "default")
    {
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
        KeyProperty = keyProperty ?? throw new ArgumentNullException(nameof(keyProperty));
        KeyType = keyProperty.PropertyType;
        KeyPropertyName = keyProperty.Name;
        _defaultValue = KeyType.IsValueType ? Activator.CreateInstance(KeyType) : null;
        if (!Enum.IsDefined(typeof(EntityKeyGenerationKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (keyProperty.DeclaringType == null || !keyProperty.DeclaringType.IsAssignableFrom(entityType) ||
            keyProperty.GetIndexParameters().Length != 0 || keyProperty.GetMethod == null ||
            keyProperty.GetMethod.IsStatic || keyProperty.GetSetMethod(true) == null)
            throw new ArgumentException("主键必须是该实体的可读写实例属性。", nameof(keyProperty));
        if (string.IsNullOrWhiteSpace(generatorName)) throw new ArgumentException("生成器名称不能为空。", nameof(generatorName));
        if (kind == EntityKeyGenerationKind.ClientGuid && KeyType != typeof(Guid) ||
            kind == EntityKeyGenerationKind.ClientSnowflake && KeyType != typeof(long) ||
            kind == EntityKeyGenerationKind.DatabaseIdentity && KeyType != typeof(long) && KeyType != typeof(int))
            throw new ArgumentException("主键类型与生成策略不匹配。", nameof(kind));
        Kind = kind;
        GeneratorName = generatorName;
        var entity = Expression.Parameter(typeof(object), "entity");
        var value = Expression.Parameter(typeof(object), "value");
        var member = Expression.Property(Expression.Convert(entity, entityType), keyProperty);
        _get = Expression.Lambda<Func<object, object>>(Expression.Convert(member, typeof(object)), entity).Compile();
        _set = Expression.Lambda<Action<object, object>>(Expression.Assign(member, Expression.Convert(value, KeyType)), entity, value).Compile();
    }

    private EntityKeyPolicy(Type entityType, string propertyName, Type keyType, EntityKeyGenerationKind kind,
        Func<object, object> getter, Action<object, object> setter, object defaultValue, string generatorName)
    {
        if (string.IsNullOrWhiteSpace(propertyName)) throw new ArgumentException("主键属性名称不能为空。", nameof(propertyName));
        if (string.IsNullOrWhiteSpace(generatorName)) throw new ArgumentException("生成器名称不能为空。", nameof(generatorName));
        if (kind < EntityKeyGenerationKind.Assigned || kind > EntityKeyGenerationKind.DatabaseIdentity)
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (kind == EntityKeyGenerationKind.ClientGuid && keyType != typeof(Guid) ||
            kind == EntityKeyGenerationKind.ClientSnowflake && keyType != typeof(long) ||
            kind == EntityKeyGenerationKind.DatabaseIdentity && keyType != typeof(long) && keyType != typeof(int))
            throw new ArgumentException("主键类型与生成策略不匹配。", nameof(kind));
        EntityType = entityType;
        KeyPropertyName = propertyName;
        KeyType = keyType;
        Kind = kind;
        GeneratorName = generatorName;
        _get = getter;
        _set = setter;
        _defaultValue = defaultValue;
        IsGenerated = true;
    }

    /// <summary>通过静态访问器创建主键策略，不查找属性或编译表达式。</summary>
    public static EntityKeyPolicy CreateGenerated<TEntity, TKey>(string propertyName, EntityKeyGenerationKind kind,
        Func<TEntity, TKey> getter, Action<TEntity, TKey> setter, string generatorName = "default")
    {
        if (getter == null) throw new ArgumentNullException(nameof(getter));
        if (setter == null) throw new ArgumentNullException(nameof(setter));
        return new EntityKeyPolicy(typeof(TEntity), propertyName, typeof(TKey), kind,
            entity => getter((TEntity)entity), (entity, value) => setter((TEntity)entity, (TKey)value), default(TKey), generatorName);
    }
    /// <summary>
    /// 读取实体当前主键。
    /// </summary>
    /// <param name="entity">待读取、赋值或准备主键的领域实体。</param>
    /// <returns>实体主键属性的当前值。</returns>
    public object GetKey(object entity) => _get(entity ?? throw new ArgumentNullException(nameof(entity)));
    /// <summary>
    /// 设置实体主键。
    /// </summary>
    /// <param name="entity">待读取、赋值或准备主键的领域实体。</param>
    /// <param name="value">需要读取或设置的主键值。</param>
    /// <remarks>仅用于准备新实体或回填数据库生成键，更新路径不得调用。</remarks>
    public void SetKey(object entity, object value) => _set(entity ?? throw new ArgumentNullException(nameof(entity)), value);
    /// <summary>
    /// 判断主键是否为 CLR 默认值。
    /// </summary>
    /// <param name="value">需要读取或设置的主键值。</param>
    /// <returns>值为 null 或主键类型的默认值时为 true，否则为 false。</returns>
    /// <remarks>负数不属于缺失键；本方法不校验键值是否满足业务取值要求。</remarks>
    public bool IsDefault(object value) => value == null || Equals(value, _defaultValue);
    /// <summary>
    /// 校验 ORM 或 SQL 主键映射符合生成策略。
    /// </summary>
    /// <param name="isKey">映射列为符合策略要求的主键时为 true，否则为 false。</param>
    /// <param name="isDatabaseGenerated">映射列由数据库生成值时为 true，否则为 false。</param>
    /// <remarks>映射冲突在发出 SQL 前抛出异常。</remarks>
    public void ValidateMapping(bool isKey, bool isDatabaseGenerated)
    {
        if (!isKey || isDatabaseGenerated != (Kind == EntityKeyGenerationKind.DatabaseIdentity))
            throw new InvalidOperationException($"实体 {EntityType.FullName} 的主键映射与 {Kind} 策略冲突。");
    }
}

/// <summary>
/// 提供冻结的实体策略。
/// </summary>
/// <remarks>未注册返回 null，继续既有框架行为。</remarks>
public interface IEntityKeyPolicyProvider
{
    /// <summary>
    /// 查找指定实体的主键策略。
    /// </summary>
    /// <param name="entityType">待查找策略的实体类型。</param>
    /// <returns>实体主键策略；未注册时为 null。</returns>
    EntityKeyPolicy Find(Type entityType);
    /// <summary>
    /// 获取已冻结的实体主键策略集合。
    /// </summary>
    IReadOnlyCollection<EntityKeyPolicy> Policies { get; }
}

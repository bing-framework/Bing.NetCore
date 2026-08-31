using System.Linq.Expressions;
using System.Reflection;
using Bing.Domain.Values;
using Bing.Helpers;
using Bing.Reflection;

namespace Bing.Domain.Entities;

/// <summary>
/// 提供实体标识生成、相等性判断和类型识别的全局辅助方法。
/// </summary>
public static class EntityHelper
{
    #region ID生成相关

    /// <summary>
    /// 保存标识类型与其当前生成委托的映射。
    /// </summary>
    private static readonly IDictionary<Type, Func<object>> _idGenerators = new Dictionary<Type, Func<object>>
    {
        { typeof(Guid), () => GuidGenerateFunc() },
        { typeof(string), () => StringGenerateFunc() },
        { typeof(long), () => LongGenerateFunc() },
        { typeof(int), () => IntGenerateFunc() }
    };

    /// <summary>
    /// 获取或设置 <see cref="Guid"/> 标识生成委托。
    /// </summary>
    /// <remarks>默认使用 <see cref="Guid.NewGuid"/>；替换会影响后续所有 Guid 标识生成。</remarks>
    public static Func<Guid> GuidGenerateFunc { get; set; } = Guid.NewGuid;

    /// <summary>
    /// 获取或设置字符串标识生成委托。
    /// </summary>
    /// <remarks>默认调用 <see cref="GuidGenerateFunc"/> 并转换为字符串。</remarks>
    public static Func<string> StringGenerateFunc { get; set; } = () => GuidGenerateFunc().ToString();

    /// <summary>
    /// 获取或设置 <see cref="long"/> 标识生成委托。
    /// </summary>
    /// <remarks>默认值为 <c>null</c>；调用生成时由委托调用行为决定结果。</remarks>
    public static Func<long> LongGenerateFunc { get; set; }

    /// <summary>
    /// 获取或设置 <see cref="int"/> 标识生成委托。
    /// </summary>
    /// <remarks>默认委托在调用时抛出 <see cref="InvalidOperationException"/>。</remarks>
    public static Func<int> IntGenerateFunc { get; set; } = () => throw new InvalidOperationException("不支持 Int 作为 ID，请使用 Guid, string 或 long。");

    /// <summary>
    /// 生成 <see cref="Guid"/> 标识。
    /// </summary>
    /// <returns>当前 Guid 生成委托产生的标识。</returns>
    public static Guid CreateGuid() => CreateKey<Guid>();

    /// <summary>
    /// 生成指定类型的标识。
    /// </summary>
    /// <typeparam name="TKey">要生成的标识类型。</typeparam>
    /// <returns>当前为 <typeparamref name="TKey"/> 注册的生成委托产生的标识。</returns>
    /// <exception cref="InvalidOperationException">未注册标识类型的生成器时抛出。</exception>
    public static TKey CreateKey<TKey>()
    {
        if (_idGenerators.TryGetValue(typeof(TKey), out var generator))
            return (TKey)generator();
        throw new InvalidOperationException($"不支持的 ID 类型: {typeof(TKey)}，请使用 Guid, string, long。");
    }

    /// <summary>
    /// 注册或替换指定标识类型的生成委托。
    /// </summary>
    /// <typeparam name="TKey">要注册的标识类型。</typeparam>
    /// <param name="generator">生成标识的委托，不能为 <c>null</c>。</param>
    /// <exception cref="ArgumentNullException"><paramref name="generator"/> 为 <c>null</c> 时抛出。</exception>
    public static void RegisterIdGenerator<TKey>(Func<TKey> generator)
    {
        Check.NotNull(generator, nameof(generator));
        _idGenerators[typeof(TKey)] = () => generator();
    }

    #endregion

    #region 实体相等性比较

    /// <summary>
    /// 获取或设置判断两个实体是否需要按多租户规则比较的委托。
    /// </summary>
    /// <remarks>默认始终返回 <c>false</c>；替换会影响后续 <see cref="EntityEquals"/> 调用。</remarks>
    public static Func<IEntity, IEntity, bool> IsMultiTenantEntity { get; set; } = (_, _) => false;

    /// <summary>
    /// 获取或设置多租户实体间是否允许相同标识视为相等的委托。
    /// </summary>
    /// <remarks>默认始终返回 <c>false</c>。</remarks>
    public static Func<IEntity, IEntity, bool> AllowSameIdAcrossTenants { get; set; } = (_, _) => false;

    /// <summary>
    /// 判断两个 <see cref="IEntity"/> 实例是否表示同一实体。
    /// </summary>
    /// <param name="entity1">第一个实体。</param>
    /// <param name="entity2">第二个实体。</param>
    /// <returns>引用相同，或类型兼容且键值相等的实体返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    /// <remarks>任一实体为 <c>null</c> 时返回 <c>false</c>；两个实体均使用默认键时不视为相等，多租户规则由可替换委托决定。</remarks>
    public static bool EntityEquals(IEntity entity1, IEntity entity2)
    {
        // 基本检查
        if (entity1 == null || entity2 == null)
            return false;
        if (ReferenceEquals(entity1, entity2))
            return true;

        // 类型兼容性检查
        var typeOfEntity1 = entity1.GetType();
        var typeOfEntity2 = entity2.GetType();
        if (!typeOfEntity1.IsAssignableFrom(typeOfEntity2) && !typeOfEntity2.IsAssignableFrom(typeOfEntity1))
            return false;

        // 多租户检查
        if (IsMultiTenantEntity(entity1, entity2))
            return AllowSameIdAcrossTenants(entity1, entity2);

        // 瞬时对象检查 - 瞬时对象不视为相等
        if (HasDefaultKeys(entity1) && HasDefaultKeys(entity2))
            return false;

        // 键数量检查
        var entity1Keys = entity1.GetKeys();
        var entity2Keys = entity2.GetKeys();
        if (entity1Keys.Length != entity2Keys.Length)
            return false;

        // 键值比较
        return KeysEqual(entity1Keys, entity2Keys);
    }

    /// <summary>
    /// 比较两个键数组是否相等
    /// </summary>
    /// <param name="keys1">第一个键数组</param>
    /// <param name="keys2">第二个键数组</param>
    /// <returns>如果键数组相等返回true，否则返回false</returns>
    private static bool KeysEqual(object[] keys1, object[] keys2)
    {
        for (var i = 0; i < keys1.Length; i++)
        {
            var key1 = keys1[i];
            var key2 = keys2[i];
            
            // 空值检查
            if (key1 == null)
                return key2 == null;
            if (key2 == null)
                return false;

            // 默认值检查 - 如果两个键值都是默认值，则视为不相等
            if (Types.IsDefaultValue(key1) && Types.IsDefaultValue(key2))
                return false;

            // 值比较
            if (!key1.Equals(key2))
                return false;
        }
        return true;
    }

    #endregion

    #region 实体和值对象类型检查

    /// <summary>
    /// 判断指定类型是否实现 <see cref="IEntity"/>。
    /// </summary>
    /// <param name="type">要检查的类型。</param>
    /// <returns>实现 <see cref="IEntity"/> 时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> 为 <c>null</c> 时抛出。</exception>
    public static bool IsEntity(Type type)
    {
        Check.NotNull(type, nameof(type));
        return typeof(IEntity).IsAssignableFrom(type);
    }

    /// <summary>
    /// 判断指定类型是否实现 <see cref="IEntity{TKey}"/> 并输出标识类型。
    /// </summary>
    /// <param name="type">要检查的类型。</param>
    /// <param name="keyType">实现单一标识实体接口时输出标识类型；否则为 <c>null</c>。</param>
    /// <returns>实现单一标识实体接口时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> 为 <c>null</c> 时抛出。</exception>
    public static bool IsEntityWithId(Type type, out Type keyType)
    {
        Check.NotNull(type, nameof(type));
        keyType = null;
        
        foreach (var interfaceType in type.GetInterfaces())
        {
            if (interfaceType.GetTypeInfo().IsGenericType && 
                interfaceType.GetGenericTypeDefinition() == typeof(IEntity<>))
            {
                keyType = interfaceType.GenericTypeArguments[0];
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断指定的类型是否实现了 <see cref="IEntity{TKey}"/> 泛型接口。
    /// </summary>
    /// <param name="type">要检查的类型。</param>
    /// <returns>如果该类型实现了 <see cref="IEntity{TKey}"/> 泛型接口，则返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    public static bool IsEntityWithId(Type type) => IsEntityWithId(type, out _);

    /// <summary>
    /// 获取或设置判断类型是否为值对象的谓词。
    /// </summary>
    /// <remarks>
    /// 默认通过 <see cref="ValueObjectBase{T}"/> 类型关系判断；替换会影响后续 <see cref="IsValueObject(Type)"/> 调用。
    /// </remarks>
    public static Func<Type, bool> IsValueObjectPredicate { get; set; } = type => typeof(ValueObjectBase<>).IsAssignableFrom(type);

    /// <summary>
    /// 判断类型是否为值对象。
    /// </summary>
    /// <param name="type">要检查的类型。</param>
    /// <returns>当前值对象谓词返回 <c>true</c> 时为 <c>true</c>；否则为 <c>false</c>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> 为 <c>null</c> 时抛出。</exception>
    public static bool IsValueObject(Type type)
    {
        Check.NotNull(type, nameof(type));
        return IsValueObjectPredicate(type);
    }

    /// <summary>
    /// 判断对象实例的类型是否为值对象。
    /// </summary>
    /// <param name="obj">要检查的对象实例。</param>
    /// <returns>对象非空且其类型满足当前值对象谓词时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    public static bool IsValueObject(object obj) => obj != null && IsValueObject(obj.GetType());

    /// <summary>
    /// 验证指定类型是否实现 <see cref="IEntity"/>。
    /// </summary>
    /// <param name="type">待验证的类型。</param>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> 为 <c>null</c> 时抛出。</exception>
    /// <exception cref="ArgumentException">类型未实现 <see cref="IEntity"/> 时抛出。</exception>
    public static void CheckEntity(Type type)
    {
        Check.NotNull(type, nameof(type));
        if (!IsEntity(type))
            throw new ArgumentException($"参数 '{type.FullName}' 不是有效的实体类型。必须实现 {typeof(IEntity).FullName} 接口。", nameof(type));
    }

    #endregion

    #region 主键检查

    /// <summary>
    /// 判断单一标识实体是否使用默认标识值。
    /// </summary>
    /// <typeparam name="TKey">实体标识类型。</typeparam>
    /// <param name="entity">要检查的实体。</param>
    /// <returns>标识等于类型默认值，或为小于等于零的 <see cref="int"/> / <see cref="long"/> 时返回 <c>true</c>。</returns>
    public static bool HasDefaultId<TKey>(IEntity<TKey> entity)
    {
        if (EqualityComparer<TKey>.Default.Equals(entity.Id, default!))
            return true;
        return IsDefaultNumericKey(entity.Id);
    }

    /// <summary>
    /// 判断是否为默认的数值类型键值
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <param name="id">ID值</param>
    /// <returns>如果是默认值返回true，否则返回false</returns>
    private static bool IsDefaultNumericKey<TKey>(TKey id)
    {
        if (typeof(TKey) == typeof(int))
            return Convert.ToInt32(id) <= 0;
        if (typeof(TKey) == typeof(long))
            return Convert.ToInt64(id) <= 0;
        return false;
    }

    /// <summary>
    /// 判断实体的全部复合键是否均为默认值。
    /// </summary>
    /// <param name="entity">要检查的实体。</param>
    /// <returns>实体的全部键均为默认值时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entity"/> 为 <c>null</c> 时抛出。</exception>
    public static bool HasDefaultKeys(IEntity entity)
    {
        Check.NotNull(entity, nameof(entity));
        foreach (var key in entity.GetKeys())
        {
            if (!IsDefaultKeyValue(key))
                return false;
        }
        return true;
    }

    /// <summary>
    /// 是否默认主键值
    /// </summary>
    /// <param name="value">值</param>
    /// <returns>true: 默认值, false: 非默认值</returns>
    private static bool IsDefaultKeyValue(object value)
    {
        if (value == null)
            return true;
        var type = value.GetType();
        if (type == typeof(int))
            return Convert.ToInt32(value) <= 0;
        if (type == typeof(long))
            return Convert.ToInt64(value) <= 0;
        return Types.IsDefaultValue(value);
    }

    #endregion

    #region 主键类型查找

    /// <summary>
    /// 获取指定实体类型的主键类型。
    /// </summary>
    /// <typeparam name="TEntity">要获取主键类型的实体类型，必须实现 <see cref="IEntity"/> 接口。</typeparam>
    /// <returns>主键的类型。</returns>
    /// <exception cref="ArgumentException">如果 <typeparamref name="TEntity"/> 不是实体类型，则抛出异常。</exception>
    public static Type FindPrimaryKeyType<TEntity>() where TEntity : IEntity => FindPrimaryKeyType(typeof(TEntity));

    /// <summary>
    /// 获取指定实体类型的主键类型。
    /// </summary>
    /// <param name="entityType">要检查的实体类型。</param>
    /// <returns>如果 <paramref name="entityType"/> 实现了 <see cref="IEntity{TKey}"/>，则返回主键的类型；否则返回 <c>null</c>。</returns>
    /// <exception cref="ArgumentNullException">如果 <paramref name="entityType"/> 为 <c>null</c>，则抛出异常。</exception>
    /// <exception cref="ArgumentException">如果 <paramref name="entityType"/> 不是有效的实体类型（未实现 <see cref="IEntity"/> 接口），则抛出异常。</exception>
    public static Type FindPrimaryKeyType(Type entityType)
    {
        Check.NotNull(entityType, nameof(entityType));
        CheckEntity(entityType);

        if (IsEntityWithId(entityType, out var keyType))
            return keyType;

        return null;
    }

    #endregion

    #region 表达式构建

    /// <summary>
    /// 创建一个用于比较实体 ID 是否相等的 Lambda 表达式。
    /// </summary>
    /// <typeparam name="TEntity">实体类型，必须实现 <see cref="IEntity{TKey}"/>。</typeparam>
    /// <typeparam name="TKey">主键类型。</typeparam>
    /// <param name="id">要匹配的实体 ID。</param>
    /// <returns>
    /// 返回一个表达式 <see cref="Expression{TDelegate}"/>，用于检查实体的 ID 是否等于指定的 <paramref name="id"/>。
    /// </returns>
    /// <exception cref="ArgumentNullException">如果 <paramref name="id"/> 为空，则抛出异常。</exception>
    /// <exception cref="InvalidOperationException">如果实体类型没有名为 "Id" 的属性或字段，则抛出异常。</exception>
    public static Expression<Func<TEntity, bool>> CreateEqualityExpressionForId<TEntity, TKey>(TKey id)
        where TEntity : IEntity<TKey>
    {
        if (id == null)
            throw new ArgumentNullException(nameof(id));
        var lambdaParam = Expression.Parameter(typeof(TEntity));
        var leftExpression = Expression.PropertyOrField(lambdaParam, "Id"); // 访问 entity.Id
        var idValue = Convert.ChangeType(id, typeof(TKey)); // 转换 ID 为 TKey 类型
        Expression<Func<object>> closure = () => idValue; // 闭包以保证表达式转换
        var rightExpression = Expression.Convert(closure.Body, leftExpression.Type); // 转换右侧值为匹配类型
        var lambdaBody = Expression.Equal(leftExpression, rightExpression); // 生成 entity.Id == id 的比较表达式
        return Expression.Lambda<Func<TEntity, bool>>(lambdaBody, lambdaParam);
    }

    #endregion
}

using System.Linq.Expressions;
using System.Reflection;
using Bing.Domain.Values;
using Bing.Helpers;
using Bing.Reflection;

namespace Bing.Domain.Entities;

/// <summary>
/// 实体帮助类
/// </summary>
public static class EntityHelper
{
    /// <summary>
    /// Guid 生成函数
    /// </summary>
    public static Func<Guid> GuidGenerateFunc { get; set; } = Guid.NewGuid;

    /// <summary>
    /// 判断两个 <see cref="IEntity"/> 实例是否相等。
    /// </summary>
    /// <param name="entity1">第一个实体对象。</param>
    /// <param name="entity2">第二个实体对象。</param>
    /// <returns>
    /// 如果两个实体对象相等，则返回 <c>true</c>；否则返回 <c>false</c>。
    /// </returns>
    public static bool EntityEquals(IEntity entity1, IEntity entity2)
    {
        if (entity1 == null || entity2 == null)
            return false;

        // 如果引用相同，则直接返回 true
        if (ReferenceEquals(entity1, entity2))
            return true;

        // 如果两个实体类型不兼容，则返回 false
        var typeOfEntity1 = entity1.GetType();
        var typeOfEntity2 = entity2.GetType();
        if (!typeOfEntity1.IsAssignableFrom(typeOfEntity2) && !typeOfEntity2.IsAssignableFrom(typeOfEntity1))
            return false;

        // 多租户委托检查

        // 瞬时对象不视为相等
        if (HasDefaultKeys(entity1) && HasDefaultKeys(entity2))
            return false;

        // 如果键数量不匹配，则不相等
        var entity1Keys = entity1.GetKeys();
        var entity2Keys = entity2.GetKeys();
        if(entity1Keys.Length!=entity2Keys.Length)
            return false;

        // 逐个比较主键值
        for (var i = 0; i < entity1Keys.Length; i++)
        {
            // 如果 `entity1Key` 为 null，`entity2Key` 也必须为 null，否则不相等
            var entity1Key = entity1Keys[i];
            var entity2Key = entity2Keys[i];
            if (entity1Key == null)
            {
                if (entity2Key == null)
                    continue;
                return false;
            }

            // 如果 `entity2Key` 为 null，则不相等
            if (entity2Key == null)
                return false;

            // 如果两个键值都是默认值（如 0、null、Guid.Empty），则继续比较
            if (Types.IsDefaultValue(entity1Key) && Types.IsDefaultValue(entity2Key))
                return false;

            // 进行键值比较，如果不同，则返回 false
            if (!entity1Key.Equals(entity2Key))
                return false;
        }
        return true;
    }

    /// <summary>
    /// 判断指定的类型是否实现了 <see cref="IEntity"/> 接口。
    /// </summary>
    /// <param name="type">要检查的类型。</param>
    /// <returns>是否为实体类型。如果类型实现了<see cref="IEntity"/>接口，则返回true；否则返回false</returns>
    /// <exception cref="ArgumentNullException">当<paramref name="type"/>为null时抛出</exception>
    public static bool IsEntity(Type type)
    {
        Check.NotNull(type, nameof(type));
        return typeof(IEntity).IsAssignableFrom(type);
    }

    /// <summary>
    /// 值对象判断谓词
    /// </summary>
    /// <remarks>
    /// 用于判断一个类型是否为值对象类型。默认实现为检查是否继承自<see cref="ValueObjectBase{T}"/>。
    /// </remarks>
    public static Func<Type, bool> IsValueObjectPredicate = type => typeof(ValueObjectBase<>).IsAssignableFrom(type);

    /// <summary>
    /// 是否值对象类型
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>是否为值对象类型。如果类型符合值对象判断条件，则返回true；否则返回false</returns>
    /// <exception cref="ArgumentNullException">当<paramref name="type"/>为null时抛出</exception>
    public static bool IsValueObject(Type type)
    {
        Check.NotNull(type, nameof(type));
        return IsValueObjectPredicate(type);
    }

    /// <summary>
    /// 是否值对象
    /// </summary>
    /// <param name="obj">对象实例</param>
    /// <returns>是否为值对象。如果对象不为null且其类型符合值对象判断条件，则返回true；否则返回false</returns>
    public static bool IsValueObject(object obj)
    {
        return obj != null && IsValueObject(obj.GetType());
    }

    /// <summary>
    /// 检查指定的类型是否为实体
    /// </summary>
    /// <param name="type">类型</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public static void CheckEntity(Type type)
    {
        Check.NotNull(type, nameof(type));
        if (!IsEntity(type))
            throw new ArgumentException($"参数 '{type.FullName}' 不是有效的实体类型。必须实现 {typeof(IEntity).FullName} 接口。", nameof(type));
    }

    /// <summary>
    /// 判断指定的类型是否实现了 <see cref="IEntity{TKey}"/> 泛型接口。
    /// </summary>
    /// <param name="type">要检查的类型。</param>
    /// <returns>如果该类型实现了 <see cref="IEntity{TKey}"/> 泛型接口，则返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    public static bool IsEntityWithId(Type type)
    {
        Check.NotNull(type, nameof(type));
        foreach (var interfaceType in type.GetInterfaces())
        {
            if (interfaceType.GetTypeInfo().IsGenericType &&
               interfaceType.GetGenericTypeDefinition() == typeof(IEntity<>))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 是否有默认标识值
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    /// <param name="entity">实体</param>
    /// <returns>
    /// 如果实体的标识为默认值，则返回true；否则返回false。
    /// 对于整数类型（int、long）的标识，小于等于0视为默认值。
    /// 对于其他类型，等于类型默认值视为默认值。
    /// </returns>
    public static bool HasDefaultId<TKey>(IEntity<TKey> entity)
    {
        if (EqualityComparer<TKey>.Default.Equals(entity.Id, default!))
            return true;
        if (typeof(TKey) == typeof(int))
            return Convert.ToInt32(entity.Id) <= 0;
        if (typeof(TKey) == typeof(long))
            return Convert.ToInt64(entity.Id) <= 0;
        return false;
    }

    /// <summary>
    /// 是否有默认值
    /// </summary>
    /// <param name="entity">实体</param>
    /// <returns>是否为默认值。如果所有键都是默认值，则返回true；否则返回false</returns>
    /// <exception cref="ArgumentNullException">当<paramref name="entity"/>为null时抛出</exception>
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

    /// <summary>
    /// 获取指定实体类型的主键类型。
    /// </summary>
    /// <typeparam name="TEntity">要获取主键类型的实体类型，必须实现 <see cref="IEntity"/> 接口。</typeparam>
    /// <returns>主键的类型。</returns>
    /// <exception cref="ArgumentException">如果 <typeparamref name="TEntity"/> 不是实体类型，则抛出异常。</exception>
    public static Type FindPrimaryKeyType<TEntity>()
        where TEntity : IEntity
    {
        return FindPrimaryKeyType(typeof(TEntity));
    }

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
        if (!typeof(IEntity).IsAssignableFrom(entityType))
            throw new ArgumentException($"参数 '{entityType.FullName}' 不是有效的实体类型。必须实现 {typeof(IEntity).FullName} 接口。", nameof(entityType));

        foreach (var interfaceType in entityType.GetTypeInfo().GetInterfaces())
        {
            if (interfaceType.GetTypeInfo().IsGenericType &&
               interfaceType.GetGenericTypeDefinition() == typeof(IEntity<>))
                return interfaceType.GenericTypeArguments[0];
        }
        return null;
    }

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
}

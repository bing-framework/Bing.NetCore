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
    #region ID生成相关

    /// <summary>
    /// ID生成器字典
    /// </summary>
    private static readonly IDictionary<Type, Func<object>> _idGenerators = new Dictionary<Type, Func<object>>
    {
        { typeof(Guid), () => GuidGenerateFunc() },
        { typeof(string), () => StringGenerateFunc() },
        { typeof(long), () => LongGenerateFunc() },
        { typeof(int), () => IntGenerateFunc() }
    };

    /// <summary>
    /// Guid 生成函数，允许外部自定义生成方式。
    /// </summary>
    public static Func<Guid> GuidGenerateFunc { get; set; } = Guid.NewGuid;

    /// <summary>
    /// String ID 生成函数（默认为 Guid 字符串）。
    /// </summary>
    public static Func<string> StringGenerateFunc { get; set; } = () => GuidGenerateFunc().ToString();

    /// <summary>
    /// Long ID 生成函数（默认为雪花 ID）。
    /// </summary>
    public static Func<long> LongGenerateFunc { get; set; }

    /// <summary>
    /// Int ID 生成函数（默认不支持）。
    /// </summary>
    public static Func<int> IntGenerateFunc { get; set; } = () => throw new InvalidOperationException("不支持 Int 作为 ID，请使用 Guid, string 或 long。");

    /// <summary>
    /// 生成唯一标识 ID，默认使用 Guid 类型。
    /// </summary>
    /// <returns>生成的 Guid 值</returns>
    public static Guid CreateGuid() => CreateKey<Guid>();

    /// <summary>
    /// 生成唯一标识 ID，支持 Guid、string、long 类型。
    /// </summary>
    /// <typeparam name="TKey">ID 类型</typeparam>
    /// <returns>生成的 ID 值</returns>
    public static TKey CreateKey<TKey>()
    {
        if (_idGenerators.TryGetValue(typeof(TKey), out var generator))
            return (TKey)generator();
        throw new InvalidOperationException($"不支持的 ID 类型: {typeof(TKey)}，请使用 Guid, string, long。");
    }

    /// <summary>
    /// 注册自定义 ID 生成器
    /// </summary>
    /// <typeparam name="TKey">ID 类型</typeparam>
    /// <param name="generator">生成器函数</param>
    /// <exception cref="ArgumentNullException">当<paramref name="generator"/>为null时抛出</exception>
    public static void RegisterIdGenerator<TKey>(Func<TKey> generator)
    {
        Check.NotNull(generator, nameof(generator));
        _idGenerators[typeof(TKey)] = () => generator();
    }

    #endregion

    #region 实体相等性比较

    /// <summary>
    /// 判断实体类型是否为多租户实体。
    /// </summary>
    public static Func<IEntity, IEntity, bool> IsMultiTenantEntity { get; set; } = (_, _) => false;

    /// <summary>
    /// 在不同租户下是否允许相同 ID 作为相等的规则（默认：不允许）。
    /// </summary>
    public static Func<IEntity, IEntity, bool> AllowSameIdAcrossTenants { get; set; } = (_, _) => false;

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
    /// 判断指定的类型是否实现了 <see cref="IEntity{TKey}"/> 接口
    /// </summary>
    /// <param name="type">要检查的类型</param>
    /// <param name="keyType">如果找到，则输出键类型；否则为null</param>
    /// <returns>是否为带主键的实体类型</returns>
    /// <exception cref="ArgumentNullException">当<paramref name="type"/>为null时抛出</exception>
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
    /// 值对象判断谓词
    /// </summary>
    /// <remarks>
    /// 用于判断一个类型是否为值对象类型。默认实现为检查是否继承自<see cref="ValueObjectBase{T}"/>。
    /// </remarks>
    public static Func<Type, bool> IsValueObjectPredicate { get; set; } = type => typeof(ValueObjectBase<>).IsAssignableFrom(type);

    /// <summary>
    /// 是否值对象类型
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>是否为值对象</returns>
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
    /// <returns>是否为值对象</returns>
    public static bool IsValueObject(object obj) => obj != null && IsValueObject(obj.GetType());

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

    #endregion

    #region 主键检查

    /// <summary>
    /// 判断实体是否有默认标识值
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
    /// 判断实体是否有默认键值
    /// </summary>
    /// <param name="entity">实体</param>
    /// <returns>是否为默认值</returns>
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

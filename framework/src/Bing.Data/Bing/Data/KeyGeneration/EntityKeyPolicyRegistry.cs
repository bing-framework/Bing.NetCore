using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Data.KeyGeneration;

/// <summary>
/// 构建不可变主键策略集合的配置构建器。
/// </summary>
/// <remarks>
/// 注册阶段使用的策略构建器，Build 后不可再次修改。
/// </remarks>
public sealed class EntityKeyPolicyBuilder
{
    /// <summary>
    /// 保存键策略构建期间登记的实体类型与策略映射。
    /// </summary>
    private readonly Dictionary<Type, EntityKeyPolicy> _policies = new Dictionary<Type, EntityKeyPolicy>();
    /// <summary>
    /// 指示键策略构建器是否已冻结而不再接受修改。
    /// </summary>
    private bool _frozen;
    /// <summary>注册静态主键访问器，用于 Native AOT 及生成模型。</summary>
    public EntityKeyPolicyBuilder ForGenerated<TEntity, TKey>(string propertyName, EntityKeyGenerationKind kind,
        Func<TEntity, TKey> getter, Action<TEntity, TKey> setter, string generatorName = "default")
    {
        if (_frozen) throw new InvalidOperationException("主键策略已经冻结。");
        var policy = EntityKeyPolicy.CreateGenerated(propertyName, kind, getter, setter, generatorName);
        if (_policies.ContainsKey(typeof(TEntity))) throw new InvalidOperationException("实体已注册主键策略。");
        _policies.Add(typeof(TEntity), policy);
        return this;
    }
    /// <summary>
    /// 注册实体主键生成策略。
    /// </summary>
    /// <typeparam name="TEntity">待插入或注册的实体类型。</typeparam>
    /// <typeparam name="TKey">实体主键的类型。</typeparam>
    /// <param name="key">直接选择实体主键属性的表达式。</param>
    /// <param name="kind">实体主键生成方式。</param>
    /// <param name="generatorName">客户端主键生成器名称。</param>
    /// <returns>当前主键策略构建器。</returns>
    /// <remarks>必须直接选择实体属性；构建器冻结后或同一实体重复注册时拒绝操作。</remarks>
    public EntityKeyPolicyBuilder For<TEntity, TKey>(Expression<Func<TEntity, TKey>> key, EntityKeyGenerationKind kind, string generatorName = "default")
    {
        if (_frozen) throw new InvalidOperationException("主键策略已经冻结。");
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (!(key.Body is MemberExpression member) || !(member.Member is PropertyInfo property) || member.Expression != key.Parameters[0])
            throw new ArgumentException("必须直接选择实体的主键属性。", nameof(key));
        var policy = new EntityKeyPolicy(typeof(TEntity), property, kind, generatorName);
        if (_policies.ContainsKey(typeof(TEntity))) throw new InvalidOperationException("实体已注册主键策略。");
        _policies.Add(typeof(TEntity), policy);
        return this;
    }
    /// <summary>
    /// 构建并冻结实体主键策略注册。
    /// </summary>
    /// <returns>由当前策略集合创建的只读策略提供器。</returns>
    public IEntityKeyPolicyProvider Build()
    {
        _frozen = true;
        return new EntityKeyPolicyRegistry(_policies.Values);
    }
}

/// <summary>
/// 按实例隔离的不可变主键策略注册表。
/// </summary>
/// <remarks>
/// 实例隔离的不可变策略集合；支持 ORM 代理继承类型。
/// </remarks>
public sealed class EntityKeyPolicyRegistry : IEntityKeyPolicyProvider
{
    /// <summary>
    /// 保存冻结后的实体类型与键策略只读映射。
    /// </summary>
    private readonly IReadOnlyDictionary<Type, EntityKeyPolicy> _policies;
    /// <inheritdoc />
    public IReadOnlyCollection<EntityKeyPolicy> Policies { get; }
    /// <summary>
    /// 初始化一个 <see cref="EntityKeyPolicyRegistry" /> 类型的实例。
    /// </summary>
    /// <param name="policies">用于选择或应用任务及实体键策略的策略集合或提供程序。</param>
    public EntityKeyPolicyRegistry(IEnumerable<EntityKeyPolicy> policies)
    {
        if (policies == null) throw new ArgumentNullException(nameof(policies));
        var map = policies.ToDictionary(x => x.EntityType);
        _policies = new ReadOnlyDictionary<Type, EntityKeyPolicy>(map);
        Policies = new ReadOnlyCollection<EntityKeyPolicy>(map.Values.ToList());
    }
    /// <inheritdoc />
    public EntityKeyPolicy Find(Type entityType)
    {
        if (entityType == null) throw new ArgumentNullException(nameof(entityType));
        for (var type = entityType; type != null; type = type.BaseType)
            if (_policies.TryGetValue(type, out var policy)) return policy;
        return null;
    }
}

/// <summary>
/// 主键策略与准备服务注册扩展。
/// </summary>
/// <remarks>
/// 主键策略及准备服务注册。
/// </remarks>
public static class EntityKeyRegistration
{
    /// <summary>
    /// 注册实体主键策略与客户端生成服务。
    /// </summary>
    /// <param name="services">依赖注入服务集合。</param>
    /// <param name="configure">配置实体主键生成策略的委托。</param>
    /// <returns>已完成主键策略注册的服务集合。</returns>
    /// <remarks>每个容器只能注册一次；默认 GUID 生成器按实例注入，不修改全局静态委托。</remarks>
    public static IServiceCollection AddEntityKeyPolicies(this IServiceCollection services, Action<EntityKeyPolicyBuilder> configure)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configure == null) throw new ArgumentNullException(nameof(configure));
        if (services.Any(x => x.ServiceType == typeof(IEntityKeyPolicyProvider))) throw new InvalidOperationException("主键策略只能注册一次。");
        var builder = new EntityKeyPolicyBuilder();
        configure(builder);
        services.AddSingleton(builder.Build());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IClientKeyGenerator<Guid>, GuidKeyGenerator>());
        services.TryAddTransient<EntityKeyInitializer>();
        return services;
    }
}

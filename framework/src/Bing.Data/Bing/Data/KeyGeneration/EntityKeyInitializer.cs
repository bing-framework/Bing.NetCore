using System;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.KeyGeneration;

/// <summary>
/// 客户端主键生成器。
/// </summary>
/// <remarks>
/// 实现应线程安全；数据库 Identity 不实现此契约。
/// </remarks>
/// <typeparam name="TKey">实体主键类型。</typeparam>
public interface IClientKeyGenerator<TKey>
{
    /// <summary>
    /// 获取客户端主键生成器名称。
    /// </summary>
    string Name { get; }
    /// <summary>
    /// 生成下一个客户端主键。
    /// </summary>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <returns>新生成的客户端主键。</returns>
    TKey Next(CancellationToken cancellationToken = default);
}

/// <summary>
/// 生成随机 GUID 主键的默认策略。
/// </summary>
/// <remarks>
/// 保留随机 GUID 默认行为。
/// </remarks>
public sealed class GuidKeyGenerator : IClientKeyGenerator<Guid>
{
    /// <inheritdoc />
    public string Name => "default";
    /// <inheritdoc />
    public Guid Next(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Guid.NewGuid();
    }
}

/// <summary>
/// 为新实体准备主键的初始化器。
/// </summary>
/// <remarks>
/// 只准备新实体主键，不重复调用领域 Init，不触发数据库操作。
/// </remarks>
public sealed class EntityKeyInitializer
{
    /// <summary>
    /// 提供实体键策略以初始化新实体键。
    /// </summary>
    private readonly IEntityKeyPolicyProvider _policies;
    /// <summary>
    /// 用于解析实体键生成器及策略相关服务。
    /// </summary>
    private readonly IServiceProvider _services;
    /// <summary>
    /// 初始化一个 <see cref="EntityKeyInitializer" /> 类型的实例。
    /// </summary>
    /// <param name="policies">用于选择或应用任务及实体键策略的策略集合或提供程序。</param>
    /// <param name="services">用于注册或解析实例依赖项的服务集合。</param>
    public EntityKeyInitializer(IEntityKeyPolicyProvider policies, IServiceProvider services)
    {
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }
    /// <summary>
    /// 按生成策略准备新实体主键。
    /// </summary>
    /// <param name="entity">待读取、赋值或准备主键的领域实体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <remarks>保留已有有效客户端键；数据库自增策略只接受默认键，未注册策略的实体保持原值。</remarks>
    public void Prepare(object entity, CancellationToken cancellationToken = default)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        cancellationToken.ThrowIfCancellationRequested();
        var policy = _policies.Find(entity.GetType());
        if (policy == null) return;
        Prepare(entity, policy, cancellationToken);
    }
    /// <summary>
    /// 使用调用方已选定的策略准备新实体主键。
    /// </summary>
    /// <param name="entity">待准备主键的实体；可以是策略实体类型的派生实例。</param>
    /// <param name="policy">与本次写入映射一致的主键策略。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <remarks>不再按运行时类型查找策略，确保主键准备、SQL 参数及回填使用相同访问器。三个参数均需显式传入，保留原两参数调用的重载解析。</remarks>
    public void Prepare(object entity, EntityKeyPolicy policy, CancellationToken cancellationToken)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        if (policy == null) throw new ArgumentNullException(nameof(policy));
        cancellationToken.ThrowIfCancellationRequested();
        var value = policy.GetKey(entity);
        var empty = policy.IsDefault(value);
        if (policy.Kind == EntityKeyGenerationKind.DatabaseIdentity)
        {
            if (!empty) throw new InvalidOperationException("自增实体的普通创建不能指定主键。回滚重试必须重建实体。");
            return;
        }
        if (policy.Kind == EntityKeyGenerationKind.Assigned)
        {
            if (empty) throw new InvalidOperationException("显式赋值策略缺少主键。");
            return;
        }
        if (policy.Kind == EntityKeyGenerationKind.ClientSnowflake && !empty && (long)value <= 0)
            throw new InvalidOperationException("雪花主键必须为正数。");
        if (!empty) return;
        value = policy.Kind == EntityKeyGenerationKind.ClientGuid
            ? (object)Generate<Guid>(policy, cancellationToken) : Generate<long>(policy, cancellationToken);
        if (policy.IsDefault(value) || value is long id && id <= 0)
            throw new InvalidOperationException("生成器返回了无效主键。");
        policy.SetKey(entity, value);
    }
    /// <summary>
    /// 调用指定名称的客户端生成器生成主键。
    /// </summary>
    /// <typeparam name="TKey">实体主键的类型。</typeparam>
    /// <param name="policy">实体主键生成策略。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>客户端生成器产生的主键值。</returns>
    /// <remarks>必须存在且仅存在一个名称匹配的客户端生成器。</remarks>
    private TKey Generate<TKey>(EntityKeyPolicy policy, CancellationToken cancellationToken)
    {
        var generators = _services.GetServices<IClientKeyGenerator<TKey>>().Where(x => x.Name == policy.GeneratorName).ToArray();
        if (generators.Length != 1) throw new InvalidOperationException($"必须为 {policy.EntityType.Name} 注册唯一生成器 {policy.GeneratorName}。");
        return generators[0].Next(cancellationToken);
    }
}

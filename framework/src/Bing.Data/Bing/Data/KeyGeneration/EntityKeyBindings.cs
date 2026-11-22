using System.Runtime.CompilerServices;

namespace Bing.Data.KeyGeneration;

/// <summary>
/// 可选的领域对象与持久化对象键回填能力，不改变既有 IStore 契约。
/// </summary>
public interface IEntityKeyBindingStore
{
    /// <summary>
    /// 在 Add 前绑定对象引用。
    /// </summary>
    /// <remarks>返回的动作在 Add 成功后同步客户端键，数据库键由 Flush 同步。</remarks>
    /// <param name="entity">需要回填主键的领域对象。</param>
    /// <param name="persistenceEntity">用于实际持久化的对象。</param>
    /// <returns>Add 成功后同步客户端主键的动作；数据库主键由 Flush 同步。</returns>
    Action BindGeneratedKey(object entity, object persistenceEntity);
}

/// <summary>
/// 记录工作单元内新增领域对象与持久化对象键关联的集合。
/// </summary>
/// <remarks>
/// 工作单元内的键关联。仅关联新增对象；不依赖实体按 Id 实现的 Equals。
/// </remarks>
public sealed class EntityKeyBindings
{
    /// <summary>
    /// 提供用于解析实体键策略的提供程序。
    /// </summary>
    private readonly IEntityKeyPolicyProvider _policies;
    /// <summary>
    /// 保存已登记但尚未完成回写的实体键回填动作，以实体引用为键。
    /// </summary>
    private readonly Dictionary<object, Func<bool>> _pending = new(new ReferenceComparer());

    /// <summary>
    /// 初始化一个 <see cref="EntityKeyBindings" /> 类型的实例。
    /// </summary>
    /// <param name="policies">用于选择或应用任务及实体键策略的策略集合或提供程序。</param>
    /// <remarks>策略为空时继续未注册实体的既有行为；工作单元自身不支持并行操作。</remarks>
    public EntityKeyBindings(IEntityKeyPolicyProvider policies) => _policies = policies;

    /// <summary>
    /// 绑定领域实体与持久化实体的生成键。
    /// </summary>
    /// <param name="entity">待读取、赋值或准备主键的领域实体。</param>
    /// <param name="persistenceEntity">接收数据库生成键的持久化实体。</param>
    /// <returns>用于同步该实体生成键的动作；无需绑定时返回空操作。</returns>
    /// <remarks>校验对象类型对应的策略、键类型和转换后的键值；成功回填后移除待处理绑定。</remarks>
    public Action Bind(object entity, object persistenceEntity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        if (persistenceEntity == null) throw new ArgumentNullException(nameof(persistenceEntity));
        var poPolicy = _policies?.Find(persistenceEntity.GetType());
        if (poPolicy == null || ReferenceEquals(entity, persistenceEntity)) return () => { };
        var entityPolicy = _policies.Find(entity.GetType());
        if (entityPolicy != null && (entityPolicy.Kind != poPolicy.Kind || entityPolicy.GeneratorName != poPolicy.GeneratorName))
            throw new InvalidOperationException("领域对象和持久化对象的主键策略必须一致。");
        entityPolicy ??= new EntityKeyPolicy(entity.GetType(), entity.GetType().GetProperty(poPolicy.KeyPropertyName)
            ?? throw new InvalidOperationException("领域对象缺少对应主键属性。"), EntityKeyGenerationKind.Assigned);
        var original = entityPolicy.GetKey(entity);
        if (entityPolicy.KeyType != poPolicy.KeyType || !Equals(original, poPolicy.GetKey(persistenceEntity)))
            throw new InvalidOperationException("转换必须保留原主键；领域对象与持久化对象的键类型必须一致。");
        if (_pending.ContainsKey(persistenceEntity)) throw new InvalidOperationException("同一个持久化对象已经绑定。");
        _pending.Add(persistenceEntity, () =>
        {
            var key = poPolicy.GetKey(persistenceEntity);
            if (poPolicy.IsDefault(key)) return false;
            var current = entityPolicy.GetKey(entity);
            if (!Equals(current, original) && !Equals(current, key))
                throw new InvalidOperationException("等待回填期间不能更改领域对象主键。");
            entityPolicy.SetKey(entity, key);
            return true;
        });
        return () => Synchronize(persistenceEntity);
    }

    /// <summary>
    /// 回填已生成的实体主键。
    /// </summary>
    /// <remarks>在物理保存成功后、事务内后续逻辑前处理全部绑定，不提交事务。</remarks>
    public void Synchronize()
    {
        foreach (var po in _pending.Keys.ToArray()) Synchronize(po);
    }

    /// <summary>
    /// 回填已生成的实体主键。
    /// </summary>
    /// <param name="po">待同步生成键的持久化实体。</param>
    /// <remarks>仅处理指定持久化对象；主键已生成并完成同步后移除绑定。</remarks>
    private void Synchronize(object po)
    {
        if (_pending.TryGetValue(po, out var synchronize) && synchronize()) _pending.Remove(po);
    }

    /// <summary>
    /// 按对象引用比较实体键绑定实例。
    /// </summary>
    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        /// <inheritdoc />
        public new bool Equals(object left, object right) => ReferenceEquals(left, right);
        /// <inheritdoc />
        public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
    }
}

using System.Data;
using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// 协调 SQL 执行资源与业务工作单元之间的绑定、租约和回滚传播。
/// </summary>
/// <remarks>
/// 该类型只持有 UnitOfWork 状态；连接、事务和执行清理仍由
/// <see cref="SqlExecutionResources" /> 的通用算法负责；查询和写入各自保存状态，只借用 Uow 连接及事务。
/// </remarks>
internal struct SqlUnitOfWorkResourceCoordinator
{
    private readonly SqlExecutionResources _owner;

    /// <summary>当前绑定的工作单元根作用域。</summary>
    internal IUnitOfWorkScope Root;

    /// <summary>当前工作单元连接资源。</summary>
    internal IUnitOfWorkConnectionResource Resource;

    /// <summary>是否已经决定工作单元亲和性。</summary>
    internal bool AffinityDecided;

    /// <summary>是否显式要求独立于工作单元。</summary>
    internal bool ExplicitlyIndependent;

    /// <summary>是否处于显式事务作用域模式。</summary>
    internal bool ExplicitTransactionScopeMode;

    /// <summary>
    /// 初始化工作单元资源协调器。
    /// </summary>
    /// <param name="owner">当前查询或写入资源所有者。</param>
    internal SqlUnitOfWorkResourceCoordinator(SqlExecutionResources owner) =>
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));

    /// <summary>
    /// 标记由显式 SQL 事务作用域创建的 owner Query，使其保持独立连接所有权。
    /// </summary>
    internal void SetExplicitTransactionScopeMode() => ExplicitTransactionScopeMode = true;

    /// <summary>
    /// 在首次执行时关联当前业务 Uow，并获取共享资源执行租约。
    /// </summary>
    /// <returns>资源租约；无可参与的 Uow 时返回空。</returns>
    internal IDisposable AcquireResourceLease()
    {
        var manager = _owner.ServiceProvider.GetService<IUnitOfWorkScopeManager>();
        var current = manager?.Current;
        if (AffinityDecided)
        {
            if (Root == null)
            {
                if (current != null && !ExplicitlyIndependent)
                    throw new InvalidOperationException("已在业务工作单元外执行的 SQL Query 不能迁移到后续工作单元。");
                return null;
            }
            if (!ReferenceEquals(current, Root))
                throw new InvalidOperationException("SQL Query 已绑定其他业务工作单元，不能迁移或在该范围之外执行。");
            if (Resource == null)
                AttachResourceForExecution(current);
            if (Resource == null)
                return null;
            return current.AcquireExecutionLease(Resource.ResourceKey);
        }

        AffinityDecided = true;
        ExplicitlyIndependent = IsExplicitlyIndependentFromUnitOfWork(current != null);
        if (current == null || ExplicitlyIndependent)
            return null;

        Root = current;
        AttachResourceForExecution(current);
        if (Resource == null)
            return null;
        return current.AcquireExecutionLease(Resource.ResourceKey);
    }

    /// <summary>
    /// 为执行过程关联工作单元连接资源。
    /// </summary>
    /// <param name="scope">当前业务工作单元作用域。</param>
    /// <remarks>资源关联失败且作用域仍活动时，将工作单元标记为仅回滚。</remarks>
    internal void AttachResourceForExecution(IUnitOfWorkScope scope)
    {
        try
        {
            AttachResource(scope);
        }
        catch
        {
            if (scope.IsActive)
                MarkRollbackOnly();
            throw;
        }
    }

    /// <summary>
    /// 判断当前查询是否显式独立于工作单元。
    /// </summary>
    /// <param name="hasCurrentRoot">当前存在工作单元根作用域时为 true，否则为 false。</param>
    /// <returns>存在独立事务作用域，或显式连接符合独立资源条件时为 true，否则为 false。</returns>
    internal bool IsExplicitlyIndependentFromUnitOfWork(bool hasCurrentRoot) =>
        ExplicitTransactionScopeMode || _owner._transactionScopeLease != null ||
        (_owner._connection != null && (hasCurrentRoot
            ? _owner._connectionOwnership == SqlResourceOwnership.Owned ||
              _owner._connectionSource != SqlConnectionSource.EntityFrameworkCore
            : true));

    /// <summary>
    /// 在异步执行入口的预取消路径登记参与关系并毒化活动 root，不创建数据库资源。
    /// </summary>
    internal void MarkPreCancelledExecution()
    {
        var manager = _owner.ServiceProvider.GetService<IUnitOfWorkScopeManager>();
        var current = manager?.Current;
        if (current == null || !current.IsActive)
            return;

        if (AffinityDecided)
        {
            if (Root != null && !ReferenceEquals(current, Root))
                throw new InvalidOperationException("SQL Query 已绑定其他业务工作单元，不能迁移或在该范围之外执行。");
            if (Root == null && !ExplicitlyIndependent)
                throw new InvalidOperationException("已在业务工作单元外执行的 SQL Query 不能迁移到后续工作单元。");
            if (Root == null)
                return;
        }
        else
        {
            AffinityDecided = true;
            ExplicitlyIndependent = IsExplicitlyIndependentFromUnitOfWork(hasCurrentRoot: true);
            if (ExplicitlyIndependent)
                return;
            Root = current;
        }

        current.SetRollbackOnly();
    }

    /// <summary>
    /// 选择映射 ORM 资源；没有映射时为默认 Dapper Query 登记自有资源。
    /// </summary>
    /// <param name="scope">当前 root 工作单元。</param>
    internal void AttachResource(IUnitOfWorkScope scope)
    {
        var context = _owner.Options.GetDatabaseContext();
        var connectionString = _owner.ResolveConnectionString();
        var contextSnapshot = new DatabaseContext
        {
            DbKey = context?.DbKey,
            ProviderKey = _owner.GetCurrentProviderKey(),
            TenantId = context?.TenantId,
            ReadPreference = context?.ReadPreference ?? SqlReadPreference.Default,
            MappingProfile = context?.MappingProfile,
            DataSource = context?.DataSource
        };
        var resource = _owner.ServiceProvider.GetRequiredService<ISqlUnitOfWorkResourceAccessor>().GetResource(
            scope, contextSnapshot, connectionString, createDapperResource: _owner._connection == null);
        if (resource != null)
        {
            if (_owner._connection != null && !ReferenceEquals(_owner._connection, resource.Connection))
                throw new InvalidOperationException("已映射的 ORM 连接与 SQL Query 绑定连接不是同一实例。");
            if (_owner._connection == null)
            {
                var connectionSource = resource is DapperSqlUnitOfWorkConnectionResource
                    ? SqlConnectionSource.DataSource
                    : SqlConnectionSource.EntityFrameworkCore;
                _owner.BindConnection(resource.Connection, SqlResourceOwnership.External,
                    connectionSource, validateExternalIdentity: false);
            }
            Resource = resource;
            return;
        }

        if (_owner._connection == null)
            throw new InvalidOperationException("SQL Uow 资源解析器未返回数据源连接资源。");
    }

    /// <summary>
    /// 准备工作单元连接资源并获取事务。
    /// </summary>
    /// <returns>工作单元资源的当前事务；没有关联资源或事务时为 null。</returns>
    internal IDbTransaction EnsureTransaction()
    {
        Resource?.EnsureReady();
        return Resource?.Transaction;
    }

    /// <summary>
    /// 异步准备工作单元连接资源并获取事务。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>工作单元资源的当前事务；没有关联资源或事务时为 null。</returns>
    internal async Task<IDbTransaction> EnsureTransactionAsync(CancellationToken cancellationToken)
    {
        if (Resource == null)
            return null;
        await Resource.EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
        return Resource.Transaction;
    }

    /// <summary>
    /// 将共享资源执行失败传递到业务 root，避免调用方捕获异常后仍提交。
    /// </summary>
    internal void MarkRollbackOnly()
    {
        try
        {
            Root?.SetRollbackOnly();
        }
        catch (InvalidOperationException)
        {
            // 清理过程中 root 可能已进入终态；保留原始执行异常。
        }
    }

    /// <summary>获取是否已创建工作单元资源。</summary>
    internal bool HasResource => Resource != null;

    /// <summary>获取根工作单元是否启用事务。</summary>
    internal bool IsRootTransactional => Root?.Options.IsTransactional == true;

    /// <summary>
    /// 将当前工作单元标记为执行失败。
    /// </summary>
    internal void MarkExecutionFailure() => MarkRollbackOnly();

    /// <summary>
    /// 拒绝在 Query 已加入 Uow 后覆盖其连接或事务绑定。
    /// </summary>
    internal void EnsureNoRuntimeBinding()
    {
        if (Root != null)
            throw new InvalidOperationException("SQL Query 已进入业务工作单元执行，不能覆盖其连接、事务或上下文绑定。");
    }
}

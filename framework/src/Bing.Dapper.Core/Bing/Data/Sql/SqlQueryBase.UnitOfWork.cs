using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// 管理默认 SQL Query 与业务工作单元之间的资源亲和关系。
/// </summary>
public abstract partial class SqlQueryBase
{
    /// <summary>
    /// 保存查询绑定的工作单元根作用域。
    /// </summary>
    private IUnitOfWorkScope _unitOfWorkRoot;
    /// <summary>
    /// 保存查询绑定的工作单元连接资源。
    /// </summary>
    private IUnitOfWorkConnectionResource _unitOfWorkResource;
    /// <summary>
    /// 指示查询是否已决定是否加入工作单元。
    /// </summary>
    private bool _unitOfWorkAffinityDecided;
    /// <summary>
    /// 指示查询是否被显式要求使用独立连接。
    /// </summary>
    private bool _unitOfWorkExplicitlyIndependent;
    /// <summary>
    /// 指示查询是否处于显式事务作用域模式。
    /// </summary>
    private bool _explicitTransactionScopeMode;

    /// <summary>
    /// 标记由显式 SQL 事务作用域创建的 owner Query，使其保持独立连接所有权。
    /// </summary>
    internal void SetExplicitTransactionScopeMode() => _explicitTransactionScopeMode = true;

    /// <summary>
    /// 在首次执行时关联当前业务 Uow，并获取共享资源执行租约。
    /// </summary>
    /// <returns>资源租约；无可参与的 Uow 时返回空。</returns>
    private IDisposable AcquireUnitOfWorkResourceLease()
    {
        var manager = ServiceProvider.GetService<IUnitOfWorkScopeManager>();
        var current = manager?.Current;
        if (_unitOfWorkAffinityDecided)
        {
            if (_unitOfWorkRoot == null)
            {
                if (current != null && !_unitOfWorkExplicitlyIndependent)
                    throw new InvalidOperationException("已在业务工作单元外执行的 SQL Query 不能迁移到后续工作单元。");
                return null;
            }
            if (!ReferenceEquals(current, _unitOfWorkRoot))
                throw new InvalidOperationException("SQL Query 已绑定其他业务工作单元，不能迁移或在该范围之外执行。");
            if (_unitOfWorkResource == null)
                AttachUnitOfWorkResourceForExecution(current);
            if (_unitOfWorkResource == null)
                return null;
            return current.AcquireExecutionLease(_unitOfWorkResource.ResourceKey);
        }

        _unitOfWorkAffinityDecided = true;
        _unitOfWorkExplicitlyIndependent = IsExplicitlyIndependentFromUnitOfWork(current != null);
        if (current == null || _unitOfWorkExplicitlyIndependent)
            return null;

        _unitOfWorkRoot = current;
        AttachUnitOfWorkResourceForExecution(current);
        if (_unitOfWorkResource == null)
            return null;
        return current.AcquireExecutionLease(_unitOfWorkResource.ResourceKey);
    }

    /// <summary>
    /// 为执行过程关联工作单元连接资源。
    /// </summary>
    /// <param name="scope">当前业务工作单元作用域。</param>
    /// <remarks>资源关联失败且作用域仍活动时，将工作单元标记为仅回滚。</remarks>
    private void AttachUnitOfWorkResourceForExecution(IUnitOfWorkScope scope)
    {
        try
        {
            AttachUnitOfWorkResource(scope);
        }
        catch
        {
            if (scope.IsActive)
                MarkUnitOfWorkRollbackOnly();
            throw;
        }
    }

    /// <summary>
    /// 判断当前查询是否显式独立于工作单元。
    /// </summary>
    /// <param name="hasCurrentRoot">当前存在工作单元根作用域时为 true，否则为 false。</param>
    /// <returns>存在独立事务作用域，或显式连接符合独立资源条件时为 true，否则为 false。</returns>
    private bool IsExplicitlyIndependentFromUnitOfWork(bool hasCurrentRoot) =>
        _explicitTransactionScopeMode || _transactionScopeLease != null ||
        (_connection != null && (hasCurrentRoot
            ? _connectionOwnership == SqlResourceOwnership.Owned ||
              _connectionSource != SqlConnectionSource.EntityFrameworkCore
            : true));

    /// <summary>
    /// 在异步执行入口的预取消路径登记参与关系并毒化活动 root，不创建数据库资源。
    /// </summary>
    private protected void MarkPreCancelledUnitOfWorkExecution()
    {
        var manager = ServiceProvider.GetService<IUnitOfWorkScopeManager>();
        var current = manager?.Current;
        if (current == null || !current.IsActive)
            return;

        if (_unitOfWorkAffinityDecided)
        {
            if (_unitOfWorkRoot != null && !ReferenceEquals(current, _unitOfWorkRoot))
                throw new InvalidOperationException("SQL Query 已绑定其他业务工作单元，不能迁移或在该范围之外执行。");
            if (_unitOfWorkRoot == null && !_unitOfWorkExplicitlyIndependent)
                throw new InvalidOperationException("已在业务工作单元外执行的 SQL Query 不能迁移到后续工作单元。");
            if (_unitOfWorkRoot == null)
                return;
        }
        else
        {
            _unitOfWorkAffinityDecided = true;
            _unitOfWorkExplicitlyIndependent = IsExplicitlyIndependentFromUnitOfWork(hasCurrentRoot: true);
            if (_unitOfWorkExplicitlyIndependent)
                return;
            _unitOfWorkRoot = current;
        }

        current.SetRollbackOnly();
    }

    /// <summary>
    /// 选择映射 ORM 资源；没有映射时为默认 Dapper Query 登记自有资源。
    /// </summary>
    /// <param name="scope">当前 root 工作单元。</param>
    private void AttachUnitOfWorkResource(IUnitOfWorkScope scope)
    {
        var context = Options.GetDatabaseContext();
        var connectionString = ResolveConnectionString();
        var contextSnapshot = new DatabaseContext
        {
            DbKey = context?.DbKey,
            ProviderKey = GetCurrentProviderKey(),
            TenantId = context?.TenantId,
            ReadPreference = context?.ReadPreference ?? SqlReadPreference.Default,
            MappingProfile = context?.MappingProfile,
            DataSource = context?.DataSource
        };
        var resource = ServiceProvider.GetRequiredService<ISqlUnitOfWorkResourceAccessor>().GetResource(
            scope, contextSnapshot, connectionString, createDapperResource: _connection == null);
        if (resource != null)
        {
            if (_connection != null && !ReferenceEquals(_connection, resource.Connection))
                throw new InvalidOperationException("已映射的 ORM 连接与 SQL Query 绑定连接不是同一实例。");
            if (_connection == null)
            {
                var connectionSource = resource is DapperSqlUnitOfWorkConnectionResource
                    ? SqlConnectionSource.DataSource
                    : SqlConnectionSource.EntityFrameworkCore;
                BindConnection(resource.Connection, SqlResourceOwnership.External,
                    connectionSource, validateExternalIdentity: false);
            }
            _unitOfWorkResource = resource;
            return;
        }

        if (_connection == null)
            throw new InvalidOperationException("SQL Uow 资源解析器未返回数据源连接资源。");
    }

    /// <summary>
    /// 准备工作单元连接资源并获取事务。
    /// </summary>
    /// <returns>工作单元资源的当前事务；没有关联资源或事务时为 null。</returns>
    private IDbTransaction EnsureUnitOfWorkTransaction()
    {
        _unitOfWorkResource?.EnsureReady();
        return _unitOfWorkResource?.Transaction;
    }

    /// <summary>
    /// 准备工作单元连接资源并获取事务。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>工作单元资源的当前事务；没有关联资源或事务时为 null。</returns>
    private async Task<IDbTransaction> EnsureUnitOfWorkTransactionAsync(CancellationToken cancellationToken)
    {
        if (_unitOfWorkResource == null)
            return null;
        await _unitOfWorkResource.EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
        return _unitOfWorkResource.Transaction;
    }

    /// <summary>
    /// 将共享资源执行失败传递到业务 root，避免调用方捕获异常后仍提交。
    /// </summary>
    private void MarkUnitOfWorkRollbackOnly()
    {
        try
        {
            _unitOfWorkRoot?.SetRollbackOnly();
        }
        catch (InvalidOperationException)
        {
            // 清理过程中 root 可能已进入终态；保留原始执行异常。
        }
    }

    /// <summary>
    /// 获取是否已创建工作单元资源。
    /// </summary>
    private protected bool HasUnitOfWorkResource => _unitOfWorkResource != null;

    /// <summary>
    /// 获取根工作单元是否启用事务。
    /// </summary>
    private protected bool IsUnitOfWorkRootTransactional => _unitOfWorkRoot?.Options.IsTransactional == true;

    /// <summary>
    /// 合并调用方与工作单元操作的取消令牌。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>持有有效取消令牌及所需链接源的租约。</returns>
    /// <remarks>调用方应持有租约直到 I/O 生命周期结束，并释放可能创建的链接取消源。</remarks>
    private protected OperationCancellationLease CreateOperationCancellationLease(
        CancellationToken cancellationToken)
    {
        var scopeToken = UnitOfWorkOperationCancellation.GetCurrentToken(
            ServiceProvider.GetService<IUnitOfWorkScopeManager>());
        if (!scopeToken.CanBeCanceled || scopeToken == cancellationToken)
            return new OperationCancellationLease(cancellationToken, null);
        if (!cancellationToken.CanBeCanceled)
            return new OperationCancellationLease(scopeToken, null);
        var source = CancellationTokenSource.CreateLinkedTokenSource(scopeToken, cancellationToken);
        return new OperationCancellationLease(source.Token, source);
    }

    /// <summary>
    /// 持有合成取消源，直到对应命令、流或 reader 完全结束。
    /// </summary>
    private protected sealed class OperationCancellationLease : IDisposable
    {
        /// <summary>
        /// 保存该取消租约对应的取消源。
        /// </summary>
        private CancellationTokenSource _source;

        /// <summary>
        /// 初始化一个 <see cref="OperationCancellationLease" /> 类型的实例。
        /// </summary>
        /// <param name="token">用于取消操作的令牌。</param>
        /// <param name="source">当前操作读取内容或状态的数据源。</param>
        internal OperationCancellationLease(CancellationToken token, CancellationTokenSource source)
        {
            Token = token;
            _source = source;
        }

        /// <summary>
        /// 获取调用方令牌与业务操作令牌合成后的令牌。
        /// </summary>
        public CancellationToken Token { get; }

        /// <inheritdoc />
        public void Dispose() => Interlocked.Exchange(ref _source, null)?.Dispose();
    }

    /// <summary>
    /// 将当前工作单元标记为执行失败。
    /// </summary>
    /// <remarks>通过仅回滚标记阻止失败工作单元继续提交。</remarks>
    private protected void MarkUnitOfWorkExecutionFailure() => MarkUnitOfWorkRollbackOnly();

    /// <summary>
    /// 拒绝在 Query 已加入 Uow 后覆盖其连接或事务绑定。
    /// </summary>
    private void EnsureNoUnitOfWorkRuntimeBinding()
    {
        if (_unitOfWorkRoot != null)
            throw new InvalidOperationException("SQL Query 已进入业务工作单元执行，不能覆盖其连接、事务或上下文绑定。");
    }
}

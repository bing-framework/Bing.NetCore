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
    private IUnitOfWorkScope _unitOfWorkRoot;
    private IUnitOfWorkConnectionResource _unitOfWorkResource;
    private bool _unitOfWorkAffinityDecided;
    private bool _unitOfWorkExplicitlyIndependent;
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
    /// 确保当前连接资源已同步打开并建立 Uow 本地事务。
    /// </summary>
    private IDbTransaction EnsureUnitOfWorkTransaction()
    {
        _unitOfWorkResource?.EnsureReady();
        return _unitOfWorkResource?.Transaction;
    }

    /// <summary>
    /// 确保当前连接资源已异步打开并建立 Uow 本地事务。
    /// </summary>
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

    private protected bool HasUnitOfWorkResource => _unitOfWorkResource != null;

    private protected bool IsUnitOfWorkRootTransactional => _unitOfWorkRoot?.Options.IsTransactional == true;

    /// <summary>
    /// 将 API 调用方取消令牌与当前业务操作令牌合并，并由调用方持有至 I/O 生命周期结束。
    /// </summary>
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
        private CancellationTokenSource _source;

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

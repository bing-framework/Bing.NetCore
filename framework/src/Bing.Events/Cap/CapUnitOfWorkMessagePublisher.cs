using System.Data.Common;
using Bing.Data.Sql;
using Bing.Events.Messages;
using Bing.Tracing;
using Bing.Uow;
using DotNetCore.CAP;
using DotNetCore.CAP.Internal;
using DotNetCore.CAP.Messages;
using DotNetCore.CAP.Persistence;
using DotNetCore.CAP.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bing.Events.Cap;

/// <summary>
/// 将 CAP 消息写入当前数据源资源，并在对应 Uow 资源提交后刷新。
/// </summary>
internal sealed class CapUnitOfWorkMessagePublisher : IUnitOfWorkMessagePublisher
{
    private readonly IUnitOfWorkScopeManager _scopeManager;
    private readonly ISqlUnitOfWorkResourceAccessor _resourceAccessor;
    private readonly IDatabaseContextAccessor _databaseContextAccessor;
    private readonly ISqlDatabaseIdentityResolver _databaseIdentityResolver;
    private readonly CapUnitOfWorkRuntimeManager _runtimeManager;
    private readonly ILogger<CapUnitOfWorkMessagePublisher> _logger;

    public CapUnitOfWorkMessagePublisher(IUnitOfWorkScopeManager scopeManager,
        ISqlUnitOfWorkResourceAccessor resourceAccessor, IDatabaseContextAccessor databaseContextAccessor,
        ISqlDatabaseIdentityResolver databaseIdentityResolver, CapUnitOfWorkRuntimeManager runtimeManager,
        ILogger<CapUnitOfWorkMessagePublisher> logger)
    {
        _scopeManager = scopeManager ?? throw new ArgumentNullException(nameof(scopeManager));
        _resourceAccessor = resourceAccessor ?? throw new ArgumentNullException(nameof(resourceAccessor));
        _databaseContextAccessor = databaseContextAccessor ?? throw new ArgumentNullException(nameof(databaseContextAccessor));
        _databaseIdentityResolver = databaseIdentityResolver ?? throw new ArgumentNullException(nameof(databaseIdentityResolver));
        _runtimeManager = runtimeManager ?? throw new ArgumentNullException(nameof(runtimeManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync<TMessage>(string dataSourceKey, TMessage message,
        CancellationToken cancellationToken = default) where TMessage : IMessageEvent
    {
        if (string.IsNullOrWhiteSpace(dataSourceKey))
            throw new ArgumentException("SQL 数据源键不能为空。", nameof(dataSourceKey));
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        if (message.Send)
            throw new InvalidOperationException(
                "事务型 CAP 发布入口要求 MessageEvent.Send=false；该入口会立即写当前 Uow 事务中的 Outbox。");

        var scope = _scopeManager.Current ??
                    throw new InvalidOperationException("CAP Uow 发布要求存在活动的事务型业务工作单元。");
        if (!scope.IsActive || scope.IsDisposed)
            throw new InvalidOperationException("CAP Uow 发布不能使用已进入终态或已释放的业务工作单元。");
        if (!scope.Options.IsTransactional)
            throw new InvalidOperationException("CAP Uow 发布要求事务型业务工作单元。");
        if (cancellationToken.IsCancellationRequested)
        {
            scope.SetRollbackOnly();
            cancellationToken.ThrowIfCancellationRequested();
        }

        var source = _runtimeManager.GetRegistration(dataSourceKey);
        var context = _databaseContextAccessor.Current;
        var frozenContext = new DatabaseContext
        {
            DbKey = source.DataSourceKey,
            TenantId = context?.TenantId,
            MappingProfile = context?.MappingProfile,
            ReadPreference = SqlReadPreference.Default
        };
        var resource = _resourceAccessor.GetResource(scope, frozenContext,
            createDapperResource: true, requireComparableIdentity: true);
        if (resource == null)
            throw new InvalidOperationException($"CAP Uow 未能取得数据源 {dataSourceKey} 的事务资源。");

        IDisposable lease;
        try
        {
            lease = scope.AcquireExecutionLease(resource.ResourceKey);
        }
        catch
        {
            if (scope.IsActive)
                scope.SetRollbackOnly();
            throw;
        }

        using (lease)
        {
            BorrowedCapTransaction borrowed = null;
            ICapPublisher publisher = null;
            var previousAmbient = default(ICapTransaction);
            try
            {
                EnsureStorageMatches(source, _runtimeManager.GetEffectiveStorageConnectionString(dataSourceKey),
                    resource);
                await resource.EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
                if (resource.Transaction is not DbTransaction dbTransaction)
                    throw new InvalidOperationException("CAP MySQL Outbox 需要由 Uow root 拥有的 DbTransaction。");

                publisher = await _runtimeManager.GetOperationPublisherAsync(scope, dataSourceKey)
                    .ConfigureAwait(false);
                var dispatcher = publisher.ServiceProvider.GetRequiredService<IDispatcher>();
                previousAmbient = publisher.Transaction.Value;
                if (previousAmbient != null)
                    throw new InvalidOperationException("当前 CAP runtime 已有环境事务，不能覆盖或跨数据源复用。");

                borrowed = new BorrowedCapTransaction(dbTransaction, dispatcher);
                publisher.Transaction.Value = borrowed;
                var headers = CreateHeaders(message, scope.Id);
                WriteLog(source.DataSourceKey, message.Name, headers, message.Callback, false);
                await publisher.PublishAsync(message.Name, message.Data, headers, cancellationToken)
                    .ConfigureAwait(false);
                WriteLog(source.DataSourceKey, message.Name, headers, message.Callback, true);
                scope.OnResourceCommitted(resource.ResourceKey, async () =>
                {
                    try
                    {
                        await borrowed.FlushNativeAsync().ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        exception.Data["Bing.Cap.DataSourceKey"] = dataSourceKey;
                        throw;
                    }
                });
            }
            catch
            {
                if (scope.IsActive)
                    scope.SetRollbackOnly();
                throw;
            }
            finally
            {
                if (publisher != null && borrowed != null)
                {
                    var currentAmbient = publisher.Transaction.Value;
                    if (ReferenceEquals(currentAmbient, borrowed) || currentAmbient == null)
                        publisher.Transaction.Value = previousAmbient;
                    else if (scope.IsActive)
                    {
                        scope.SetRollbackOnly();
                        publisher.Transaction.Value = previousAmbient;
                    }
                    borrowed.Dispose();
                }
            }
        }
    }

    private void EnsureStorageMatches(CapUnitOfWorkDataSourceRegistration source, string effectiveStorageConnectionString,
        IUnitOfWorkConnectionResource resource)
    {
        var configured = _databaseIdentityResolver.Resolve(source.DatabaseType, source.StorageConnectionString);
        var effective = _databaseIdentityResolver.Resolve(source.DatabaseType, effectiveStorageConnectionString);
        var actual = _databaseIdentityResolver.Resolve(source.DatabaseType,
            resource.Connection.ConnectionString);
        if (!configured.IsComparable || configured.IsExclusiveMemory ||
            !effective.IsComparable || effective.IsExclusiveMemory ||
            !actual.IsComparable || actual.IsExclusiveMemory)
            throw new InvalidOperationException(
                $"数据源 {source.DataSourceKey} 的 CAP Outbox 和 Uow 资源无法比较物理数据库身份。");
        if (!configured.Equals(effective) || !effective.Equals(actual))
            throw new InvalidOperationException(
                $"数据源 {source.DataSourceKey} 的 CAP 声明存储、最终 Provider 存储与 Uow 当前连接未指向同一物理数据库。");
    }

    private static IDictionary<string, string> CreateHeaders(IMessageEvent message, Guid rootId)
    {
        var headers = new Dictionary<string, string>
        {
            [DotNetCore.CAP.Messages.Headers.CallbackName] = message.Callback,
            ["Bing-EventId"] = message.Id,
            ["Bing-UowRootId"] = rootId.ToString("N")
        };
        if (TraceIdContext.Current == null)
            TraceIdContext.Current = new TraceIdContext(string.Empty);
        headers.TryAdd(Headers.TraceId, TraceIdContext.Current.TraceId);
        if (message is IClusterEventMetadata metadata)
            ClusterEventHeaders.Write(headers, metadata);
        return headers;
    }

    private void WriteLog(string dataSourceKey, string name, IDictionary<string, string> headers,
        string callback, bool isEnd)
    {
        if (!_logger.IsEnabled(LogLevel.Trace))
            return;
        _logger.LogTrace("CAP Uow 事件写入{Phase}。EventName={EventName}, DataSource={DataSourceKey}, Callback={Callback}, HeaderCount={HeaderCount}",
            isEnd ? "结束" : "开始", name, dataSourceKey, callback, headers?.Count ?? 0);
    }
}

/// <summary>
/// 保留 CAP 原生缓冲，同时由 Uow 资源持有物理事务。
/// </summary>
internal sealed class BorrowedCapTransaction : CapTransactionBase
{
    private readonly TrackingDispatcher _trackingDispatcher;

    public BorrowedCapTransaction(DbTransaction transaction, IDispatcher dispatcher)
        : this(transaction, new TrackingDispatcher(dispatcher))
    {
    }

    private BorrowedCapTransaction(DbTransaction transaction, TrackingDispatcher dispatcher)
        : base(dispatcher)
    {
        DbTransaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        _trackingDispatcher = dispatcher;
        AutoCommit = false;
    }

    public override void Commit() => throw new InvalidOperationException(
        "CAP 借用事务不能提交物理事务；应由业务 Uow root 完成提交。");

    public override Task CommitAsync(CancellationToken cancellationToken = default) => Task.FromException(
        new InvalidOperationException("CAP 借用事务不能提交物理事务；应由业务 Uow root 完成提交。"));

    public override void Rollback() => throw new InvalidOperationException(
        "CAP 借用事务不能回滚物理事务；应由业务 Uow root 完成回滚。");

    public override Task RollbackAsync(CancellationToken cancellationToken = default) => Task.FromException(
        new InvalidOperationException("CAP 借用事务不能回滚物理事务；应由业务 Uow root 完成回滚。"));

    public override void Dispose() => DbTransaction = null;

    public async Task FlushNativeAsync()
    {
        Exception failure = null;
        try
        {
            Flush();
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        try
        {
            await _trackingDispatcher.WaitForCompletionAsync().ConfigureAwait(false);
        }
        catch (Exception dispatchException)
        {
            if (failure == null)
                failure = dispatchException;
            else
                failure.Data["Bing.CapDispatchException"] = dispatchException;
        }

        if (failure != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class TrackingDispatcher : IDispatcher
    {
        private readonly IDispatcher _inner;
        private readonly object _gate = new();
        private readonly List<Task> _pending = new();

        public TrackingDispatcher(IDispatcher inner) =>
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public ValueTask EnqueueToPublish(MediumMessage message) => Track(_inner.EnqueueToPublish(message));
        public ValueTask EnqueueToExecute(MediumMessage message, ConsumerExecutorDescriptor descriptor) =>
            Track(_inner.EnqueueToExecute(message, descriptor));
        public ValueTask EnqueueToScheduler(MediumMessage message, DateTime executeAt, object payload) =>
            Track(_inner.EnqueueToScheduler(message, executeAt, payload));
        public Task Start(CancellationToken stoppingToken) => _inner.Start(stoppingToken);

        // 底层 dispatcher 仍由对应 CAP runtime 管理。
        public void Dispose() { }

        private ValueTask Track(ValueTask operation)
        {
            if (operation.IsCompletedSuccessfully)
                return operation;
            var task = operation.AsTask();
            lock (_gate)
                _pending.Add(task);
            return operation;
        }

        public async Task WaitForCompletionAsync()
        {
            Task[] pending;
            lock (_gate)
            {
                pending = _pending.ToArray();
                _pending.Clear();
            }
            if (pending.Length == 0)
                return;
            try
            {
                await Task.WhenAll(pending).ConfigureAwait(false);
            }
            catch
            {
                var errors = pending.Where(task => task.IsFaulted && task.Exception != null)
                    .SelectMany(task => task.Exception.InnerExceptions).ToArray();
                if (errors.Length == 1)
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
                if (errors.Length > 1)
                    throw new AggregateException("CAP dispatcher 有多个异步入队操作失败。", errors);
                throw;
            }
        }
    }
}

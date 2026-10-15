using System.Data;
using System.Data.Common;
using System.Reflection;
using Bing.Admin.Data;
using Bing.Admin.Data.Inventory;
using Bing.Auditing;
using Bing.Auditing.Persistence;
using Bing.Data.Inventory;
using Bing.Data.Transaction;
using Bing.Events.Messages;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

#if BING_ADMIN_EFCORE_TESTS
namespace Bing.Admin.EFCore.Tests;
#else
namespace Bing.Admin.FreeSQL.Tests;
#endif

/// <summary>
/// 验证库存整单执行器的结果恢复与作用域清理契约。
/// </summary>
public sealed class AdminInventoryOrderExecutorTest
{
    /// <summary>
    /// 死锁带有任何事务或作用域清理错误时不自动重试。
    /// </summary>
    /// <param name="cleanupKey">清理错误的诊断键。</param>
    [Theory]
    [InlineData("Bing.InventoryScopeCleanupException")]
    [InlineData("Bing.TransactionCleanupException")]
    [InlineData("Bing.TransactionRollbackException")]
    public async Task ExecuteAsync_WhenDeadlockHasCleanupFailure_ShouldNotRetry(string cleanupKey)
    {
        var failure = CreateNativeMySqlFailure(1213, TransactionOutcome.RolledBack);
        failure.Data[cleanupKey] = new InvalidOperationException("清理失败。");
        var state = new ExecutionState { Failure = failure };
        await using var provider = CreateProvider(state);

        var actual = await Assert.ThrowsAsync<MySqlException>(() =>
            provider.GetRequiredService<AdminInventoryOrderExecutor>().ExecuteAsync(CreateRequest()));

        Assert.Same(failure, actual);
        Assert.Equal(1, state.ExecuteCalls);
        Assert.Equal(1, state.CleanupCompletions);
        Assert.Equal(1, state.UnitOfWorkDisposals);
    }

    /// <summary>
    /// 持续死锁只做四次独立作用域尝试且先完成上一轮清理。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenDeadlockPersists_ShouldBoundAttemptsAndAwaitEachCleanup()
    {
        var failure = CreateNativeMySqlFailure(1213, TransactionOutcome.RolledBack);
        var state = new ExecutionState { Failure = failure };
        state.OnExecute = () => Assert.Equal(state.ExecuteCalls - 1, state.CleanupCompletions);
        await using var provider = CreateProvider(state);

        var actual = await Assert.ThrowsAsync<MySqlException>(() =>
            provider.GetRequiredService<AdminInventoryOrderExecutor>().ExecuteAsync(CreateRequest()));

        Assert.Same(failure, actual);
        Assert.Equal(4, state.ExecuteCalls);
        Assert.Equal(4, state.CleanupCompletions);
        Assert.Equal(4, state.UnitOfWorkDisposals);
        Assert.Equal(4, state.UnitOfWorkInstances.Distinct().Count());
    }

    /// <summary>
    /// 已确认回滚的死锁可在下一轮新作用域成功。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenDeadlockThenSuccess_ShouldReturnNewAttemptResult()
    {
        var state = new ExecutionState
        {
            Failure = CreateNativeMySqlFailure(1213, TransactionOutcome.RolledBack), Result = CreateResult()
        };
        state.OnExecute = () =>
        {
            Assert.Equal(state.ExecuteCalls - 1, state.CleanupCompletions);
            if (state.ExecuteCalls == 2)
                state.Failure = null;
        };
        await using var provider = CreateProvider(state);

        var result = await provider.GetRequiredService<AdminInventoryOrderExecutor>().ExecuteAsync(CreateRequest());

        Assert.Same(state.Result, result);
        Assert.Equal(2, state.ExecuteCalls);
        Assert.Equal(2, state.CleanupCompletions);
        Assert.Equal(2, state.UnitOfWorkInstances.Distinct().Count());
    }

    /// <summary>
    /// 未知事务结果或非死锁的数据库错误不自动重试。
    /// </summary>
    /// <param name="number">MySQL 错误编号。</param>
    /// <param name="outcome">已知事务结果。</param>
    [Theory]
    [InlineData(1213, TransactionOutcome.Unknown)]
    [InlineData(1213, TransactionOutcome.Committed)]
    [InlineData(1205, TransactionOutcome.RolledBack)]
    public async Task ExecuteAsync_WhenRetryIsUnsafe_ShouldKeepOriginalFailure(int number, TransactionOutcome outcome)
    {
        var failure = CreateNativeMySqlFailure(number, outcome);
        var state = new ExecutionState { Failure = failure };
        await using var provider = CreateProvider(state);

        Assert.Same(failure, await Assert.ThrowsAsync<MySqlException>(() =>
            provider.GetRequiredService<AdminInventoryOrderExecutor>().ExecuteAsync(CreateRequest())));
        Assert.Equal(1, state.ExecuteCalls);
        Assert.Equal(1, state.CleanupCompletions);
    }

    /// <summary>
    /// 未知结果使用原请求查询，不重复执行业务，并先完成作用域释放。
    /// </summary>
    [Fact]
    public async Task ExecuteWithRecoveryAsync_WhenOutcomeIsUnknown_ShouldRecoverWithoutRetry()
    {
        var state = new ExecutionState { Failure = CreateFailure(TransactionOutcome.Unknown) };
        await using var provider = CreateProvider(state);
        var request = CreateRequest();
        var recovered = CreateResult();
        var recoveryCalls = 0;

        var result = await provider.GetRequiredService<AdminInventoryOrderExecutor>()
            .ExecuteWithRecoveryAsync(request, "user", "trace", (original, _) =>
            {
                recoveryCalls++;
                Assert.Same(request, original);
                Assert.Equal(1, state.CleanupCompletions);
                return Task.FromResult(recovered);
            });

        Assert.Same(recovered, result);
        Assert.Equal(1, state.ExecuteCalls);
        Assert.Equal(1, state.UnitOfWorkDisposals);
        Assert.Equal(1, state.CleanupCalls);
        Assert.Equal(1, recoveryCalls);
    }

    /// <summary>
    /// 查询确认业务已提交时仍保留业务异常和作用域释放异常。
    /// </summary>
    /// <param name="outcome">业务异常最初记录的事务结果。</param>
    [Theory]
    [InlineData(TransactionOutcome.Unknown)]
    [InlineData(TransactionOutcome.Committed)]
    public async Task ExecuteWithRecoveryAsync_WhenBusinessAndCleanupFail_ShouldPreserveBothFailures(
        TransactionOutcome outcome)
    {
        var failure = CreateFailure(outcome);
        var cleanup = new InvalidOperationException("作用域释放失败。");
        var state = new ExecutionState { Failure = failure, CleanupFailure = cleanup };
        await using var provider = CreateProvider(state);
        var recovered = CreateResult();
        var recoveryCalls = 0;

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetRequiredService<AdminInventoryOrderExecutor>().ExecuteWithRecoveryAsync(
                CreateRequest(), "user", "trace", (_, _) =>
                {
                    recoveryCalls++;
                    return Task.FromResult(recovered);
                }));

        Assert.Same(failure, actual);
        Assert.Same(cleanup, actual.Data["Bing.InventoryScopeCleanupException"]);
        Assert.Same(recovered, actual.Data["Bing.InventoryRecoveredResult"]);
        Assert.Equal(TransactionOutcome.Committed, actual.GetTransactionOutcome());
        Assert.Equal(1, state.ExecuteCalls);
        Assert.Equal(1, state.CleanupCalls);
        Assert.Equal(1, recoveryCalls);
    }

    /// <summary>
    /// 业务成功但释放失败时保留已提交结果，不再查询恢复。
    /// </summary>
    [Fact]
    public async Task ExecuteWithRecoveryAsync_WhenOnlyCleanupFails_ShouldPreserveCommittedResultWithoutRecovery()
    {
        var result = CreateResult();
        var cleanup = new InvalidOperationException("作用域释放失败。");
        var state = new ExecutionState { Result = result, CleanupFailure = cleanup };
        await using var provider = CreateProvider(state);
        var recoveryCalls = 0;

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetRequiredService<AdminInventoryOrderExecutor>().ExecuteWithRecoveryAsync(
                CreateRequest(), "user", "trace", (_, _) =>
                {
                    recoveryCalls++;
                    return Task.FromResult(CreateResult());
                }));

        Assert.Same(cleanup, actual);
        Assert.Same(result, actual.Data["Bing.InventoryCommittedResult"]);
        Assert.Equal(TransactionOutcome.Committed, actual.GetTransactionOutcome());
        Assert.Equal(0, recoveryCalls);
        Assert.Equal(1, state.ExecuteCalls);
        Assert.Equal(1, state.CleanupCalls);
    }

    /// <summary>
    /// 恢复查询没有找到结果时保留原始未知结果异常。
    /// </summary>
    [Fact]
    public async Task ExecuteWithRecoveryAsync_WhenRecoveryReturnsNull_ShouldPreserveOriginalFailure()
    {
        var failure = CreateFailure(TransactionOutcome.Unknown);
        var state = new ExecutionState { Failure = failure };
        await using var provider = CreateProvider(state);
        var recoveryCalls = 0;

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetRequiredService<AdminInventoryOrderExecutor>().ExecuteWithRecoveryAsync(
                CreateRequest(), "user", "trace", (_, _) =>
                {
                    recoveryCalls++;
                    return Task.FromResult<InventoryOrderResult>(null);
                }));

        Assert.Same(failure, actual);
        Assert.Equal(TransactionOutcome.Unknown, actual.GetTransactionOutcome());
        Assert.False(actual.Data.Contains("Bing.InventoryRecoveredResult"));
        Assert.Equal(1, state.ExecuteCalls);
        Assert.Equal(1, recoveryCalls);
    }

    /// <summary>
    /// 恢复查询失败时将查询异常附加到原始业务异常。
    /// </summary>
    [Fact]
    public async Task ExecuteWithRecoveryAsync_WhenRecoveryThrows_ShouldPreserveOriginalAndRecoveryFailures()
    {
        var failure = CreateFailure(TransactionOutcome.Unknown);
        var recoveryFailure = new InvalidOperationException("结果查询失败。");
        var state = new ExecutionState { Failure = failure };
        await using var provider = CreateProvider(state);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetRequiredService<AdminInventoryOrderExecutor>().ExecuteWithRecoveryAsync(
                CreateRequest(), "user", "trace", (_, _) =>
                    Task.FromException<InventoryOrderResult>(recoveryFailure)));

        Assert.Same(failure, actual);
        Assert.Same(recoveryFailure, actual.Data["Bing.InventoryRecoveryException"]);
        Assert.Equal(TransactionOutcome.Unknown, actual.GetTransactionOutcome());
        Assert.Equal(1, state.ExecuteCalls);
        Assert.Equal(1, state.CleanupCompletions);
    }

    /// <summary>
    /// 普通回滚异常不执行恢复查询，也不自动重做业务。
    /// </summary>
    [Fact]
    public async Task ExecuteWithRecoveryAsync_WhenOutcomeIsRolledBack_ShouldNotRecoverOrRetry()
    {
        var failure = CreateFailure(TransactionOutcome.RolledBack);
        var state = new ExecutionState { Failure = failure };
        await using var provider = CreateProvider(state);
        var recoveryCalls = 0;

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetRequiredService<AdminInventoryOrderExecutor>().ExecuteWithRecoveryAsync(
                CreateRequest(), "user", "trace", (_, _) =>
                {
                    recoveryCalls++;
                    return Task.FromResult(CreateResult());
                }));

        Assert.Same(failure, actual);
        Assert.Equal(TransactionOutcome.RolledBack, actual.GetTransactionOutcome());
        Assert.Equal(0, recoveryCalls);
        Assert.Equal(1, state.ExecuteCalls);
        Assert.Equal(1, state.CleanupCompletions);
    }

    /// <summary>
    /// 取消后跳过恢复查询，仍等待异步作用域释放结束。
    /// </summary>
    [Fact]
    public async Task ExecuteWithRecoveryAsync_WhenCancelled_ShouldAwaitCleanupAndSkipRecovery()
    {
        using var cancellation = new CancellationTokenSource();
        var failure = new OperationCanceledException(cancellation.Token);
        var state = new ExecutionState
        {
            Failure = failure,
            OnExecute = cancellation.Cancel,
            CleanupRelease = NewSignal()
        };
        await using var provider = CreateProvider(state);
        var recoveryCalls = 0;
        var execution = provider.GetRequiredService<AdminInventoryOrderExecutor>().ExecuteWithRecoveryAsync(
            CreateRequest(), "user", "trace", (_, _) =>
            {
                recoveryCalls++;
                return Task.FromResult(CreateResult());
            }, cancellation.Token);

        try
        {
            await state.CleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(execution.IsCompleted);
            Assert.Equal(0, recoveryCalls);
            Assert.Equal(0, state.CleanupCompletions);
        }
        finally
        {
            state.CleanupRelease.TrySetResult(true);
        }

        var actual = await Assert.ThrowsAsync<OperationCanceledException>(() => execution);
        Assert.Same(failure, actual);
        Assert.Equal(0, recoveryCalls);
        Assert.Equal(1, state.ExecuteCalls);
        Assert.Equal(1, state.CleanupCompletions);
    }

    /// <summary>
    /// 未完成异步释放前既不执行恢复查询，也不返回结果。
    /// </summary>
    [Fact]
    public async Task ExecuteWithRecoveryAsync_WhenCleanupIsPending_ShouldWaitBeforeRecovery()
    {
        var state = new ExecutionState
        {
            Failure = CreateFailure(TransactionOutcome.Unknown), CleanupRelease = NewSignal()
        };
        await using var provider = CreateProvider(state);
        var recovered = CreateResult();
        var recoveryCalls = 0;
        var execution = provider.GetRequiredService<AdminInventoryOrderExecutor>().ExecuteWithRecoveryAsync(
            CreateRequest(), "user", "trace", (_, _) =>
            {
                recoveryCalls++;
                Assert.Equal(1, state.CleanupCompletions);
                return Task.FromResult(recovered);
            });

        try
        {
            await state.CleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(execution.IsCompleted);
            Assert.Equal(0, recoveryCalls);
        }
        finally
        {
            state.CleanupRelease.TrySetResult(true);
        }

        Assert.Same(recovered, await execution);
        Assert.Equal(1, recoveryCalls);
        Assert.Equal(1, state.ExecuteCalls);
        Assert.Equal(1, state.CleanupCalls);
    }

    /// <summary>
    /// 业务成功后仍等待异步释放结束才返回结果。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenCleanupIsPending_ShouldWaitBeforeReturningCommittedResult()
    {
        var result = CreateResult();
        var state = new ExecutionState { Result = result, CleanupRelease = NewSignal() };
        await using var provider = CreateProvider(state);
        var execution = provider.GetRequiredService<AdminInventoryOrderExecutor>().ExecuteAsync(CreateRequest());

        try
        {
            await state.CleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(execution.IsCompleted);
            Assert.Equal(0, state.CleanupCompletions);
        }
        finally
        {
            state.CleanupRelease.TrySetResult(true);
        }

        Assert.Same(result, await execution);
        Assert.Equal(1, state.CleanupCompletions);
        Assert.Equal(1, state.UnitOfWorkDisposals);
    }

    /// <summary>
    /// 创建具有真实作用域捕获和异步释放行为的 Microsoft DI 容器。
    /// </summary>
    /// <param name="state">测试操作和释放行为的可观测状态。</param>
    /// <returns>负责创建业务作用域的服务提供程序。</returns>
    private static ServiceProvider CreateProvider(ExecutionState state)
    {
        var services = new ServiceCollection();
        services.AddScoped<IAdminUnitOfWork>(provider =>
            new FakeUnitOfWork(provider.GetRequiredService<ITransactionActionManager>(), state));
        services.AddScoped<ITransactionActionManager, TransactionActionManager>();
        services.AddScoped<IInventoryOrderStore>(_ => new TestOrderStore(state));
        services.AddScoped<ITransactionalAuditingStore, NoopAuditingStore>();
        services.AddScoped<IMessageEventBus, NoopMessageEventBus>();
        services.AddScoped<AsyncCleanupResource>(_ => new AsyncCleanupResource(state));
        services.AddScoped<AdminInventoryService>(provider =>
        {
            // 只解析一次，避免同一资源经多个服务别名被重复捕获。
            provider.GetRequiredService<AsyncCleanupResource>();
            var unitOfWork = (FakeUnitOfWork)provider.GetRequiredService<IAdminUnitOfWork>();
            return new AdminInventoryService(unitOfWork,
                provider.GetRequiredService<ITransactionActionManager>(), () => unitOfWork.Transaction,
                provider.GetRequiredService<IInventoryOrderStore>(),
                provider.GetRequiredService<ITransactionalAuditingStore>(),
                provider.GetRequiredService<IMessageEventBus>());
        });
        services.AddSingleton<AdminInventoryOrderExecutor>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    /// <summary>
    /// 创建固定身份的最小整单请求。
    /// </summary>
    /// <returns>仅包含一项库存明细的请求。</returns>
    private static InventoryOrderRequest CreateRequest() => new("tenant", "warehouse", "order",
        new[] { new InventoryOrderLine("sku", 1) });

    /// <summary>
    /// 创建可用于业务执行或恢复查询的固定结果。
    /// </summary>
    /// <returns>已记录的整单处理结果。</returns>
    private static InventoryOrderResult CreateResult() =>
        new(InventoryDeductionStatus.Insufficient, true, Array.Empty<long?>());

    /// <summary>
    /// 创建携带指定事务结果的原始业务异常。
    /// </summary>
    /// <param name="outcome">异常对应的数据库事务结果。</param>
    /// <returns>可进行引用身份断言的业务异常。</returns>
    private static InvalidOperationException CreateFailure(TransactionOutcome outcome)
    {
        var failure = new InvalidOperationException("库存执行失败。");
        failure.SetTransactionOutcome(outcome);
        return failure;
    }

    /// <summary>
    /// 创建具有真实驱动类型及错误编号的诊断异常。
    /// </summary>
    /// <param name="number">MySQL 错误编号。</param>
    /// <param name="outcome">异常携带的事务结果。</param>
    /// <returns>用于验证重试分派的驱动异常。</returns>
    /// <remarks>仅验证执行器契约，不代替真实数据库死锁验收。</remarks>
    private static MySqlException CreateNativeMySqlFailure(int number, TransactionOutcome outcome)
    {
        var constructor = typeof(MySqlException).GetConstructor(BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic, null, new[] { typeof(MySqlErrorCode), typeof(string) }, null);
        Assert.NotNull(constructor);
        var failure = (MySqlException)constructor.Invoke(new object[] { (MySqlErrorCode)number, "测试数据库错误。" });
        failure.SetTransactionOutcome(outcome);
        return failure;
    }

    /// <summary>
    /// 创建不会同步运行后续回调的测试信号。
    /// </summary>
    /// <returns>用于确定性控制异步释放的完成信号。</returns>
    private static TaskCompletionSource<bool> NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 保存单次业务和作用域释放的可观测状态。
    /// </summary>
    private sealed class ExecutionState
    {
        /// <summary>
        /// 获取各次独立作用域创建的工作单元实例。
        /// </summary>
        internal List<object> UnitOfWorkInstances { get; } = new();

        /// <summary>
        /// 获取或设置业务操作返回的结果。
        /// </summary>
        internal InventoryOrderResult Result { get; set; }

        /// <summary>
        /// 获取或设置业务操作抛出的原始异常。
        /// </summary>
        internal Exception Failure { get; set; }

        /// <summary>
        /// 获取或设置异步释放抛出的异常。
        /// </summary>
        internal Exception CleanupFailure { get; set; }

        /// <summary>
        /// 获取或设置业务操作开始时执行的测试回调。
        /// </summary>
        internal Action OnExecute { get; set; }

        /// <summary>
        /// 获取异步释放已经开始的信号。
        /// </summary>
        internal TaskCompletionSource<bool> CleanupStarted { get; } = NewSignal();

        /// <summary>
        /// 获取或设置允许异步释放结束的信号。
        /// </summary>
        internal TaskCompletionSource<bool> CleanupRelease { get; set; }

        /// <summary>
        /// 获取或设置整单存储的实际执行次数。
        /// </summary>
        internal int ExecuteCalls { get; set; }

        /// <summary>
        /// 获取或设置异步资源的实际释放次数。
        /// </summary>
        internal int CleanupCalls { get; set; }

        /// <summary>
        /// 获取或设置异步资源成功完成释放的次数。
        /// </summary>
        internal int CleanupCompletions { get; set; }

        /// <summary>
        /// 获取或设置工作单元的实际释放次数。
        /// </summary>
        internal int UnitOfWorkDisposals { get; set; }
    }

    /// <summary>
    /// 执行真实事务回调的最小工作单元。
    /// </summary>
    private sealed class FakeUnitOfWork : IAdminUnitOfWork
    {
        /// <summary>
        /// 当前作用域的事务回调管理器。
        /// </summary>
        private readonly ITransactionActionManager _actions;

        /// <summary>
        /// 记录当前测试的工作单元释放行为。
        /// </summary>
        private readonly ExecutionState _state;

        /// <summary>
        /// 初始化一个 <see cref="FakeUnitOfWork" /> 类型的实例。
        /// </summary>
        /// <param name="actions">当前作用域的事务回调管理器。</param>
        /// <param name="state">工作单元释放状态。</param>
        public FakeUnitOfWork(ITransactionActionManager actions, ExecutionState state)
        {
            _actions = actions;
            _state = state;
            state.UnitOfWorkInstances.Add(this);
        }

        /// <summary>
        /// 获取当前作用域的最小数据库事务。
        /// </summary>
        internal FakeDbTransaction Transaction { get; } = new();

        /// <inheritdoc />
        public int Commit() => CommitAsync().GetAwaiter().GetResult();

        /// <inheritdoc />
        public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
        {
            await _actions.CommitAsync(Transaction);
            return 0;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _state.UnitOfWorkDisposals++;
            Transaction.Dispose();
        }
    }

    /// <summary>
    /// 提供可等待且可失败的真实作用域异步资源。
    /// </summary>
    private sealed class AsyncCleanupResource : IAsyncDisposable
    {
        /// <summary>
        /// 当前测试的异步释放控制状态。
        /// </summary>
        private readonly ExecutionState _state;

        /// <summary>
        /// 初始化一个 <see cref="AsyncCleanupResource" /> 类型的实例。
        /// </summary>
        /// <param name="state">异步释放控制状态。</param>
        public AsyncCleanupResource(ExecutionState state) => _state = state;

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            _state.CleanupCalls++;
            _state.CleanupStarted.TrySetResult(true);
            if (_state.CleanupRelease != null)
                await _state.CleanupRelease.Task;
            if (_state.CleanupFailure != null)
                throw _state.CleanupFailure;
            _state.CleanupCompletions++;
        }
    }

    /// <summary>
    /// 提供指定结果或异常的整单存储。
    /// </summary>
    private sealed class TestOrderStore : IInventoryOrderStore
    {
        /// <summary>
        /// 当前测试的业务执行状态。
        /// </summary>
        private readonly ExecutionState _state;

        /// <summary>
        /// 初始化一个 <see cref="TestOrderStore" /> 类型的实例。
        /// </summary>
        /// <param name="state">业务执行状态。</param>
        internal TestOrderStore(ExecutionState state) => _state = state;

        /// <inheritdoc />
        public Task<InventoryOrderResult> FindAsync(DbConnection connection, InventoryOrderRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult<InventoryOrderResult>(null);

        /// <inheritdoc />
        public Task<InventoryOrderResult> ExecuteAsync(DbTransaction transaction, InventoryOrderRequest request,
            Func<DbTransaction, InventoryOrderResult, CancellationToken, Task> onCompleted,
            CancellationToken cancellationToken = default)
        {
            _state.ExecuteCalls++;
            _state.OnExecute?.Invoke();
            return _state.Failure != null
                ? Task.FromException<InventoryOrderResult>(_state.Failure)
                : Task.FromResult(_state.Result);
        }
    }

    /// <summary>
    /// 为执行器测试提供不产生外部副作用的审计存储。
    /// </summary>
    private sealed class NoopAuditingStore : ITransactionalAuditingStore
    {
        /// <inheritdoc />
        public Task SaveAsync(DbTransaction transaction, string auditId, AuditLogInfo auditInfo,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// 为执行器测试提供不产生外部副作用的消息总线。
    /// </summary>
    private sealed class NoopMessageEventBus : IMessageEventBus
    {
        /// <inheritdoc />
        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : IMessageEvent => Task.CompletedTask;

        /// <inheritdoc />
        public Task PublishAsync(string name, object data, string callback, bool send = false,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// 为执行器测试提供最小数据库事务。
    /// </summary>
    private sealed class FakeDbTransaction : DbTransaction
    {
        /// <summary>
        /// 当前最小事务关联的连接。
        /// </summary>
        private readonly FakeDbConnection _connection = new();

        /// <inheritdoc />
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        /// <inheritdoc />
        protected override DbConnection DbConnection => _connection;

        /// <inheritdoc />
        public override void Commit()
        {
        }

        /// <inheritdoc />
        public override void Rollback()
        {
        }
    }

    /// <summary>
    /// 为最小数据库事务提供关联连接。
    /// </summary>
    private sealed class FakeDbConnection : DbConnection
    {
        /// <inheritdoc />
        public override string ConnectionString { get; set; }

        /// <inheritdoc />
        public override string Database => "test";

        /// <inheritdoc />
        public override string DataSource => "test";

        /// <inheritdoc />
        public override string ServerVersion => "test";

        /// <inheritdoc />
        public override ConnectionState State => ConnectionState.Open;

        /// <inheritdoc />
        public override void ChangeDatabase(string databaseName)
        {
        }

        /// <inheritdoc />
        public override void Close()
        {
        }

        /// <inheritdoc />
        public override void Open()
        {
        }

        /// <inheritdoc />
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new FakeDbTransaction();

        /// <inheritdoc />
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }
}

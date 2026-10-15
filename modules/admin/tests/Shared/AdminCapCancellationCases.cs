using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.ExceptionServices;
using Bing.Events.Consumption;
using Bing.Events.Cap;
using DotNetCore.CAP;
using DotNetCore.CAP.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MySqlConnector;
using Xunit;

#if BING_ADMIN_EFCORE_TESTS
namespace Bing.Admin.EFCore.Tests;
#else
namespace Bing.Admin.FreeSQL.Tests;
#endif

/// <summary>
/// 验证真实 CAP 消费取消后的持久化重试与事务收据边界。
/// </summary>
/// <remarks>业务写入使用直接 MySQL 事务，不代替两套 ORM 工作单元全部取消行为的验收。</remarks>
public sealed partial class AdminMySqlCapIntegrationTest
{
#if BING_ADMIN_EFCORE_TESTS
    /// <summary>
    /// EFCore 测试宿主独立的取消消费主题。
    /// </summary>
    private const string CancellationTopic = "bing.tests.admin.efcore.cap-cancellation";

    /// <summary>
    /// EFCore 测试使用的稳定订阅组与逻辑收据消费者名称。
    /// </summary>
    private const string CancellationConsumer = "bing-admin-efcore-cap-cancellation";
#else
    /// <summary>
    /// FreeSQL 测试宿主独立的取消消费主题。
    /// </summary>
    private const string CancellationTopic = "bing.tests.admin.freesql.cap-cancellation";

    /// <summary>
    /// FreeSQL 测试使用的稳定订阅组与逻辑收据消费者名称。
    /// </summary>
    private const string CancellationConsumer = "bing-admin-freesql-cap-cancellation";
#endif

    /// <summary>
    /// 验证提交前取消回滚业务与收据，原生 CAP 重试随后只提交一次。
    /// </summary>
    [MySqlCapIntegrationFact]
    public Task Consumption_WhenCancelledBeforeBusinessCommit_ShouldRetryAndCommitOnce() =>
        VerifyCapConsumptionCancellationAsync(afterCommit: false);

    /// <summary>
    /// 验证提交后取消触发原生 CAP 重试，并由原事件收据阻止重复业务写入。
    /// </summary>
    [MySqlCapIntegrationFact]
    public Task Consumption_WhenCancelledAfterBusinessCommit_ShouldRetryWithoutDuplicateWrites() =>
        VerifyCapConsumptionCancellationAsync(afterCommit: true);

    /// <summary>
    /// 使用真实 Admin 宿主验证指定提交边界的消费取消与原记录重试。
    /// </summary>
    /// <param name="afterCommit">是否在真实业务事务提交后抛出取消；否则先回滚事务。</param>
    private static async Task VerifyCapConsumptionCancellationAsync(bool afterCommit)
    {
        var observation = new CapCancellationObservation(Guid.NewGuid().ToString("N"), afterCommit);
        Exception failure = null;
        IHost host = null;
        object ownedOrm = null;
        var dataCreated = false;
        try
        {
            Action<IServiceCollection> configure = services =>
            {
                services.EnableBingCapConsumerCancellationRetry();
                services.AddSingleton(observation);
                services.AddSingleton<ICapSubscribe, CapCancellationSubscriber>();
                services.Configure<CapOptions>(options =>
                {
                    options.FailedRetryInterval = 1;
                    options.FallbackWindowLookbackSeconds = 5;
                });
            };
#if BING_ADMIN_EFCORE_TESTS
            var settings = MySqlCapIntegrationEnvironment.Load();
            observation.ConnectionString = settings.ConnectionString;
            host = CreateHost(settings, withInventory: true, configureInventory: configure);
#else
            var settings = IntegrationSettings.Load();
            observation.ConnectionString = settings.ConnectionString;
            var orm = CreateOrm(settings.ConnectionString);
            ownedOrm = orm;
            host = CreateHost(settings, orm, withInventory: true, configureInventory: configure);
#endif
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            dataCreated = true;
            await CreateCapCancellationTablesAsync(observation, timeout.Token);
            await host.StartAsync(timeout.Token);
            await EnsureCapCancellationSubscriberReadyAsync(host, observation, timeout.Token);
            await using (var scope = host.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<ICapPublisher>().PublishAsync(
                    CancellationTopic, new CapCancellationMessage { EventId = observation.EventId },
                    cancellationToken: timeout.Token);
            }

            var cancellation = await observation.FirstCancellation.Task.WaitAsync(timeout.Token);
            Assert.True(cancellation.CancellationToken.IsCancellationRequested);
            await observation.RetryStarted.Task.WaitAsync(timeout.Token);
            var failed = await WaitForCapCancellationReceivedAsync(observation, "Failed", null, timeout.Token);
            Assert.True(failed.Retries >= 1);
            Assert.True(Volatile.Read(ref observation.Ended) >= 1);
            var initial = await ReadCapCancellationWritesAsync(observation, timeout.Token);
            Assert.Equal(afterCommit ? 1L : 0L, initial.Receipts);
            Assert.Equal(afterCommit ? 1L : 0L, initial.Business);

            // 第二次回调已由 CAP 自动发起，只放行回调，不重发消息或调用重试处理器。
            observation.RetryAllowed.TrySetResult(true);
            var succeeded = await WaitForCapCancellationReceivedAsync(observation, "Succeeded", failed.Id,
                timeout.Token);
            Assert.Equal(failed.Id, succeeded.Id);
            Assert.True(succeeded.Retries >= 1);
            var final = await ReadCapCancellationWritesAsync(observation, timeout.Token);
            Assert.Equal(1, final.Receipts);
            Assert.Equal(1, final.Business);
            Assert.True(Volatile.Read(ref observation.Started) >= 2);
            Assert.Single(observation.MessageIds.Distinct(StringComparer.Ordinal));
            if (afterCommit)
                Assert.True(Volatile.Read(ref observation.DuplicateReceipts) >= 1);
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            observation.RetryAllowed.TrySetResult(true);
            var cleanup = new CapCancellationCleanup(host, ownedOrm, observation,
                dataCreated ? () => CleanupCapCancellationAsync(observation) : null,
                failure, TimeSpan.FromSeconds(30));
            failure = await cleanup.RunAsync();
        }
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
        Assert.Equal(Volatile.Read(ref observation.Started), Volatile.Read(ref observation.Ended));
    }

    /// <summary>
    /// 创建测试独占业务表并幂等建立事务消费收据表。
    /// </summary>
    /// <param name="observation">包含 GUID 表名与专用写库连接的测试状态。</param>
    /// <param name="token">建表取消令牌。</param>
    private static async Task CreateCapCancellationTablesAsync(CapCancellationObservation observation,
        CancellationToken token)
    {
        await using var connection = new MySqlConnection(observation.ConnectionString);
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 15;
        command.CommandText = "CREATE TABLE IF NOT EXISTS `bing_event_consumption` (" +
                              "`Consumer` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL, " +
                              "`EventId` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL, " +
                              "`ProcessedAtUtc` datetime(6) NOT NULL, PRIMARY KEY (`Consumer`,`EventId`)) ENGINE=InnoDB";
        await command.ExecuteNonQueryAsync(token);
        command.CommandText = $"CREATE TABLE `{observation.BusinessTable}` (" +
                              "`EventId` varchar(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY) ENGINE=InnoDB";
        await command.ExecuteNonQueryAsync(token);
    }

    /// <summary>
    /// 使用独立探针事件确认本宿主取消主题已经绑定到真实 RabbitMQ。
    /// </summary>
    /// <param name="host">已经启动的真实 Admin 测试宿主。</param>
    /// <param name="observation">探针身份与就绪信号。</param>
    /// <param name="token">测试总时限取消令牌。</param>
    private static async Task EnsureCapCancellationSubscriberReadyAsync(IHost host,
        CapCancellationObservation observation, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        while (!observation.Ready.Task.IsCompletedSuccessfully)
        {
            await using var scope = host.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ICapPublisher>().PublishAsync(CancellationTopic,
                new CapCancellationMessage { EventId = observation.ProbeEventId, IsProbe = true },
                cancellationToken: deadline.Token);
            await Task.Delay(100, deadline.Token);
        }
    }

    /// <summary>
    /// 等待唯一原 CAP 接收记录达到指定持久化状态。
    /// </summary>
    /// <param name="observation">目标事件身份及数据库连接。</param>
    /// <param name="status">需要观察的 Failed 或 Succeeded 状态。</param>
    /// <param name="originalId">首次失败时保存的数据库标识；首次读取时可为 null。</param>
    /// <param name="token">读取和轮询的取消令牌。</param>
    /// <returns>同一原 CAP 接收记录的标识、最终观察状态和重试次数。</returns>
    private static async Task<(string Id, string Status, int Retries)> WaitForCapCancellationReceivedAsync(
        CapCancellationObservation observation, string status, string originalId, CancellationToken token)
    {
        while (true)
        {
            await using var connection = new MySqlConnection(observation.ConnectionString);
            await connection.OpenAsync(token);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 15;
            command.CommandText = "SELECT `Id`,`StatusName`,`Retries` FROM `cap.received` " +
                                  "WHERE `Name`=@topic AND `Content` LIKE @event";
            command.Parameters.AddWithValue("@topic", CancellationTopic);
            command.Parameters.AddWithValue("@event", "%" + observation.EventId + "%");
            await using var reader = await command.ExecuteReaderAsync(token);
            if (await reader.ReadAsync(token))
            {
                var current = (Id: reader.GetInt64(0).ToString(CultureInfo.InvariantCulture),
                    Status: reader.GetString(1), Retries: reader.GetInt32(2));
                Assert.False(await reader.ReadAsync(token), "目标事件存在多条 CAP 接收记录。");
                if (originalId != null)
                    Assert.Equal(originalId, current.Id);
                if (current.Status == status)
                    return current;
            }
            await Task.Delay(100, token);
        }
    }

    /// <summary>
    /// 从独立直连写库读取目标事件的收据和业务行数量。
    /// </summary>
    /// <param name="observation">限定消费者、事件与 GUID 独占表的测试状态。</param>
    /// <param name="token">读取取消令牌。</param>
    /// <returns>已提交的唯一消费者收据数量和独占表业务行数量。</returns>
    private static async Task<(long Receipts, long Business)> ReadCapCancellationWritesAsync(
        CapCancellationObservation observation, CancellationToken token)
    {
        await using var connection = new MySqlConnection(observation.ConnectionString);
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 15;
        command.CommandText = "SELECT (SELECT COUNT(*) FROM `bing_event_consumption` " +
                              "WHERE `Consumer`=@consumer AND `EventId`=@event), " +
                              $"(SELECT COUNT(*) FROM `{observation.BusinessTable}`)";
        command.Parameters.AddWithValue("@consumer", CancellationConsumer);
        command.Parameters.AddWithValue("@event", observation.EventId);
        await using var reader = await command.ExecuteReaderAsync(token);
        Assert.True(await reader.ReadAsync(token));
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    /// <summary>
    /// 在宿主停止后仅清理本 GUID 业务表和本消费者的本事件收据。
    /// </summary>
    /// <param name="observation">当前测试独占的数据身份。</param>
    private static async Task CleanupCapCancellationAsync(CapCancellationObservation observation)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var connection = new MySqlConnection(observation.ConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 15;
        command.CommandText = "DELETE FROM `bing_event_consumption` WHERE `Consumer`=@consumer AND `EventId`=@event";
        command.Parameters.AddWithValue("@consumer", CancellationConsumer);
        command.Parameters.AddWithValue("@event", observation.EventId);
        await command.ExecuteNonQueryAsync(timeout.Token);
        command.CommandText = $"DROP TABLE IF EXISTS `{observation.BusinessTable}`";
        await command.ExecuteNonQueryAsync(timeout.Token);
    }

    /// <summary>
    /// 承载业务事件或独立就绪探针的测试消息。
    /// </summary>
    public sealed class CapCancellationMessage
    {
        /// <summary>
        /// 获取或设置本次测试生成的 GUID 事件标识。
        /// </summary>
        public string EventId { get; set; }

        /// <summary>
        /// 获取或设置消息是否只验证绑定就绪而不执行业务事务。
        /// </summary>
        public bool IsProbe { get; set; }
    }

    /// <summary>
    /// 保存单个取消用例的事务身份与线程安全观测信号。
    /// </summary>
    public sealed class CapCancellationObservation
    {
        /// <summary>
        /// 初始化一个 <see cref="CapCancellationObservation" /> 类型的实例。
        /// </summary>
        /// <param name="eventId">采用 N 格式的测试 GUID 事件标识。</param>
        /// <param name="afterCommit">是否在首次真实提交后取消。</param>
        internal CapCancellationObservation(string eventId, bool afterCommit)
        {
            EventId = Guid.ParseExact(eventId, "N").ToString("N");
            BusinessTable = "bing_cap_cancel_" + EventId;
            ProbeEventId = Guid.NewGuid().ToString("N");
            AfterCommit = afterCommit;
        }

        /// <summary>
        /// 串行化业务回调入场和清理关闭操作的锁。
        /// </summary>
        private readonly object _callbackLock = new();

        /// <summary>
        /// 是否仍允许新的目标业务回调入场。
        /// </summary>
        private bool _acceptingCallbacks = true;

        /// <summary>
        /// 已入场且尚未退出的目标业务回调数量。
        /// </summary>
        private int _activeCallbacks;

        /// <summary>
        /// 关闭入场后所有已入场回调退出的信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _callbacksDrained =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 原子检查入场开关并登记目标业务回调。
        /// </summary>
        /// <param name="attempt">成功入场时的回调序号；拒绝时为零。</param>
        /// <returns>是否允许本次回调执行业务事务。</returns>
        internal bool TryEnterCallback(out int attempt)
        {
            lock (_callbackLock)
            {
                attempt = 0;
                if (!_acceptingCallbacks)
                    return false;
                _activeCallbacks++;
                attempt = Interlocked.Increment(ref Started);
                return true;
            }
        }

        /// <summary>
        /// 登记已入场回调退出，并在关闭后完成排空信号。
        /// </summary>
        internal void ExitCallback()
        {
            lock (_callbackLock)
            {
                Interlocked.Increment(ref Ended);
                _activeCallbacks--;
                if (!_acceptingCallbacks && _activeCallbacks == 0)
                    _callbacksDrained.TrySetResult(true);
            }
        }

        /// <summary>
        /// 原子关闭新业务回调入场并返回已入场回调的排空任务。
        /// </summary>
        /// <returns>所有已入场回调实际退出后完成的任务。</returns>
        internal Task CloseCallbackAdmission()
        {
            lock (_callbackLock)
            {
                _acceptingCallbacks = false;
                if (_activeCallbacks == 0)
                    _callbacksDrained.TrySetResult(true);
                return _callbacksDrained.Task;
            }
        }

        /// <summary>
        /// 获取不会随 CAP 重试改变的业务事件标识。
        /// </summary>
        internal string EventId { get; }

        /// <summary>
        /// 获取仅由固定前缀和已验证 GUID 组成的独占业务表名。
        /// </summary>
        internal string BusinessTable { get; }

        /// <summary>
        /// 获取与目标业务事件不同的就绪探针 GUID。
        /// </summary>
        internal string ProbeEventId { get; }

        /// <summary>
        /// 获取首次取消是否发生在真实业务提交之后。
        /// </summary>
        internal bool AfterCommit { get; }

        /// <summary>
        /// 获取或设置本宿主专用直连写库连接字符串；不输出到测试诊断。
        /// </summary>
        internal string ConnectionString { get; set; }

        /// <summary>
        /// 通过 Interlocked 更新的已开始业务回调次数。
        /// </summary>
        internal int Started;

        /// <summary>
        /// 在每次回调 finally 中通过 Interlocked 更新的已结束次数。
        /// </summary>
        internal int Ended;

        /// <summary>
        /// 通过 Interlocked 更新的已存在消费收据命中次数。
        /// </summary>
        internal int DuplicateReceipts;

        /// <summary>
        /// 获取每次目标业务回调实际收到的原 CAP 消息头标识。
        /// </summary>
        internal ConcurrentQueue<string> MessageIds { get; } = new();

        /// <summary>
        /// 获取实际同主题探针消费完成的异步信号。
        /// </summary>
        internal TaskCompletionSource<bool> Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取首次事务边界抛出的原始取消异常信号。
        /// </summary>
        internal TaskCompletionSource<OperationCanceledException> FirstCancellation { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取 CAP 原生再次调用目标消费者的信号。
        /// </summary>
        internal TaskCompletionSource<bool> RetryStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取观察 Failed 状态后放行重试的有界等待 gate。
        /// </summary>
        internal TaskCompletionSource<bool> RetryAllowed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// 在真实 MySQL 事务边界抛出取消并依靠消费收据去重的 CAP 订阅者。
    /// </summary>
    public sealed class CapCancellationSubscriber : ICapSubscribe
    {
        /// <summary>
        /// 当前宿主唯一目标事件的观测状态。
        /// </summary>
        private readonly CapCancellationObservation _observation;

        /// <summary>
        /// 初始化一个 <see cref="CapCancellationSubscriber" /> 类型的实例。
        /// </summary>
        /// <param name="observation">当前测试的独占事务身份与观测信号。</param>
        public CapCancellationSubscriber(CapCancellationObservation observation) => _observation = observation;

        /// <summary>
        /// 在首次回调的指定事务边界取消，后续回调按原消费者和事件收据提交。
        /// </summary>
        /// <param name="message">真实 RabbitMQ 传输反序列化的目标消息或探针。</param>
        /// <param name="header">本次 CAP 回调的真实消息头。</param>
        /// <remarks>取消异常与宿主停止令牌无关；重试由 CAP 持久化消费路径发起，不声称是 RabbitMQ 重新入队。</remarks>
        [CapSubscribe(CancellationTopic, Group = CancellationConsumer)]
        public async Task ConsumeAsync(CapCancellationMessage message, [FromCap] CapHeader header)
        {
            if (message.IsProbe)
            {
                if (message.EventId == _observation.ProbeEventId)
                    _observation.Ready.TrySetResult(true);
                return;
            }
            if (message.EventId != _observation.EventId)
                return;
            if (!_observation.TryEnterCallback(out var attempt))
                throw new OperationCanceledException("测试清理已关闭目标业务回调入场。");
            try
            {
                _observation.MessageIds.Enqueue(header[Headers.MessageId]);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                if (attempt > 1)
                {
                    _observation.RetryStarted.TrySetResult(true);
                    await _observation.RetryAllowed.Task.WaitAsync(timeout.Token);
                }
                await using var connection = new MySqlConnection(_observation.ConnectionString);
                await connection.OpenAsync(timeout.Token);
                await using var transaction = await connection.BeginTransactionAsync(timeout.Token);
                var store = new EventConsumptionStore(EventConsumptionDatabase.MySql);
                var recorded = await store.TryRecordAsync(transaction, CancellationConsumer, message.EventId,
                    timeout.Token);
                if (recorded)
                {
                    await using var business = connection.CreateCommand();
                    business.CommandTimeout = 15;
                    business.Transaction = transaction;
                    business.CommandText = $"INSERT INTO `{_observation.BusinessTable}` (`EventId`) VALUES (@event)";
                    business.Parameters.AddWithValue("@event", message.EventId);
                    Assert.Equal(1, await business.ExecuteNonQueryAsync(timeout.Token));
                }
                else
                {
                    Interlocked.Increment(ref _observation.DuplicateReceipts);
                }
                if (attempt == 1)
                {
                    Assert.True(recorded);
                    if (_observation.AfterCommit)
                        await transaction.CommitAsync(timeout.Token);
                    else
                        await transaction.RollbackAsync(timeout.Token);
                    using var cancellation = new CancellationTokenSource();
                    cancellation.Cancel();
                    var original = new OperationCanceledException("测试消费者在指定业务事务边界取消。", cancellation.Token);
                    _observation.FirstCancellation.TrySetResult(original);
                    throw original;
                }
                await transaction.CommitAsync(timeout.Token);
            }
            finally
            {
                _observation.ExitCallback();
            }
        }
    }
}

/// <summary>
/// 在确认停止与业务回调排空后执行取消夹具的唯一清理任务。
/// </summary>
internal sealed class CapCancellationCleanup
{
    /// <summary>
    /// 当前夹具自行拥有的宿主。
    /// </summary>
    private readonly IHost _host;

    /// <summary>
    /// 当前夹具自行拥有的可选 ORM。
    /// </summary>
    private readonly object _ownedOrm;

    /// <summary>
    /// 提供入场关闭、回调排空和安全 GUID 的观察状态。
    /// </summary>
    private readonly AdminMySqlCapIntegrationTest.CapCancellationObservation _observation;

    /// <summary>
    /// 仅在安全条件成立后调用的独占数据清理动作。
    /// </summary>
    private readonly Func<Task> _cleanupData;

    /// <summary>
    /// 每个异步清理阶段的等待上限。
    /// </summary>
    private readonly TimeSpan _waitTimeout;

    /// <summary>
    /// 串行化唯一清理任务创建的锁。
    /// </summary>
    private readonly object _runLock = new();

    /// <summary>
    /// 保留原始错误并附加后续清理错误的异常。
    /// </summary>
    private Exception _failure;

    /// <summary>
    /// 所有重复或并发调用共同等待的唯一清理任务。
    /// </summary>
    private Task<Exception> _runTask;

    /// <summary>
    /// 使用无取消令牌启动的实际宿主停止任务。
    /// </summary>
    private Task _stopTask;

    /// <summary>
    /// 初始化一个 <see cref="CapCancellationCleanup" /> 类型的实例。
    /// </summary>
    /// <param name="host">自行持有的宿主；构造失败时可为空。</param>
    /// <param name="ownedOrm">自行持有的 ORM；EFCore 或构造前失败时为空。</param>
    /// <param name="observation">本次独占事件的入场与排空状态。</param>
    /// <param name="cleanupData">独占数据清理动作；未建表时为空。</param>
    /// <param name="original">原始验证异常；验证成功时为空。</param>
    /// <param name="waitTimeout">停止、排空及异步释放的等待上限。</param>
    internal CapCancellationCleanup(IHost host, object ownedOrm,
        AdminMySqlCapIntegrationTest.CapCancellationObservation observation, Func<Task> cleanupData,
        Exception original, TimeSpan waitTimeout)
    {
        _host = host;
        _ownedOrm = ownedOrm;
        _observation = observation;
        _cleanupData = cleanupData;
        _failure = original;
        _waitTimeout = waitTimeout;
    }

    /// <summary>
    /// 获取或创建唯一清理任务，避免重复删除与重复释放。
    /// </summary>
    /// <returns>包含原始或首个清理异常的唯一任务；成功时结果为空。</returns>
    internal Task<Exception> RunAsync()
    {
        lock (_runLock)
            return _runTask ??= RunCoreAsync();
    }

    /// <summary>
    /// 关闭入场并等待实际停止和已入场回调，再按单一路径清理资源。
    /// </summary>
    /// <returns>保留附加诊断的原始或首个异常；成功时为空。</returns>
    private async Task<Exception> RunCoreAsync()
    {
        _observation.RetryAllowed.TrySetResult(true);
        var drainTask = _observation.CloseCallbackAdmission();
        var stopped = _host == null;
        var drained = false;
        try
        {
            if (_host != null)
            {
                _stopTask = _host.StopAsync(CancellationToken.None);
                ObserveFault(_stopTask);
                await _stopTask.WaitAsync(_waitTimeout);
                stopped = true;
            }
        }
        catch (Exception exception)
        {
            RecordFailure("HostStop", exception);
        }
        try
        {
            await drainTask.WaitAsync(_waitTimeout);
            drained = true;
        }
        catch (Exception exception)
        {
            RecordFailure("CallbackDrain", exception);
        }
        if (!stopped || !drained)
        {
            RetainResources(stopped, drained);
            return _failure;
        }

        try
        {
            if (_cleanupData != null)
                await _cleanupData();
        }
        catch (Exception exception)
        {
            RecordFailure("DataCleanup", exception);
        }
        var hostDispose = await DisposeResourceAsync(_host);
        if (hostDispose.Failure != null)
            RecordFailure("HostDispose", hostDispose.Failure);
        if (!hostDispose.Completed)
        {
            RetainResources(stopped, drained);
            return _failure;
        }
        var ormDispose = await DisposeResourceAsync(_ownedOrm);
        if (ormDispose.Failure != null)
            RecordFailure("OrmDispose", ormDispose.Failure);
        if (!ormDispose.Completed)
        {
            RetainResources(stopped, drained);
        }
        return _failure;
    }

    /// <summary>
    /// 选择一次异步或同步释放，并对实际释放任务进行有界等待。
    /// </summary>
    /// <param name="resource">需要释放的资源；为空时不执行动作。</param>
    /// <returns>实际释放是否已结束，以及等待或释放阶段的异常。</returns>
    private async Task<(bool Completed, Exception Failure)> DisposeResourceAsync(object resource)
    {
        Task disposeTask = null;
        try
        {
            if (resource is IAsyncDisposable asyncDisposable)
                disposeTask = asyncDisposable.DisposeAsync().AsTask();
            else if (resource is IDisposable disposable)
                disposeTask = Task.Run(disposable.Dispose);
            else
                return (true, null);
            ObserveFault(disposeTask);
            await disposeTask.WaitAsync(_waitTimeout);
            return (true, null);
        }
        catch (Exception exception)
        {
            // 同步抛错或实际任务已失败完成时，依赖可以继续按唯一选定路径释放。
            return (disposeTask == null || disposeTask.IsCompleted, exception);
        }
    }

    /// <summary>
    /// 保留首个错误并在其诊断数据中记录后续阶段异常。
    /// </summary>
    /// <param name="stage">不包含凭据的固定阶段名称。</param>
    /// <param name="exception">本阶段实际异常。</param>
    private void RecordFailure(string stage, Exception exception)
    {
        if (_failure == null)
            _failure = exception;
        else if (!ReferenceEquals(_failure, exception))
            _failure.Data["Bing.TestCapCancellation" + stage + "Exception"] = exception;
    }

    /// <summary>
    /// 保留未释放的资源并附加仅含 GUID 与安全状态的恢复诊断。
    /// </summary>
    /// <param name="stopped">是否确认实际宿主停止成功。</param>
    /// <param name="drained">是否确认所有已入场业务回调退出。</param>
    private void RetainResources(bool stopped, bool drained)
    {
        _failure.Data["Bing.TestCapCancellationPendingCleanupOwner"] = this;
        _failure.Data["Bing.TestCapCancellationRecovery"] =
            $"EventId={_observation.EventId}; Table={_observation.BusinessTable}; " +
            $"StopConfirmed={stopped}; CallbacksDrained={drained}; ResourcesRetained=True";
    }

    /// <summary>
    /// 观察有界等待退出后实际任务的延迟失败，不触发额外清理。
    /// </summary>
    /// <param name="task">停止或释放的实际任务。</param>
    private static void ObserveFault(Task task)
    {
        _ = task.ContinueWith(completed => { _ = completed.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}

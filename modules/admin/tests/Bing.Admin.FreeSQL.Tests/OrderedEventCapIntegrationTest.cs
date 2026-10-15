using System.Collections.Concurrent;
using System.Data.Common;
using System.Text.Json;
using Bing.Admin.Data;
using Bing.Data.Events.Ordering;
using Bing.Events.Cap;
using Bing.Events.Messages;
using Bing.Events.Ordering;
using Bing.FreeSQL.Events;
using Bing.Test.Shared;
using DotNetCore.CAP;
using FreeSql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MySqlConnector;
using Xunit;

namespace Bing.Admin.FreeSQL.Tests;

/// <summary>
/// 验证 FreeSQL、CAP 与有序消费后台服务的真实联动。
/// </summary>
public sealed class OrderedEventCapIntegrationTest
{
    /// <summary>
    /// 稳定的 CAP 订阅主题。
    /// </summary>
    private const string Topic = "bing.tests.admin.freesql.ordered";

    /// <summary>
    /// 处理器在同一 CAP 事务中投递的派生主题。
    /// </summary>
    private const string DerivedTopic = "bing.tests.admin.freesql.ordered.derived";

    /// <summary>
    /// 稳定的逻辑消费者名称。
    /// </summary>
    private const string Consumer = "bing-admin-freesql-ordered-integration";

    /// <summary>
    /// 验证同事务出箱、真实接收、按序处理和重复投递去重。
    /// </summary>
    [OrderedCapIntegrationFact]
    public async Task PublishAsync_WhenCommitted_ShouldReceiveAndProcessOnceInOrder()
    {
        var settings = AdminMySqlCapIntegrationTest.IntegrationSettings.Load();
        var scopeKey = Guid.NewGuid().ToString("N");
        using var orm = CreateOrm(settings.ConnectionString);
        await EnsureTablesAsync(settings.ConnectionString);
        using var host = CreateHost(settings, orm);
        await host.StartAsync();
        try
        {
            var subscriber = (OrderedSubscriber)host.Services.GetRequiredService<ICapSubscribe>();
            await EnsureSubscriberReadyAsync(host, subscriber);
            var events = new List<MessageEvent<TestPayload>>();
            using (var scope = host.Services.CreateScope())
            using (var unitOfWork = orm.CreateUnitOfWork())
            {
                var capPublisher = scope.ServiceProvider.GetRequiredService<ICapPublisher>();
                var publisher = scope.ServiceProvider.GetRequiredService<OrderedEventPublisher>();
                var previousTransaction = capPublisher.Transaction.Value;
                try
                {
                    using var capTransaction = unitOfWork.BeginTransaction(capPublisher);
                    var transaction = unitOfWork.GetOrBeginTransaction();
                    Assert.Same(transaction, capPublisher.Transaction.Value.DbTransaction);
                    for (var sequence = 1; sequence <= 3; sequence++)
                    {
                        var message = CreateEvent(scopeKey);
                        await publisher.PublishAsync(transaction, message);
                        Assert.Equal(sequence, message.Sequence);
                        await InsertBusinessAsync(transaction, scopeKey, "publish", sequence, message.Id);
                        events.Add(message);
                    }

                    Assert.Empty(await ReadBusinessAsync(settings.ConnectionString, scopeKey, "publish"));
                    Assert.Equal(0, await CountOutboxAsync(settings.ConnectionString, scopeKey));
                    unitOfWork.Commit(capTransaction);
                }
                finally
                {
                    capPublisher.Transaction.Value = previousTransaction;
                }
            }

            Assert.Equal(new long[] { 1, 2, 3 },
                (await ReadBusinessAsync(settings.ConnectionString, scopeKey, "publish")).Select(row => row.Sequence));
            Assert.Equal(3, await CountOutboxAsync(settings.ConnectionString, scopeKey));
            await WaitForBusinessAsync(settings.ConnectionString, scopeKey, 3);
            Assert.Equal(new long[] { 1, 2, 3 },
                (await ReadBusinessAsync(settings.ConnectionString, scopeKey, "consume")).Select(row => row.Sequence));
            Assert.Equal(3, await CountOutboxAsync(settings.ConnectionString, scopeKey, DerivedTopic));
            Assert.All(events, message => Assert.True(subscriber.Deliveries(message.Id) >= 1));

            using (var duplicateScope = host.Services.CreateScope())
            using (var duplicateUnitOfWork = orm.CreateUnitOfWork())
            {
                var duplicate = events[1];
                var headers = new Dictionary<string, string>();
                ClusterEventHeaders.Write(headers, duplicate);
                var capPublisher = duplicateScope.ServiceProvider.GetRequiredService<ICapPublisher>();
                var previousTransaction = capPublisher.Transaction.Value;
                try
                {
                    using var capTransaction = duplicateUnitOfWork.BeginTransaction(capPublisher);
                    await capPublisher.PublishAsync(Topic, duplicate.Data, headers);
                    duplicateUnitOfWork.Commit(capTransaction);
                }
                finally
                {
                    capPublisher.Transaction.Value = previousTransaction;
                }
                await subscriber.WaitForDeliveriesAsync(duplicate.Id, 2);
            }

            Assert.Equal(new long[] { 1, 2, 3 },
                (await ReadBusinessAsync(settings.ConnectionString, scopeKey, "consume")).Select(row => row.Sequence));
            Assert.Equal(3, await CountOutboxAsync(settings.ConnectionString, scopeKey, DerivedTopic));
            using var stateScope = host.Services.CreateScope();
            var state = await stateScope.ServiceProvider.GetRequiredService<OrderedEventCoordinator>()
                .GetStateAsync(Consumer, Stream(scopeKey));
            Assert.Equal(3, state.LastSequence);
            Assert.Equal(0, state.PendingCount);
        }
        finally
        {
            try
            {
                await host.StopAsync();
            }
            finally
            {
                await CleanupAsync(settings.ConnectionString, scopeKey);
            }
        }
    }

    /// <summary>
    /// 验证发布事务回滚后业务、序号和出箱均不留痕迹。
    /// </summary>
    [OrderedCapIntegrationFact]
    public async Task PublishAsync_WhenRolledBack_ShouldRetryFromFirstSequence()
    {
        var settings = AdminMySqlCapIntegrationTest.IntegrationSettings.Load();
        var scopeKey = Guid.NewGuid().ToString("N");
        using var orm = CreateOrm(settings.ConnectionString);
        await EnsureTablesAsync(settings.ConnectionString);
        using var host = CreateHost(settings, orm);
        await host.StartAsync();
        try
        {
            await EnsureSubscriberReadyAsync(host,
                (OrderedSubscriber)host.Services.GetRequiredService<ICapSubscribe>());
            using (var scope = host.Services.CreateScope())
            using (var unitOfWork = orm.CreateUnitOfWork())
            {
                var capPublisher = scope.ServiceProvider.GetRequiredService<ICapPublisher>();
                var previousTransaction = capPublisher.Transaction.Value;
                try
                {
                    using var capTransaction = unitOfWork.BeginTransaction(capPublisher);
                    var transaction = unitOfWork.GetOrBeginTransaction();
                    var message = CreateEvent(scopeKey);
                    await scope.ServiceProvider.GetRequiredService<OrderedEventPublisher>()
                        .PublishAsync(transaction, message);
                    Assert.Equal(1, message.Sequence);
                    await InsertBusinessAsync(transaction, scopeKey, "publish", 1, message.Id);
                    capTransaction.Rollback();
                    unitOfWork.Rollback();
                }
                finally
                {
                    capPublisher.Transaction.Value = previousTransaction;
                }
            }

            Assert.Empty(await ReadBusinessAsync(settings.ConnectionString, scopeKey, "publish"));
            Assert.Equal(0, await CountOutboxAsync(settings.ConnectionString, scopeKey));
            Assert.Equal(0, await CountOutboxAsync(settings.ConnectionString, scopeKey, DerivedTopic));
            Assert.Equal(0, await ReadAllocatedSequenceAsync(settings.ConnectionString, scopeKey));

            using (var scope = host.Services.CreateScope())
            using (var unitOfWork = orm.CreateUnitOfWork())
            {
                var capPublisher = scope.ServiceProvider.GetRequiredService<ICapPublisher>();
                var previousTransaction = capPublisher.Transaction.Value;
                try
                {
                    using var capTransaction = unitOfWork.BeginTransaction(capPublisher);
                    var transaction = unitOfWork.GetOrBeginTransaction();
                    var retry = CreateEvent(scopeKey);
                    await scope.ServiceProvider.GetRequiredService<OrderedEventPublisher>()
                        .PublishAsync(transaction, retry);
                    Assert.Equal(1, retry.Sequence);
                    await InsertBusinessAsync(transaction, scopeKey, "publish", 1, retry.Id);
                    unitOfWork.Commit(capTransaction);
                }
                finally
                {
                    capPublisher.Transaction.Value = previousTransaction;
                }
            }

            Assert.Equal(new long[] { 1 },
                (await ReadBusinessAsync(settings.ConnectionString, scopeKey, "publish")).Select(row => row.Sequence));
            Assert.Equal(1, await CountOutboxAsync(settings.ConnectionString, scopeKey));
            await WaitForBusinessAsync(settings.ConnectionString, scopeKey, 1);
            Assert.Equal(1, await CountOutboxAsync(settings.ConnectionString, scopeKey, DerivedTopic));
        }
        finally
        {
            try
            {
                await host.StopAsync();
            }
            finally
            {
                await CleanupAsync(settings.ConnectionString, scopeKey);
            }
        }
    }

    /// <summary>
    /// 验证 CAP 乱序到达时先暂存未来事件并按连续序号处理。
    /// </summary>
    [OrderedCapIntegrationFact]
    public async Task ReceiveAsync_WhenCapMessagesArriveOutOfOrder_ShouldProcessInSequence()
    {
        var settings = AdminMySqlCapIntegrationTest.IntegrationSettings.Load();
        var scopeKey = Guid.NewGuid().ToString("N");
        using var orm = CreateOrm(settings.ConnectionString);
        await EnsureTablesAsync(settings.ConnectionString);
        using var host = CreateHost(settings, orm);
        await host.StartAsync();
        try
        {
            var subscriber = (OrderedSubscriber)host.Services.GetRequiredService<ICapSubscribe>();
            await EnsureSubscriberReadyAsync(host, subscriber);
            var third = CreateEvent(scopeKey);
            third.Sequence = 3;
            await PublishIncomingAsync(host, orm, third);
            await subscriber.WaitForDeliveriesAsync(third.Id, 1);

            using (var stateScope = host.Services.CreateScope())
            {
                var state = await stateScope.ServiceProvider.GetRequiredService<OrderedEventCoordinator>()
                    .GetStateAsync(Consumer, Stream(scopeKey));
                Assert.Equal(0, state.LastSequence);
                Assert.Equal(1, state.PendingCount);
                Assert.Equal(3, state.FirstPendingSequence);
            }
            Assert.Empty(await ReadBusinessAsync(settings.ConnectionString, scopeKey, "consume"));

            var first = CreateEvent(scopeKey);
            first.Sequence = 1;
            await PublishIncomingAsync(host, orm, first);
            await subscriber.WaitForDeliveriesAsync(first.Id, 1);
            await WaitForBusinessAsync(settings.ConnectionString, scopeKey, 1);
            Assert.Equal(new long[] { 1 },
                (await ReadBusinessAsync(settings.ConnectionString, scopeKey, "consume")).Select(row => row.Sequence));

            var second = CreateEvent(scopeKey);
            second.Sequence = 2;
            await PublishIncomingAsync(host, orm, second);
            await subscriber.WaitForDeliveriesAsync(second.Id, 1);
            await WaitForBusinessAsync(settings.ConnectionString, scopeKey, 3);
            Assert.Equal(new long[] { 1, 2, 3 },
                (await ReadBusinessAsync(settings.ConnectionString, scopeKey, "consume")).Select(row => row.Sequence));
            Assert.Equal(3, await CountOutboxAsync(settings.ConnectionString, scopeKey, DerivedTopic));
            using var finalScope = host.Services.CreateScope();
            var finalState = await finalScope.ServiceProvider.GetRequiredService<OrderedEventCoordinator>()
                .GetStateAsync(Consumer, Stream(scopeKey));
            Assert.Equal(3, finalState.LastSequence);
            Assert.Equal(0, finalState.PendingCount);
        }
        finally
        {
            try
            {
                await host.StopAsync();
            }
            finally
            {
                await CleanupAsync(settings.ConnectionString, scopeKey);
            }
        }
    }

    /// <summary>
    /// 在新的 CAP 业务事务中投递已带序号的入站消息。
    /// </summary>
    private static async Task PublishIncomingAsync(IHost host, IFreeSql orm, MessageEvent<TestPayload> message)
    {
        using var scope = host.Services.CreateScope();
        using var unitOfWork = orm.CreateUnitOfWork();
        var publisher = scope.ServiceProvider.GetRequiredService<ICapPublisher>();
        var previousTransaction = publisher.Transaction.Value;
        try
        {
            using var capTransaction = unitOfWork.BeginTransaction(publisher);
            var headers = new Dictionary<string, string>();
            ClusterEventHeaders.Write(headers, message);
            await publisher.PublishAsync(Topic, message.Data, headers);
            unitOfWork.Commit(capTransaction);
        }
        finally
        {
            publisher.Transaction.Value = previousTransaction;
        }
    }

    /// <summary>
    /// 建立使用稳定逻辑消费者的测试宿主。
    /// </summary>
    private static IHost CreateHost(AdminMySqlCapIntegrationTest.IntegrationSettings settings, IFreeSql orm) =>
        new HostBuilder().ConfigureServices(services =>
        {
            services.AddLogging();
            services.AddSingleton<IOrderedEventStore>(new DbOrderedEventStore(OrderedEventDatabase.MySql));
            services.AddScoped<FreeSqlOrderedEventTransactionFactory>(_ =>
                new FreeSqlOrderedEventTransactionFactory(orm));
            services.AddSingleton<ICapSubscribe, OrderedSubscriber>();
            services.AddBingOrderedConsumer<TestHandler>(Consumer, TimeSpan.FromMilliseconds(50), 20);
            services.AddCapEventBus(options =>
            {
                options.UseMySql(settings.ConnectionString);
                options.UseRabbitMQ(rabbit =>
                {
                    rabbit.HostName = settings.RabbitHost;
                    rabbit.Port = settings.RabbitPort;
                    rabbit.UserName = settings.RabbitUser;
                    rabbit.Password = settings.RabbitPassword;
                });
                options.Version = "1.0";
                options.ConsumerThreadCount = 1;
                options.UseDispatchingPerGroup = true;
            });
            services.AddBingCapOrderedEventTransaction<FreeSqlOrderedEventTransactionFactory>();
        }).Build();

    /// <summary>
    /// 通过同主题真实消息等待当前 CAP 订阅绑定完成。
    /// </summary>
    private static async Task EnsureSubscriberReadyAsync(IHost host, OrderedSubscriber subscriber)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var id = Guid.NewGuid().ToString("N");
            var waiter = subscriber.ExpectProbe(id);
            try
            {
                using var scope = host.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ICapPublisher>()
                    .PublishAsync(Topic, new TestPayload { ProbeId = id, IsProbe = true });
                await waiter.WaitAsync(TimeSpan.FromSeconds(3));
                return;
            }
            catch (TimeoutException)
            {
                subscriber.ForgetProbe(id);
            }
            catch (MySqlException exception) when (exception.Number == 1146 &&
                exception.Message.Contains("cap.published", StringComparison.Ordinal))
            {
                // 空测试库的 CAP 表由后台初始化；仅在就绪阶段等待，业务错误仍直接失败。
                subscriber.ForgetProbe(id);
                await Task.Delay(200);
            }
            catch (NullReferenceException exception) when (exception.StackTrace?.Contains(
                "DotNetCore.CAP.Processor.DispatcherPerGroup.EnqueueToPublish", StringComparison.Ordinal) == true)
            {
                subscriber.ForgetProbe(id);
                await Task.Delay(200);
            }
        }
        throw new TimeoutException("CAP 有序事件订阅者未完成当前宿主的主题绑定。");
    }

    /// <summary>
    /// 创建尚未分配业务序号的消息。
    /// </summary>
    private static MessageEvent<TestPayload> CreateEvent(string scopeKey) => new(new TestPayload { Scope = scopeKey })
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = Topic,
        TenantId = scopeKey,
        Stream = "inventory",
        EntityKey = "item",
        ContractVersion = "1"
    };

    /// <summary>
    /// 创建测试业务流。
    /// </summary>
    private static OrderedEventStream Stream(string scopeKey) => new(scopeKey, "inventory", "item");

    /// <summary>
    /// 创建 FreeSQL 的 MySQL 宿主实例。
    /// </summary>
    private static IFreeSql CreateOrm(string connectionString) => new FreeSqlBuilder()
        .UseConnectionString(DataType.MySql, connectionString).Build();

    /// <summary>
    /// 建立有序事件与业务结果表。
    /// </summary>
    private static async Task EnsureTablesAsync(string connectionString)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var sql in OrderedEventSchema.GetCreateStatements(OrderedEventDatabase.MySql))
        {
            using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
        using var business = new MySqlCommand("CREATE TABLE IF NOT EXISTS bing_admin_ordered_test_business " +
            "(Ordinal bigint NOT NULL AUTO_INCREMENT PRIMARY KEY, ScopeKey varchar(32) NOT NULL, " +
            "Phase varchar(20) NOT NULL, Sequence bigint NOT NULL, EventId varchar(128) NOT NULL, " +
            "UNIQUE KEY ux_bing_admin_ordered_test (ScopeKey, Phase, Sequence)) ENGINE=InnoDB", connection);
        await business.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 使用传入事务写入测试业务结果。
    /// </summary>
    private static async Task InsertBusinessAsync(DbTransaction transaction, string scopeKey, string phase,
        long sequence, string eventId, CancellationToken cancellationToken = default)
    {
        using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO bing_admin_ordered_test_business " +
            "(ScopeKey, Phase, Sequence, EventId) VALUES (@scope, @phase, @sequence, @eventId)";
        AddParameter(command, "@scope", scopeKey);
        AddParameter(command, "@phase", phase);
        AddParameter(command, "@sequence", sequence);
        AddParameter(command, "@eventId", eventId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 按真实插入顺序读取测试业务结果。
    /// </summary>
    private static async Task<(long Sequence, string EventId)[]> ReadBusinessAsync(string connectionString,
        string scopeKey, string phase)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new MySqlCommand("SELECT Sequence, EventId FROM bing_admin_ordered_test_business " +
            "WHERE ScopeKey = @scope AND Phase = @phase ORDER BY Ordinal", connection);
        command.Parameters.AddWithValue("@scope", scopeKey);
        command.Parameters.AddWithValue("@phase", phase);
        using var reader = await command.ExecuteReaderAsync();
        var rows = new List<(long, string)>();
        while (await reader.ReadAsync())
            rows.Add((reader.GetInt64(0), reader.GetString(1)));
        return rows.ToArray();
    }

    /// <summary>
    /// 读取当前业务流的 CAP 出箱消息数量。
    /// </summary>
    private static async Task<long> CountOutboxAsync(string connectionString, string scopeKey,
        string topic = Topic)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new MySqlCommand("SELECT COUNT(*) FROM `cap.published` " +
            "WHERE `Name` = @topic AND `Content` LIKE @scope", connection);
        command.Parameters.AddWithValue("@topic", topic);
        command.Parameters.AddWithValue("@scope", "%" + scopeKey + "%");
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// 读取已提交的业务流最后分配序号。
    /// </summary>
    private static async Task<long> ReadAllocatedSequenceAsync(string connectionString, string scopeKey)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new MySqlCommand("SELECT LastSequence FROM bing_event_sequence " +
            "WHERE StreamKey = @stream", connection);
        command.Parameters.AddWithValue("@stream",
            new DbOrderedEventStore(OrderedEventDatabase.MySql).GetStreamKey(Stream(scopeKey)));
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// 有界等待后台业务处理达到预期数量。
    /// </summary>
    private static async Task WaitForBusinessAsync(string connectionString, string scopeKey, int count)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if ((await ReadBusinessAsync(connectionString, scopeKey, "consume")).Length == count)
                return;
            await Task.Delay(100);
        }
        throw new TimeoutException("有序事件后台处理未达到预期数量。");
    }

    /// <summary>
    /// 仅删除本次 GUID 业务流产生的测试数据。
    /// </summary>
    private static async Task CleanupAsync(string connectionString, string scopeKey)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        var streamKey = new DbOrderedEventStore(OrderedEventDatabase.MySql).GetStreamKey(Stream(scopeKey));
        foreach (var (sql, value) in new[]
                 {
                     ("DELETE FROM bing_admin_ordered_test_business WHERE ScopeKey = @value", scopeKey),
                     ("DELETE FROM bing_event_inbox WHERE StreamKey = @value", streamKey),
                     ("DELETE FROM bing_event_inbox_archive WHERE StreamKey = @value", streamKey),
                     ("DELETE FROM bing_event_cursor WHERE StreamKey = @value", streamKey),
                     ("DELETE FROM bing_event_sequence WHERE StreamKey = @value", streamKey)
                 })
        {
            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@value", value);
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// 向命令添加参数。
    /// </summary>
    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    /// <summary>
    /// 测试用消息负载。
    /// </summary>
    public sealed class TestPayload
    {
        /// <summary>
        /// 业务流标识。
        /// </summary>
        public string Scope { get; set; }
        /// <summary>
        /// 就绪探针标识。
        /// </summary>
        public string ProbeId { get; set; }
        /// <summary>
        /// 是否为就绪探针。
        /// </summary>
        public bool IsProbe { get; set; }
    }

    /// <summary>
    /// 真实 CAP 订阅者，先持久化收件箱再确认投递。
    /// </summary>
    public sealed class OrderedSubscriber : ICapSubscribe
    {
        /// <summary>
        /// 作用域工厂。
        /// </summary>
        private readonly IServiceScopeFactory _scopes;
        /// <summary>
        /// 探针等待者。
        /// </summary>
        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _probes = new();
        /// <summary>
        /// 各事件的已完成投递数。
        /// </summary>
        private readonly ConcurrentDictionary<string, int> _deliveries = new();

        /// <summary>
        /// 初始化一个 <see cref="OrderedSubscriber" /> 类型的实例。
        /// </summary>
        public OrderedSubscriber(IServiceScopeFactory scopes) => _scopes = scopes;

        /// <summary>
        /// 登记待接收的就绪探针。
        /// </summary>
        public Task ExpectProbe(string id) => _probes.GetOrAdd(id,
            _ => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)).Task;

        /// <summary>
        /// 移除超时探针。
        /// </summary>
        public void ForgetProbe(string id) => _probes.TryRemove(id, out _);

        /// <summary>
        /// 读取已完成的投递数量。
        /// </summary>
        public int Deliveries(string eventId) => _deliveries.TryGetValue(eventId, out var count) ? count : 0;

        /// <summary>
        /// 有界等待重复投递完成。
        /// </summary>
        public async Task WaitForDeliveriesAsync(string eventId, int count)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                if (Deliveries(eventId) >= count)
                    return;
                await Task.Delay(100);
            }
            throw new TimeoutException("CAP 重复投递未到达有序事件订阅者。");
        }

        /// <summary>
        /// 持久化真实消息，探针则只确认订阅就绪。
        /// </summary>
        [CapSubscribe(Topic, Group = "bing-admin-freesql-ordered-integration")]
        public async Task ReceiveAsync(TestPayload message, [FromCap] CapHeader header)
        {
            if (message.IsProbe)
            {
                if (message.ProbeId != null && _probes.TryRemove(message.ProbeId, out var waiter))
                    waiter.TrySetResult(true);
                return;
            }
            var headers = new Dictionary<string, string>();
            foreach (var name in new[]
                     {
                         ClusterEventHeaders.EventId, ClusterEventHeaders.TenantId, ClusterEventHeaders.Stream,
                         ClusterEventHeaders.EntityKey, ClusterEventHeaders.Sequence,
                         ClusterEventHeaders.ContractVersion
                     })
            {
                if (header.TryGetValue(name, out var value))
                    headers[name] = value;
            }
            var metadata = ClusterEventHeaders.Read(headers);
            Assert.NotNull(metadata);
            Assert.Equal(message.Scope, metadata.TenantId);
            await using var scope = _scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<OrderedEventCoordinator>()
                .ReceiveAsync(Consumer, headers, JsonSerializer.Serialize(message));
            _deliveries.AddOrUpdate(metadata.EventId, 1, (_, count) => count + 1);
        }
    }

    /// <summary>
    /// 使用处理事务写入业务结果。
    /// </summary>
    public sealed class TestHandler : IOrderedEventHandler
    {
        /// <summary>
        /// CAP 发布器。
        /// </summary>
        private readonly ICapPublisher _publisher;

        /// <summary>
        /// 初始化一个 <see cref="TestHandler" /> 类型的实例。
        /// </summary>
        public TestHandler(ICapPublisher publisher) => _publisher = publisher;

        /// <inheritdoc />
        public async Task HandleAsync(OrderedEventStream stream, OrderedEventMessage message,
            DbTransaction transaction, CancellationToken cancellationToken)
        {
            await InsertBusinessAsync(transaction, stream.TenantId, "consume", message.Sequence,
                message.EventId, cancellationToken);
            await _publisher.PublishAsync(DerivedTopic,
                new { message.EventId, message.Sequence, Scope = stream.TenantId },
                cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// 仅在专用 MySQL/CAP/RabbitMQ 环境执行。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    private sealed class OrderedCapIntegrationFactAttribute : FactAttribute
    {
        /// <summary>
        /// 初始化一个 <see cref="OrderedCapIntegrationFactAttribute" /> 类型的实例。
        /// </summary>
        public OrderedCapIntegrationFactAttribute()
        {
            Skip = IntegrationTestGate.IsProviderEnabled("MySql") &&
                   string.Equals(Environment.GetEnvironmentVariable("RUN_MYSQL_CAP_INTEGRATION_TESTS"),
                       "true", StringComparison.OrdinalIgnoreCase)
                ? null
                : "需要启用专用 MySQL/CAP 集成测试环境。";
        }
    }
}

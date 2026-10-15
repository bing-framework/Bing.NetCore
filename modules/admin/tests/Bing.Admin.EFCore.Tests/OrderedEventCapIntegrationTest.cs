using System.Collections.Concurrent;
using System.Data.Common;
using System.Text.Json;
using AspectCore.Extensions.Hosting;
using Bing.Data.Events.Ordering;
using Bing.DependencyInjection;
using Bing.EntityFrameworkCore.Events;
using Bing.Events.Cap;
using Bing.Events.Messages;
using Bing.Events.Ordering;
using Bing.Test.Shared;
using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MySqlConnector;
using Xunit;

namespace Bing.Admin.EFCore.Tests;

/// <summary>
/// 验证 AspectCore 宿主中 EFCore、CAP 与有序消费者的真实联动。
/// </summary>
public sealed class OrderedEventCapIntegrationTest
{
    /// <summary>
    /// 稳定的 CAP 订阅主题。
    /// </summary>
    private const string Topic = "bing.tests.admin.efcore.ordered";

    /// <summary>
    /// 处理器在同一 CAP 事务中写入的派生主题。
    /// </summary>
    private const string DerivedTopic = "bing.tests.admin.efcore.ordered.derived";

    /// <summary>
    /// 稳定的逻辑消费者名称。
    /// </summary>
    private const string Consumer = "bing-admin-efcore-ordered-integration";

    /// <summary>
    /// 验证同事务出箱、真实接收、处理顺序与重复投递去重。
    /// </summary>
    [OrderedCapIntegrationFact]
    public async Task PublishAsync_WhenCommitted_ShouldReceiveAndProcessOnceInOrder()
    {
        var settings = IntegrationSettings.Load();
        var scopeKey = Guid.NewGuid().ToString("N");
        await EnsureTablesAsync(settings.ConnectionString);
        using var host = CreateHost(settings);
        await host.StartAsync();
        try
        {
            var subscriber = (OrderedSubscriber)host.Services.GetRequiredService<ICapSubscribe>();
            await EnsureSubscriberReadyAsync(host, subscriber);
            var events = new List<MessageEvent<TestPayload>>();
            await using (var scope = host.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<OrderedTestDbContext>();
                var capPublisher = scope.ServiceProvider.GetRequiredService<ICapPublisher>();
                var previousTransaction = capPublisher.Transaction.Value;
                try
                {
                    await using var capTransaction = context.Database.BeginTransaction(capPublisher, autoCommit: false);
                    var transaction = context.Database.CurrentTransaction.GetDbTransaction();
                    var publisher = scope.ServiceProvider.GetRequiredService<OrderedEventPublisher>();
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
                    await capTransaction.CommitAsync();
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
            var evidence = host.Services.GetRequiredService<ScopeEvidence>();
            Assert.Equal(3, events.Select(message => evidence.HandlerScope(message.Id)).Distinct().Count());
            Assert.All(events, message =>
                Assert.NotEqual(evidence.ReceiveScope(message.Id), evidence.HandlerScope(message.Id)));

            var duplicate = events[1];
            await PublishIncomingAsync(host, duplicate);
            await subscriber.WaitForDeliveriesAsync(duplicate.Id, 2);
            Assert.Equal(new long[] { 1, 2, 3 },
                (await ReadBusinessAsync(settings.ConnectionString, scopeKey, "consume")).Select(row => row.Sequence));
            Assert.Equal(3, await CountOutboxAsync(settings.ConnectionString, scopeKey, DerivedTopic));
            await using var stateScope = host.Services.CreateAsyncScope();
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
    /// 验证发布回滚后业务、出箱和序号均回退。
    /// </summary>
    [OrderedCapIntegrationFact]
    public async Task PublishAsync_WhenRolledBack_ShouldRetryFromFirstSequence()
    {
        var settings = IntegrationSettings.Load();
        var scopeKey = Guid.NewGuid().ToString("N");
        await EnsureTablesAsync(settings.ConnectionString);
        using var host = CreateHost(settings);
        await host.StartAsync();
        try
        {
            await EnsureSubscriberReadyAsync(host,
                (OrderedSubscriber)host.Services.GetRequiredService<ICapSubscribe>());
            await using (var scope = host.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<OrderedTestDbContext>();
                var capPublisher = scope.ServiceProvider.GetRequiredService<ICapPublisher>();
                var previousTransaction = capPublisher.Transaction.Value;
                try
                {
                    await using var capTransaction = context.Database.BeginTransaction(capPublisher, autoCommit: false);
                    var transaction = context.Database.CurrentTransaction.GetDbTransaction();
                    Assert.Same(transaction,
                        ((IDbContextTransaction)capPublisher.Transaction.Value.DbTransaction).GetDbTransaction());
                    var message = CreateEvent(scopeKey);
                    await scope.ServiceProvider.GetRequiredService<OrderedEventPublisher>()
                        .PublishAsync(transaction, message);
                    Assert.Equal(1, message.Sequence);
                    await InsertBusinessAsync(transaction, scopeKey, "publish", 1, message.Id);
                    await capTransaction.RollbackAsync();
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

            await using (var scope = host.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<OrderedTestDbContext>();
                var capPublisher = scope.ServiceProvider.GetRequiredService<ICapPublisher>();
                var previousTransaction = capPublisher.Transaction.Value;
                try
                {
                    await using var capTransaction = context.Database.BeginTransaction(capPublisher, autoCommit: false);
                    var transaction = context.Database.CurrentTransaction.GetDbTransaction();
                    var retry = CreateEvent(scopeKey);
                    await scope.ServiceProvider.GetRequiredService<OrderedEventPublisher>()
                        .PublishAsync(transaction, retry);
                    Assert.Equal(1, retry.Sequence);
                    await InsertBusinessAsync(transaction, scopeKey, "publish", 1, retry.Id);
                    await capTransaction.CommitAsync();
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
    /// 验证真实 CAP 乱序到达后按连续序号处理。
    /// </summary>
    [OrderedCapIntegrationFact]
    public async Task ReceiveAsync_WhenCapMessagesArriveOutOfOrder_ShouldProcessInSequence()
    {
        var settings = IntegrationSettings.Load();
        var scopeKey = Guid.NewGuid().ToString("N");
        await EnsureTablesAsync(settings.ConnectionString);
        using var host = CreateHost(settings);
        await host.StartAsync();
        try
        {
            var subscriber = (OrderedSubscriber)host.Services.GetRequiredService<ICapSubscribe>();
            await EnsureSubscriberReadyAsync(host, subscriber);
            var third = CreateEvent(scopeKey);
            third.Sequence = 3;
            await PublishIncomingAsync(host, third);
            await subscriber.WaitForDeliveriesAsync(third.Id, 1);

            await using (var stateScope = host.Services.CreateAsyncScope())
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
            await PublishIncomingAsync(host, first);
            await subscriber.WaitForDeliveriesAsync(first.Id, 1);
            await WaitForBusinessAsync(settings.ConnectionString, scopeKey, 1);
            Assert.Equal(new long[] { 1 },
                (await ReadBusinessAsync(settings.ConnectionString, scopeKey, "consume")).Select(row => row.Sequence));

            var second = CreateEvent(scopeKey);
            second.Sequence = 2;
            await PublishIncomingAsync(host, second);
            await subscriber.WaitForDeliveriesAsync(second.Id, 1);
            await WaitForBusinessAsync(settings.ConnectionString, scopeKey, 3);
            Assert.Equal(new long[] { 1, 2, 3 },
                (await ReadBusinessAsync(settings.ConnectionString, scopeKey, "consume")).Select(row => row.Sequence));
            Assert.Equal(3, await CountOutboxAsync(settings.ConnectionString, scopeKey, DerivedTopic));
            await using var finalScope = host.Services.CreateAsyncScope();
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
    /// 在新 EFCore/CAP 事务内发布已带序号的入站消息。
    /// </summary>
    private static async Task PublishIncomingAsync(IHost host, MessageEvent<TestPayload> message)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderedTestDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<ICapPublisher>();
        var previousTransaction = publisher.Transaction.Value;
        try
        {
            await using var capTransaction = context.Database.BeginTransaction(publisher, autoCommit: false);
            var headers = new Dictionary<string, string>();
            ClusterEventHeaders.Write(headers, message);
            await publisher.PublishAsync(Topic, message.Data, headers);
            await capTransaction.CommitAsync();
        }
        finally
        {
            publisher.Transaction.Value = previousTransaction;
        }
    }

    /// <summary>
    /// 建立启用 AspectCore 的稳定逻辑消费者宿主。
    /// </summary>
    private static IHost CreateHost(IntegrationSettings settings) => new HostBuilder()
        .UseServiceContext()
        .ConfigureServices(services =>
        {
            services.EnableAop();
            services.AddLogging();
            services.AddDbContext<OrderedTestDbContext>(options => options.UseMySql(
                settings.ConnectionString, ServerVersion.AutoDetect(settings.ConnectionString)));
            services.AddSingleton<IOrderedEventStore>(new DbOrderedEventStore(OrderedEventDatabase.MySql));
            services.AddScoped<EfCoreOrderedEventTransactionFactory<OrderedTestDbContext>>();
            services.AddScoped<ScopeWitness>();
            services.AddSingleton<ScopeEvidence>();
            services.AddSingleton<ICapSubscribe, OrderedSubscriber>();
            services.AddBingOrderedConsumer<TestHandler>(Consumer, TimeSpan.FromMilliseconds(50), 20);
            services.AddCapEventBus(options =>
            {
                options.UseEntityFramework<OrderedTestDbContext>();
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
            services.AddBingCapOrderedEventTransaction<EfCoreOrderedEventTransactionFactory<OrderedTestDbContext>>();
        }).Build();

    /// <summary>
    /// 通过同主题真实消息确认 CAP 订阅绑定。
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
                await using var scope = host.Services.CreateAsyncScope();
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
        throw new TimeoutException("EFCore CAP 有序事件订阅者未完成主题绑定。");
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
        using var business = new MySqlCommand("CREATE TABLE IF NOT EXISTS bing_admin_ef_ordered_test_business " +
            "(Ordinal bigint NOT NULL AUTO_INCREMENT PRIMARY KEY, ScopeKey varchar(32) NOT NULL, " +
            "Phase varchar(20) NOT NULL, Sequence bigint NOT NULL, EventId varchar(128) NOT NULL, " +
            "UNIQUE KEY ux_bing_admin_ef_ordered_test (ScopeKey, Phase, Sequence)) ENGINE=InnoDB", connection);
        await business.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 使用传入原生事务写入业务结果。
    /// </summary>
    private static async Task InsertBusinessAsync(DbTransaction transaction, string scopeKey, string phase,
        long sequence, string eventId, CancellationToken cancellationToken = default)
    {
        using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO bing_admin_ef_ordered_test_business " +
            "(ScopeKey, Phase, Sequence, EventId) VALUES (@scope, @phase, @sequence, @eventId)";
        AddParameter(command, "@scope", scopeKey);
        AddParameter(command, "@phase", phase);
        AddParameter(command, "@sequence", sequence);
        AddParameter(command, "@eventId", eventId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 按真实插入顺序读取已提交业务结果。
    /// </summary>
    private static async Task<(long Sequence, string EventId)[]> ReadBusinessAsync(string connectionString,
        string scopeKey, string phase)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new MySqlCommand("SELECT Sequence, EventId FROM bing_admin_ef_ordered_test_business " +
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
    /// 读取当前测试业务流的 CAP 出箱数量。
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
    /// 读取当前业务流的已提交发布序号。
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
    /// 有界等待后台处理达到预期业务记录数。
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
        throw new TimeoutException("EFCore 有序事件后台处理未达到预期数量。");
    }

    /// <summary>
    /// 仅清理本次 GUID 业务流产生的测试记录。
    /// </summary>
    private static async Task CleanupAsync(string connectionString, string scopeKey)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        var streamKey = new DbOrderedEventStore(OrderedEventDatabase.MySql).GetStreamKey(Stream(scopeKey));
        foreach (var (sql, value) in new[]
                 {
                     ("DELETE FROM bing_admin_ef_ordered_test_business WHERE ScopeKey = @value", scopeKey),
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
    /// 向当前事务命令添加参数。
    /// </summary>
    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    /// <summary>
    /// 测试专用 EFCore 上下文。
    /// </summary>
    public sealed class OrderedTestDbContext : DbContext
    {
        /// <summary>
        /// 初始化一个 <see cref="OrderedTestDbContext" /> 类型的实例。
        /// </summary>
        public OrderedTestDbContext(DbContextOptions<OrderedTestDbContext> options) : base(options) { }
    }

    /// <summary>
    /// 测试消息负载。
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
    /// 标记一次容器服务作用域。
    /// </summary>
    public sealed class ScopeWitness
    {
        /// <summary>
        /// 当前作用域的唯一标识。
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// 记录接收和处理器实际使用的作用域。
    /// </summary>
    public sealed class ScopeEvidence
    {
        /// <summary>
        /// 接收回调的作用域标识。
        /// </summary>
        private readonly ConcurrentDictionary<string, Guid> _received = new();

        /// <summary>
        /// 业务处理器的作用域标识。
        /// </summary>
        private readonly ConcurrentDictionary<string, Guid> _handled = new();

        /// <summary>
        /// 记录接收作用域。
        /// </summary>
        public void RecordReceive(string eventId, Guid scopeId) => _received[eventId] = scopeId;

        /// <summary>
        /// 记录处理作用域。
        /// </summary>
        public void RecordHandle(string eventId, Guid scopeId) => _handled[eventId] = scopeId;

        /// <summary>
        /// 读取接收作用域。
        /// </summary>
        public Guid ReceiveScope(string eventId) => _received[eventId];

        /// <summary>
        /// 读取处理作用域。
        /// </summary>
        public Guid HandlerScope(string eventId) => _handled[eventId];
    }

    /// <summary>
    /// 真实 CAP 订阅者，等待持久化完成后确认消息。
    /// </summary>
    public sealed class OrderedSubscriber : ICapSubscribe
    {
        /// <summary>
        /// 作用域工厂。
        /// </summary>
        private readonly IServiceScopeFactory _scopes;

        /// <summary>
        /// 作用域观测记录。
        /// </summary>
        private readonly ScopeEvidence _evidence;

        /// <summary>
        /// 探针等待者。
        /// </summary>
        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _probes = new();

        /// <summary>
        /// 业务消息完成投递次数。
        /// </summary>
        private readonly ConcurrentDictionary<string, int> _deliveries = new();

        /// <summary>
        /// 初始化一个 <see cref="OrderedSubscriber" /> 类型的实例。
        /// </summary>
        public OrderedSubscriber(IServiceScopeFactory scopes, ScopeEvidence evidence)
        {
            _scopes = scopes;
            _evidence = evidence;
        }

        /// <summary>
        /// 登记就绪探针。
        /// </summary>
        public Task ExpectProbe(string id) => _probes.GetOrAdd(id,
            _ => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)).Task;

        /// <summary>
        /// 移除超时探针。
        /// </summary>
        public void ForgetProbe(string id) => _probes.TryRemove(id, out _);

        /// <summary>
        /// 读取已完成投递数量。
        /// </summary>
        public int Deliveries(string eventId) => _deliveries.TryGetValue(eventId, out var count) ? count : 0;

        /// <summary>
        /// 有界等待业务消息投递完成。
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
            throw new TimeoutException("EFCore CAP 重复投递未到达有序事件订阅者。");
        }

        /// <summary>
        /// 先持久化真实消息；探针只确认主题就绪。
        /// </summary>
        [CapSubscribe(Topic, Group = "bing-admin-efcore-ordered-integration")]
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
            _evidence.RecordReceive(metadata.EventId, scope.ServiceProvider.GetRequiredService<ScopeWitness>().Id);
            await scope.ServiceProvider.GetRequiredService<OrderedEventCoordinator>()
                .ReceiveAsync(Consumer, headers, JsonSerializer.Serialize(message));
            _deliveries.AddOrUpdate(metadata.EventId, 1, (_, count) => count + 1);
        }
    }

    /// <summary>
    /// 在后台作用域内写入业务处理结果。
    /// </summary>
    public sealed class TestHandler : IOrderedEventHandler
    {
        /// <summary>
        /// 当前处理作用域。
        /// </summary>
        private readonly ScopeWitness _witness;

        /// <summary>
        /// 作用域观测记录。
        /// </summary>
        private readonly ScopeEvidence _evidence;

        /// <summary>
        /// 当前作用域的 CAP 发布器。
        /// </summary>
        private readonly ICapPublisher _publisher;

        /// <summary>
        /// 初始化一个 <see cref="TestHandler" /> 类型的实例。
        /// </summary>
        public TestHandler(ScopeWitness witness, ScopeEvidence evidence, ICapPublisher publisher)
        {
            _witness = witness;
            _evidence = evidence;
            _publisher = publisher;
        }

        /// <inheritdoc />
        public async Task HandleAsync(OrderedEventStream stream, OrderedEventMessage message,
            DbTransaction transaction, CancellationToken cancellationToken)
        {
            await InsertBusinessAsync(transaction, stream.TenantId, "consume", message.Sequence,
                message.EventId, cancellationToken);
            await _publisher.PublishAsync(DerivedTopic, new { message.EventId, message.Sequence,
                Scope = stream.TenantId }, cancellationToken: cancellationToken);
            _evidence.RecordHandle(message.EventId, _witness.Id);
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

    /// <summary>
    /// 读取并校验专用集成环境配置。
    /// </summary>
    private sealed class IntegrationSettings
    {
        /// <summary>
        /// 初始化一个 <see cref="IntegrationSettings" /> 类型的实例。
        /// </summary>
        private IntegrationSettings(string connectionString, string rabbitHost, int rabbitPort,
            string rabbitUser, string rabbitPassword)
        {
            ConnectionString = connectionString;
            RabbitHost = rabbitHost;
            RabbitPort = rabbitPort;
            RabbitUser = rabbitUser;
            RabbitPassword = rabbitPassword;
        }

        /// <summary>
        /// 专用 MySQL 连接字符串。
        /// </summary>
        public string ConnectionString { get; }

        /// <summary>
        /// RabbitMQ 主机。
        /// </summary>
        public string RabbitHost { get; }

        /// <summary>
        /// RabbitMQ 端口。
        /// </summary>
        public int RabbitPort { get; }

        /// <summary>
        /// RabbitMQ 用户名。
        /// </summary>
        public string RabbitUser { get; }

        /// <summary>
        /// RabbitMQ 密码。
        /// </summary>
        public string RabbitPassword { get; }

        /// <summary>
        /// 读取专用环境变量并校验数据库安全边界。
        /// </summary>
        public static IntegrationSettings Load()
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MySqlConnection");
            IntegrationDatabaseSafetyValidator.EnsureDatabaseOperationAllowed(connectionString, "MySql");
            var port = 5672;
            var portText = Environment.GetEnvironmentVariable("CAP_RABBITMQ_PORT");
            if (!string.IsNullOrWhiteSpace(portText) &&
                (!int.TryParse(portText, out port) || port is < 1 or > 65535))
                throw new InvalidOperationException("CAP_RABBITMQ_PORT 必须是 1-65535 的整数。");
            return new IntegrationSettings(connectionString, Required("CAP_RABBITMQ_HOST"), port,
                Required("CAP_RABBITMQ_USERNAME"), Required("CAP_RABBITMQ_PASSWORD"));
        }

        /// <summary>
        /// 读取必需的环境变量。
        /// </summary>
        private static string Required(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"集成测试缺少环境变量 {name}。");
            return value;
        }
    }
}

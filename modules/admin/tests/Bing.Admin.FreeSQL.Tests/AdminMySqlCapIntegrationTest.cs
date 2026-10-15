using System.Collections.Concurrent;
using System.Text.Json;
using AspectCore.Extensions.Hosting;
using Bing.Admin.Data;
using Bing.Admin.Data.UnitOfWorks.MySql;
using Bing.Application.Aspects;
using Bing.Data.Transaction;
using Bing.DependencyInjection;
using Bing.Domain.Entities.Events;
using Bing.Events.Cap;
using Bing.Events.Consumption;
using Bing.Events.Messages;
using Bing.FreeSQL;
using Bing.Test.Shared;
using Bing.Uow;
using DotNetCore.CAP;
using FreeSql;
using IUnitOfWorkManager = Bing.Uow.IUnitOfWorkManager;
using FreeSql.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MySqlConnector;
using Xunit;

namespace Bing.Admin.FreeSQL.Tests;

/// <summary>
/// 验证 FreeSQL 工作单元与 MySQL、CAP、RabbitMQ 的事务联动。
/// </summary>
public sealed partial class AdminMySqlCapIntegrationTest
{
    /// <summary>
    /// 验证领域事件产生的 CAP 消息与业务记录同事务提交。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task DomainEvent_ShouldPublishToCapAfterBusinessCommit()
    {
        var settings = IntegrationSettings.Load();
        using var orm = CreateOrm(settings.ConnectionString);
        EnsureConsumptionTables(orm);
        using var host = CreateHost(settings, orm, withDomainEvents: true);
        var subscriber = (IntegrationSubscriber)host.Services.GetRequiredService<ICapSubscribe>();
        await host.StartAsync();
        try
        {
            await EnsureSubscriberReadyAsync(host, subscriber);
            var id = Guid.NewGuid().ToString("N");
            var payload = Guid.NewGuid().ToString("N");
            subscriber.Expect(payload);
            using (var scope = host.Services.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<AdminUnitOfWork>();
                var record = new IntegrationRecord { Id = id, Value = payload };
                record.RaiseCreated(payload);
                unitOfWork.RegisterDomainEventSource(record);
                unitOfWork.Add(record);
                await unitOfWork.SaveChangesAsync();
                Assert.Empty(record.GetDomainEvents());
            }

            Assert.True(await subscriber.WaitForAsync(payload, TimeSpan.FromSeconds(30)));
            Assert.True(await HasPublishedMessageAsync(settings.ConnectionString, payload));
            Assert.Equal(payload, orm.Select<IntegrationRecord>().Where(record => record.Id == id).First().Value);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 验证业务保存失败时领域事件的 CAP 消息共同回滚。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task DomainEvent_WhenBusinessFails_ShouldRollbackCapMessage()
    {
        var settings = IntegrationSettings.Load();
        using var orm = CreateOrm(settings.ConnectionString);
        EnsureConsumptionTables(orm);
        using var host = CreateHost(settings, orm, withDomainEvents: true);
        await host.StartAsync();
        try
        {
            var id = Guid.NewGuid().ToString("N");
            orm.Insert(new IntegrationRecord { Id = id, Value = "existing" }).ExecuteAffrows();

            var payload = Guid.NewGuid().ToString("N");
            using (var scope = host.Services.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<AdminUnitOfWork>();
                var record = new IntegrationRecord { Id = id, Value = payload };
                record.RaiseCreated(payload);
                unitOfWork.RegisterDomainEventSource(record);
                unitOfWork.Add(record);
                await Assert.ThrowsAnyAsync<Exception>(() => unitOfWork.SaveChangesAsync());
                Assert.Single(record.GetDomainEvents());
            }

            Assert.False(await HasPublishedMessageAsync(settings.ConnectionString, payload));
            Assert.Equal("existing", orm.Select<IntegrationRecord>().Where(record => record.Id == id).First().Value);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 验证同步保存将领域事件消息写入 CAP 出箱。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task DomainEvent_SyncSave_ShouldPublishInCapTransaction()
    {
        var settings = IntegrationSettings.Load();
        using var orm = CreateOrm(settings.ConnectionString);
        EnsureConsumptionTables(orm);
        using var host = CreateHost(settings, orm, withDomainEvents: true);
        await host.StartAsync();
        try
        {
            var id = Guid.NewGuid().ToString("N");
            var payload = Guid.NewGuid().ToString("N");
            using (var scope = host.Services.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<AdminUnitOfWork>();
                var record = new IntegrationRecord { Id = id, Value = payload };
                record.RaiseCreated(payload);
                unitOfWork.RegisterDomainEventSource(record);
                unitOfWork.Add(record);
                unitOfWork.SaveChanges();
                Assert.Empty(record.GetDomainEvents());
            }

            Assert.True(await HasPublishedMessageAsync(settings.ConnectionString, payload));
            Assert.Equal(payload, orm.Select<IntegrationRecord>().Where(record => record.Id == id).First().Value);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 验证重复 CAP 投递只提交一次业务记录与消费收据。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task DuplicateDelivery_ShouldCommitBusinessAndReceiptOnce()
    {
        var settings = IntegrationSettings.Load();
        using var orm = CreateOrm(settings.ConnectionString);
        EnsureConsumptionTables(orm);
        using var host = CreateHost(settings, orm);
        var subscriber = (IntegrationSubscriber)host.Services.GetRequiredService<ICapSubscribe>();
        await host.StartAsync();
        try
        {
            await EnsureSubscriberReadyAsync(host, subscriber, idempotent: true);
            var eventId = Guid.NewGuid().ToString("N");
            var payload = Guid.NewGuid().ToString("N");
            using (var scope = host.Services.CreateScope())
            {
                var bus = scope.ServiceProvider.GetRequiredService<IMessageEventBus>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<AdminUnitOfWork>();
                for (var index = 0; index < 2; index++)
                    await bus.PublishAsync(new MessageEvent<IdempotentIntegrationMessage>(
                        new IdempotentIntegrationMessage(eventId, payload))
                    {
                        Name = IntegrationSubscriber.IdempotentTopic
                    });
                await unitOfWork.SaveChangesAsync();
            }

            Assert.True(await subscriber.WaitForDeliveriesAsync(eventId, 2, TimeSpan.FromSeconds(30)));
            Assert.Equal(1, orm.Select<IntegrationRecord>().Where(record => record.Id == eventId).Count());
            Assert.Equal(1, await CountConsumptionReceiptsAsync(settings.ConnectionString, eventId));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 验证业务失败回滚消费收据后，同一事件可重新处理。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task ConsumerFailure_ShouldRollbackReceiptAndAllowRetry()
    {
        var settings = IntegrationSettings.Load();
        using var orm = CreateOrm(settings.ConnectionString);
        EnsureConsumptionTables(orm);
        using var host = CreateHost(settings, orm);
        await host.StartAsync();
        try
        {
            var eventId = Guid.NewGuid().ToString("N");
            var existingId = Guid.NewGuid().ToString("N");
            var retryId = Guid.NewGuid().ToString("N");
            orm.Insert(new IntegrationRecord { Id = existingId, Value = "existing" }).ExecuteAffrows();
            var store = new EventConsumptionStore(EventConsumptionDatabase.MySql);

            await using (var connection = new MySqlConnection(settings.ConnectionString))
            {
                await connection.OpenAsync();
                await using var transaction = await connection.BeginTransactionAsync();
                Assert.True(await store.TryRecordAsync(transaction,
                    IntegrationSubscriber.IdempotentConsumer, eventId));
                await Assert.ThrowsAnyAsync<Exception>(() => orm.Insert(new IntegrationRecord
                    { Id = existingId, Value = "duplicate" }).WithTransaction(transaction).ExecuteAffrowsAsync());
                await transaction.RollbackAsync();
            }

            Assert.Equal(0, await CountConsumptionReceiptsAsync(settings.ConnectionString, eventId));
            await using (var connection = new MySqlConnection(settings.ConnectionString))
            {
                await connection.OpenAsync();
                await using var transaction = await connection.BeginTransactionAsync();
                Assert.True(await store.TryRecordAsync(transaction,
                    IntegrationSubscriber.IdempotentConsumer, eventId));
                Assert.Equal(1, await orm.Insert(new IntegrationRecord
                    { Id = retryId, Value = "retried" }).WithTransaction(transaction).ExecuteAffrowsAsync());
                await transaction.CommitAsync();
            }

            Assert.Equal(1, await CountConsumptionReceiptsAsync(settings.ConnectionString, eventId));
            Assert.Equal("retried", orm.Select<IntegrationRecord>().Where(record => record.Id == retryId).First().Value);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 验证直接同步保存将业务记录和消息写入同一事务。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task SyncSaveChanges_ShouldPersistBusinessAndOutbox()
    {
        var settings = IntegrationSettings.Load();
        using var orm = CreateOrm(settings.ConnectionString);
        orm.Ado.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS `bing_admin_freesql_cap_record` (" +
                                "`Id` varchar(32) NOT NULL PRIMARY KEY, `Value` varchar(255) NOT NULL)");
        using var host = CreateHost(settings, orm);
        await host.StartAsync();
        try
        {
            var id = Guid.NewGuid().ToString("N");
            var payload = Guid.NewGuid().ToString("N");
            using var scope = host.Services.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<AdminUnitOfWork>();
            var publisher = scope.ServiceProvider.GetRequiredService<ICapPublisher>();
            var previousTransaction = publisher.Transaction.Value;
            unitOfWork.Add(new IntegrationRecord { Id = id, Value = payload });
            await scope.ServiceProvider.GetRequiredService<IMessageEventBus>().PublishAsync(
                new MessageEvent<IntegrationMessage>(new IntegrationMessage(payload))
                { Name = IntegrationSubscriber.Topic, Send = false });

            unitOfWork.SaveChanges();

            Assert.Same(previousTransaction, publisher.Transaction.Value);
            Assert.Equal(payload, orm.Select<IntegrationRecord>().Where(record => record.Id == id).First().Value);
            Assert.True(await HasPublishedMessageAsync(settings.ConnectionString, payload));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 验证 FreeSQL 事务写入事件元数据并由真实订阅者接收。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task SaveChangesAsync_OrderedEvent_ShouldPersistHeadersAndReachSubscriber()
    {
        var settings = IntegrationSettings.Load();
        using var orm = CreateOrm(settings.ConnectionString);
        orm.Ado.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS `bing_admin_freesql_cap_record` (" +
                                "`Id` varchar(32) NOT NULL PRIMARY KEY, `Value` varchar(255) NOT NULL)");
        using var host = CreateHost(settings, orm);
        var subscriber = (IntegrationSubscriber)host.Services.GetRequiredService<ICapSubscribe>();
        await host.StartAsync();
        try
        {
            await EnsureSubscriberReadyAsync(host, subscriber);
            var id = Guid.NewGuid().ToString("N");
            var eventId = Guid.NewGuid().ToString("N");
            var payload = Guid.NewGuid().ToString("N");
            subscriber.Expect(payload);
            using (var scope = host.Services.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<AdminUnitOfWork>();
                unitOfWork.Add(new IntegrationRecord { Id = id, Value = payload });
                await scope.ServiceProvider.GetRequiredService<IMessageEventBus>().PublishAsync(
                    new MessageEvent<IntegrationMessage>(new IntegrationMessage(payload))
                    {
                        Id = eventId, Name = IntegrationSubscriber.Topic, TenantId = "tenant-1",
                        Stream = "inventory", EntityKey = id, Sequence = 1,
                        ContractVersion = "v1", TraceId = "trace-1"
                    });
                await unitOfWork.SaveChangesAsync();
            }

            Assert.True(await subscriber.WaitForAsync(payload, TimeSpan.FromSeconds(30)));
            var headers = await ReadPublishedHeadersAsync(settings.ConnectionString, payload);
            var metadata = ClusterEventHeaders.Read(headers);
            Assert.Equal(eventId, metadata.EventId);
            Assert.Equal("tenant-1", metadata.TenantId);
            Assert.Equal("inventory", metadata.Stream);
            Assert.Equal(id, metadata.EntityKey);
            Assert.Equal(1, metadata.Sequence);
            Assert.Equal("v1", metadata.ContractVersion);
            Assert.Equal("trace-1", metadata.TraceId);
            Assert.Equal(payload, orm.Select<IntegrationRecord>().Where(record => record.Id == id).First().Value);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 验证 FreeSQL 事务回调产生的消息在同次提交中入出箱。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task SaveChangesAsync_DerivedMessage_ShouldPublishInSameTransaction()
    {
        var settings = IntegrationSettings.Load();
        using var orm = CreateOrm(settings.ConnectionString);
        orm.Ado.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS `bing_admin_freesql_cap_record` (" +
                                "`Id` varchar(32) NOT NULL PRIMARY KEY, `Value` varchar(255) NOT NULL)");
        using var host = CreateHost(settings, orm);
        var subscriber = (IntegrationSubscriber)host.Services.GetRequiredService<ICapSubscribe>();
        await host.StartAsync();
        try
        {
            await EnsureSubscriberReadyAsync(host, subscriber);
            var id = Guid.NewGuid().ToString("N");
            var first = Guid.NewGuid().ToString("N");
            var derived = Guid.NewGuid().ToString("N");
            subscriber.Expect(first);
            subscriber.Expect(derived);
            using (var scope = host.Services.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<AdminUnitOfWork>();
                var bus = scope.ServiceProvider.GetRequiredService<IMessageEventBus>();
                var actions = scope.ServiceProvider.GetRequiredService<ITransactionActionManager>();
                unitOfWork.Add(new IntegrationRecord { Id = id, Value = first });
                await bus.PublishAsync(new MessageEvent<IntegrationMessage>(new IntegrationMessage(first))
                    { Name = IntegrationSubscriber.Topic });
                actions.Register(_ => bus.PublishAsync(
                    new MessageEvent<IntegrationMessage>(new IntegrationMessage(derived))
                        { Name = IntegrationSubscriber.Topic }));
                await unitOfWork.SaveChangesAsync();
            }

            Assert.True(await subscriber.WaitForAsync(first, TimeSpan.FromSeconds(30)));
            Assert.True(await subscriber.WaitForAsync(derived, TimeSpan.FromSeconds(30)));
            Assert.True(await HasPublishedMessageAsync(settings.ConnectionString, first));
            Assert.True(await HasPublishedMessageAsync(settings.ConnectionString, derived));
            Assert.Equal(first, orm.Select<IntegrationRecord>().Where(record => record.Id == id).First().Value);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 验证直接同步保存失败时业务记录与消息共同回滚。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task SyncSaveChanges_WhenBusinessFails_ShouldRollbackOutbox()
    {
        var settings = IntegrationSettings.Load();
        using var orm = CreateOrm(settings.ConnectionString);
        orm.Ado.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS `bing_admin_freesql_cap_record` (" +
                                "`Id` varchar(32) NOT NULL PRIMARY KEY, `Value` varchar(255) NOT NULL)");
        using var host = CreateHost(settings, orm);
        await host.StartAsync();
        try
        {
            var id = Guid.NewGuid().ToString("N");
            var first = Guid.NewGuid().ToString("N");
            var failed = Guid.NewGuid().ToString("N");
            using (var scope = host.Services.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<AdminUnitOfWork>();
                unitOfWork.Add(new IntegrationRecord { Id = id, Value = first });
                unitOfWork.SaveChanges();
            }
            using (var scope = host.Services.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<AdminUnitOfWork>();
                var publisher = scope.ServiceProvider.GetRequiredService<ICapPublisher>();
                var previousTransaction = publisher.Transaction.Value;
                unitOfWork.Add(new IntegrationRecord { Id = id, Value = failed });
                await scope.ServiceProvider.GetRequiredService<IMessageEventBus>().PublishAsync(
                    new MessageEvent<IntegrationMessage>(new IntegrationMessage(failed))
                    { Name = IntegrationSubscriber.Topic, Send = false });
                Assert.ThrowsAny<Exception>(() => unitOfWork.SaveChanges());
                Assert.Same(previousTransaction, publisher.Transaction.Value);
            }

            Assert.Equal(first, orm.Select<IntegrationRecord>().Where(record => record.Id == id).First().Value);
            Assert.False(await HasPublishedMessageAsync(settings.ConnectionString, failed));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 验证代理提交业务记录与 CAP 出箱消息。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task Commit_ShouldPersistBusinessDataAndPublishMessage()
    {
        var settings = IntegrationSettings.Load();
        using var orm = CreateOrm(settings.ConnectionString);
        orm.Ado.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS `bing_admin_freesql_cap_record` (" +
                                "`Id` varchar(32) NOT NULL PRIMARY KEY, `Value` varchar(255) NOT NULL)");
        using var host = CreateHost(settings, orm);
        var subscriber = (IntegrationSubscriber)host.Services.GetRequiredService<ICapSubscribe>();
        await host.StartAsync();
        try
        {
            await EnsureSubscriberReadyAsync(host, subscriber);
            var id = Guid.NewGuid().ToString("N");
            var payload = Guid.NewGuid().ToString("N");
            subscriber.Expect(payload);
            using (var scope = host.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<IIntegrationCommand>().ExecuteAsync(id, payload);

            Assert.Equal(payload, orm.Select<IntegrationRecord>().Where(record => record.Id == id).First().Value);
            Assert.True(await HasPublishedMessageAsync(settings.ConnectionString, payload), "CAP 出箱记录未持久化。");
            Assert.True(await subscriber.WaitForAsync(payload, TimeSpan.FromSeconds(30)),
                "CAP 消息未被 RabbitMQ 消费者收到。");
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 验证业务主键冲突同时回滚业务记录与 CAP 出箱消息。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task DuplicateBusinessKey_ShouldRollbackMessage()
    {
        var settings = IntegrationSettings.Load();
        using var orm = CreateOrm(settings.ConnectionString);
        orm.Ado.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS `bing_admin_freesql_cap_record` (" +
                                "`Id` varchar(32) NOT NULL PRIMARY KEY, `Value` varchar(255) NOT NULL)");
        using var host = CreateHost(settings, orm);
        var subscriber = (IntegrationSubscriber)host.Services.GetRequiredService<ICapSubscribe>();
        await host.StartAsync();
        try
        {
            await EnsureSubscriberReadyAsync(host, subscriber);
            var id = Guid.NewGuid().ToString("N");
            var committed = Guid.NewGuid().ToString("N");
            var rolledBack = Guid.NewGuid().ToString("N");
            using (var scope = host.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<IIntegrationCommand>().ExecuteAsync(id, committed);
            Assert.True(await HasPublishedMessageAsync(settings.ConnectionString, committed));

            subscriber.Expect(rolledBack);
            using (var scope = host.Services.CreateScope())
                await Assert.ThrowsAnyAsync<Exception>(() =>
                    scope.ServiceProvider.GetRequiredService<IIntegrationCommand>().ExecuteAsync(id, rolledBack));

            Assert.Equal(committed, orm.Select<IntegrationRecord>().Where(record => record.Id == id).First().Value);
            Assert.False(await HasPublishedMessageAsync(settings.ConnectionString, rolledBack), "回滚后仍存在 CAP 出箱记录。");
            Assert.False(await subscriber.WaitForAsync(rolledBack, TimeSpan.FromSeconds(5)));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 验证出箱动作执行后取消仍会回滚业务与消息。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task CancellationAfterOutboxAction_ShouldRollbackBoth()
    {
        var settings = IntegrationSettings.Load();
        using var orm = CreateOrm(settings.ConnectionString);
        orm.Ado.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS `bing_admin_freesql_cap_record` (" +
                                "`Id` varchar(32) NOT NULL PRIMARY KEY, `Value` varchar(255) NOT NULL)");
        using var host = CreateHost(settings, orm);
        await host.StartAsync();
        try
        {
            var id = Guid.NewGuid().ToString("N");
            var payload = Guid.NewGuid().ToString("N");
            using var scope = host.Services.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<AdminUnitOfWork>();
            var eventBus = scope.ServiceProvider.GetRequiredService<IMessageEventBus>();
            var actions = scope.ServiceProvider.GetRequiredService<ITransactionActionManager>();
            using var cancellation = new CancellationTokenSource();
            unitOfWork.Add(new IntegrationRecord { Id = id, Value = payload });
            await eventBus.PublishAsync(new MessageEvent<IntegrationMessage>(new IntegrationMessage(payload))
            {
                Name = IntegrationSubscriber.Topic,
                Send = false
            });
            actions.Register(_ =>
            {
                cancellation.Cancel();
                return Task.CompletedTask;
            });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                unitOfWork.SaveChangesAsync(cancellation.Token));
            Assert.Equal(0, orm.Select<IntegrationRecord>().Where(record => record.Id == id).Count());
            Assert.False(await HasPublishedMessageAsync(settings.ConnectionString, payload));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 创建连接到专用 MySQL 集成测试库的 FreeSQL 实例。
    /// </summary>
    /// <param name="connectionString">测试数据库连接字符串。</param>
    /// <returns>FreeSQL 实例。</returns>
    private static IFreeSql CreateOrm(string connectionString) => new FreeSqlBuilder()
        .UseConnectionString(DataType.MySql, connectionString).Build();

    /// <summary>
    /// 等待当前 CAP 宿主的主题绑定可实际投递消息。
    /// </summary>
    private static async Task EnsureSubscriberReadyAsync(IHost host, IntegrationSubscriber subscriber,
        bool idempotent = false)
    {
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageEventBus>();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var probe = Guid.NewGuid().ToString("N");
            var remaining = deadline - DateTime.UtcNow;
            var wait = remaining < TimeSpan.FromSeconds(3) ? remaining : TimeSpan.FromSeconds(3);
            try
            {
                if (idempotent)
                {
                    await bus.PublishAsync(new MessageEvent<IdempotentIntegrationMessage>(
                        new IdempotentIntegrationMessage(probe, probe))
                    {
                        Name = IntegrationSubscriber.IdempotentTopic,
                        Send = true
                    });
                    if (await subscriber.WaitForDeliveriesAsync(probe, 1, wait))
                        return;
                }
                else
                {
                    subscriber.Expect(probe);
                    await bus.PublishAsync(new MessageEvent<IntegrationMessage>(new IntegrationMessage(probe))
                    {
                        Name = IntegrationSubscriber.Topic,
                        Send = true
                    });
                    if (await subscriber.WaitForAsync(probe, wait))
                        return;
                }
            }
            catch (NullReferenceException exception) when (exception.StackTrace?.Contains(
                "DotNetCore.CAP.Processor.DispatcherPerGroup.EnqueueToPublish", StringComparison.Ordinal) == true)
            {
                subscriber.Forget(probe);
                await Task.Delay(200);
            }
        }

        throw new TimeoutException("CAP 测试订阅者未完成当前宿主的主题绑定。");
    }

    /// <summary>
    /// 建立业务记录与消费收据表。
    /// </summary>
    private static void EnsureConsumptionTables(IFreeSql orm)
    {
        orm.Ado.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS `bing_admin_freesql_cap_record` (" +
                                "`Id` varchar(32) NOT NULL PRIMARY KEY, `Value` varchar(255) NOT NULL)");
        orm.Ado.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS `bing_event_consumption` (" +
                                "`Consumer` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL, " +
                                "`EventId` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL, " +
                                "`ProcessedAtUtc` datetime(6) NOT NULL, PRIMARY KEY (`Consumer`, `EventId`))");
    }

    /// <summary>
    /// 创建使用真实 MySQL 与 CAP 传输的测试宿主。
    /// </summary>
    /// <param name="settings">专用 MySQL 和 RabbitMQ 测试环境配置。</param>
    /// <param name="orm">当前测试拥有的 FreeSQL 实例。</param>
    /// <param name="withDomainEvents">是否注册领域事件测试处理器。</param>
    /// <param name="withInventory">是否注册整单库存测试业务。</param>
    /// <param name="failInventoryAudit">是否在关键审计写入后注入失败。</param>
    /// <param name="configureInventory">库存默认注册后的可选测试配置回调。</param>
    /// <returns>已配置但尚未启动的测试宿主。</returns>
    private static IHost CreateHost(IntegrationSettings settings, IFreeSql orm, bool withDomainEvents = false,
        bool withInventory = false, bool failInventoryAudit = false, Action<IServiceCollection> configureInventory = null)
    {
        var wrapper = new FreeSqlWrapper { Orm = orm };
        return new HostBuilder().UseServiceContext().ConfigureServices(services =>
        {
            services.EnableAop();
            services.EnableBingUnitOfWork();
            services.AddLogging();
            services.AddSingleton(wrapper);
            services.AddSingleton(settings);
            services.AddScoped<ILazyServiceProvider, LazyServiceProvider>();
            services.AddScoped<ITransactionActionManager, TransactionActionManager>();
            if (withDomainEvents)
                services.AddDomainEventDispatcher(new[] { typeof(IntegrationDomainEventHandler) });
            services.AddScoped<IUnitOfWorkManager, Bing.Uow.UnitOfWorkManager>();
            services.AddScoped<AdminUnitOfWork>();
            if (withInventory)
            {
                ConfigureInventory(services, failInventoryAudit);
                configureInventory?.Invoke(services);
            }
            services.AddScoped<IIntegrationCommand, IntegrationCommand>();
            services.AddSingleton<ICapSubscribe, IntegrationSubscriber>();
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
        }).Build();
    }

    /// <summary>
    /// 检查 CAP 已发布消息表中是否包含指定内容。
    /// </summary>
    /// <param name="connectionString">测试数据库连接字符串。</param>
    /// <param name="payload">待查消息内容。</param>
    /// <returns>找到消息时返回 <see langword="true"/>。</returns>
    private static async Task<bool> HasPublishedMessageAsync(string connectionString, string payload)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM `cap.published` WHERE `Name` = @topic AND `Content` LIKE @payload";
        command.Parameters.AddWithValue("@topic", IntegrationSubscriber.Topic);
        command.Parameters.AddWithValue("@payload", $"%{payload}%");
        return Convert.ToInt64(await command.ExecuteScalarAsync()) > 0;
    }

    /// <summary>
    /// 查询指定事件的消费收据数量。
    /// </summary>
    private static async Task<long> CountConsumptionReceiptsAsync(string connectionString, string eventId)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM `bing_event_consumption` " +
                              "WHERE `Consumer` = @consumer AND `EventId` = @eventId";
        command.Parameters.AddWithValue("@consumer", IntegrationSubscriber.IdempotentConsumer);
        command.Parameters.AddWithValue("@eventId", eventId);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// 读取指定业务消息的 CAP 出箱消息头。
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string>> ReadPublishedHeadersAsync(
        string connectionString, string payload)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `Content` FROM `cap.published` WHERE `Name` = @topic " +
                              "AND `Content` LIKE @payload ORDER BY `Id` DESC LIMIT 1";
        command.Parameters.AddWithValue("@topic", IntegrationSubscriber.Topic);
        command.Parameters.AddWithValue("@payload", $"%{payload}%");
        var content = (string)await command.ExecuteScalarAsync();
        Assert.False(string.IsNullOrWhiteSpace(content));
        using var document = JsonDocument.Parse(content);
        return document.RootElement.GetProperty("Headers").EnumerateObject()
            .Where(item => item.Name.StartsWith("bing-", StringComparison.Ordinal))
            .ToDictionary(item => item.Name, item => item.Value.GetString());
    }

    /// <summary>
    /// 定义写入记录并发布 CAP 消息的测试命令。
    /// </summary>
    public interface IIntegrationCommand
    {
        Task ExecuteAsync(string id, string payload);
    }

    /// <summary>
    /// 执行数据库写入及领域事件发布的集成测试命令。
    /// </summary>
    private sealed class IntegrationCommand : IIntegrationCommand
    {
        /// <summary>
        /// 执行当前集成测试的工作单元。
        /// </summary>
        private readonly AdminUnitOfWork _unitOfWork;

        /// <summary>
        /// 发布测试集成事件的消息总线。
        /// </summary>
        private readonly IMessageEventBus _eventBus;

        /// <summary>
        /// 初始化一个 <see cref="IntegrationCommand" /> 类型的实例。
        /// </summary>
        /// <param name="unitOfWork">管理端工作单元。</param>
        /// <param name="eventBus">集成消息总线。</param>
        public IntegrationCommand(AdminUnitOfWork unitOfWork, IMessageEventBus eventBus)
        {
            _unitOfWork = unitOfWork;
            _eventBus = eventBus;
        }

        /// <inheritdoc />
        [UnitOfWork]
        public async Task ExecuteAsync(string id, string payload)
        {
            _unitOfWork.Add(new IntegrationRecord { Id = id, Value = payload });
            await _eventBus.PublishAsync(new MessageEvent<IntegrationMessage>(new IntegrationMessage(payload))
            {
                Name = IntegrationSubscriber.Topic,
                Send = false
            });
        }
    }

    [Table(Name = "bing_admin_freesql_cap_record")]
    /// <summary>
    /// 保存集成测试数据并承载待发布领域事件的实体。
    /// </summary>
    private sealed class IntegrationRecord : IHasDomainEvents
    {
        /// <summary>
        /// 尚未确认的领域事件。
        /// </summary>
        private readonly List<DomainEvent> _domainEvents = new();

        [Column(IsPrimary = true, StringLength = 32)]
        /// <summary>
        /// 记录标识。
        /// </summary>
        public string Id { get; set; }

        [Column(StringLength = 255)]
        /// <summary>
        /// 记录内容。
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// 登记业务记录创建事件。
        /// </summary>
        public void RaiseCreated(string payload) => _domainEvents.Add(new IntegrationCreatedDomainEvent(payload));

        /// <inheritdoc />
        public IReadOnlyCollection<DomainEvent> GetDomainEvents() => _domainEvents;

        /// <inheritdoc />
        public void RemoveDomainEvent(DomainEvent @event) => _domainEvents.Remove(@event);

        /// <inheritdoc />
        public void ClearDomainEvents() => _domainEvents.Clear();
    }

    /// <summary>
    /// 业务记录创建事件。
    /// </summary>
    public sealed class IntegrationCreatedDomainEvent : DomainEvent
    {
        /// <summary>
        /// 初始化一个 <see cref="IntegrationCreatedDomainEvent" /> 类型的实例。
        /// </summary>
        public IntegrationCreatedDomainEvent(string payload) => Payload = payload;

        /// <summary>
        /// 消息负载。
        /// </summary>
        public string Payload { get; }
    }

    /// <summary>
    /// 将领域事件转为事务内 CAP 消息。
    /// </summary>
    public sealed class IntegrationDomainEventHandler : IDomainEventHandler<IntegrationCreatedDomainEvent>
    {
        /// <summary>
        /// 事件总线。
        /// </summary>
        private readonly IMessageEventBus _eventBus;

        /// <summary>
        /// 初始化一个 <see cref="IntegrationDomainEventHandler" /> 类型的实例。
        /// </summary>
        public IntegrationDomainEventHandler(IMessageEventBus eventBus) => _eventBus = eventBus;

        /// <inheritdoc />
        public Task HandleAsync(IntegrationCreatedDomainEvent @event) =>
            _eventBus.PublishAsync(new MessageEvent<IntegrationMessage>(new IntegrationMessage(@event.Payload))
            {
                Name = IntegrationSubscriber.Topic
            });
    }

    /// <summary>
    /// 普通 CAP 集成测试消息。
    /// </summary>
    public sealed class IntegrationMessage
    {
        /// <summary>
        /// 初始化一个 <see cref="IntegrationMessage" /> 类型的实例。
        /// </summary>
        /// <param name="payload">消息负载。</param>
        public IntegrationMessage(string payload) => Payload = payload;

        /// <summary>
        /// 消息负载。
        /// </summary>
        public string Payload { get; }
    }

    /// <summary>
    /// 用于验证重复投递的 CAP 消息。
    /// </summary>
    public sealed class IdempotentIntegrationMessage
    {
        /// <summary>
        /// 初始化一个 <see cref="IdempotentIntegrationMessage" /> 类型的实例。
        /// </summary>
        public IdempotentIntegrationMessage(string eventId, string payload)
        {
            EventId = eventId;
            Payload = payload;
        }

        /// <summary>
        /// 事件标识。
        /// </summary>
        public string EventId { get; }

        /// <summary>
        /// 业务内容。
        /// </summary>
        public string Payload { get; }
    }

    /// <summary>
    /// 接收并跟踪 CAP 集成测试消息。
    /// </summary>
    public sealed class IntegrationSubscriber : ICapSubscribe
    {
        /// <summary>
        /// 普通消息的测试主题。
        /// </summary>
        public const string Topic = "bing.tests.admin.freesql.mysqlcap";
        /// <summary>
        /// 幂等消费测试主题。
        /// </summary>
        public const string IdempotentTopic = "bing.tests.admin.freesql.mysqlcap.idempotent";

        /// <summary>
        /// 稳定的逻辑消费者名称。
        /// </summary>
        public const string IdempotentConsumer = "bing-admin-freesql-integration-idempotent";

        /// <summary>
        /// 消费所用的服务作用域工厂。
        /// </summary>
        private readonly IServiceScopeFactory _scopeFactory;

        /// <summary>
        /// 集成测试连接配置。
        /// </summary>
        private readonly IntegrationSettings _settings;

        /// <summary>
        /// 已完成的事件投递次数。
        /// </summary>
        private readonly ConcurrentDictionary<string, int> _deliveryCounts = new();

        /// <summary>
        /// 初始化一个 <see cref="IntegrationSubscriber" /> 类型的实例。
        /// </summary>
        /// <param name="scopeFactory">消息处理作用域工厂。</param>
        /// <param name="settings">集成测试连接配置。</param>
        public IntegrationSubscriber(IServiceScopeFactory scopeFactory, IntegrationSettings settings)
        {
            _scopeFactory = scopeFactory;
            _settings = settings;
        }

        /// <summary>
        /// 等待测试事件投递完成的通知集合。
        /// </summary>
        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _waiters = new();

        /// <summary>
        /// 移除未完成的就绪探针。
        /// </summary>
        /// <param name="payload">消息内容。</param>
        public void Forget(string payload) => _waiters.TryRemove(payload, out _);

        /// <summary>
        /// 等待指定事件收到预期次数的投递。
        /// </summary>
        /// <param name="eventId">事件标识。</param>
        /// <param name="expected">预期投递次数。</param>
        /// <param name="timeout">最大等待时长。</param>
        /// <returns>达到预期次数时返回 <see langword="true"/>。</returns>
        public async Task<bool> WaitForDeliveriesAsync(string eventId, int expected, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (_deliveryCounts.TryGetValue(eventId, out var count) && count >= expected)
                    return true;
                await Task.Delay(100);
            }
            return false;
        }

        /// <summary>
        /// 注册指定消息的接收等待项。
        /// </summary>
        /// <param name="payload">消息内容。</param>
        public void Expect(string payload) => _waiters.GetOrAdd(payload, _ =>
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));

        /// <summary>
        /// 等待指定消息到达或超时。
        /// </summary>
        /// <param name="payload">消息内容。</param>
        /// <param name="timeout">最大等待时长。</param>
        /// <returns>消息按时到达时返回 <see langword="true"/>。</returns>
        public async Task<bool> WaitForAsync(string payload, TimeSpan timeout)
        {
            Expect(payload);
            try
            {
                var task = _waiters[payload].Task;
                return await Task.WhenAny(task, Task.Delay(timeout)) == task && task.Result;
            }
            finally
            {
                _waiters.TryRemove(payload, out _);
            }
        }

        /// <summary>
        /// 接收普通 CAP 集成测试消息。
        /// </summary>
        /// <param name="message">收到的集成消息。</param>
        [CapSubscribe(Topic, Group = "bing-admin-freesql-integration")]
        public Task ReceiveAsync(IntegrationMessage message)
        {
            if (message?.Payload != null && _waiters.TryGetValue(message.Payload, out var waiter))
                waiter.TrySetResult(true);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 在同一事务中记录首次消费并写入业务数据。
        /// </summary>
        /// <param name="message">幂等消费消息。</param>
        [CapSubscribe(IdempotentTopic, Group = "bing-admin-freesql-integration-idempotent")]
        public async Task ReceiveOnceAsync(IdempotentIntegrationMessage message)
        {
            using var scope = _scopeFactory.CreateScope();
            var orm = scope.ServiceProvider.GetRequiredService<FreeSqlWrapper>().Orm;
            await using var connection = new MySqlConnection(_settings.ConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            var store = new EventConsumptionStore(EventConsumptionDatabase.MySql);
            if (await store.TryRecordAsync(transaction, IdempotentConsumer, message.EventId))
                await orm.Insert(new IntegrationRecord { Id = message.EventId, Value = message.Payload })
                    .WithTransaction(transaction).ExecuteAffrowsAsync();
            await transaction.CommitAsync();
            _deliveryCounts.AddOrUpdate(message.EventId, 1, (_, count) => count + 1);
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    /// <summary>
    /// 根据 MySQL 与 CAP 环境配置启用集成测试的特性。
    /// </summary>
    private sealed class MySqlCapIntegrationFactAttribute : FactAttribute
    {
        /// <summary>
        /// 初始化一个 <see cref="MySqlCapIntegrationFactAttribute" /> 类型的实例。
        /// </summary>
        public MySqlCapIntegrationFactAttribute()
        {
            Skip = IntegrationTestGate.IsProviderEnabled("MySql") &&
                   string.Equals(Environment.GetEnvironmentVariable("RUN_MYSQL_CAP_INTEGRATION_TESTS"),
                       "true", StringComparison.OrdinalIgnoreCase)
                ? null
                : "需要启用专用 MySQL/CAP 集成测试环境。";
        }
    }

    /// <summary>
    /// 保存 MySQL 和 RabbitMQ 集成测试配置。
    /// </summary>
    public sealed class IntegrationSettings
    {
        /// <summary>
        /// 测试数据库连接字符串。
        /// </summary>
        public string ConnectionString { get; private init; }

        /// <summary>
        /// RabbitMQ 主机名。
        /// </summary>
        public string RabbitHost { get; private init; }

        /// <summary>
        /// RabbitMQ 端口。
        /// </summary>
        public int RabbitPort { get; private init; }

        /// <summary>
        /// RabbitMQ 用户名。
        /// </summary>
        public string RabbitUser { get; private init; }

        /// <summary>
        /// RabbitMQ 密码。
        /// </summary>
        public string RabbitPassword { get; private init; }

        /// <summary>
        /// 为本机故障代理复制已验证的集成环境配置。
        /// </summary>
        /// <param name="connectionString">仍指向专用测试库的代理连接字符串。</param>
        /// <returns>保留消息队列配置的独立测试环境。</returns>
        internal IntegrationSettings WithConnectionString(string connectionString)
        {
            IntegrationDatabaseSafetyValidator.EnsureDatabaseOperationAllowed(connectionString, "MySql");
            return new IntegrationSettings
            {
                ConnectionString = connectionString, RabbitHost = RabbitHost, RabbitPort = RabbitPort,
                RabbitUser = RabbitUser, RabbitPassword = RabbitPassword
            };
        }

        /// <summary>
        /// 从环境变量读取并校验集成测试配置。
        /// </summary>
        /// <returns>集成测试环境配置。</returns>
        public static IntegrationSettings Load()
        {
            var connection = Required("ConnectionStrings__MySqlConnection");
            IntegrationDatabaseSafetyValidator.EnsureDatabaseOperationAllowed(connection, "MySql");
            var portValue = Environment.GetEnvironmentVariable("CAP_RABBITMQ_PORT");
            if (!string.IsNullOrWhiteSpace(portValue) &&
                (!int.TryParse(portValue, out var port) || port is < 1 or > 65535))
                throw new InvalidOperationException("CAP_RABBITMQ_PORT 必须是 1-65535 的整数。");
            return new IntegrationSettings
            {
                ConnectionString = connection,
                RabbitHost = Required("CAP_RABBITMQ_HOST"),
                RabbitPort = string.IsNullOrWhiteSpace(portValue) ? 5672 : int.Parse(portValue),
                RabbitUser = Required("CAP_RABBITMQ_USERNAME"),
                RabbitPassword = Required("CAP_RABBITMQ_PASSWORD")
            };
        }

        /// <summary>
        /// 获取必须配置的环境变量。
        /// </summary>
        /// <param name="name">环境变量名称。</param>
        /// <returns>环境变量值。</returns>
        private static string Required(string name) =>
            Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
                ? value
                : throw new InvalidOperationException($"缺少环境变量 {name}。");
    }
}

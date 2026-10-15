using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using AspectCore.Extensions.Hosting;
using Bing.Admin.Data;
using Bing.Admin.Data.UnitOfWorks.MySql;
using Bing.Application.Aspects;
using Bing.Auditing;
using Bing.Data.Transaction;
using Bing.DependencyInjection;
using Bing.Domain.Entities.Events;
using Bing.Events.Cap;
using Bing.Events.Consumption;
using Bing.Events.Messages;
using Bing.Test.Shared;
using Bing.Timing;
using Bing.Uow;
using Bing.Users;
using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MySqlConnector;
using Xunit;

namespace Bing.Admin.EFCore.Tests;

/// <summary>
/// 验证 Admin EFCore 工作单元与 MySQL、CAP、RabbitMQ 的真实事务联动。
/// </summary>
public sealed partial class AdminMySqlCapIntegrationTest
{
    /// <summary>
    /// 验证领域事件处理器产生的 CAP 消息与业务数据同事务提交。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task DomainEvent_ShouldPublishToCapAfterBusinessCommit()
    {
        var environment = MySqlCapIntegrationEnvironment.Load();
        using var host = CreateHost(environment, withDomainEvents: true);
        var subscriber = (IntegrationSubscriber)host.Services.GetRequiredService<ICapSubscribe>();
        await EnsureDatabaseAsync(host);
        await host.StartAsync();
        try
        {
            await WaitForSubscriberReadyAsync(host, subscriber);
            var id = Guid.NewGuid().ToString("N");
            var payload = Guid.NewGuid().ToString("N");
            subscriber.Expect(payload);
            using (var scope = host.Services.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
                var record = new IntegrationRecord { Id = id, Value = payload };
                record.RaiseCreated(payload);
                unitOfWork.Records.Add(record);
                await unitOfWork.SaveChangesAsync();
                Assert.Empty(record.GetDomainEvents());
            }

            Assert.True(await subscriber.WaitForAsync(payload, TimeSpan.FromSeconds(30)));
            Assert.True(await HasPublishedMessageAsync(environment.ConnectionString, payload));
            Assert.Equal(payload, (await ReadRecordAsync(host, id)).Value);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 验证业务提交失败时领域事件产生的 CAP 消息共同回滚。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task DomainEvent_WhenBusinessFails_ShouldRollbackCapMessage()
    {
        var environment = MySqlCapIntegrationEnvironment.Load();
        using var host = CreateHost(environment, withDomainEvents: true);
        await EnsureDatabaseAsync(host);
        await host.StartAsync();
        try
        {
            var id = Guid.NewGuid().ToString("N");
            using (var scope = host.Services.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
                unitOfWork.Records.Add(new IntegrationRecord { Id = id, Value = "existing" });
                await unitOfWork.SaveChangesAsync();
            }

            var payload = Guid.NewGuid().ToString("N");
            using (var scope = host.Services.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
                var record = new IntegrationRecord { Id = id, Value = payload };
                record.RaiseCreated(payload);
                unitOfWork.Records.Add(record);
                await Assert.ThrowsAsync<DbUpdateException>(() => unitOfWork.SaveChangesAsync());
                Assert.Single(record.GetDomainEvents());
            }

            Assert.False(await HasPublishedMessageAsync(environment.ConnectionString, payload));
            Assert.Equal("existing", (await ReadRecordAsync(host, id)).Value);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 验证同步保存也将领域事件产生的消息写入 CAP 出箱。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task DomainEvent_SyncSave_ShouldPublishInCapTransaction()
    {
        var environment = MySqlCapIntegrationEnvironment.Load();
        using var host = CreateHost(environment, withDomainEvents: true);
        await EnsureDatabaseAsync(host);
        await host.StartAsync();
        try
        {
            var id = Guid.NewGuid().ToString("N");
            var payload = Guid.NewGuid().ToString("N");
            using (var scope = host.Services.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
                var record = new IntegrationRecord { Id = id, Value = payload };
                record.RaiseCreated(payload);
                unitOfWork.Records.Add(record);
                unitOfWork.SaveChanges();
                Assert.Empty(record.GetDomainEvents());
            }

            Assert.True(await HasPublishedMessageAsync(environment.ConnectionString, payload));
            Assert.Equal(payload, (await ReadRecordAsync(host, id)).Value);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 验证重复 CAP 投递只在同一数据库事务内执行一次业务处理。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task DuplicateDelivery_ShouldCommitBusinessAndReceiptOnce()
    {
        var environment = MySqlCapIntegrationEnvironment.Load();
        using var host = CreateHost(environment);
        var subscriber = (IntegrationSubscriber)host.Services.GetRequiredService<ICapSubscribe>();
        await EnsureDatabaseAsync(host);
        await host.StartAsync();
        try
        {
            await WaitForSubscriberReadyAsync(host, subscriber, idempotent: true);
            var eventId = Guid.NewGuid().ToString("N");
            var payload = Guid.NewGuid().ToString("N");
            using (var scope = host.Services.CreateScope())
            {
                var bus = scope.ServiceProvider.GetRequiredService<IMessageEventBus>();
                var publishingUnitOfWork = scope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
                for (var index = 0; index < 2; index++)
                    await bus.PublishAsync(new MessageEvent<IdempotentIntegrationMessage>(
                        new IdempotentIntegrationMessage(eventId, payload))
                    {
                        Name = IntegrationSubscriber.IdempotentTopic
                    });
                await publishingUnitOfWork.SaveChangesAsync();
            }

            Assert.True(await subscriber.WaitForDeliveriesAsync(eventId, 2, TimeSpan.FromSeconds(30)));
            using var verificationScope = host.Services.CreateScope();
            var unitOfWork = verificationScope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
            Assert.Equal(1, await unitOfWork.Records.AsNoTracking().CountAsync(record => record.Id == eventId));
            Assert.Equal(1, await CountConsumptionReceiptsAsync(environment.ConnectionString, eventId));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 验证业务失败回滚消费收据，允许相同事件在下一事务中重试。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task ConsumerFailure_ShouldRollbackReceiptAndAllowRetry()
    {
        var environment = MySqlCapIntegrationEnvironment.Load();
        using var host = CreateHost(environment);
        await EnsureDatabaseAsync(host);
        await host.StartAsync();
        try
        {
            var eventId = Guid.NewGuid().ToString("N");
            var existingId = Guid.NewGuid().ToString("N");
            var retryId = Guid.NewGuid().ToString("N");
            var store = new EventConsumptionStore(EventConsumptionDatabase.MySql);
            using (var scope = host.Services.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
                unitOfWork.Records.Add(new IntegrationRecord { Id = existingId, Value = "existing" });
                await unitOfWork.SaveChangesAsync();
            }

            using (var scope = host.Services.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
                await using var transaction = await unitOfWork.Database.BeginTransactionAsync();
                Assert.True(await store.TryRecordAsync(transaction.GetDbTransaction(),
                    IntegrationSubscriber.IdempotentConsumer, eventId));
                unitOfWork.Records.Add(new IntegrationRecord { Id = existingId, Value = "duplicate" });
                await Assert.ThrowsAsync<DbUpdateException>(() => unitOfWork.SaveChangesAsync());
                await transaction.RollbackAsync();
            }

            using (var scope = host.Services.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
                await using var transaction = await unitOfWork.Database.BeginTransactionAsync();
                Assert.True(await store.TryRecordAsync(transaction.GetDbTransaction(),
                    IntegrationSubscriber.IdempotentConsumer, eventId));
                unitOfWork.Records.Add(new IntegrationRecord { Id = retryId, Value = "retried" });
                await unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();
            }

            Assert.Equal(1, await CountConsumptionReceiptsAsync(environment.ConnectionString, eventId));
            Assert.Equal("retried", (await ReadRecordAsync(host, retryId)).Value);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 验证并发事务只能为同一消费者和事件提交一条收据。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task ConcurrentConsumers_ShouldAllowOnlyOneReceipt()
    {
        var environment = MySqlCapIntegrationEnvironment.Load();
        using var host = CreateHost(environment);
        await EnsureDatabaseAsync(host);
        var eventId = Guid.NewGuid().ToString("N");
        var store = new EventConsumptionStore(EventConsumptionDatabase.MySql);
        await using var firstConnection = new MySqlConnection(environment.ConnectionString);
        await using var secondConnection = new MySqlConnection(environment.ConnectionString);
        await firstConnection.OpenAsync();
        await secondConnection.OpenAsync();
        await using var firstTransaction = await firstConnection.BeginTransactionAsync();
        await using var secondTransaction = await secondConnection.BeginTransactionAsync();

        Assert.True(await store.TryRecordAsync(firstTransaction,
            IntegrationSubscriber.IdempotentConsumer, eventId));
        var competingInsert = store.TryRecordAsync(secondTransaction,
            IntegrationSubscriber.IdempotentConsumer, eventId);
        Assert.False(competingInsert.IsCompleted);
        await firstTransaction.CommitAsync();
        Assert.False(await competingInsert);
        await secondTransaction.CommitAsync();
        Assert.Equal(1, await CountConsumptionReceiptsAsync(environment.ConnectionString, eventId));
    }

    /// <summary>
    /// 验证直接同步保存将业务记录和消息写入同一事务。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task SyncSaveChanges_ShouldPersistBusinessAndOutbox()
    {
        var environment = MySqlCapIntegrationEnvironment.Load();
        using var host = CreateHost(environment);
        await EnsureDatabaseAsync(host);
        await host.StartAsync();
        try
        {
            var id = Guid.NewGuid().ToString("N");
            var payload = Guid.NewGuid().ToString("N");
            using var scope = host.Services.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
            var publisher = scope.ServiceProvider.GetRequiredService<ICapPublisher>();
            var previousTransaction = publisher.Transaction.Value;
            unitOfWork.Records.Add(new IntegrationRecord { Id = id, Value = payload });
            await scope.ServiceProvider.GetRequiredService<IMessageEventBus>().PublishAsync(
                new MessageEvent<IntegrationMessage>(new IntegrationMessage(payload))
                { Name = IntegrationSubscriber.Topic, Send = false });

            unitOfWork.SaveChanges();

            Assert.Same(previousTransaction, publisher.Transaction.Value);
            Assert.Equal(payload, (await ReadRecordAsync(host, id)).Value);
            Assert.True(await HasPublishedMessageAsync(environment.ConnectionString, payload));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 验证业务事件元数据随 CAP 出箱消息到达真实订阅链路。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task SaveChangesAsync_OrderedEvent_ShouldPersistHeadersAndReachSubscriber()
    {
        var environment = MySqlCapIntegrationEnvironment.Load();
        using var host = CreateHost(environment);
        var subscriber = (IntegrationSubscriber)host.Services.GetRequiredService<ICapSubscribe>();
        await EnsureDatabaseAsync(host);
        await host.StartAsync();
        try
        {
            await WaitForSubscriberReadyAsync(host, subscriber);
            var id = Guid.NewGuid().ToString("N");
            var eventId = Guid.NewGuid().ToString("N");
            var payload = Guid.NewGuid().ToString("N");
            subscriber.Expect(payload);
            using (var scope = host.Services.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
                unitOfWork.Records.Add(new IntegrationRecord { Id = id, Value = payload });
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
            var headers = await ReadPublishedHeadersAsync(environment.ConnectionString, payload);
            var metadata = ClusterEventHeaders.Read(headers);
            Assert.Equal(eventId, metadata.EventId);
            Assert.Equal("tenant-1", metadata.TenantId);
            Assert.Equal("inventory", metadata.Stream);
            Assert.Equal(id, metadata.EntityKey);
            Assert.Equal(1, metadata.Sequence);
            Assert.Equal("v1", metadata.ContractVersion);
            Assert.Equal("trace-1", metadata.TraceId);
            Assert.Equal(payload, (await ReadRecordAsync(host, id)).Value);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 验证事务回调登记的派生消息在同一次保存中写入出箱。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task SaveChangesAsync_DerivedMessage_ShouldPublishInSameTransaction()
    {
        var environment = MySqlCapIntegrationEnvironment.Load();
        using var host = CreateHost(environment);
        var subscriber = (IntegrationSubscriber)host.Services.GetRequiredService<ICapSubscribe>();
        await EnsureDatabaseAsync(host);
        await host.StartAsync();
        try
        {
            await WaitForSubscriberReadyAsync(host, subscriber);
            var id = Guid.NewGuid().ToString("N");
            var first = Guid.NewGuid().ToString("N");
            var derived = Guid.NewGuid().ToString("N");
            subscriber.Expect(first);
            subscriber.Expect(derived);
            using (var scope = host.Services.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
                var bus = scope.ServiceProvider.GetRequiredService<IMessageEventBus>();
                var actions = scope.ServiceProvider.GetRequiredService<ITransactionActionManager>();
                unitOfWork.Records.Add(new IntegrationRecord { Id = id, Value = first });
                await bus.PublishAsync(new MessageEvent<IntegrationMessage>(new IntegrationMessage(first))
                    { Name = IntegrationSubscriber.Topic });
                actions.Register(_ => bus.PublishAsync(
                    new MessageEvent<IntegrationMessage>(new IntegrationMessage(derived))
                        { Name = IntegrationSubscriber.Topic }));
                await unitOfWork.SaveChangesAsync();
            }

            Assert.True(await subscriber.WaitForAsync(first, TimeSpan.FromSeconds(30)));
            Assert.True(await subscriber.WaitForAsync(derived, TimeSpan.FromSeconds(30)));
            Assert.True(await HasPublishedMessageAsync(environment.ConnectionString, first));
            Assert.True(await HasPublishedMessageAsync(environment.ConnectionString, derived));
            Assert.Equal(first, (await ReadRecordAsync(host, id)).Value);
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
        var environment = MySqlCapIntegrationEnvironment.Load();
        using var host = CreateHost(environment);
        await EnsureDatabaseAsync(host);
        await host.StartAsync();
        try
        {
            var id = Guid.NewGuid().ToString("N");
            var first = Guid.NewGuid().ToString("N");
            var failed = Guid.NewGuid().ToString("N");
            using (var scope = host.Services.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
                unitOfWork.Records.Add(new IntegrationRecord { Id = id, Value = first });
                unitOfWork.SaveChanges();
            }
            using (var scope = host.Services.CreateScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
                var publisher = scope.ServiceProvider.GetRequiredService<ICapPublisher>();
                var previousTransaction = publisher.Transaction.Value;
                unitOfWork.Records.Add(new IntegrationRecord { Id = id, Value = failed });
                await scope.ServiceProvider.GetRequiredService<IMessageEventBus>().PublishAsync(
                    new MessageEvent<IntegrationMessage>(new IntegrationMessage(failed))
                    { Name = IntegrationSubscriber.Topic, Send = false });
                Assert.ThrowsAny<DbUpdateException>(() => unitOfWork.SaveChanges());
                Assert.Same(previousTransaction, publisher.Transaction.Value);
            }

            Assert.Equal(first, (await ReadRecordAsync(host, id)).Value);
            Assert.False(await HasPublishedMessageAsync(environment.ConnectionString, failed));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 验证成功提交同时写入业务表并由 CAP 消费者收到消息。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task Commit_ShouldPersistBusinessDataAndDeliverCapMessage()
    {
        var environment = MySqlCapIntegrationEnvironment.Load();
        using var host = CreateHost(environment);
        var subscriber = (IntegrationSubscriber)host.Services.GetRequiredService<ICapSubscribe>();
        await EnsureDatabaseAsync(host);
        await host.StartAsync();
        try
        {
            await WaitForSubscriberReadyAsync(host, subscriber);
            var payload = Guid.NewGuid().ToString("N");
            var recordId = Guid.NewGuid().ToString("N");
            subscriber.Expect(payload);
            using (var scope = host.Services.CreateScope())
            {
                var command = scope.ServiceProvider.GetRequiredService<IIntegrationCommand>();
                await command.ExecuteAsync(recordId, payload);
            }

            Assert.True(await subscriber.WaitForAsync(payload, TimeSpan.FromSeconds(30)),
                "CAP 消息未在超时内由 RabbitMQ 消费者收到。");
            Assert.True(await HasPublishedMessageAsync(environment.ConnectionString, payload), "成功事务未持久化 CAP 出箱记录。");
            var record = await ReadRecordAsync(host, recordId);
            Assert.Equal(payload, record.Value);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 验证业务提交失败时 CAP 消息不会被消费者收到。
    /// </summary>
    [MySqlCapIntegrationFact]
    public async Task DuplicateBusinessKey_ShouldRollbackCapMessage()
    {
        var environment = MySqlCapIntegrationEnvironment.Load();
        using var host = CreateHost(environment);
        var subscriber = (IntegrationSubscriber)host.Services.GetRequiredService<ICapSubscribe>();
        await EnsureDatabaseAsync(host);
        await host.StartAsync();
        try
        {
            var committedPayload = Guid.NewGuid().ToString("N");
            var rolledBackPayload = Guid.NewGuid().ToString("N");
            var recordId = Guid.NewGuid().ToString("N");
            using (var scope = host.Services.CreateScope())
            {
                var command = scope.ServiceProvider.GetRequiredService<IIntegrationCommand>();
                await command.ExecuteAsync(recordId, committedPayload);
            }

            Assert.True(await HasPublishedMessageAsync(environment.ConnectionString, committedPayload));

            subscriber.Expect(rolledBackPayload);
            using (var scope = host.Services.CreateScope())
            {
                var command = scope.ServiceProvider.GetRequiredService<IIntegrationCommand>();
                await Assert.ThrowsAnyAsync<DbUpdateException>(() =>
                    command.ExecuteAsync(recordId, rolledBackPayload));
            }

            Assert.False(await subscriber.WaitForAsync(rolledBackPayload, TimeSpan.FromSeconds(5)),
                "业务事务回滚后，CAP 消息仍被 RabbitMQ 消费者收到。");
            Assert.False(await HasPublishedMessageAsync(environment.ConnectionString, rolledBackPayload),
                "业务事务回滚后仍存在 CAP 出箱记录。");
            var record = await ReadRecordAsync(host, recordId);
            Assert.Equal(committedPayload, record.Value);
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
        var environment = MySqlCapIntegrationEnvironment.Load();
        using var host = CreateHost(environment);
        await EnsureDatabaseAsync(host);
        await host.StartAsync();
        try
        {
            var id = Guid.NewGuid().ToString("N");
            var payload = Guid.NewGuid().ToString("N");
            using var scope = host.Services.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
            var eventBus = scope.ServiceProvider.GetRequiredService<IMessageEventBus>();
            var actions = scope.ServiceProvider.GetRequiredService<ITransactionActionManager>();
            using var cancellation = new CancellationTokenSource();
            unitOfWork.Records.Add(new IntegrationRecord { Id = id, Value = payload });
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
            Assert.False(await unitOfWork.Records.AsNoTracking().AnyAsync(record => record.Id == id));
            Assert.False(await HasPublishedMessageAsync(environment.ConnectionString, payload));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 等待对应 CAP 主题的订阅者完成实际消费。
    /// </summary>
    private static async Task WaitForSubscriberReadyAsync(IHost host, IntegrationSubscriber subscriber,
        bool idempotent = false)
    {
        for (var attempt = 0; attempt < 15; attempt++)
        {
            var probe = Guid.NewGuid().ToString("N");
            using (var scope = host.Services.CreateScope())
            {
                var bus = scope.ServiceProvider.GetRequiredService<IMessageEventBus>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
                if (idempotent)
                    await bus.PublishAsync(new MessageEvent<IdempotentIntegrationMessage>(
                        new IdempotentIntegrationMessage(probe, probe))
                    {
                        Name = IntegrationSubscriber.IdempotentTopic
                    });
                else
                {
                    subscriber.Expect(probe);
                    await bus.PublishAsync(new MessageEvent<IntegrationMessage>(new IntegrationMessage(probe))
                    {
                        Name = IntegrationSubscriber.Topic
                    });
                }
                await unitOfWork.SaveChangesAsync();
            }

            var received = idempotent
                ? await subscriber.WaitForDeliveriesAsync(probe, 1, TimeSpan.FromSeconds(2))
                : await subscriber.WaitForAsync(probe, TimeSpan.FromSeconds(2));
            if (received)
                return;
        }
        throw new TimeoutException("CAP 订阅未在探针验证期间就绪。");
    }

    /// <summary>
    /// 创建使用真实 MySQL 与 CAP 传输的测试宿主。
    /// </summary>
    /// <param name="environment">专用 MySQL 和 RabbitMQ 测试环境配置。</param>
    /// <param name="withDomainEvents">是否注册领域事件测试处理器。</param>
    /// <param name="withInventory">是否注册整单库存测试业务。</param>
    /// <param name="failInventoryAudit">是否在关键审计写入后注入失败。</param>
    /// <param name="configureInventory">库存默认注册后的可选测试配置回调。</param>
    /// <returns>已配置但尚未启动的测试宿主。</returns>
    private static IHost CreateHost(MySqlCapIntegrationEnvironment environment, bool withDomainEvents = false,
        bool withInventory = false, bool failInventoryAudit = false, Action<IServiceCollection> configureInventory = null)
    {
        var options = new DbContextOptionsBuilder<AdminUnitOfWork>()
            .UseMySql(environment.ConnectionString, ServerVersion.AutoDetect(environment.ConnectionString))
            .Options;
        return new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.EnableAop();
                services.EnableBingUnitOfWork();
                services.AddLogging();
                services.AddSingleton(options);
                services.AddScoped<ILazyServiceProvider, LazyServiceProvider>();
                services.AddScoped<ITransactionActionManager, TransactionActionManager>();
                services.AddTransient<IAuditPropertySetter, AuditPropertySetter>();
                services.AddSingleton(NullCurrentUser.Instance);
                services.AddSingleton<IClock, SystemClock>();
                services.AddScoped<IUnitOfWorkManager, UnitOfWorkManager>();
                services.AddScoped<TestAdminUnitOfWork>();
                services.AddScoped<AdminUnitOfWork>(provider =>
                    provider.GetRequiredService<TestAdminUnitOfWork>());
                services.AddScoped<IAdminUnitOfWork>(provider =>
                    provider.GetRequiredService<AdminUnitOfWork>());
                services.AddScoped<IUnitOfWork>(provider =>
                    provider.GetRequiredService<AdminUnitOfWork>());
                if (withInventory)
                {
                    ConfigureInventory(services, failInventoryAudit);
                    configureInventory?.Invoke(services);
                }
                if (withDomainEvents)
                    services.AddDomainEventDispatcher(new[] { typeof(IntegrationDomainEventHandler) });
                services.AddSingleton<ICapSubscribe, IntegrationSubscriber>();
                services.AddScoped<IIntegrationCommand, IntegrationCommand>();
                services.AddCapEventBus(options =>
                {
                    options.UseEntityFramework<AdminUnitOfWork>();
                    options.UseMySql(environment.ConnectionString);
                    options.UseRabbitMQ(rabbitOptions =>
                    {
                        rabbitOptions.HostName = environment.RabbitHost;
                        rabbitOptions.Port = environment.RabbitPort;
                        rabbitOptions.UserName = environment.RabbitUserName;
                        rabbitOptions.Password = environment.RabbitPassword;
                    });
                    options.Version = "1.0";
                    options.ConsumerThreadCount = 1;
                    options.UseDispatchingPerGroup = true;
                });
            })
            .Build();
    }

    /// <summary>
    /// 幂等创建专用业务表。
    /// </summary>
    private static async Task EnsureDatabaseAsync(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
        await unitOfWork.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS `bing_admin_mysqlcap_record` (" +
            "`Id` varchar(32) NOT NULL PRIMARY KEY, `Value` varchar(255) NOT NULL)");
        await unitOfWork.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS `bing_event_consumption` (" +
            "`Consumer` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL, " +
            "`EventId` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL, " +
            "`ProcessedAtUtc` datetime(6) NOT NULL, PRIMARY KEY (`Consumer`, `EventId`))");
    }

    /// <summary>
    /// 查询指定事件的业务消费收据数量。
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
    /// 查询指定消息的 CAP 出箱记录。
    /// </summary>
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
    /// 从新作用域读取业务记录。
    /// </summary>
    private static async Task<IntegrationRecord> ReadRecordAsync(IHost host, string id)
    {
        using var scope = host.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
        return await unitOfWork.Records.AsNoTracking().SingleAsync(record => record.Id == id);
    }

    /// <summary>
    /// MySQL/CAP 集成测试专用 Fact 特性。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    private sealed class MySqlCapIntegrationFactAttribute : FactAttribute
    {
        /// <summary>
        /// 初始化一个 <see cref="MySqlCapIntegrationFactAttribute" /> 类型的实例。
        /// </summary>
        public MySqlCapIntegrationFactAttribute()
        {
            Skip = IntegrationTestGate.IsProviderEnabled("MySql") &&
                   IsEnabled("RUN_MYSQL_CAP_INTEGRATION_TESTS")
                ? null
                : "MySQL/CAP 集成测试已跳过。需设置 RUN_MYSQL_INTEGRATION_TESTS=true 与 " +
                  "RUN_MYSQL_CAP_INTEGRATION_TESTS=true。";
        }

        /// <summary>
        /// 判断指定环境变量是否显式启用。
        /// </summary>
        /// <param name="name">环境变量名称。</param>
        /// <returns>环境变量值为 true 时返回 <see langword="true"/>。</returns>
        private static bool IsEnabled(string name) =>
            string.Equals(Environment.GetEnvironmentVariable(name), "true",
                StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 测试用工作单元命令。
    /// </summary>
    public interface IIntegrationCommand
    {
        /// <summary>
        /// 写入业务记录并登记 CAP 消息。
        /// </summary>
        Task ExecuteAsync(string id, string payload);
    }

    /// <summary>
    /// 测试用工作单元命令实现。
    /// </summary>
    private sealed class IntegrationCommand : IIntegrationCommand
    {
        /// <summary>
        /// 执行当前集成测试的工作单元。
        /// </summary>
        private readonly TestAdminUnitOfWork _unitOfWork;

        /// <summary>
        /// 发布测试集成事件的消息总线。
        /// </summary>
        private readonly IMessageEventBus _messageEventBus;
        /// <summary>
        /// 初始化一个 <see cref="IntegrationCommand" /> 类型的实例。
        /// </summary>

        public IntegrationCommand(TestAdminUnitOfWork unitOfWork, IMessageEventBus messageEventBus)
        {
            _unitOfWork = unitOfWork;
            _messageEventBus = messageEventBus;
        }

        /// <inheritdoc />
        [UnitOfWork]
        public async Task ExecuteAsync(string id, string payload)
        {
            _unitOfWork.Records.Add(new IntegrationRecord { Id = id, Value = payload });
            await _messageEventBus.PublishAsync(new MessageEvent<IntegrationMessage>(
                new IntegrationMessage(payload))
            {
                Name = IntegrationSubscriber.Topic,
                Send = false
            });
        }
    }

    /// <summary>
    /// 测试用 EFCore 工作单元。
    /// </summary>
    private sealed class TestAdminUnitOfWork : AdminUnitOfWork
    {
        /// <summary>
        /// 初始化一个 <see cref="TestAdminUnitOfWork" /> 类型的实例。
        /// </summary>
        /// <param name="options">工作单元数据库选项。</param>
        /// <param name="serviceProvider">测试服务提供程序。</param>
        public TestAdminUnitOfWork(DbContextOptions<AdminUnitOfWork> options, IServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }

        /// <summary>
        /// 获取集成测试记录集合。
        /// </summary>
        public DbSet<IntegrationRecord> Records => Set<IntegrationRecord>();

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IntegrationRecord>(entity =>
            {
                entity.ToTable("bing_admin_mysqlcap_record");
                entity.HasKey(record => record.Id);
                entity.Property(record => record.Id).HasMaxLength(32);
                entity.Property(record => record.Value).HasMaxLength(255);
            });
        }
    }

    /// <summary>
    /// 测试用业务记录。
    /// </summary>
    private sealed class IntegrationRecord : IHasDomainEvents
    {
        /// <summary>
        /// 尚未确认的领域事件。
        /// </summary>
        private readonly List<DomainEvent> _domainEvents = new();

        /// <summary>
        /// 获取或设置主键。
        /// </summary>
        [Key]
        public string Id { get; set; }

        /// <summary>
        /// 获取或设置消息负载。
        /// </summary>
        [Required]
        public string Value { get; set; }

        /// <summary>
        /// 登记业务记录创建事件。
        /// </summary>
        public void RaiseCreated(string payload) =>
            _domainEvents.Add(new IntegrationCreatedDomainEvent(payload));

        /// <inheritdoc />
        public IReadOnlyCollection<DomainEvent> GetDomainEvents() => _domainEvents;

        /// <inheritdoc />
        public void RemoveDomainEvent(DomainEvent @event) => _domainEvents.Remove(@event);

        /// <inheritdoc />
        public void ClearDomainEvents() => _domainEvents.Clear();
    }

    /// <summary>
    /// 业务记录创建后的领域事件。
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
    /// 测试用 CAP 消息负载。
    /// </summary>
    public sealed class IntegrationMessage
    {
        /// <summary>
        /// 初始化一个 <see cref="IntegrationMessage" /> 类型的实例。
        /// </summary>
        /// <param name="payload">负载标识。</param>
        public IntegrationMessage(string payload) => Payload = payload;

        /// <summary>
        /// 获取负载标识。
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
    /// CAP 测试订阅者。
    /// </summary>
    public sealed class IntegrationSubscriber : ICapSubscribe
    {
        /// <summary>
        /// 测试主题名称。
        /// </summary>
        public const string Topic = "bing.tests.admin.mysqlcap";

        /// <summary>
        /// 幂等消费测试主题。
        /// </summary>
        public const string IdempotentTopic = "bing.tests.admin.mysqlcap.idempotent";

        /// <summary>
        /// 稳定的逻辑消费者名称。
        /// </summary>
        public const string IdempotentConsumer = "bing-admin-integration-idempotent";

        /// <summary>
        /// 创建独立消费作用域的工厂。
        /// </summary>
        private readonly IServiceScopeFactory _scopeFactory;

        /// <summary>
        /// 每个事件已完成的投递次数。
        /// </summary>
        private readonly ConcurrentDictionary<string, int> _deliveryCounts = new();

        /// <summary>
        /// 初始化一个 <see cref="IntegrationSubscriber" /> 类型的实例。
        /// </summary>
        public IntegrationSubscriber(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

        /// <summary>
        /// 等待测试事件投递完成的通知集合。
        /// </summary>
        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _waiters = new();

        /// <summary>
        /// 等待指定事件的投递次数达到目标值。
        /// </summary>
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
        /// 注册需要等待的消息负载。
        /// </summary>
        public void Expect(string payload)
        {
            _waiters.GetOrAdd(payload, _ =>
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
        }

        /// <summary>
        /// 等待指定负载被消费。
        /// </summary>
        public async Task<bool> WaitForAsync(string payload, TimeSpan timeout)
        {
            Expect(payload);
            var waiter = _waiters[payload];
            try
            {
                var completed = await Task.WhenAny(waiter.Task, Task.Delay(timeout));
                return completed == waiter.Task && waiter.Task.Result;
            }
            finally
            {
                _waiters.TryRemove(payload, out _);
            }
        }

        /// <summary>
        /// 接收 CAP 消息。
        /// </summary>
        [CapSubscribe(Topic, Group = "bing-admin-integration")]
        public Task ReceiveAsync(IntegrationMessage message)
        {
            if (message?.Payload != null && _waiters.TryGetValue(message.Payload, out var waiter))
                waiter.TrySetResult(true);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 在同一事务中记录收据并处理首次投递。
        /// </summary>
        [CapSubscribe(IdempotentTopic, Group = "bing-admin-integration-idempotent")]
        public async Task ReceiveOnceAsync(IdempotentIntegrationMessage message)
        {
            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
            await using var transaction = await unitOfWork.Database.BeginTransactionAsync();
            var store = new EventConsumptionStore(EventConsumptionDatabase.MySql);
            if (await store.TryRecordAsync(transaction.GetDbTransaction(), IdempotentConsumer, message.EventId))
            {
                unitOfWork.Records.Add(new IntegrationRecord { Id = message.EventId, Value = message.Payload });
                await unitOfWork.SaveChangesAsync();
            }
            await transaction.CommitAsync();
            _deliveryCounts.AddOrUpdate(message.EventId, 1, (_, count) => count + 1);
        }
    }

    /// <summary>
    /// MySQL/CAP 集成环境配置。
    /// </summary>
    private sealed class MySqlCapIntegrationEnvironment
    {
        /// <summary>
        /// 初始化一个 <see cref="MySqlCapIntegrationEnvironment" /> 类型的实例。
        /// </summary>
        private MySqlCapIntegrationEnvironment(string connectionString, string rabbitHost,
            int rabbitPort, string rabbitUserName, string rabbitPassword)
        {
            ConnectionString = connectionString;
            RabbitHost = rabbitHost;
            RabbitPort = rabbitPort;
            RabbitUserName = rabbitUserName;
            RabbitPassword = rabbitPassword;
        }

        /// <summary>
        /// 获取 MySQL 连接字符串。
        /// </summary>
        public string ConnectionString { get; }

        /// <summary>
        /// 获取 RabbitMQ 主机。
        /// </summary>
        public string RabbitHost { get; }

        /// <summary>
        /// 获取 RabbitMQ 端口。
        /// </summary>
        public int RabbitPort { get; }

        /// <summary>
        /// 获取 RabbitMQ 用户名。
        /// </summary>
        public string RabbitUserName { get; }

        /// <summary>
        /// 获取 RabbitMQ 密码。
        /// </summary>
        public string RabbitPassword { get; }

        /// <summary>
        /// 为本机故障代理复制已验证的集成环境配置。
        /// </summary>
        /// <param name="connectionString">仍指向专用测试库的代理连接字符串。</param>
        /// <returns>保留消息队列配置的独立测试环境。</returns>
        internal MySqlCapIntegrationEnvironment WithConnectionString(string connectionString)
        {
            IntegrationDatabaseSafetyValidator.EnsureDatabaseOperationAllowed(connectionString, "MySql");
            return new MySqlCapIntegrationEnvironment(connectionString, RabbitHost, RabbitPort,
                RabbitUserName, RabbitPassword);
        }

        /// <summary>
        /// 读取并校验集成环境配置。
        /// </summary>
        public static MySqlCapIntegrationEnvironment Load()
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MySqlConnection");
            IntegrationDatabaseSafetyValidator.EnsureDatabaseOperationAllowed(connectionString, "MySql");
            var rabbitHost = Required("CAP_RABBITMQ_HOST");
            var rabbitPort = 5672;
            var rabbitPortValue = Environment.GetEnvironmentVariable("CAP_RABBITMQ_PORT");
            if (!string.IsNullOrWhiteSpace(rabbitPortValue) &&
                (!int.TryParse(rabbitPortValue, out rabbitPort) || rabbitPort is < 1 or > 65535))
                throw new InvalidOperationException("MySQL/CAP 集成测试的 CAP_RABBITMQ_PORT 必须是 1-65535 的整数。");
            var rabbitUserName = Required("CAP_RABBITMQ_USERNAME");
            var rabbitPassword = Required("CAP_RABBITMQ_PASSWORD");
            return new MySqlCapIntegrationEnvironment(connectionString, rabbitHost, rabbitPort,
                rabbitUserName, rabbitPassword);
        }

        /// <summary>
        /// 获取必须配置的环境变量。
        /// </summary>
        /// <param name="name">环境变量名称。</param>
        /// <returns>环境变量值。</returns>
        private static string Required(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"MySQL/CAP 集成测试缺少环境变量 {name}。");
            return value;
        }
    }
}

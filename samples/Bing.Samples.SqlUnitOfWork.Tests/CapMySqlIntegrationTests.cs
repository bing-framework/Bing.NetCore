using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Bing.Data.Transaction;
using Bing.Dapper;
using Bing.Dapper.MySql;
using Bing.Data.Sql;
using Bing.Events.Cap;
using Bing.Events.Messages;
using Bing.Samples.SqlUnitOfWork;
using Bing.Samples.SqlUnitOfWork.Application;
using Bing.Samples.SqlUnitOfWork.Controllers;
using Bing.Samples.SqlUnitOfWork.Consumer;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Diagnostics;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Database;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Events;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Scenarios;
using Bing.Uow;
using DotNetCore.CAP;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MySqlConnector;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Samples.SqlUnitOfWork.Tests;

[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class CapMySqlIntegrationCollection
{
    public const string CollectionName = "SQL unit of work CAP MySQL integration";
}

/// <summary>
/// 在专属 MySQL/RabbitMQ Compose 服务上检查旧入口失败和 CAP/Uow 共用物理事务。
/// </summary>
[Collection(CapMySqlIntegrationCollection.CollectionName)]
public sealed class CapMySqlIntegrationTests
{
    private readonly ITestOutputHelper _testOutput;

    private const string OrdersDatabase = "bing_sqluow_orders";
    private const string OrdersConnectionString =
        "Server=127.0.0.1;Port=13307;User ID=cap_app;Password=cap_local_only;Database=bing_sqluow_orders;Pooling=false";
    private const string ReportingConnectionString =
        "Server=127.0.0.1;Port=13307;User ID=cap_app;Password=cap_local_only;Database=bing_sqluow_reporting;Pooling=false";
    private const string RabbitMqConnectionString = "amqp://cap_local:cap_local_only@127.0.0.1:5673/sqluow";

    public CapMySqlIntegrationTests(ITestOutputHelper testOutput) =>
        _testOutput = testOutput;

    [CapMySqlFact]
    public async Task LegacyMessageEventBus_SendTrue_ShouldLeaveOutboxWhenBusinessTransactionRollsBack()
    {
        using (var schemaHost = await StartCapSampleHostAsync("CapMySql"))
            await schemaHost.StopAsync();
        var operationId = Guid.NewGuid().ToString("N");
        var eventId = $"{operationId}:legacy-unsafe";
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddScoped<ITransactionActionManager, TransactionActionManager>();
                services.AddCapEventBus(options => SqlUnitOfWorkCapRuntimeConfiguration.Configure(
                    options, OrdersConnectionString, RabbitMqConnectionString, "sqluow-legacy-regression"));
                services.EnableBingCapPublisherOnly();
            })
            .Build();
        await host.StartAsync();
        try
        {
            await using var businessConnection = new MySqlConnection(OrdersConnectionString);
            await businessConnection.OpenAsync();
            await using var businessTransaction = await businessConnection.BeginTransactionAsync();
            await using (var insert = businessConnection.CreateCommand())
            {
                insert.Transaction = businessTransaction;
                insert.CommandText = @"INSERT INTO OrderRecords (Id, OperationId, Layer, Sequence, Value)
                    VALUES (@id, @operationId, 'legacy', 1, 'rolled-back-business-row')";
                insert.Parameters.AddWithValue("@id", eventId);
                insert.Parameters.AddWithValue("@operationId", operationId);
                await insert.ExecuteNonQueryAsync();
            }

            using (var eventScope = host.Services.CreateScope())
            {
                var bus = eventScope.ServiceProvider.GetRequiredService<IMessageEventBus>();
                await bus.PublishAsync(new MessageEvent<SqlUnitOfWorkEventPayload>(new SqlUnitOfWorkEventPayload(
                    operationId, eventId, operationId, Guid.NewGuid().ToString("N"), "orders", "legacy-unsafe"))
                {
                    Id = eventId,
                    Name = SqlUnitOfWorkEventTopics.Orders,
                    Callback = string.Empty,
                    Send = true
                });
            }

            await businessTransaction.RollbackAsync();
            Assert.Equal(0, await ReadOrderCountAsync(eventId));
            Assert.Equal(1, await ReadOutboxCountAsync(eventId));
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    [CapMySqlFact]
    public async Task UnitOfWorkPublisher_ShouldKeepBusinessAndOutboxInvisibleUntilRootCommit()
    {
        using var host = await StartCapSampleHostAsync("CapMySql");
        var committedOperationId = Guid.NewGuid().ToString("N");
        var committedEventId = $"{committedOperationId}:safe-commit";
        await using (var requestScope = host.Services.CreateAsyncScope())
        {
            var manager = requestScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
            await using var root = manager.Begin();
            var executor = requestScope.ServiceProvider.GetRequiredService<Bing.Data.Sql.ISqlExecutor>();
            await executor.ExecuteSqlAsync(
                "INSERT INTO OrderRecords (Id, OperationId, Layer, Sequence, Value) " +
                "VALUES (@id, @operationId, 'uow', 1, 'uncommitted')",
                new { id = $"{committedOperationId}:business", operationId = committedOperationId });
            var publisher = requestScope.ServiceProvider.GetRequiredService<IUnitOfWorkMessagePublisher>();
            await publisher.PublishAsync("orders", CreateMessage(committedOperationId, committedEventId));
            Assert.Null((await host.Services.GetRequiredService<CapUnitOfWorkRuntimeManager>()
                .GetOperationPublisherAsync(root, "orders")).Transaction.Value);
            await executor.ExecuteSqlAsync(
                "INSERT INTO OrderRecords (Id, OperationId, Layer, Sequence, Value) " +
                "VALUES (@id, @operationId, 'uow', 2, 'same-resource-after-publish')",
                new { id = $"{committedOperationId}:after", operationId = committedOperationId });

            Assert.Equal(0, await ReadOrderCountAsync($"{committedOperationId}:business"));
            Assert.Equal(0, await ReadOrderCountAsync($"{committedOperationId}:after"));
            Assert.Equal(0, await ReadOutboxCountAsync(committedEventId));
            await root.CompleteAsync();
        }
        Assert.Equal(1, await ReadOrderCountAsync($"{committedOperationId}:business"));
        Assert.Equal(1, await ReadOrderCountAsync($"{committedOperationId}:after"));
        Assert.Equal(1, await ReadOutboxCountAsync(committedEventId));

        var rolledBackOperationId = Guid.NewGuid().ToString("N");
        var rolledBackEventId = $"{rolledBackOperationId}:safe-rollback";
        await using (var requestScope = host.Services.CreateAsyncScope())
        {
            var manager = requestScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
            await using var root = manager.Begin();
            var executor = requestScope.ServiceProvider.GetRequiredService<Bing.Data.Sql.ISqlExecutor>();
            await executor.ExecuteSqlAsync(
                "INSERT INTO OrderRecords (Id, OperationId, Layer, Sequence, Value) " +
                "VALUES (@id, @operationId, 'uow', 1, 'rolled-back')",
                new { id = $"{rolledBackOperationId}:business", operationId = rolledBackOperationId });
            var publisher = requestScope.ServiceProvider.GetRequiredService<IUnitOfWorkMessagePublisher>();
            await publisher.PublishAsync("orders", CreateMessage(rolledBackOperationId, rolledBackEventId));
            await root.RollbackAsync();
        }
        Assert.Equal(0, await ReadOrderCountAsync($"{rolledBackOperationId}:business"));
        Assert.Equal(0, await ReadOutboxCountAsync(rolledBackEventId));
    }

    [InlineData(false)]
    [InlineData(true)]
    [CapMySqlTheory]
    public async Task OperationExecutor_WhenEfAccessOrderChanges_ShouldCommitExactBusinessAndOutboxRows(
        bool efFirst)
    {
        using var host = await StartCapSampleHostAsync("CapMySqlEfBridge", operationsEnabled: true);
        var operationId = Guid.NewGuid().ToString("N");
        var eventId = $"{operationId}:operation-outbox";
        var executor = host.Services.GetRequiredService<IBingOperationExecutor>();

        await executor.ExecuteAsync<CapOperationWork>(
            (work, cancellationToken) => work.ExecuteAsync(operationId, eventId, efFirst, false,
                cancellationToken),
            cancellationToken: CancellationToken.None);

        var rows = await ReadOperationRowsAsync(operationId);
        Assert.Collection(rows,
            row => Assert.Equal(new OperationOrderRow($"{operationId}:operation", operationId,
                "operation", 1, "operation"), row),
            row => Assert.Equal(new OperationOrderRow($"{operationId}:ef", operationId,
                "ef-operation", 2, "tracked-only"), row));
        Assert.Equal(1, await ReadOutboxCountAsync(eventId));
    }

    [CapMySqlFact]
    public async Task OperationExecutor_WhenWorkFailsAfterPublish_ShouldRollbackBusinessAndOutboxRows()
    {
        using var host = await StartCapSampleHostAsync("CapMySqlEfBridge", operationsEnabled: true);
        var operationId = Guid.NewGuid().ToString("N");
        var eventId = $"{operationId}:operation-outbox-rollback";
        var executor = host.Services.GetRequiredService<IBingOperationExecutor>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync<CapOperationWork>(
                (work, cancellationToken) => work.ExecuteAsync(operationId, eventId, false, true,
                    cancellationToken),
                cancellationToken: CancellationToken.None));

        Assert.Equal("操作在 CAP Outbox 写入后失败。", exception.Message);
        Assert.Empty(await ReadOperationRowsAsync(operationId));
        Assert.Equal(0, await ReadOutboxCountAsync(eventId));
    }

    [CapMySqlFact]
    public async Task OperationWebHost_WithIndependentConsumer_ShouldPersistExactDualSourceDeliveryAcrossEntryPoints()
    {
        var repositoryRoot = FindRepositoryRoot();
        var processDirectory = Path.Combine(Path.GetTempPath(), "Bing.Samples.SqlUnitOfWork.Tests",
            $"cap-independent-{Guid.NewGuid():N}");
        processDirectory = Path.Combine(repositoryRoot, "ai_docs", "tasks",
            "20261022-abp-phase-b-operation-uow", "artifacts", "review-fix-round-1", "l3",
            Path.GetFileName(processDirectory));
        Directory.CreateDirectory(processDirectory);
        var journalPath = Path.Combine(processDirectory, "producer-operations.jsonl");
        var port = GetAvailableTcpPort();
        var baseAddress = new Uri($"http://127.0.0.1:{port}/");
        using var client = new HttpClient { BaseAddress = baseAddress, Timeout = Timeout.InfiniteTimeSpan };
        ChildProcess consumer = null;
        ChildProcess producer = null;

        try
        {
            consumer = StartSampleProcess(repositoryRoot,
                "samples/Bing.Samples.SqlUnitOfWork.Consumer/Bing.Samples.SqlUnitOfWork.Consumer.csproj");
            producer = StartSampleProcess(repositoryRoot,
                "samples/Bing.Samples.SqlUnitOfWork/Bing.Samples.SqlUnitOfWork.csproj",
                "--urls", baseAddress.ToString().TrimEnd('/'),
                "--SqlUow:Storage=CapMySqlEfBridge",
                "--SqlUow:OperationsEnabled=true",
                $"--SqlUow:ProcessEvidencePath={journalPath}");

            await WaitForHttpHostAsync(client, producer.Process);
            await WaitForConsumerSchemaAsync(TimeSpan.FromSeconds(45));

            using var cancellation = new CancellationTokenSource();
            var cancellationRequest = client.PostAsync(
                $"api/operation-scenarios/{SqlUnitOfWorkScenarios.OperationCapCancellation}", null,
                cancellation.Token);
            var cancelledOperationId = await WaitForCancellationScenarioAsync(journalPath,
                cancellationRequest, TimeSpan.FromSeconds(45));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancellationRequest);
            await WaitForOperationStatusAsync(client, cancelledOperationId, "Cancelled", TimeSpan.FromSeconds(30));
            await AssertCapOperationHasNoCommittedRowsAsync(cancelledOperationId);

            var nextResponse = await client.PostAsync(
                $"api/operation-scenarios/{SqlUnitOfWorkScenarios.OperationCapSuccess}", null);
            nextResponse.EnsureSuccessStatusCode();
            var nextResult = await nextResponse.Content.ReadFromJsonAsync<ScenarioExecutionResult>();
            Assert.NotNull(nextResult);
            await AssertCapOperationDeliveredAsync(nextResult.OperationId, SqlUnitOfWorkScenarios.OperationCapSuccess);

            var efFirstResponse = await client.PostAsync(
                $"api/operation-scenarios/{SqlUnitOfWorkScenarios.OperationCapEfFirstSuccess}", null);
            efFirstResponse.EnsureSuccessStatusCode();
            var efFirstResult = await efFirstResponse.Content.ReadFromJsonAsync<ScenarioExecutionResult>();
            Assert.NotNull(efFirstResult);
            await AssertCapOperationDeliveredAsync(efFirstResult.OperationId,
                SqlUnitOfWorkScenarios.OperationCapEfFirstSuccess);

            var workerResponse = await client.PostAsync(
                $"api/worker/scenarios/{SqlUnitOfWorkScenarios.OperationCapSuccess}", null);
            Assert.Equal(System.Net.HttpStatusCode.Accepted, workerResponse.StatusCode);
            var workerOperationId = await ReadOperationIdAsync(workerResponse);
            await WaitForOperationStatusAsync(client, workerOperationId, "Completed", TimeSpan.FromSeconds(45));
            await AssertCapOperationDeliveredAsync(workerOperationId, SqlUnitOfWorkScenarios.OperationCapSuccess);

            var hangfireResponse = await client.PostAsync(
                $"api/jobs/scenarios/{SqlUnitOfWorkScenarios.OperationCapSuccess}", null);
            Assert.Equal(System.Net.HttpStatusCode.Accepted, hangfireResponse.StatusCode);
            var hangfireOperationId = await ReadOperationIdAsync(hangfireResponse);
            await WaitForOperationStatusAsync(client, hangfireOperationId, "Completed", TimeSpan.FromSeconds(45));
            await AssertCapOperationDeliveredAsync(hangfireOperationId, SqlUnitOfWorkScenarios.OperationCapSuccess);

            var failedResponse = await client.PostAsync(
                $"api/operation-scenarios/{SqlUnitOfWorkScenarios.OperationCapFailure}", null);
            Assert.Equal(System.Net.HttpStatusCode.Conflict, failedResponse.StatusCode);
            var failedOperationId = await ReadOperationIdAsync(failedResponse);
            await WaitForOperationStatusAsync(client, failedOperationId, "Failed", TimeSpan.FromSeconds(15));
            await AssertCapOperationHasNoCommittedRowsAsync(failedOperationId);

            var afterFailureResponse = await client.PostAsync(
                $"api/operation-scenarios/{SqlUnitOfWorkScenarios.OperationCapSuccess}", null);
            afterFailureResponse.EnsureSuccessStatusCode();
            var afterFailureResult = await afterFailureResponse.Content.ReadFromJsonAsync<ScenarioExecutionResult>();
            Assert.NotNull(afterFailureResult);
            await AssertCapOperationDeliveredAsync(afterFailureResult.OperationId,
                SqlUnitOfWorkScenarios.OperationCapSuccess);

            var partialResponse = await client.PostAsync(
                $"api/scenarios/{SqlUnitOfWorkScenarios.CapPartialCommit}", null);
            Assert.Equal(System.Net.HttpStatusCode.Conflict, partialResponse.StatusCode);
            var partialOperationId = await ReadOperationIdAsync(partialResponse);
            await WaitForConsumerDeliveryAsync("orders", $"{partialOperationId}:orders-event",
                TimeSpan.FromSeconds(45));
            Assert.Equal(1, await ReadOrderCountAsync($"{partialOperationId}:cap-orders"));
            Assert.Equal(0, await ReadReportCountAsync(partialOperationId));
            Assert.Equal(1, await ReadOutboxCountAsync(OrdersConnectionString,
                $"{partialOperationId}:orders-event"));
            Assert.Equal(0, await ReadOutboxCountAsync(ReportingConnectionString,
                $"{partialOperationId}:reporting-event"));
            Assert.Empty(await ReadBusinessEventsAsync(ReportingConnectionString,
                $"{partialOperationId}:reporting-event"));
            await AssertCapDeliveryAsync("orders", $"{partialOperationId}:orders-event", partialOperationId,
                SqlUnitOfWorkScenarios.CapPartialCommit);
            Assert.Equal(0, await ReadConsumerDeliveryCountAsync("reporting",
                $"{partialOperationId}:reporting-event"));

            _testOutput.WriteLine($"cap-independent-process producerPid={producer.Process.Id} " +
                $"consumerPid={consumer.Process.Id} efFirst={efFirstResult.OperationId} " +
                $"worker={workerOperationId} hangfire={hangfireOperationId} failed={failedOperationId} " +
                $"afterFailure={afterFailureResult.OperationId} " +
                $"cancelled={cancelledOperationId} next={nextResult.OperationId} partial={partialOperationId}");
        }
        finally
        {
            await StopSampleProcessAsync(producer);
            await StopSampleProcessAsync(consumer);
        }
    }

    [InlineData("CapMySql")]
    [InlineData("CapMySqlEfBridge")]
    [CapMySqlTheory]
    public async Task MessageFirstScenario_ShouldPublishBeforeAnyBusinessEventAndRetainExactRows(
        string storageMode)
    {
        using var host = await StartCapSampleHostAsync(storageMode);
        var operationId = Guid.NewGuid().ToString("N");
        await using var scope = host.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<SqlUnitOfWorkApplicationService>();
        await service.ExecuteAsync(SqlUnitOfWorkScenarios.CapMessageFirstCommit, operationId,
            CancellationToken.None);

        Assert.Equal(1, await ReadOrderCountAsync($"{operationId}:cap-orders"));
        Assert.Equal(1, await ReadReportCountAsync(operationId));
        var ordersEventId = $"{operationId}:orders-message-first";
        var reportingEventId = $"{operationId}:reporting-event";
        Assert.Equal(1, await ReadBusinessEventCountAsync(OrdersConnectionString, ordersEventId));
        Assert.Equal(1, await ReadBusinessEventCountAsync(ReportingConnectionString, reportingEventId));
        var ordersMessageIds = await ReadNativeCapMessageIdsAsync(OrdersConnectionString, ordersEventId);
        var reportingMessageIds = await ReadNativeCapMessageIdsAsync(ReportingConnectionString, reportingEventId);
        Assert.Single(ordersMessageIds);
        Assert.Single(reportingMessageIds);

        var diagnostics = await scope.ServiceProvider.GetRequiredService<CapEventDiagnosticsService>()
            .ReadAsync(operationId, null, null, null, null, CancellationToken.None);
        var orderEvent = Assert.Single(diagnostics.Events, item => item.EventId == ordersEventId);
        Assert.Equal(ordersMessageIds, orderEvent.CapMessageIds);
        var timeline = scope.ServiceProvider.GetRequiredService<ScenarioOperationStore>();
        Assert.True(timeline.TryGet(operationId, out var operation));
        var steps = operation.ProcessSteps!.Where(item => item.Source == "orders")
            .Select(item => item.Step).ToArray();
        Assert.True(Array.IndexOf(steps, "publisher-call-started") < Array.IndexOf(steps, "publisher-returned"));
        Assert.True(Array.IndexOf(steps, "publisher-returned") < Array.IndexOf(steps, "business-event-sql-started"));
        Assert.True(Array.IndexOf(steps, "business-event-sql-started") < Array.IndexOf(steps, "order-row-sql-started"));
        Assert.Single(operation.ProcessSteps, item => item.EventId == ordersEventId &&
            item.Step == "business-event-sql-started");
        Assert.Single(operation.ProcessSteps, item => item.EventId == $"{operationId}:cap-orders" &&
            item.Step == "order-row-sql-started");
    }

    [CapMySqlFact]
    public async Task DiagnosticsProjection_ShouldPreserveEveryNativeMessageAndConsumerAttempt()
    {
        using var host = await StartCapSampleHostAsync("CapMySql");
        await using var scope = host.Services.CreateAsyncScope();
        var operationId = Guid.NewGuid().ToString("N");
        var eventId = $"{operationId}:diagnostics-projection";
        var traceId = Guid.NewGuid().ToString("N");
        await using var root = scope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
        var operationStore = scope.ServiceProvider.GetRequiredService<ScenarioOperationStore>();
        operationStore.Create(operationId, "diagnostics-projection");
        operationStore.RecordCapEvent(operationId, "diagnostics-projection", eventId, traceId,
            root.Id.ToString("N"), "orders", "bing.sqluow.integration.unhandled");
        operationStore.MarkPublisherCallStarted(operationId, eventId, "orders");

        var publisher = scope.ServiceProvider.GetRequiredService<IUnitOfWorkMessagePublisher>();
        var message = CreateMessage(operationId, eventId);
        message.Name = "bing.sqluow.integration.unhandled";
        await publisher.PublishAsync("orders", message);
        operationStore.MarkPublisherReturned(operationId, eventId, "orders");
        await publisher.PublishAsync("orders", message);
        operationStore.MarkPublisherReturned(operationId, eventId, "orders");

        Assert.Equal(0, await ReadOutboxCountAsync(OrdersConnectionString, eventId));
        await root.CompleteAsync();

        var createdAt = DateTime.UtcNow;
        createdAt = new DateTime(createdAt.Ticks - createdAt.Ticks % 10, DateTimeKind.Utc);
        var attemptIds = await SeedConsumerDiagnosticRowsAsync(OrdersConnectionString, operationId, eventId, traceId,
            root.Id.ToString("N"), createdAt);
        var messageIds = await ReadNativeCapMessageIdsAsync(OrdersConnectionString, eventId);
        Assert.Equal(2, messageIds.Length);

        var diagnostics = await scope.ServiceProvider.GetRequiredService<CapEventDiagnosticsService>()
            .ReadAsync(null, eventId, null, "orders", null, CancellationToken.None);
        var row = Assert.Single(diagnostics.Events);
        Assert.Equal(messageIds.OrderBy(id => id, StringComparer.Ordinal),
            row.CapMessageIds.OrderBy(id => id, StringComparer.Ordinal));
        Assert.Equal(2, row.OutboxMessageCount);
        Assert.Equal(2, row.ConsumerAttempts);
        Assert.Equal(1, row.ConsumerEffectCount);
        Assert.Contains("sqluow-orders-consumer.contract", row.ConsumerNames);
        Assert.Collection(row.ConsumerAttemptDetails,
            attempt =>
            {
                Assert.Equal(attemptIds[0], attempt.AttemptId);
                Assert.Equal("sqluow-orders-consumer.contract", attempt.ConsumerName);
                Assert.Equal("Failed", attempt.Status);
                Assert.Equal("contract retry failure", attempt.Error);
                Assert.Equal(new DateTimeOffset(createdAt.AddSeconds(-1), TimeSpan.Zero), attempt.CreatedAt);
            },
            attempt =>
            {
                Assert.Equal(attemptIds[1], attempt.AttemptId);
                Assert.Equal("sqluow-orders-consumer.contract", attempt.ConsumerName);
                Assert.Equal("Succeeded", attempt.Status);
                Assert.Null(attempt.Error);
                Assert.Equal(new DateTimeOffset(createdAt, TimeSpan.Zero), attempt.CreatedAt);
            });
    }

    [CapMySqlFact]
    public async Task RuntimePublisher_ShouldReuseWithinOperationScopeAndDisposeWithRoot()
    {
        using var host = await StartCapSampleHostAsync("CapMySql");
        await using var requestScope = host.Services.CreateAsyncScope();
        var scopeManager = requestScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        var runtime = host.Services.GetRequiredService<CapUnitOfWorkRuntimeManager>();
        var root = scopeManager.Begin();
        var firstPublisher = await runtime.GetOperationPublisherAsync(root, "orders");
        var sameScopePublisher = await runtime.GetOperationPublisherAsync(root, "orders");
        Assert.Same(firstPublisher, sameScopePublisher);
        Assert.NotSame(host.Services, firstPublisher.ServiceProvider);
        var nativePublisher = firstPublisher.ServiceProvider.GetRequiredService<ICapPublisher>();
        Assert.Same(nativePublisher, sameScopePublisher.ServiceProvider.GetRequiredService<ICapPublisher>());
        Assert.Same(firstPublisher.ServiceProvider,
            nativePublisher.GetType().GetProperty("ServiceProvider")?.GetValue(nativePublisher));

        await Task.Yield();
        var participant = scopeManager.Begin();
        Assert.Same(firstPublisher, await runtime.GetOperationPublisherAsync(participant, "orders"));
        participant.Complete();
        await participant.DisposeAsync();

        var independentRoot = scopeManager.Begin(requiresNew: true);
        var independentPublisher = await runtime.GetOperationPublisherAsync(independentRoot, "orders");
        var disposedProvider = independentPublisher.ServiceProvider;
        Assert.NotSame(firstPublisher, independentPublisher);
        Assert.NotSame(firstPublisher.ServiceProvider, disposedProvider);
        var independentNativePublisher = disposedProvider.GetRequiredService<ICapPublisher>();
        Assert.NotSame(nativePublisher, independentNativePublisher);
        Assert.Same(disposedProvider,
            independentNativePublisher.GetType().GetProperty("ServiceProvider")?.GetValue(independentNativePublisher));
        await independentRoot.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => disposedProvider.GetRequiredService<ICapPublisher>());

        var rootProvider = firstPublisher.ServiceProvider;
        await root.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => rootProvider.GetRequiredService<ICapPublisher>());
    }

    [CapMySqlFact]
    public async Task MinimalConsumerHost_ShouldStartBothConfiguredCapRuntimes()
    {
        using var host = SqlUnitOfWorkConsumerHost.CreateHostBuilder(Array.Empty<string>()).Build();
        var identityResolver = host.Services.GetRequiredService<ISqlDatabaseIdentityResolver>();
        Assert.Null(host.Services.GetService<ISqlUnitOfWorkResourceAccessor>());

        await host.StartAsync();

        var runtime = host.Services.GetRequiredService<CapUnitOfWorkRuntimeManager>();
        Assert.Equal(identityResolver.Resolve(Bing.Data.Enums.DatabaseType.MySql, OrdersConnectionString),
            identityResolver.Resolve(Bing.Data.Enums.DatabaseType.MySql,
                runtime.GetEffectiveStorageConnectionString("orders")));
        Assert.Equal(identityResolver.Resolve(Bing.Data.Enums.DatabaseType.MySql, ReportingConnectionString),
            identityResolver.Resolve(Bing.Data.Enums.DatabaseType.MySql,
                runtime.GetEffectiveStorageConnectionString("reporting")));

        await host.StopAsync();
    }

    [CapMySqlFact]
    public async Task OperationScope_WhenPublisherConstructionFails_ShouldDisposeDependenciesOnceAndRetainFailures()
    {
        var tracker = new OperationScopeConstructionFailureTracker();
        var host = BuildOperationScopeConstructionFailureHost(tracker);
        try
        {
            await host.StartAsync();
            await using var requestScope = host.Services.CreateAsyncScope();
            var scopeManager = requestScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
            var publisher = requestScope.ServiceProvider.GetRequiredService<IUnitOfWorkMessagePublisher>();

            foreach (var cleanupFails in new[] { false, true })
            {
                tracker.FailAsyncDispose = cleanupFails;
                var root = scopeManager.Begin();
                var eventId = $"{Guid.NewGuid():N}:publisher-construction-failure";
                Exception failure = null;
                try
                {
                    await publisher.PublishAsync("orders", CreateMessage(eventId, eventId));
                }
                catch (Exception exception)
                {
                    failure = exception;
                }

                Assert.NotNull(failure);
                Assert.True(root.IsRollbackOnly);
                if (cleanupFails)
                {
                    var aggregate = Assert.IsType<AggregateException>(failure);
                    var inner = aggregate.Flatten().InnerExceptions;
                    Assert.Contains(tracker.PublisherConstructionException, inner);
                    Assert.Contains(tracker.AsyncDisposeException, inner);
                }
                else
                    Assert.Same(tracker.PublisherConstructionException, failure);

                await Assert.ThrowsAsync<InvalidOperationException>(() => root.CompleteAsync());
                await root.DisposeAsync();
                Assert.Equal(tracker.SyncCreatedCount, tracker.SyncDisposedCount);
                Assert.Equal(tracker.AsyncCreatedCount, tracker.AsyncDisposedCount);
                Assert.Equal(0, await ReadOutboxCountAsync(OrdersConnectionString, eventId));
                Assert.Equal(0, await ReadOutboxCountAsync(ReportingConnectionString, eventId));
            }

            await host.StopAsync();
        }
        finally
        {
            host.Dispose();
        }

        Assert.Equal(tracker.SyncCreatedCount, tracker.SyncDisposedCount);
        Assert.Equal(2, tracker.AsyncCreatedCount);
        Assert.Equal(2, tracker.AsyncDisposedCount);
    }

    [CapMySqlFact]
    public async Task OperationScope_WhenAsyncPublisherCleanupCapturesSingleThreadContext_ShouldReturnAwaitableFailure()
    {
        const string childProcessVariable = "BING_CAP_ASYNC_DISPOSE_CONTEXT_CHILD";
        if (!string.Equals(Environment.GetEnvironmentVariable(childProcessVariable), "1",
                StringComparison.Ordinal))
        {
            await RunContextFailureRegressionInBoundedChildAsync(childProcessVariable);
            return;
        }

        var tracker = new OperationScopeConstructionFailureTracker
        {
            FailAsyncDispose = true,
            CaptureSynchronizationContextOnDispose = true
        };
        var host = BuildOperationScopeConstructionFailureHost(tracker);
        IUnitOfWorkScope root = null;
        try
        {
            await host.StartAsync();
            await using (var requestScope = host.Services.CreateAsyncScope())
            {
                var services = requestScope.ServiceProvider;
                var scopeManager = services.GetRequiredService<IUnitOfWorkScopeManager>();
                var publisher = services.GetRequiredService<IUnitOfWorkMessagePublisher>();
                var operationRoot = scopeManager.Begin();
                root = operationRoot;
                var resource = services.GetRequiredService<ISqlUnitOfWorkResourceAccessor>().GetResource(
                    operationRoot, new DatabaseContext { DbKey = "orders" },
                    createDapperResource: true, requireComparableIdentity: true);
                Assert.NotNull(resource);
                await resource!.EnsureReadyAsync();
                Assert.NotNull(resource.Transaction);

                var eventId = $"{Guid.NewGuid():N}:captured-async-cleanup";
                var synchronizationContext = new SingleThreadPumpSynchronizationContext();
                var ownerThread = Environment.CurrentManagedThreadId;
                var previousContext = SynchronizationContext.Current;
                Task publishTask;
                try
                {
                    SynchronizationContext.SetSynchronizationContext(synchronizationContext);
                    publishTask = publisher.PublishAsync("orders", CreateMessage(eventId, eventId));
                    Assert.False(publishTask.IsCompleted);
                    synchronizationContext.PumpUntil(publishTask, TimeSpan.FromSeconds(10));
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                    synchronizationContext.Dispose();
                }

                var failure = await Assert.ThrowsAsync<AggregateException>(() => publishTask);
                var failures = failure.Flatten().InnerExceptions;
                Assert.Contains(tracker.PublisherConstructionException, failures);
                Assert.Contains(tracker.AsyncDisposeException, failures);
                Assert.Same(synchronizationContext, tracker.AsyncDisposeSynchronizationContext);
                Assert.Same(synchronizationContext, tracker.AsyncDisposeContinuationContext);
                Assert.Equal(ownerThread, tracker.AsyncDisposeContinuationThreadId);
                Assert.True(operationRoot.IsRollbackOnly);
                Assert.Equal(1, tracker.SyncCreatedCount);
                Assert.Equal(1, tracker.SyncDisposedCount);
                Assert.Equal(1, tracker.AsyncCreatedCount);
                Assert.Equal(1, tracker.AsyncDisposedCount);

                await Assert.ThrowsAsync<InvalidOperationException>(() => operationRoot.CompleteAsync());
                await operationRoot.DisposeAsync();
                Assert.Equal(1, tracker.SyncDisposedCount);
                Assert.Equal(1, tracker.AsyncDisposedCount);
                var ordersOutboxRows = await ReadOutboxCountAsync(OrdersConnectionString, eventId);
                var reportingOutboxRows = await ReadOutboxCountAsync(ReportingConnectionString, eventId);
                Assert.Equal(0, ordersOutboxRows);
                Assert.Equal(0, reportingOutboxRows);
                _testOutput.WriteLine($"async-dispose-context-regression eventId={eventId} " +
                                      $"ready=true publishTaskCompleted={publishTask.IsCompleted} " +
                                      "originalFailure=retained cleanupFailure=retained rollbackOnly=true " +
                                      "syncDisposed=1 asyncDisposed=1 " +
                                      $"independentOutboxRows=orders:{ordersOutboxRows},reporting:{reportingOutboxRows}");
            }

            await host.StopAsync();
        }
        finally
        {
            if (root is { IsActive: true })
                await root.DisposeAsync();
            host.Dispose();
        }

        Assert.Equal(1, tracker.SyncDisposedCount);
        Assert.Equal(1, tracker.AsyncDisposedCount);
    }

    private async Task RunContextFailureRegressionInBoundedChildAsync(string childProcessVariable)
    {
        var projectFile = FindSampleTestsProjectFile();
        var framework = $"net{Environment.Version.Major}.0";
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = Path.GetDirectoryName(projectFile)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("test");
        process.StartInfo.ArgumentList.Add(projectFile);
        process.StartInfo.ArgumentList.Add("--configuration");
        process.StartInfo.ArgumentList.Add("Release");
        process.StartInfo.ArgumentList.Add("--framework");
        process.StartInfo.ArgumentList.Add(framework);
        process.StartInfo.ArgumentList.Add("--no-build");
        process.StartInfo.ArgumentList.Add("--no-restore");
        process.StartInfo.ArgumentList.Add("--filter");
        process.StartInfo.ArgumentList.Add(
            "FullyQualifiedName=Bing.Samples.SqlUnitOfWork.Tests.CapMySqlIntegrationTests." +
            nameof(OperationScope_WhenAsyncPublisherCleanupCapturesSingleThreadContext_ShouldReturnAwaitableFailure));
        process.StartInfo.ArgumentList.Add("--verbosity");
        process.StartInfo.ArgumentList.Add("quiet");
        process.StartInfo.ArgumentList.Add("--logger");
        process.StartInfo.ArgumentList.Add("console;verbosity=detailed");
        process.StartInfo.Environment[childProcessVariable] = "1";

        Assert.True(process.Start(), "未能启动有界生命周期回归子进程。");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        var exitTask = process.WaitForExitAsync();
        var finished = await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(45))) == exitTask;
        if (!finished)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
            }
            await process.WaitForExitAsync();
        }

        var output = await standardOutput;
        var error = await standardError;
        _testOutput.WriteLine($"async-dispose-context-child pid={process.Id} finished={finished} exitCode={process.ExitCode}");
        _testOutput.WriteLine(output);
        if (!string.IsNullOrWhiteSpace(error))
            _testOutput.WriteLine(error);
        if (!finished)
        {
            Assert.Fail($"有界子测试在45秒内未结束，已终止仅由本测试启动的进程树。\n{output}\n{error}");
        }

        Assert.True(process.ExitCode == 0, $"单线程上下文回归子测试失败。\n{output}\n{error}");
    }

    private static string FindSampleTestsProjectFile()
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(typeof(CapMySqlIntegrationTests).Assembly.Location)!);
        while (directory != null)
        {
            var projectFile = Path.Combine(directory.FullName, "Bing.Samples.SqlUnitOfWork.Tests.csproj");
            if (File.Exists(projectFile))
                return projectFile;
            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法从当前测试程序集定位 Sample 测试项目。");
    }

    [CapMySqlFact]
    public async Task UnitOfWorkPublisher_ShouldRestoreNativeAmbientAfterSuccessAcrossNestedAndIndependentScopes()
    {
        using var host = await StartCapSampleHostAsync("CapMySql");
        await using var requestScope = host.Services.CreateAsyncScope();
        var scopeManager = requestScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        var runtime = host.Services.GetRequiredService<CapUnitOfWorkRuntimeManager>();
        var publisher = requestScope.ServiceProvider.GetRequiredService<IUnitOfWorkMessagePublisher>();
        var operationId = Guid.NewGuid().ToString("N");
        var ordersEventId = $"{operationId}:ambient-orders";
        var reportingEventId = $"{operationId}:ambient-reporting";
        var independentEventId = $"{operationId}:ambient-independent-rollback";
        var root = scopeManager.Begin();
        var ordersOperationPublisher = await runtime.GetOperationPublisherAsync(root, "orders");
        var ordersNativePublisher = ordersOperationPublisher.ServiceProvider.GetRequiredService<ICapPublisher>();
        Assert.Null(ordersNativePublisher.Transaction.Value);

        await publisher.PublishAsync("orders", CreateMessage(operationId, ordersEventId));
        await Task.Yield();
        Assert.Same(root, scopeManager.Current);
        Assert.Same(ordersOperationPublisher, await runtime.GetOperationPublisherAsync(root, "orders"));
        Assert.Null(ordersNativePublisher.Transaction.Value);

        var participant = scopeManager.Begin();
        var reportingOperationPublisher = await runtime.GetOperationPublisherAsync(participant, "reporting");
        var reportingNativePublisher = reportingOperationPublisher.ServiceProvider.GetRequiredService<ICapPublisher>();
        Assert.NotSame(ordersOperationPublisher, reportingOperationPublisher);
        await publisher.PublishAsync("reporting", CreateMessage(operationId, reportingEventId, "reporting"));
        await Task.Yield();
        Assert.Same(root, scopeManager.Current);
        Assert.Null(reportingNativePublisher.Transaction.Value);
        Assert.Null(ordersNativePublisher.Transaction.Value);
        participant.Complete();
        await participant.DisposeAsync();
        Assert.Same(root, scopeManager.Current);
        Assert.Same(ordersOperationPublisher, await runtime.GetOperationPublisherAsync(root, "orders"));

        var independentRoot = scopeManager.Begin(requiresNew: true);
        var independentOperationPublisher = await runtime.GetOperationPublisherAsync(independentRoot, "orders");
        var independentNativePublisher = independentOperationPublisher.ServiceProvider.GetRequiredService<ICapPublisher>();
        Assert.NotSame(ordersOperationPublisher, independentOperationPublisher);
        await publisher.PublishAsync("orders", CreateMessage(operationId, independentEventId));
        await Task.Yield();
        Assert.Same(independentRoot, scopeManager.Current);
        Assert.Null(independentNativePublisher.Transaction.Value);
        await independentRoot.RollbackAsync();
        await independentRoot.DisposeAsync();
        Assert.Same(root, scopeManager.Current);
        Assert.Null(ordersNativePublisher.Transaction.Value);
        Assert.Equal(0, await ReadOutboxCountAsync(OrdersConnectionString, ordersEventId));
        Assert.Equal(0, await ReadOutboxCountAsync(ReportingConnectionString, reportingEventId));
        Assert.Equal(0, await ReadOutboxCountAsync(OrdersConnectionString, independentEventId));

        await root.CompleteAsync();
        Assert.Equal(1, await ReadOutboxCountAsync(OrdersConnectionString, ordersEventId));
        Assert.Equal(1, await ReadOutboxCountAsync(ReportingConnectionString, reportingEventId));
        await root.DisposeAsync();
        Assert.Null(ordersNativePublisher.Transaction.Value);
        Assert.Null(reportingNativePublisher.Transaction.Value);
        Assert.Null(independentNativePublisher.Transaction.Value);
        Assert.Throws<ObjectDisposedException>(() => ordersOperationPublisher.ServiceProvider
            .GetRequiredService<ICapPublisher>());
        Assert.Throws<ObjectDisposedException>(() => reportingOperationPublisher.ServiceProvider
            .GetRequiredService<ICapPublisher>());
        Assert.Throws<ObjectDisposedException>(() => independentOperationPublisher.ServiceProvider
            .GetRequiredService<ICapPublisher>());
    }

    [CapMySqlFact]
    public async Task UnitOfWorkPublisher_WhenNativePublishThrows_ShouldRestoreAmbientAndCurrentSourceRoot()
    {
        using var host = await StartCapSampleHostAsync("CapMySql");
        await using var requestScope = host.Services.CreateAsyncScope();
        var scopeManager = requestScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        var runtime = host.Services.GetRequiredService<CapUnitOfWorkRuntimeManager>();
        var publisher = requestScope.ServiceProvider.GetRequiredService<IUnitOfWorkMessagePublisher>();
        var operationId = Guid.NewGuid().ToString("N");
        var eventId = $"{operationId}:native-publish-failure";
        var root = scopeManager.Begin();
        var ordersOperationPublisher = await runtime.GetOperationPublisherAsync(root, "orders");
        var nativePublisher = ordersOperationPublisher.ServiceProvider.GetRequiredService<ICapPublisher>();
        var message = CreateMessage(operationId, eventId);
        message.Name = string.Empty;

        await Assert.ThrowsAsync<ArgumentNullException>(() => publisher.PublishAsync("orders", message));
        await Task.Yield();
        Assert.Null(nativePublisher.Transaction.Value);
        Assert.Same(root, scopeManager.Current);
        Assert.Same(ordersOperationPublisher, await runtime.GetOperationPublisherAsync(root, "orders"));
        Assert.True(root.IsRollbackOnly);
        Assert.Equal(0, await ReadOutboxCountAsync(OrdersConnectionString, eventId));

        await root.DisposeAsync();
        Assert.Null(nativePublisher.Transaction.Value);
        Assert.Throws<ObjectDisposedException>(() => ordersOperationPublisher.ServiceProvider
            .GetRequiredService<ICapPublisher>());
    }

    [CapMySqlFact]
    public async Task RuntimeStartFailure_ShouldStopAttemptedServicesInReverseAndDisposeAfterCleanupErrors()
    {
        var lifecycle = new List<string>();
        var first = new RecordingHostedService("first", lifecycle, failStop: true);
        var second = new RecordingHostedService("second", lifecycle, failStart: true, failStop: true);
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddMySqlProvider();
                services.AddSqlDataSource("orders", Bing.Data.Enums.DatabaseType.MySql, OrdersConnectionString);
                services.AddCapUnitOfWorkMessagePublisher(builder => builder.AddDataSource("orders",
                    Bing.Data.Enums.DatabaseType.MySql, OrdersConnectionString,
                    (options, connectionString) => SqlUnitOfWorkCapRuntimeConfiguration.Configure(options,
                        connectionString, RabbitMqConnectionString, "sqluow-lifecycle"),
                    runtimeServices =>
                    {
                        runtimeServices.AddSingleton<IHostedService>(_ => first);
                        runtimeServices.AddSingleton<IHostedService>(_ => second);
                    }, publisherOnly: true));
            })
            .Build();

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => host.StartAsync());
        Assert.Contains("start:second", failure.ToString(), StringComparison.Ordinal);
        Assert.Equal(new[] { "start:first", "start:second", "stop:second", "stop:first" }, lifecycle.Take(4));
        Assert.Contains("dispose:first", lifecycle);
        Assert.Contains("dispose:second", lifecycle);
    }

    [CapMySqlFact]
    public async Task UnitOfWorkPublisher_WhenForeignAmbientTransactionExists_ShouldPreserveAmbientAndRollback()
    {
        using var host = await StartCapSampleHostAsync("CapMySql");
        var operationId = Guid.NewGuid().ToString("N");
        var eventId = $"{operationId}:foreign-ambient";
        await using var requestScope = host.Services.CreateAsyncScope();
        var scopeManager = requestScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        await using var root = scopeManager.Begin();
        var runtime = host.Services.GetRequiredService<CapUnitOfWorkRuntimeManager>();
        var capPublisher = await runtime.GetOperationPublisherAsync(root, "orders");
        var foreignTransaction = new Moq.Mock<ICapTransaction>().Object;
        capPublisher.Transaction.Value = foreignTransaction;
        await Task.Yield();

        var publisher = requestScope.ServiceProvider.GetRequiredService<IUnitOfWorkMessagePublisher>();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            publisher.PublishAsync("orders", CreateMessage(operationId, eventId)));

        Assert.Contains("已有环境事务", failure.Message);
        Assert.Same(foreignTransaction, capPublisher.Transaction.Value);
        Assert.True(root.IsRollbackOnly);
        await Assert.ThrowsAsync<InvalidOperationException>(() => root.CompleteAsync());
        Assert.Equal(0, await ReadOutboxCountAsync(OrdersConnectionString, eventId));
    }

    [CapMySqlFact]
    public async Task Runtime_WhenFinalNativeMySqlOptionsPointToAnotherDatabase_ShouldRejectBeforeOutboxSql()
    {
        using (var schemaHost = await StartCapSampleHostAsync("CapMySql"))
            await schemaHost.StopAsync();

        var eventId = $"{Guid.NewGuid():N}:wrong-cap-database";
        var ordersBefore = await ReadOutboxCountAsync(OrdersConnectionString, eventId);
        var reportingBefore = await ReadOutboxCountAsync(ReportingConnectionString, eventId);
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddMySqlProvider();
                services.AddSqlDataSource("orders", Bing.Data.Enums.DatabaseType.MySql, OrdersConnectionString);
                services.AddCapUnitOfWorkMessagePublisher(builder => builder.AddDataSource("orders",
                    Bing.Data.Enums.DatabaseType.MySql, OrdersConnectionString,
                    (options, _) => SqlUnitOfWorkCapRuntimeConfiguration.Configure(options,
                        ReportingConnectionString, RabbitMqConnectionString, "sqluow-wrong-store"),
                    publisherOnly: true));
            })
            .Build();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());
        Assert.Contains("最终 MySqlOptions", failure.Message, StringComparison.Ordinal);
        Assert.Equal(ordersBefore, await ReadOutboxCountAsync(OrdersConnectionString, eventId));
        Assert.Equal(reportingBefore, await ReadOutboxCountAsync(ReportingConnectionString, eventId));
    }

    [InlineData("CapMySql", false)]
    [InlineData("CapMySql", true)]
    [InlineData("CapMySqlEfBridge", false)]
    [InlineData("CapMySqlEfBridge", true)]
    [CapMySqlTheory]
    public async Task UnitOfWorkPublisher_WhenPreCancelled_ShouldMarkReadyAndNotReadyDapperOrEfRollbackOnly(
        string storageMode, bool prepareResource)
    {
        using var host = await StartCapSampleHostAsync(storageMode);
        var operationId = Guid.NewGuid().ToString("N");
        var eventId = $"{operationId}:pre-cancelled";
        await using var requestScope = host.Services.CreateAsyncScope();
        var scopeManager = requestScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        var root = scopeManager.Begin();
        var runtime = host.Services.GetRequiredService<CapUnitOfWorkRuntimeManager>();
        var operationPublisher = await runtime.GetOperationPublisherAsync(root, "orders");
        var nativePublisher = operationPublisher.ServiceProvider.GetRequiredService<ICapPublisher>();
        var ambientBefore = nativePublisher.Transaction.Value;
        Assert.Null(ambientBefore);
        if (prepareResource)
        {
            if (storageMode == "CapMySqlEfBridge")
            {
                var context = requestScope.ServiceProvider.GetRequiredService<SampleUnitOfWorkDbContext>();
                context.OrderRecords.Add(new OrderRecord
                {
                    Id = $"{operationId}:business",
                    OperationId = operationId,
                    Layer = "ef-ready",
                    Sequence = 1,
                    Value = "pre-cancelled"
                });
                await context.SaveChangesAsync();
            }
            else
            {
                var executor = requestScope.ServiceProvider.GetRequiredService<Bing.Data.Sql.ISqlExecutor>();
                await executor.ExecuteSqlAsync(
                    "INSERT INTO OrderRecords (Id, OperationId, Layer, Sequence, Value) " +
                    "VALUES (@id, @operationId, 'dapper-ready', 1, 'pre-cancelled')",
                    new { id = $"{operationId}:business", operationId });
            }
        }

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var publisher = requestScope.ServiceProvider.GetRequiredService<IUnitOfWorkMessagePublisher>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            publisher.PublishAsync("orders", CreateMessage(operationId, eventId), cancelled.Token));
        await Task.Yield();
        Assert.Same(ambientBefore, nativePublisher.Transaction.Value);
        Assert.True(root.IsRollbackOnly);

        var resource = requestScope.ServiceProvider.GetRequiredService<ISqlUnitOfWorkResourceAccessor>()
            .GetResource(root, new DatabaseContext { DbKey = "orders" }, createDapperResource: false,
                requireComparableIdentity: true);
        if (prepareResource)
        {
            if (storageMode == "CapMySql")
                resource = requestScope.ServiceProvider.GetRequiredService<ISqlUnitOfWorkResourceAccessor>()
                    .GetResource(root, new DatabaseContext { DbKey = "orders" }, createDapperResource: true,
                        requireComparableIdentity: true);
            Assert.NotNull(resource);
            Assert.NotNull(resource.Transaction);
        }
        else if (storageMode == "CapMySqlEfBridge")
        {
            Assert.NotNull(resource);
            Assert.Null(resource.Transaction);
            Assert.Equal(System.Data.ConnectionState.Closed, resource.Connection.State);
        }
        else
            Assert.Null(resource);

        await Assert.ThrowsAsync<InvalidOperationException>(() => root.CompleteAsync());
        Assert.Equal(0, await ReadOrderCountAsync($"{operationId}:business"));
        Assert.Equal(0, await ReadOutboxCountAsync(OrdersConnectionString, eventId));
        await root.DisposeAsync();
        Assert.Same(ambientBefore, nativePublisher.Transaction.Value);
        Assert.Throws<ObjectDisposedException>(() => operationPublisher.ServiceProvider
            .GetRequiredService<ICapPublisher>());
    }

    [CapMySqlFact]
    public async Task PartialCommitDiagnostics_ShouldRecoverFromStorageAfterHostRestart()
    {
        var journalPath = Path.Combine(Path.GetTempPath(), "Bing.Samples.SqlUnitOfWork.Tests",
            $"fix-round-1-{Guid.NewGuid():N}.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(journalPath)!);
        using (var schemaHost = await StartCapSampleHostAsync("CapMySql", journalPath))
            await schemaHost.StopAsync();

        string operationId;
        using (var writeHost = await StartCapSampleHostAsync("CapMySql", journalPath))
        {
            await using var scope = writeHost.Services.CreateAsyncScope();
            var controller = new ScenarioController(
                scope.ServiceProvider.GetRequiredService<SqlUnitOfWorkApplicationService>(),
                scope.ServiceProvider.GetRequiredService<ScenarioOperationStore>());
            var action = await controller.Run(SqlUnitOfWorkScenarios.CapPartialCommit, null,
                CancellationToken.None);
            var conflict = Assert.IsType<Microsoft.AspNetCore.Mvc.ConflictObjectResult>(action);
            var failure = Assert.IsType<ScenarioController.ScenarioFailure>(conflict.Value);
            operationId = failure.OperationId;
        }

        using var recoveryHost = await StartCapSampleHostAsync("CapMySql", journalPath);
        await using var recoveryScope = recoveryHost.Services.CreateAsyncScope();
        var diagnostics = recoveryScope.ServiceProvider.GetRequiredService<CapEventDiagnosticsService>();
        var snapshot = await diagnostics.ReadAsync(operationId, null, null, null, null, CancellationToken.None);
        var operation = Assert.Single(snapshot.Operations);

        Assert.Equal("cap-partial-commit", operation.Scenario);
        Assert.Equal("partial-commit", operation.Status);
        Assert.Equal("committed", operation.OrdersCommitStatus);
        Assert.Equal("no-matching-rows", operation.ReportingCommitStatus);
        Assert.Equal(1, await ReadOrderCountAsync($"{operationId}:cap-orders"));
        Assert.Equal(0, await ReadReportCountAsync(operationId));
        var ordersEvent = Assert.Single(snapshot.Events, item => item.Source == "orders");
        Assert.Equal("cap-partial-commit", ordersEvent.Scenario);
        Assert.Equal("database", ordersEvent.EvidenceSource);
        Assert.Equal("observed-rows", ordersEvent.DatabaseCommitStatus);
        Assert.Equal(1, ordersEvent.OutboxMessageCount);
        Assert.Equal(1, await ReadOutboxCountAsync(ordersEvent.EventId));
        var reportingIntent = Assert.Single(snapshot.Events, item => item.Source == "reporting");
        Assert.Equal("cap-partial-commit", reportingIntent.Scenario);
        Assert.Equal("process-journal", reportingIntent.EvidenceSource);
        Assert.Equal("no-matching-rows", reportingIntent.DatabaseCommitStatus);
    }

    private sealed class CapOperationWork
    {
        private readonly IServiceProvider _services;
        private readonly OperationScenarioService _operation;
        private readonly IUnitOfWorkMessagePublisher _publisher;

        public CapOperationWork(IServiceProvider services, OperationScenarioService operation,
            IUnitOfWorkMessagePublisher publisher)
        {
            _services = services;
            _operation = operation;
            _publisher = publisher;
        }

        public async Task ExecuteAsync(string operationId, string eventId, bool efFirst,
            bool failAfterPublish, CancellationToken cancellationToken)
        {
            if (efFirst)
            {
                var dbContext = _services.GetRequiredService<SampleUnitOfWorkDbContext>();
                await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            }

            await _operation.ExecuteAsync(SqlUnitOfWorkScenarios.OperationSuccess, operationId,
                cancellationToken);
            await _publisher.PublishAsync("orders", CreateMessage(operationId, eventId), cancellationToken);

            if (failAfterPublish)
                throw new InvalidOperationException("操作在 CAP Outbox 写入后失败。");
        }
    }

    private sealed record OperationOrderRow(string Id, string OperationId, string Layer, int Sequence,
        string Value);

    private sealed record ConsumerDeliveryRow(string Source, string EventId, string ConsumerName,
        string OperationId, string TraceId, string RootId, int AttemptCount, string EffectValue);

    private sealed record BusinessEventRow(string EventId, string OperationId, string TraceId, string RootId,
        string Source, string Topic, string Scenario);

    private sealed record ReportRow(string Id, string OperationId, string Value);

    private sealed record ChildProcess(Process Process, Task StandardOutputDrain, Task StandardErrorDrain);

    private static MessageEvent<SqlUnitOfWorkEventPayload> CreateMessage(string operationId, string eventId,
        string source = "orders") =>
        new(new SqlUnitOfWorkEventPayload(operationId, eventId, operationId,
            Guid.NewGuid().ToString("N"), source, "integration"))
        {
            Id = eventId,
            Name = source == "reporting" ? SqlUnitOfWorkEventTopics.Reporting : SqlUnitOfWorkEventTopics.Orders,
            Callback = string.Empty,
            Send = false
        };

    private static IHost BuildOperationScopeConstructionFailureHost(
        OperationScopeConstructionFailureTracker tracker) => Host.CreateDefaultBuilder()
        .ConfigureServices((_, services) =>
        {
            services.ConfigureSqlMetadata(options => options.DataSources.DefaultDataSourceKey = "orders");
            services.AddMySqlProvider();
            services.AddSqlDataSource("orders", Bing.Data.Enums.DatabaseType.MySql, OrdersConnectionString);
            services.AddUnitOfWorkScopes();
            services.AddCapUnitOfWorkMessagePublisher(builder => builder.AddDataSource("orders",
                Bing.Data.Enums.DatabaseType.MySql, OrdersConnectionString,
                (options, connectionString) => SqlUnitOfWorkCapRuntimeConfiguration.Configure(options,
                    connectionString, RabbitMqConnectionString, "sqluow-operation-scope-failure"),
                runtimeServices =>
                {
                    runtimeServices.AddSingleton(tracker);
                    runtimeServices.AddScoped<OperationScopeSyncDependency>();
                    runtimeServices.AddScoped<OperationScopeAsyncDependency>();
                    runtimeServices.AddScoped<ICapPublisher>(provider =>
                    {
                        provider.GetRequiredService<OperationScopeAsyncDependency>();
                        provider.GetRequiredService<OperationScopeSyncDependency>();
                        throw tracker.PublisherConstructionException;
                    });
                }, publisherOnly: true));
        })
        .Build();

    private static async Task<IHost> StartCapSampleHostAsync(string storageMode, string? processEvidencePath = null,
        bool operationsEnabled = false)
    {
        var builder = Program.CreateHostBuilder(Array.Empty<string>(), storageMode,
                webBuilder => webBuilder.UseUrls("http://127.0.0.1:0"))
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["SqlUow:ProcessEvidencePath"] = processEvidencePath ?? string.Empty,
                    ["SqlUow:OperationsEnabled"] = operationsEnabled.ToString()
                }))
            .ConfigureServices((_, services) => services.AddScoped<CapOperationWork>());
        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    private static string FindRepositoryRoot()
    {
        var testsDirectory = Path.GetDirectoryName(FindSampleTestsProjectFile())!;
        return Path.GetFullPath(Path.Combine(testsDirectory, "..", ".."));
    }

    private static int GetAvailableTcpPort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        try
        {
            listener.Start();
            return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static ChildProcess StartSampleProcess(string repositoryRoot, string projectRelativePath,
        params string[] applicationArguments)
    {
        var projectPath = Path.GetFullPath(Path.Combine(repositoryRoot,
            projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--no-restore");
        startInfo.ArgumentList.Add("--no-launch-profile");
        if (applicationArguments.Length != 0)
        {
            startInfo.ArgumentList.Add("--");
            foreach (var argument in applicationArguments)
                startInfo.ArgumentList.Add(argument);
        }

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException(
            $"无法启动 Sample 子进程：{Path.GetFileName(projectPath)}。");
        return new ChildProcess(process, DrainAsync(process.StandardOutput), DrainAsync(process.StandardError));
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        while (await reader.ReadLineAsync() != null)
        {
        }
    }

    private static async Task StopSampleProcessAsync(ChildProcess child)
    {
        if (child == null)
            return;

        try
        {
            if (!child.Process.HasExited)
                child.Process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) when (child.Process.HasExited)
        {
        }

        await child.Process.WaitForExitAsync();
        await Task.WhenAll(child.StandardOutputDrain, child.StandardErrorDrain);
        child.Process.Dispose();
    }

    private static async Task WaitForHttpHostAsync(HttpClient client, Process process)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            Assert.False(process.HasExited, "CAP producer host 在 HTTP 监听就绪前退出。");
            try
            {
                using var response = await client.GetAsync(string.Empty);
                return;
            }
            catch (HttpRequestException)
            {
                await Task.Delay(200);
            }
            catch (TaskCanceledException)
            {
                await Task.Delay(200);
            }
        }

        Assert.Fail("CAP producer host 在45秒内未开始监听 HTTP。");
    }

    private static async Task WaitForConsumerSchemaAsync(TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await ConsumerTableExistsAsync(OrdersConnectionString) &&
                await ConsumerTableExistsAsync(ReportingConnectionString))
                return;
            await Task.Delay(250);
        }

        Assert.Fail("独立 CAP consumer 未能在限定时间内建立两个 MySQL inbox schema。");
    }

    private static async Task<bool> ConsumerTableExistsAsync(string connectionString)
    {
        try
        {
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM information_schema.tables " +
                                  "WHERE table_schema = DATABASE() AND table_name = 'CapConsumerInbox'";
            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        }
        catch (MySqlException)
        {
            return false;
        }
    }

    private static async Task<string> ReadOperationIdAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("operationId").GetString() ??
               throw new InvalidOperationException("Sample operation 响应未包含 operationId。");
    }

    private static async Task WaitForOperationStatusAsync(HttpClient client, string operationId, string status,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync($"api/operations/{operationId}");
            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (string.Equals(document.RootElement.GetProperty("status").GetString(), status,
                        StringComparison.Ordinal))
                    return;
            }
            await Task.Delay(150);
        }

        Assert.Fail($"Sample operation {operationId} 未在限定时间内达到状态 {status}。");
    }

    private async Task<string> WaitForCancellationScenarioAsync(string journalPath,
        Task<HttpResponseMessage> request, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (request.IsFaulted)
                await request;
            if (request.IsCanceled)
                throw new OperationCanceledException("CAP cancellation request ended before the test cancelled it.");

            try
            {
                if (File.Exists(journalPath))
                {
                    var lines = File.ReadAllLines(journalPath, Encoding.UTF8);
                    for (var lineIndex = lines.Length - 1; lineIndex >= 0; lineIndex--)
                    {
                        try
                        {
                            var operation = JsonSerializer.Deserialize<ScenarioOperationSnapshot>(lines[lineIndex],
                                serializerOptions);
                            if (operation?.Scenario == SqlUnitOfWorkScenarios.OperationCapCancellation &&
                                operation.ProcessSteps?.Any(step =>
                                    step.Step == "operation-waiting-for-cancellation") == true)
                                return operation.OperationId;
                        }
                        catch (JsonException)
                        {
                        }
                    }
                }
            }
            catch (IOException)
            {
            }

            await Task.Delay(100);
        }

        var current = ReadLatestOperationSnapshot(journalPath,
            SqlUnitOfWorkScenarios.OperationCapCancellation);
        var databaseStates = await ReadMySqlProcessStatesAsync();
        var steps = string.Join(",", current?.ProcessSteps?.Select(step => step.Step) ??
            Array.Empty<string>());
        Assert.Fail($"CAP cancellation scenario 未完成两条 Outbox 写入并进入可取消等待; " +
                    $"status={current?.Status ?? "missing"}; steps={steps}; db={string.Join(" | ", databaseStates)}");
        return string.Empty;
    }

    private static ScenarioOperationSnapshot ReadLatestOperationSnapshot(string journalPath, string scenario)
    {
        if (!File.Exists(journalPath))
            return null;

        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var lines = File.ReadAllLines(journalPath, Encoding.UTF8);
        for (var lineIndex = lines.Length - 1; lineIndex >= 0; lineIndex--)
        {
            try
            {
                var operation = JsonSerializer.Deserialize<ScenarioOperationSnapshot>(lines[lineIndex],
                    serializerOptions);
                if (operation?.Scenario == scenario)
                    return operation;
            }
            catch (JsonException)
            {
            }
        }

        return null;
    }

    private static async Task<string[]> ReadMySqlProcessStatesAsync()
    {
        var results = new List<string>();
        foreach (var (name, connectionString) in new[]
                 {
                     ("orders", OrdersConnectionString),
                     ("reporting", ReportingConnectionString)
                 })
        {
            try
            {
                var builder = new MySqlConnectionStringBuilder(connectionString)
                {
                    Pooling = false,
                    ConnectionTimeout = 3
                };
                await using var connection = new MySqlConnection(builder.ConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandTimeout = 3;
                command.CommandText = "SELECT ID, COMMAND, TIME, STATE FROM information_schema.PROCESSLIST " +
                                      "WHERE DB = DATABASE() ORDER BY TIME DESC LIMIT 12";
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add($"{name}:thread={reader.GetUInt64(0)},command={reader.GetString(1)}," +
                                $"seconds={reader.GetInt64(2)},state={(reader.IsDBNull(3) ? "null" : reader.GetString(3))}");
                }
            }
            catch (MySqlException)
            {
                results.Add($"{name}:process-state-unavailable");
            }
        }

        return results.ToArray();
    }

    private async Task AssertCapOperationDeliveredAsync(string operationId, string scenario)
    {
        Assert.Collection(await ReadOperationRowsAsync(operationId),
            row => Assert.Equal(new OperationOrderRow($"{operationId}:operation", operationId,
                "operation", 1, "operation"), row),
            row => Assert.Equal(new OperationOrderRow($"{operationId}:ef", operationId,
                "ef-operation", 2, "tracked-only"), row));
        Assert.Equal(new ReportRow($"{operationId}:reporting", operationId, "operation-report"),
            Assert.Single(await ReadReportRowsAsync(operationId)));

        foreach (var source in new[] { "orders", "reporting" })
        {
            var eventId = $"{operationId}:{source}-operation";
            var connectionString = source == "orders" ? OrdersConnectionString : ReportingConnectionString;
            Assert.Equal(1, await ReadOutboxCountAsync(connectionString, eventId));
            var businessEvent = Assert.Single(await ReadBusinessEventsAsync(connectionString, eventId));
            Assert.Equal(new BusinessEventRow(eventId, operationId, operationId, businessEvent.RootId,
                source, source == "orders" ? SqlUnitOfWorkEventTopics.Orders :
                    SqlUnitOfWorkEventTopics.Reporting, scenario), businessEvent);
            Assert.False(string.IsNullOrWhiteSpace(businessEvent.RootId));
            await WaitForConsumerDeliveryAsync(source, eventId, TimeSpan.FromSeconds(45));
            await AssertCapDeliveryAsync(source, eventId, operationId, scenario);
        }
    }

    private static async Task AssertCapOperationHasNoCommittedRowsAsync(string operationId)
    {
        Assert.Empty(await ReadOperationRowsAsync(operationId));
        Assert.Empty(await ReadReportRowsAsync(operationId));
        foreach (var source in new[] { "orders", "reporting" })
        {
            var eventId = $"{operationId}:{source}-operation";
            var connectionString = source == "orders" ? OrdersConnectionString : ReportingConnectionString;
            Assert.Equal(0, await ReadOutboxCountAsync(connectionString, eventId));
            Assert.Empty(await ReadBusinessEventsAsync(connectionString, eventId));
            Assert.Equal(0, await ReadConsumerDeliveryCountAsync(source, eventId));
            Assert.Empty(await ReadConsumerAttemptStatusesAsync(source, eventId));
        }
    }

    private static async Task WaitForConsumerDeliveryAsync(string source, string eventId, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await ReadConsumerDeliveryCountAsync(source, eventId) == 2)
                return;
            await Task.Delay(200);
        }

        Assert.Fail($"独立 consumer 未处理 source={source} 的 CAP event {eventId}。");
    }

    private static async Task AssertCapDeliveryAsync(string source, string eventId, string operationId,
        string expectedScenario)
    {
        var connectionString = source == "orders" ? OrdersConnectionString : ReportingConnectionString;
        var businessEvent = Assert.Single(await ReadBusinessEventsAsync(connectionString, eventId));
        Assert.Equal(operationId, businessEvent.OperationId);
        Assert.Equal(operationId, businessEvent.TraceId);
        Assert.Equal(source, businessEvent.Source);
        Assert.Equal(expectedScenario, businessEvent.Scenario);
        Assert.False(string.IsNullOrWhiteSpace(businessEvent.RootId));

        var rows = await ReadConsumerDeliveryRowsAsync(source, eventId);
        var row = Assert.Single(rows);
        Assert.Equal(source, row.Source);
        Assert.Equal(eventId, row.EventId);
        Assert.Equal(source == "orders" ? "sqluow-orders-consumer" : "sqluow-reporting-consumer",
            row.ConsumerName);
        Assert.Equal(operationId, row.OperationId);
        Assert.Equal(operationId, row.TraceId);
        Assert.False(string.IsNullOrWhiteSpace(row.RootId));
        Assert.Equal(businessEvent.RootId, row.RootId);
        Assert.Equal(1, row.AttemptCount);
        Assert.Equal($"applied:{source}", row.EffectValue);
        Assert.Equal(new[] { "Succeeded" }, await ReadConsumerAttemptStatusesAsync(source, eventId));
    }

    private static async Task<int> ReadOrderCountAsync(string id)
    {
        await using var connection = new MySqlConnection(OrdersConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM OrderRecords WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<OperationOrderRow[]> ReadOperationRowsAsync(string operationId)
    {
        await using var connection = new MySqlConnection(OrdersConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, OperationId, Layer, Sequence, Value FROM OrderRecords " +
                             "WHERE OperationId = @operationId ORDER BY Sequence, Id";
        command.Parameters.AddWithValue("@operationId", operationId);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<OperationOrderRow>();
        while (await reader.ReadAsync())
        {
            rows.Add(new OperationOrderRow(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetInt32(3), reader.GetString(4)));
        }
        return rows.ToArray();
    }

    private static async Task<int> ReadOutboxCountAsync(string eventId)
        => await ReadOutboxCountAsync(OrdersConnectionString, eventId);

    private static async Task<int> ReadOutboxCountAsync(string connectionString, string eventId)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM `cap.published` WHERE Content LIKE @eventPattern";
        command.Parameters.AddWithValue("@eventPattern", $"%{eventId}%");
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> ReadReportCountAsync(string operationId)
    {
        await using var connection = new MySqlConnection(ReportingConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ReportRecords WHERE OperationId = @operationId";
        command.Parameters.AddWithValue("@operationId", operationId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<ReportRow[]> ReadReportRowsAsync(string operationId)
    {
        await using var connection = new MySqlConnection(ReportingConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, OperationId, Value FROM ReportRecords WHERE OperationId = @operationId";
        command.Parameters.AddWithValue("@operationId", operationId);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<ReportRow>();
        while (await reader.ReadAsync())
            rows.Add(new ReportRow(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        return rows.ToArray();
    }

    private static async Task<int> ReadBusinessEventCountAsync(string connectionString, string eventId)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM CapBusinessEvents WHERE EventId = @eventId";
        command.Parameters.AddWithValue("@eventId", eventId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<BusinessEventRow[]> ReadBusinessEventsAsync(string connectionString, string eventId)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EventId, OperationId, TraceId, RootId, Source, Topic, Scenario " +
                              "FROM CapBusinessEvents WHERE EventId = @eventId";
        command.Parameters.AddWithValue("@eventId", eventId);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<BusinessEventRow>();
        while (await reader.ReadAsync())
            rows.Add(new BusinessEventRow(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6)));
        return rows.ToArray();
    }

    private static async Task<int> ReadConsumerDeliveryCountAsync(string source, string eventId)
    {
        var connectionString = source == "orders" ? OrdersConnectionString : ReportingConnectionString;
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT
            (SELECT COUNT(*) FROM CapConsumerInbox WHERE Source = @source AND EventId = @eventId) +
            (SELECT COUNT(*) FROM CapConsumerEffects WHERE Source = @source AND EventId = @eventId)";
        command.Parameters.AddWithValue("@source", source);
        command.Parameters.AddWithValue("@eventId", eventId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<ConsumerDeliveryRow[]> ReadConsumerDeliveryRowsAsync(string source, string eventId)
    {
        var connectionString = source == "orders" ? OrdersConnectionString : ReportingConnectionString;
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT i.Source, i.EventId, i.ConsumerName, i.OperationId, i.TraceId,
                i.RootId, i.AttemptCount, e.EffectValue
            FROM CapConsumerInbox i
            INNER JOIN CapConsumerEffects e ON e.Source = i.Source AND e.EventId = i.EventId
                AND e.ConsumerName = i.ConsumerName
            WHERE i.Source = @source AND i.EventId = @eventId";
        command.Parameters.AddWithValue("@source", source);
        command.Parameters.AddWithValue("@eventId", eventId);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<ConsumerDeliveryRow>();
        while (await reader.ReadAsync())
            rows.Add(new ConsumerDeliveryRow(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetInt32(6),
                reader.GetString(7)));
        return rows.ToArray();
    }

    private static async Task<string[]> ReadConsumerAttemptStatusesAsync(string source, string eventId)
    {
        var connectionString = source == "orders" ? OrdersConnectionString : ReportingConnectionString;
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Status FROM CapConsumerAttempts WHERE Source = @source " +
                              "AND EventId = @eventId ORDER BY AttemptId";
        command.Parameters.AddWithValue("@source", source);
        command.Parameters.AddWithValue("@eventId", eventId);
        await using var reader = await command.ExecuteReaderAsync();
        var statuses = new List<string>();
        while (await reader.ReadAsync())
            statuses.Add(reader.GetString(0));
        return statuses.ToArray();
    }

    private static async Task<string[]> ReadNativeCapMessageIdsAsync(string connectionString, string eventId)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM `cap.published` WHERE Content LIKE @eventPattern ORDER BY Id";
        command.Parameters.AddWithValue("@eventPattern", $"%{eventId}%");
        await using var reader = await command.ExecuteReaderAsync();
        var ids = new List<string>();
        while (await reader.ReadAsync())
            ids.Add(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture)!);
        return ids.ToArray();
    }

    private static async Task<long[]> SeedConsumerDiagnosticRowsAsync(string connectionString, string operationId,
        string eventId, string traceId, string rootId, DateTime createdAt)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand())
        {
            create.CommandText = @"CREATE TABLE IF NOT EXISTS CapConsumerEffects (
                Source VARCHAR(64) NOT NULL,
                EventId VARCHAR(191) NOT NULL,
                ConsumerName VARCHAR(191) NOT NULL,
                OperationId VARCHAR(191) NOT NULL,
                EffectValue VARCHAR(191) NOT NULL,
                AppliedAt DATETIME(6) NOT NULL,
                PRIMARY KEY (Source, EventId, ConsumerName)
            ) ENGINE=InnoDB;
            CREATE TABLE IF NOT EXISTS CapConsumerAttempts (
                AttemptId BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                Source VARCHAR(64) NOT NULL,
                EventId VARCHAR(191) NOT NULL,
                OperationId VARCHAR(191) NOT NULL,
                TraceId VARCHAR(191) NOT NULL,
                RootId VARCHAR(64) NOT NULL,
                ConsumerName VARCHAR(191) NOT NULL,
                Status VARCHAR(32) NOT NULL,
                ErrorText VARCHAR(500) NULL,
                CreatedAt DATETIME(6) NOT NULL,
                INDEX IX_CapConsumerAttempts_Event (Source, EventId, AttemptId)
            ) ENGINE=InnoDB;";
            await create.ExecuteNonQueryAsync();
        }

        await using var insert = connection.CreateCommand();
        insert.CommandText = @"INSERT INTO CapConsumerAttempts
                (Source, EventId, OperationId, TraceId, RootId, ConsumerName, Status, ErrorText, CreatedAt)
            VALUES
                ('orders', @eventId, @operationId, @traceId, @rootId, @consumerName, 'Failed',
                    'contract retry failure', @failedAt),
                ('orders', @eventId, @operationId, @traceId, @rootId, @consumerName, 'Succeeded',
                    NULL, @succeededAt);
            INSERT INTO CapConsumerEffects
                (Source, EventId, ConsumerName, OperationId, EffectValue, AppliedAt)
            VALUES ('orders', @eventId, @consumerName, @operationId, 'one-effect', @succeededAt);";
        insert.Parameters.AddWithValue("@eventId", eventId);
        insert.Parameters.AddWithValue("@operationId", operationId);
        insert.Parameters.AddWithValue("@traceId", traceId);
        insert.Parameters.AddWithValue("@rootId", rootId);
        insert.Parameters.AddWithValue("@consumerName", "sqluow-orders-consumer.contract");
        insert.Parameters.AddWithValue("@failedAt", createdAt.AddSeconds(-1));
        insert.Parameters.AddWithValue("@succeededAt", createdAt);
        await insert.ExecuteNonQueryAsync();

        await using var select = connection.CreateCommand();
        select.CommandText = "SELECT AttemptId FROM CapConsumerAttempts WHERE Source = 'orders' " +
                             "AND EventId = @eventId ORDER BY AttemptId";
        select.Parameters.AddWithValue("@eventId", eventId);
        await using var reader = await select.ExecuteReaderAsync();
        var attemptIds = new List<long>();
        while (await reader.ReadAsync())
            attemptIds.Add(reader.GetInt64(0));
        return attemptIds.ToArray();
    }

    private sealed class OperationScopeConstructionFailureTracker
    {
        public InvalidOperationException PublisherConstructionException { get; } =
            new("review-publisher-construction-failure");
        public InvalidOperationException AsyncDisposeException { get; } =
            new("review-scope-async-dispose-failure");
        public bool FailAsyncDispose { get; set; }
        public bool CaptureSynchronizationContextOnDispose { get; set; }
        public int SyncCreatedCount { get; set; }
        public int SyncDisposedCount { get; set; }
        public int AsyncCreatedCount { get; set; }
        public int AsyncDisposedCount { get; set; }
        public SynchronizationContext AsyncDisposeSynchronizationContext { get; set; }
        public SynchronizationContext AsyncDisposeContinuationContext { get; set; }
        public int AsyncDisposeContinuationThreadId { get; set; }
    }

    private sealed class OperationScopeSyncDependency : IDisposable
    {
        private readonly OperationScopeConstructionFailureTracker _tracker;

        public OperationScopeSyncDependency(OperationScopeConstructionFailureTracker tracker)
        {
            _tracker = tracker;
            _tracker.SyncCreatedCount++;
        }

        public void Dispose() => _tracker.SyncDisposedCount++;
    }

    private sealed class OperationScopeAsyncDependency : IAsyncDisposable
    {
        private readonly OperationScopeConstructionFailureTracker _tracker;

        public OperationScopeAsyncDependency(OperationScopeConstructionFailureTracker tracker)
        {
            _tracker = tracker;
            _tracker.AsyncCreatedCount++;
        }

        public async ValueTask DisposeAsync()
        {
            _tracker.AsyncDisposedCount++;
            if (_tracker.CaptureSynchronizationContextOnDispose)
            {
                _tracker.AsyncDisposeSynchronizationContext = SynchronizationContext.Current;
                await Task.Yield();
                _tracker.AsyncDisposeContinuationContext = SynchronizationContext.Current;
                _tracker.AsyncDisposeContinuationThreadId = Environment.CurrentManagedThreadId;
            }

            if (_tracker.FailAsyncDispose)
                throw _tracker.AsyncDisposeException;
        }
    }

    private sealed class SingleThreadPumpSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object State)> _callbacks = new();

        public override void Post(SendOrPostCallback callback, object state) =>
            _callbacks.Add((callback, state));

        public void PumpUntil(Task task, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (!task.IsCompleted)
            {
                var remaining = timeout - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero ||
                    !_callbacks.TryTake(out var work, remaining))
                {
                    if (!task.IsCompleted)
                        throw new TimeoutException("单线程上下文未能在有界时间内完成异步清理。");
                    break;
                }

                work.Callback(work.State);
            }
        }

        public void Dispose() => _callbacks.Dispose();
    }

}

public sealed class CapMySqlFactAttribute : FactAttribute
{
    public CapMySqlFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_CAP_MYSQL_TESTS"), "1",
                StringComparison.Ordinal))
            Skip = "需要任务专属 MySQL/RabbitMQ Compose 服务和 RUN_CAP_MYSQL_TESTS=1。";
    }
}

public sealed class CapMySqlTheoryAttribute : TheoryAttribute
{
    public CapMySqlTheoryAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_CAP_MYSQL_TESTS"), "1",
                StringComparison.Ordinal))
            Skip = "需要任务专属 MySQL/RabbitMQ Compose 服务和 RUN_CAP_MYSQL_TESTS=1。";
    }
}

internal sealed class RecordingHostedService : IHostedService, IDisposable
{
    private readonly string _name;
    private readonly ICollection<string> _lifecycle;
    private readonly bool _failStart;
    private readonly bool _failStop;

    public RecordingHostedService(string name, ICollection<string> lifecycle, bool failStart = false,
        bool failStop = false)
    {
        _name = name;
        _lifecycle = lifecycle;
        _failStart = failStart;
        _failStop = failStop;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _lifecycle.Add($"start:{_name}");
        return _failStart
            ? Task.FromException(new InvalidOperationException($"start:{_name}"))
            : Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _lifecycle.Add($"stop:{_name}");
        return _failStop
            ? Task.FromException(new InvalidOperationException($"stop:{_name}"))
            : Task.CompletedTask;
    }

    public void Dispose() => _lifecycle.Add($"dispose:{_name}");
}

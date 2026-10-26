#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bing.Biz.FileManagement;
using Bing.FileStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Xunit;

#if BING_ADMIN_EFCORE_TESTS
namespace Bing.Admin.EFCore.Tests;
#else
namespace Bing.Admin.FreeSQL.Tests;
#endif

[Collection("AdminFileHostIsolation")]
public sealed class AdminFileGovernanceTests
{
    [AdminFileManagementMySqlFact]
    public async Task MySqlGovernance_ReceiptsReplayPagingAndHistoryAreScopedAndRestorable()
    {
        var connectionString = Environment.GetEnvironmentVariable("BING_FILE_MANAGEMENT_MYSQL_TEST_CONNECTION")!;
        await using var database = await AdminFileMySqlTestDatabase.CreateAsync(connectionString);
        await using var environment = new AdminFileHttpEnvironment(database.DatabaseConnectionString);
        var success = new GovernanceHandler("governance-success");
        var failure = new GovernanceHandler("governance-failure") { Fail = true };
        var services = new ServiceCollection();
        environment.Configure(services);
        services.AddSingleton<IFileLifecycleEventHandler>(success);
        services.AddSingleton<IFileLifecycleEventHandler>(failure);
        services.AddScoped<FileOperationProcessor>(provider => new FileOperationProcessor(
            provider.GetRequiredService<IFileManagementStore>(),
            provider.GetRequiredService<IFileStorageFactory>(),
            provider.GetRequiredService<FileMigrationService>(),
            provider.GetRequiredService<FileManagementOptions>(),
            provider.GetRequiredService<ILogger<FileOperationProcessor>>(),
            outbox: null));
        await using var provider = services.BuildServiceProvider();
        await environment.InitializeDatabaseAsync(provider);

        using var scope = provider.CreateScope();
        var scoped = scope.ServiceProvider;
        var files = scoped.GetRequiredService<FileManagementService>();
        var store = scoped.GetRequiredService<IFileManagementStore>();
        var administration = scoped.GetRequiredService<FileGovernanceAdministration>();
        var dispatcher = scoped.GetRequiredService<FileOutboxDispatcher>();
        var tenantScope = StorageScope.ForTenant(environment.Identity.Tenant).Key;
        var otherScope = StorageScope.ForTenant("tenant-other").Key;

        var firstFileId = await UploadReadyAsync(files, environment, provider, "governance-first.bin");
        var secondFileId = await UploadReadyAsync(files, environment, provider, "governance-second.bin");
        Assert.Equal(FileState.Ready, (await store.FindAsync(firstFileId))!.File.State);
        Assert.Equal(FileState.Ready, (await store.FindAsync(secondFileId))!.File.State);

        await dispatcher.DispatchAsync();
        var firstPage = await administration.ListEventsAsync(new FileEventQuery { ScopeKey = tenantScope, Take = 1 });
        var secondPage = await administration.ListEventsAsync(new FileEventQuery { ScopeKey = tenantScope, Skip = 1, Take = 1 });
        Assert.Single(firstPage);
        Assert.Single(secondPage);
        Assert.NotEqual(firstPage[0].Id, secondPage[0].Id);
        Assert.Empty(await administration.ListEventsAsync(new FileEventQuery { ScopeKey = otherScope, Take = 20 }));
        Assert.Empty((await administration.PreviewAsync(otherScope, FileHistoryKind.Event)).Items);

        var pendingEvents = await administration.ListEventsAsync(new FileEventQuery
        {
            ScopeKey = tenantScope,
            FailedOnly = true,
            Take = 100
        });
        Assert.NotEmpty(pendingEvents);
        var pendingEvent = pendingEvents[0];
        var initialDetails = await administration.GetEventAsync(tenantScope, pendingEvent.Id);
        Assert.Equal(2, initialDetails.Deliveries.Count);
        Assert.True(initialDetails.Deliveries.Single(x => x.Consumer == success.Name).Delivered);
        Assert.False(initialDetails.Deliveries.Single(x => x.Consumer == failure.Name).Delivered);
        Assert.Equal("handler_failed", initialDetails.Deliveries.Single(x => x.Consumer == failure.Name).ErrorCode);
        var notFound = await Assert.ThrowsAsync<FileManagementException>(() =>
            administration.GetEventAsync(otherScope, pendingEvent.Id));
        Assert.Equal("not_found", notFound.Code);

        await SetPendingEventsDueAsync(database.DatabaseConnectionString);
        var leaseNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Assert.True(await store.TryClaimEventAsync(pendingEvent.Id, pendingEvent.Version, "governance-active-lease",
            leaseNow, leaseNow + 60_000));
        var leasedEvent = (await store.FindEventAsync(pendingEvent.Id))!;
        var blockedUpdate = Clone(leasedEvent);
        blockedUpdate.Version++;
        var blockedAudit = new FileGovernanceAudit
        {
            Id = Guid.NewGuid(),
            ScopeKey = tenantScope,
            ActorId = environment.Identity.UserId,
            Action = "event-replay",
            TargetId = leasedEvent.Id,
            Reason = "active lease CAS check",
            CreatedAt = leaseNow,
            RequestJson = "{}",
            ResultJson = "{}"
        };
        Assert.False(await store.TryUpdateEventDeliveryAsync(leasedEvent, blockedUpdate,
            await store.ListEventDeliveriesAsync(leasedEvent.Id), null, blockedAudit));
        Assert.True(await store.FinishEventAsync(leasedEvent.Id, "governance-active-lease", false,
            leaseNow, "consumer_pending"));

        var replayTarget = (await store.FindEventAsync(pendingEvent.Id))!;
        var replay = new ReplayFileEvent
        {
            RequestId = Guid.NewGuid(),
            Consumer = failure.Name,
            ExpectedVersion = replayTarget.Version,
            Reason = "replay failed consumer"
        };
        var replayAudit = await administration.ReplayAsync(tenantScope, environment.Identity.UserId, replayTarget.Id, replay);
        var repeatedReplay = await administration.ReplayAsync(tenantScope, environment.Identity.UserId, replayTarget.Id, replay);
        Assert.Equal(replayAudit.ResultJson, repeatedReplay.ResultJson);
        Assert.Single(await administration.ListAuditAsync(tenantScope, replayTarget.Id, 0, 10));
        failure.Fail = false;
        await dispatcher.DispatchAsync();
        var deliveredDetails = await administration.GetEventAsync(tenantScope, replayTarget.Id);
        Assert.True(deliveredDetails.Event.Delivered);
        Assert.All(deliveredDetails.Deliveries, receipt => Assert.True(receipt.Delivered));

        var operationPageOne = await store.QueryOperationsAsync(new FileOperationQuery
        {
            ScopeKey = tenantScope,
            Skip = 0,
            Take = 1
        });
        var operationPageTwo = await store.QueryOperationsAsync(new FileOperationQuery
        {
            ScopeKey = tenantScope,
            Skip = 1,
            Take = 1
        });
        Assert.Single(operationPageOne);
        Assert.Single(operationPageTwo);
        Assert.NotEqual(operationPageOne[0].Id, operationPageTwo[0].Id);

        var migration = scoped.GetRequiredService<FileMigrationBatchService>();
        var batch = await migration.CreateAsync(StorageScope.ForTenant(environment.Identity.Tenant), environment.Identity.UserId,
            new CreateMigrationBatch
            {
                RequestId = Guid.NewGuid(),
                SourceStore = "admin-test-local",
                TargetStore = "admin-test-target",
                Container = "attachments"
            });
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await environment.RunOperationsAsync(provider);
            await migration.AdvanceAsync();
        }
        Assert.Equal(FileMigrationBatchState.Completed, (await migration.GetAsync(tenantScope, batch.Id)).Batch.State);

        var oldTime = DateTimeOffset.UtcNow.AddDays(-120).ToUnixTimeMilliseconds();
        await SetHistoryTimesAsync(database.DatabaseConnectionString, oldTime);

        var operationPreview = await administration.PreviewAsync(tenantScope, FileHistoryKind.Operation, take: 100);
        Assert.True(operationPreview.Items.Count >= 2);
        var operationCandidate = operationPreview.Items[0];
        var operationSelection = SingleSelection(operationPreview, operationCandidate);
        var operationRequest = new FileGovernanceRequest { RequestId = Guid.NewGuid(), Reason = "archive operation" };
        await administration.ArchiveAsync(tenantScope, environment.Identity.UserId, operationSelection, operationRequest);
        Assert.DoesNotContain(await store.QueryOperationsAsync(new FileOperationQuery { ScopeKey = tenantScope, Take = 100 }),
            operation => operation.Id == operationCandidate.Id);
        var operationMarker = Assert.Single(await administration.ListArchivesAsync(tenantScope, FileHistoryKind.Operation, 0, 100));
        await administration.RestoreAsync(tenantScope, environment.Identity.UserId, operationMarker.Id,
            new FileGovernanceRequest { RequestId = Guid.NewGuid(), Reason = "restore operation" });
        Assert.Contains(await store.QueryOperationsAsync(new FileOperationQuery { ScopeKey = tenantScope, Take = 100 }),
            operation => operation.Id == operationCandidate.Id);

        var eventPreview = await administration.PreviewAsync(tenantScope, FileHistoryKind.Event, take: 100);
        var eventCandidate = Assert.Single(eventPreview.Items, item => item.Id == replayTarget.Id);
        var eventSelection = SingleSelection(eventPreview, eventCandidate);
        var eventRequest = new FileGovernanceRequest { RequestId = Guid.NewGuid(), Reason = "archive event" };
        var eventArchiveAudit = await administration.ArchiveAsync(tenantScope, environment.Identity.UserId,
            eventSelection, eventRequest);
        Assert.Equal(eventArchiveAudit.ResultJson, (await administration.ArchiveAsync(tenantScope,
            environment.Identity.UserId, eventSelection, eventRequest)).ResultJson);
        Assert.DoesNotContain(await administration.ListEventsAsync(new FileEventQuery { ScopeKey = tenantScope, Take = 100 }),
            item => item.Id == replayTarget.Id);
        var eventMarker = Assert.Single(await administration.ListArchivesAsync(tenantScope, FileHistoryKind.Event, 0, 100));
        var eventRestore = new FileGovernanceRequest { RequestId = Guid.NewGuid(), Reason = "restore event" };
        await administration.RestoreAsync(tenantScope, environment.Identity.UserId, eventMarker.Id, eventRestore);
        await administration.RestoreAsync(tenantScope, environment.Identity.UserId, eventMarker.Id, eventRestore);
        Assert.Contains(await administration.ListEventsAsync(new FileEventQuery { ScopeKey = tenantScope, Take = 100 }),
            item => item.Id == replayTarget.Id);

        var batchPreview = await administration.PreviewAsync(tenantScope, FileHistoryKind.MigrationBatch, take: 100);
        var batchCandidate = Assert.Single(batchPreview.Items, item => item.Id == batch.Id);
        await administration.ArchiveAsync(tenantScope, environment.Identity.UserId, SingleSelection(batchPreview, batchCandidate),
            new FileGovernanceRequest { RequestId = Guid.NewGuid(), Reason = "archive completed batch" });
        Assert.Empty(await store.ListBatchesAsync(tenantScope, 0, 100));
        var batchMarker = Assert.Single(await administration.ListArchivesAsync(tenantScope, FileHistoryKind.MigrationBatch, 0, 100));
        await administration.RestoreAsync(tenantScope, environment.Identity.UserId, batchMarker.Id,
            new FileGovernanceRequest { RequestId = Guid.NewGuid(), Reason = "restore completed batch" });
        Assert.Contains(await store.ListBatchesAsync(tenantScope, 0, 100), item => item.Id == batch.Id);
        Assert.NotNull(await store.FindOperationAsync(operationCandidate.Id, tenantScope));
        Assert.NotNull(await store.FindEventAsync(replayTarget.Id));
        Assert.NotNull(await store.FindBatchAsync(batch.Id, tenantScope));
    }

    private static async Task<Guid> UploadReadyAsync(FileManagementService files, AdminFileHttpEnvironment environment,
        IServiceProvider provider, string fileName)
    {
        var session = await files.CreateUploadAsync(new CreateFileUpload
        {
            RequestId = Guid.NewGuid(),
            FileName = fileName,
            Length = 0,
            ContentType = "application/octet-stream"
        });
        await environment.RunOperationsAsync(provider);
        await files.CompleteUploadAsync(session.Id);
        await environment.RunOperationsAsync(provider);
        return session.FileId;
    }

    private static FileHistorySelection SingleSelection(FileHistorySelection preview, FileHistoryCandidate candidate)
        => new() { Kind = preview.Kind, Cutoff = preview.Cutoff, Items = new[] { candidate } };

    private static FileOutboxEvent Clone(FileOutboxEvent value) => new()
    {
        Id = value.Id,
        FileId = value.FileId,
        ScopeKey = value.ScopeKey,
        FileVersion = value.FileVersion,
        Kind = value.Kind,
        CreatedAt = value.CreatedAt,
        NextAttemptAt = value.NextAttemptAt,
        Attempts = value.Attempts,
        LeaseToken = value.LeaseToken,
        LeaseUntil = value.LeaseUntil,
        Delivered = value.Delivered,
        Version = value.Version,
        ErrorCode = value.ErrorCode
    };

    private static async Task SetHistoryTimesAsync(string connectionString, long time)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await UpdateTimeAsync(connection, "UPDATE BingFileOperations SET FinishedAt = @historyTime WHERE FinishedAt > 0", time);
        await UpdateTimeAsync(connection, "UPDATE BingFileOutboxEvents SET CreatedAt = @historyTime", time);
        await UpdateTimeAsync(connection, "UPDATE BingFileMigrationBatches SET CreatedAt = @historyTime", time);
    }

    private static async Task SetPendingEventsDueAsync(string connectionString)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await UpdateTimeAsync(connection, "UPDATE BingFileOutboxEvents SET NextAttemptAt = @historyTime WHERE Delivered = 0", 0);
    }

    private static async Task UpdateTimeAsync(MySqlConnection connection, string sql, long time)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@historyTime", time);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class GovernanceHandler : IFileLifecycleEventHandler
    {
        public GovernanceHandler(string name) => Name = name;
        public string Name { get; }
        public bool Fail { get; set; }

        public Task HandleAsync(FileLifecycleEvent message, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (Fail) throw new IOException("intentional governance test failure");
            return Task.CompletedTask;
        }
    }
}

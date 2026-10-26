#nullable enable

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Bing.Admin.Apis.Commons;
using Bing.Biz.FileManagement;
using Bing.Biz.FileManagement.AspNetCore;
using Bing.FileStorage;
using Bing.FileStorage.FileSystem;
using Bing.MultiTenancy;
using Bing.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Xunit;

#if BING_ADMIN_EFCORE_TESTS
using Bing.Biz.FileManagement.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace Bing.Admin.EFCore.Tests;
#else
using Bing.Biz.FileManagement.FreeSql;
using FreeSql;
namespace Bing.Admin.FreeSQL.Tests;
#endif

/// <summary>通过 TestServer 验证实际 Admin 文件控制器与 SQLite、本地存储的 HTTP 闭环。</summary>
public sealed class AdminFileControllerHttpTests
{
    [Fact]
    public Task RealAdminController_Upload_Bind_Download_Range_TenantIsolation_AndDelete()
        => RunHttpCycleAsync(mysqlConnectionString: null, verifyMySqlCasAndClaim: false);

    [AdminFileManagementMySqlFact]
    public async Task RealAdminController_MySql_Upload_Bind_Download_Range_TenantIsolation_Cas_Claim_AndDelete()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable("BING_FILE_MANAGEMENT_MYSQL_TEST_CONNECTION")!;
        await using var database = await AdminFileMySqlTestDatabase.CreateAsync(serverConnectionString);
        await RunHttpCycleAsync(database.DatabaseConnectionString, verifyMySqlCasAndClaim: true);
    }

    private async Task RunHttpCycleAsync(string? mysqlConnectionString, bool verifyMySqlCasAndClaim)
    {
        await using var environment = new AdminFileHttpEnvironment(mysqlConnectionString);
        using var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(environment.Configure)
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            }));
        await environment.InitializeDatabaseAsync(server.Services);

        using var client = server.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/files")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "admin-file-test");

        var bytes = new byte[] { 11, 22, 33, 44, 55 };
        var create = await client.PostAsJsonAsync("/api/files/upload-sessions", new CreateFileUpload
        {
            RequestId = Guid.NewGuid(),
            FileName = "中文 附件.txt",
            ContentType = "text/plain",
            Container = "attachments",
            Length = bytes.Length,
            LastModified = 123456
        });
        Assert.Equal(HttpStatusCode.Accepted, create.StatusCode);
        var session = (await create.Content.ReadFromJsonAsync<UploadSessionView>())!;
        await environment.RunOperationsAsync(server.Services);

        var queriedSession = await client.GetFromJsonAsync<UploadSessionView>($"/api/files/upload-sessions/{session.Id}");
        Assert.Equal(FileState.Uploading, queriedSession!.State);
        Assert.Empty(queriedSession.Parts);
        Assert.Equal(session.Id, Assert.Single((await client.GetFromJsonAsync<UploadSessionSummary[]>("/api/files/upload-sessions"))!).Id);

        var partRequest = await client.PostAsJsonAsync($"/api/files/upload-sessions/{session.Id}/part-requests",
            new PartUploadRequest { Number = 1, Sha256 = Convert.ToBase64String(SHA256.HashData(bytes)) });
        Assert.Equal(HttpStatusCode.OK, partRequest.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsync($"/api/files/upload-sessions/{session.Id}/parts/1",
            new ByteArrayContent(bytes))).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted,
            (await client.PostAsync($"/api/files/upload-sessions/{session.Id}/complete", content: null)).StatusCode);
        await environment.RunOperationsAsync(server.Services);

        var file = await client.GetFromJsonAsync<FileView>($"/api/files/{session.FileId}");
        Assert.NotNull(file);
        Assert.Equal(FileState.Ready, file!.State);
        Assert.Equal("中文 附件.txt", file.FileName);
        await AssertVisiblePaginationAsync(server.Services, client, session.FileId);
        if (verifyMySqlCasAndClaim)
            await environment.AssertVersionConflictAndTaskClaimAsync(server.Services, session.FileId);

        var bind = await client.PostAsJsonAsync($"/api/files/{session.FileId}/bindings",
            new FileManagementControllerBase.BindingRequest { BusinessType = "orders", BusinessId = "order-42" });
        Assert.Equal(HttpStatusCode.OK, bind.StatusCode);
        var binding = (await bind.Content.ReadFromJsonAsync<FileBinding>())!;
        Assert.NotEqual(Guid.Empty, binding.Id);
        Assert.Equal(session.FileId, Assert.Single((await client.GetFromJsonAsync<FileView[]>("/api/files/by-business?businessType=orders&businessId=order-42"))!).Id);
        Assert.Equal(binding.Id, Assert.Single((await client.GetFromJsonAsync<FileBinding[]>($"/api/files/{session.FileId}/bindings"))!).Id);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/files/{session.FileId}/bindings/{binding.Id}")).StatusCode);

        var download = await client.GetAsync($"/api/files/{session.FileId}/content");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", Assert.Single(download.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("中文 附件.txt", download.Content.Headers.ContentDisposition.FileNameStar);

        using var rangeRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/files/{session.FileId}/content");
        rangeRequest.Headers.Range = new(1, 3);
        var rangeResponse = await client.SendAsync(rangeRequest);
        Assert.Equal(HttpStatusCode.PartialContent, rangeResponse.StatusCode);
        Assert.Equal("bytes 1-3/5", rangeResponse.Content.Headers.ContentRange!.ToString());
        Assert.Equal(new byte[] { 22, 33, 44 }, await rangeResponse.Content.ReadAsByteArrayAsync());

        environment.Identity.Tenant = "tenant-b";
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/files/{session.FileId}/content")).StatusCode);
        environment.Identity.Tenant = "tenant-a";

        Assert.Equal(HttpStatusCode.Accepted, (await client.DeleteAsync($"/api/files/{session.FileId}")).StatusCode);
        await environment.RunOperationsAsync(server.Services);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/files/{session.FileId}")).StatusCode);
        Assert.Null(await environment.GetStorage(server.Services).GetPropertiesAsync(new FileAddress("attachments", session.FileId.ToString("N"))));
        await AssertEnhancementTransactionsAsync(server.Services, environment);
    }

    private static async Task AssertEnhancementTransactionsAsync(IServiceProvider services, AdminFileHttpEnvironment environment)
    {
        using var scope = services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IFileManagementStore>();
        var files = scope.ServiceProvider.GetRequiredService<FileManagementService>();
        var session = await files.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "batch.txt", Length = 0 });
        await environment.RunOperationsAsync(services); await files.CompleteUploadAsync(session.Id); await environment.RunOperationsAsync(services);
        var file = (await store.FindAsync(session.FileId))!.File;
        Assert.Single(await store.QueryFilesAsync(new() { ScopeKey = file.ScopeKey, StoreName = file.StoreName, Name = "batch" }));
        Assert.Empty(await store.QueryFilesAsync(new() { ScopeKey = file.ScopeKey, AfterCreatedAt = file.CreatedAt, AfterKey = file.RequestKey }));
        var batches = scope.ServiceProvider.GetRequiredService<FileMigrationBatchService>();
        var batch = await batches.CreateAsync(file.Scope(), "operator", new() { RequestId = Guid.NewGuid(), SourceStore = file.StoreName, TargetStore = "admin-test-target" });
        await batches.AdvanceAsync();
        var queued = await batches.GetAsync(file.ScopeKey, batch.Id);
        Assert.Equal(1, queued.Queued);
        Assert.Empty(await store.ListBatchItemsToReconcileAsync(100));
        await batches.SetPausedAsync(file.ScopeKey, batch.Id, queued.Batch.Version, true);
        await environment.RunOperationsAsync(services);
        Assert.Equal(file.Id, Assert.Single(await store.ListBatchItemsToReconcileAsync(100)).FileId);
        for (var i = 0; i < 5; i++) await environment.RunOperationsAsync(services);
        var pausedProgress = await batches.GetAsync(file.ScopeKey, batch.Id);
        Assert.Equal(1, pausedProgress.Succeeded); Assert.Equal(0, pausedProgress.Queued);
        Assert.Equal(FileMigrationBatchState.Paused, pausedProgress.Batch.State);
        Assert.Empty(await store.ListBatchItemsToReconcileAsync(100));
        await batches.SetPausedAsync(file.ScopeKey, batch.Id, pausedProgress.Batch.Version, false);
        await batches.AdvanceAsync();
        Assert.Equal(FileMigrationBatchState.Completed, (await batches.GetAsync(file.ScopeKey, batch.Id)).Batch.State);
        var events = await store.ListDueEventsAsync(long.MaxValue, 100);
        Assert.Contains(events, e => e.FileId == session.FileId && e.Kind == "FileReady");
        Assert.Equal(0, (await store.GetOperationalSnapshotAsync()).ReservedBytes);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var lease = new FileIoLease { Id = Guid.NewGuid(), PoolKey = "integration", Token = "owner", ExpiresAt = now + 30000 };
        Assert.True(await store.TryAcquireIoLeaseAsync(lease, 1, now));
        Assert.False(await store.TryAcquireIoLeaseAsync(new() { Id = Guid.NewGuid(), PoolKey = lease.PoolKey, Token = "second", ExpiresAt = lease.ExpiresAt }, 1, now));
        await store.ReleaseIoLeaseAsync(lease.Id, lease.Token);
        // 两个宿主的 MySQL 路径也验证恢复审计与后继任务的真实事务。
        var entry = (await store.FindAsync(session.FileId))!; entry.File.State = FileState.DeletePending;
        var failed = new FileOperation { Id = Guid.NewGuid(), FileId = session.FileId, Kind = FileOperationKind.Delete, State = FileOperationState.Failed, Version = 1 };
        Assert.True(await store.TryUpdateAsync(entry, entry.File.Version, operations: new[] { failed }));
        var next = await scope.ServiceProvider.GetRequiredService<FileOperationAdministration>().RecoverAsync(file.ScopeKey, "operator", failed.Id,
            new() { RequestId = Guid.NewGuid(), ExpectedVersion = 1, Reason = "integration recovery" });
        Assert.Equal(next, Assert.Single(await store.ListOperationAuditAsync(file.ScopeKey, failed.Id)).ReplacementId);
        await environment.RunOperationsAsync(services);
        Assert.Equal(FileState.Deleted, (await store.FindAsync(file.Id))!.File.State);
    }
    private static async Task AssertVisiblePaginationAsync(IServiceProvider services, HttpClient client, Guid fileId)
    {
        var store = services.GetRequiredService<IFileManagementStore>();
        var ready = (await store.FindAsync(fileId))!.File;
        // 用真实元数据库模拟历史终态记录，不能让它们占用当前用户的文件列表首页。
        for (var i = 0; i < 50; i++)
        {
            var id = Guid.NewGuid();
            Assert.True(await store.TryCreateAsync(new FileEntry
            {
                File = new FileRecord
                {
                    Id = id, RequestKey = id.ToString("N"), ScopeKind = ready.ScopeKind, ScopeKey = ready.ScopeKey,
                    TenantId = ready.TenantId, CreatorId = ready.CreatorId, StoreName = ready.StoreName,
                    Container = ready.Container, ObjectKey = id.ToString("N"), FileName = "历史附件.txt",
                    ContentType = "text/plain", State = i % 3 == 0 ? FileState.Aborted : i % 3 == 1 ? FileState.Deleted : FileState.Failed,
                    CreatedAt = ready.CreatedAt + i + 1, Version = 1
                }
            }));
        }
        Assert.Equal(fileId, Assert.Single((await client.GetFromJsonAsync<FileView[]>("/api/files"))!).Id);
        Assert.Empty((await client.GetFromJsonAsync<FileView[]>("/api/files?skip=1&take=50"))!);
    }
}

internal sealed class AdminFileHttpEnvironment : IAsyncDisposable
{
    private readonly string? _mysqlConnectionString;

    public AdminFileHttpEnvironment(string? mysqlConnectionString = null)
    {
        _mysqlConnectionString = mysqlConnectionString;
        Root = Path.Combine(Path.GetTempPath(), "bing-admin-file-http-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }
    public AdminFileTestIdentity Identity { get; } = new();
    public AdminFileTestAuthorizer Authorizer { get; } = new();

    public void Configure(IServiceCollection services) => Configure(services, configureTestAuthentication: true);

    public void Configure(IServiceCollection services, bool configureTestAuthentication)
    {
        services.AddLogging();
        if (configureTestAuthentication)
        {
            services.AddSingleton<ICurrentUser>(Identity);
            services.AddSingleton<ICurrentTenant>(Identity);
        }
        services.AddSingleton<IFileAccessAuthorizer>(Authorizer);
        services.AddBingFileStorage(options => options.DefaultStore = "admin-test-local");
        services.AddBingFileSystemStorage("admin-test-local", options => options.RootPath = Path.Combine(Root, "storage"));
        services.AddBingFileSystemStorage("admin-test-target", options => options.RootPath = Path.Combine(Root, "target"));
        services.AddBingFileManagement();
#if BING_ADMIN_EFCORE_TESTS
        services.AddBingFileManagementEntityFrameworkCore(options =>
        {
            if (_mysqlConnectionString == null)
                options.UseSqlite($"Data Source={Path.Combine(Root, "files.db")}");
            else
                options.UseMySql(_mysqlConnectionString, new MySqlServerVersion(new Version(8, 4, 0)));
        });
#else
        services.AddSingleton<IFreeSql>(_ => new FreeSqlBuilder()
            .UseConnectionString(_mysqlConnectionString == null ? DataType.Sqlite : DataType.MySql,
                _mysqlConnectionString ?? $"Data Source={Path.Combine(Root, "files.db")}")
            .UseAutoSyncStructure(false)
            .Build());
        services.AddBingFileManagementFreeSql(provider => provider.GetRequiredService<IFreeSql>());
#endif
        if (configureTestAuthentication)
        {
            services.AddAuthentication("admin-file-test")
                .AddScheme<AuthenticationSchemeOptions, AdminFileTestAuthenticationHandler>("admin-file-test", _ => { });
        }
        services.AddAuthorization();
        services.AddControllers().AddApplicationPart(typeof(FileController).Assembly);
        services.AddBingFileManagementWeb(runBackgroundWorker: false);
    }

    public async Task InitializeDatabaseAsync(IServiceProvider services)
    {
#if BING_ADMIN_EFCORE_TESTS
        await using var db = await services.GetRequiredService<IDbContextFactory<FileManagementDbContext>>().CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
#else
        FileManagementFreeSqlSchema.SyncStructure(services.GetRequiredService<IFreeSql>());
#endif
    }

    public async Task RunOperationsAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<FileOperationProcessor>().RunBatchAsync();
    }

    public IFileStorage GetStorage(IServiceProvider services)
        => services.GetRequiredService<IFileStorageFactory>().GetStorage("admin-test-local", StorageScope.ForTenant(Identity.Tenant!));

    public async Task AssertVersionConflictAndTaskClaimAsync(IServiceProvider services, Guid fileId)
    {
        FileRecord fresh;
        using (var scope = services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IFileManagementStore>();
            var first = await store.FindAsync(fileId);
            var stale = await store.FindAsync(fileId);
            Assert.NotNull(first);
            Assert.NotNull(stale);
            Assert.True(await store.TryUpdateAsync(first!, first!.File.Version));
            Assert.False(await store.TryUpdateAsync(stale!, stale!.File.Version));
            fresh = (await store.FindAsync(fileId))!.File;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var operation = new FileOperation
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            Kind = FileOperationKind.Abort,
            State = FileOperationState.Pending,
            NextAttemptAt = now,
            Version = 1
        };
        using (var scope = services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IFileManagementStore>();
            var entry = await store.FindAsync(fileId);
            Assert.NotNull(entry);
            Assert.Equal(fresh.Version, entry!.File.Version);
            Assert.True(await store.TryUpdateAsync(entry, entry.File.Version, operations: new[] { operation }));
        }

        using var firstWorker = services.CreateScope();
        using var secondWorker = services.CreateScope();
        var firstStore = firstWorker.ServiceProvider.GetRequiredService<IFileManagementStore>();
        var secondStore = secondWorker.ServiceProvider.GetRequiredService<IFileManagementStore>();
        var due = Assert.Single(await firstStore.ListDueOperationsAsync(now, 100), candidate => candidate.Id == operation.Id);
        var claimTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var oldLeaseUntil = claimTime + 30000;
        var claimResults = await Task.WhenAll(
            firstStore.TryClaimAsync(due.Id, due.Version, "worker-one", claimTime, oldLeaseUntil),
            secondStore.TryClaimAsync(due.Id, due.Version, "worker-two", claimTime, oldLeaseUntil));
        Assert.Equal(1, claimResults.Count(result => result));

        var oldToken = claimResults[0] ? "worker-one" : "worker-two";
        var newToken = claimResults[0] ? "worker-two" : "worker-one";
        var takeoverTime = oldLeaseUntil;
        var expiredClaim = Assert.Single(await firstStore.ListDueOperationsAsync(takeoverTime, 100),
            candidate => candidate.Id == operation.Id);
        Assert.True(await secondStore.TryClaimAsync(expiredClaim.Id, expiredClaim.Version, newToken, takeoverTime,
            takeoverTime + 30000));

        var staleLease = new FileOperationLease(operation.Id, oldToken);
        var activeLease = new FileOperationLease(operation.Id, newToken);
        var beforeLeaseUpdate = await secondStore.FindAsync(fileId);
        Assert.NotNull(beforeLeaseUpdate);
        Assert.False(await firstStore.TryUpdateAsync(beforeLeaseUpdate!, beforeLeaseUpdate!.File.Version, lease: staleLease));
        var afterStaleLease = await secondStore.FindAsync(fileId);
        Assert.Equal(beforeLeaseUpdate.File.Version, afterStaleLease!.File.Version);
        Assert.True(await secondStore.TryUpdateAsync(afterStaleLease, afterStaleLease.File.Version, lease: activeLease));

        await AssertTransferPublishRetirementAndRollbackAsync(firstStore, secondStore, fileId);     }

    private static async Task AssertTransferPublishRetirementAndRollbackAsync(IFileManagementStore firstStore,
        IFileManagementStore secondStore, Guid fileId)
    {
        var file = (await firstStore.FindAsync(fileId))!.File;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var retiredAttempt = NewTransferAttempt(file, file.ObjectKey, "retired-transfer", now + 30000);
        await firstStore.CreateTransferAsync(retiredAttempt);
        Assert.Equal(FileTransferState.Retired,
            (await firstStore.RetireTransferAsync(retiredAttempt.Id, retiredAttempt.LeaseToken))!.State);
        var beforeRetiredPublish = await secondStore.FindAsync(fileId);
        Assert.False(await secondStore.TryUpdateAsync(beforeRetiredPublish!, beforeRetiredPublish!.File.Version,
            transfer: new FileTransferLease(retiredAttempt.Id, retiredAttempt.LeaseToken)));
        Assert.Equal(beforeRetiredPublish.File.Version, (await firstStore.FindAsync(fileId))!.File.Version);

        var rollbackAttempt = NewTransferAttempt(file, file.ObjectKey + "-cas-rollback", "rollback-transfer", now + 30000);
        await firstStore.CreateTransferAsync(rollbackAttempt);
        var rootCasFailure = await secondStore.FindAsync(fileId);
        rootCasFailure!.File.ObjectKey = rollbackAttempt.ObjectKey;
        Assert.False(await secondStore.TryUpdateAsync(rootCasFailure, rootCasFailure.File.Version - 1,
            transfer: new FileTransferLease(rollbackAttempt.Id, rollbackAttempt.LeaseToken)));
        var rolledBack = await firstStore.RetireTransferAsync(rollbackAttempt.Id, rollbackAttempt.LeaseToken);
        Assert.NotNull(rolledBack);
        Assert.Equal(FileTransferState.Retired, rolledBack!.State);
        Assert.NotEqual(rollbackAttempt.ObjectKey, (await firstStore.FindAsync(fileId))!.File.ObjectKey);

        var publishAttempt = NewTransferAttempt(file, file.ObjectKey, "published-transfer", now + 30000);
        await firstStore.CreateTransferAsync(publishAttempt);
        var publishEntry = await secondStore.FindAsync(fileId);
        Assert.True(await secondStore.TryUpdateAsync(publishEntry!, publishEntry!.File.Version,
            transfer: new FileTransferLease(publishAttempt.Id, publishAttempt.LeaseToken)));
        Assert.Null(await firstStore.RetireTransferAsync(publishAttempt.Id, publishAttempt.LeaseToken));
    }

    private static FileTransferAttempt NewTransferAttempt(FileRecord file, string objectKey, string token, long leaseUntil)
        => new()
        {
            Id = Guid.NewGuid(),
            FileId = file.Id,
            ScopeKey = file.ScopeKey,
            ScopeKind = file.ScopeKind,
            TenantId = file.TenantId,
            StoreName = file.StoreName,
            Container = file.Container,
            ObjectKey = objectKey,
            LeaseToken = token,
            LeaseUntil = leaseUntil,
            NextCheckAt = leaseUntil,
            State = FileTransferState.Active,
            WriterCompleted = true,
            Version = 1
        };

    public ValueTask DisposeAsync()
    {
        if (_mysqlConnectionString == null)
        {
#if BING_ADMIN_EFCORE_TESTS
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
#else
            System.Data.SQLite.SQLiteConnection.ClearAllPools();
#endif
        }
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        return ValueTask.CompletedTask;
    }
}

internal sealed class AdminFileTestIdentity : ICurrentUser, ICurrentTenant
{
    private static readonly Claim[] Permissions =
    {
        new("permission", "files.upload"),
        new("permission", "files.publish")
    };

    public string Tenant { get; set; } = "tenant-a";
    public bool IsAuthenticated => true;
    public string UserId => "admin-file-user";
    public string UserName => UserId;
    public string PhoneNumber => "";
    public bool PhoneNumberVerified => false;
    public string Email => "";
    public bool EmailVerified => false;
    public string TenantId => Tenant;
    public string[] Roles => Array.Empty<string>();
    public bool IsAvailable => !string.IsNullOrWhiteSpace(Tenant);
    public string? Id => Tenant;
    public string? Name => Tenant;
    public Claim FindClaim(string claimType) => FindClaims(claimType).FirstOrDefault()!;
    public Claim[] FindClaims(string claimType) => Permissions.Where(claim => claim.Type == claimType).ToArray();
    public Claim[] GetAllClaims() => Permissions.ToArray();
    public bool IsInRole(string roleName) => false;
    public IDisposable Change(string id, string? name = null)
    {
        var original = Tenant;
        Tenant = id;
        return new TenantRestore(this, original);
    }

    private sealed class TenantRestore : IDisposable
    {
        private readonly AdminFileTestIdentity _identity;
        private readonly string _original;
        public TenantRestore(AdminFileTestIdentity identity, string original) { _identity = identity; _original = original; }
        public void Dispose() => _identity.Tenant = _original;
    }
}

internal sealed class AdminFileTestAuthorizer : IFileAccessAuthorizer
{
    public Task<bool> AuthorizeAsync(string businessType, string businessId, FileBusinessAccess access,
        CancellationToken ct = default)
        => Task.FromResult(businessType == "orders" && businessId == "order-42");
}

internal sealed class AdminFileTestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public AdminFileTestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!string.Equals(Request.Headers.Authorization, "Bearer admin-file-test", StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "admin-file-user"),
            new Claim("permission", "files.upload"),
            new Claim("permission", "files.publish")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}

internal sealed class AdminFileManagementMySqlFactAttribute : FactAttribute
{
    public AdminFileManagementMySqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BING_FILE_MANAGEMENT_MYSQL_TEST_CONNECTION")))
            Skip = "MySQL 文件管理集成测试已跳过。设置 BING_FILE_MANAGEMENT_MYSQL_TEST_CONNECTION 以启用。";
    }
}

internal sealed class AdminFileMySqlTestDatabase : IAsyncDisposable
{
    private readonly string _serverConnectionString;

    private AdminFileMySqlTestDatabase(string serverConnectionString, string databaseName)
    {
        _serverConnectionString = serverConnectionString;
        DatabaseName = databaseName;
        var databaseBuilder = new MySqlConnectionStringBuilder(serverConnectionString) { Database = databaseName, Pooling = false };
        DatabaseConnectionString = databaseBuilder.ConnectionString;
    }

    public string DatabaseName { get; }
    public string DatabaseConnectionString { get; }

    public static async Task<AdminFileMySqlTestDatabase> CreateAsync(string serverConnectionString)
    {
        var serverBuilder = new MySqlConnectionStringBuilder(serverConnectionString)
        {
            Database = string.Empty,
            Pooling = false,
            AllowPublicKeyRetrieval = true
        };
        var databaseName = "bing_file_tests_" + Guid.NewGuid().ToString("N");
        await using var connection = new MySqlConnection(serverBuilder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE `{databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
        await command.ExecuteNonQueryAsync();
        return new AdminFileMySqlTestDatabase(serverBuilder.ConnectionString, databaseName);
    }

    public async ValueTask DisposeAsync()
    {
        await using var connection = new MySqlConnection(_serverConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS `{DatabaseName}`";
        await command.ExecuteNonQueryAsync();
    }
}

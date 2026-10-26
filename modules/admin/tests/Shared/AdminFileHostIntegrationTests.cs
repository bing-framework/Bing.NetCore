#nullable enable
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Bing.Admin.Apis.Commons;
using Bing.Admin.Modules;
using Bing.Admin.Service.FileStorage;
using Bing.Auditing.Persistence;
using Bing.Biz.FileManagement;
using Bing.Biz.FileManagement.AspNetCore;
using Bing.MultiTenancy;
using Bing.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MySqlConnector;
using Xunit;

#if BING_ADMIN_EFCORE_TESTS
using Bing.Biz.FileManagement.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace Bing.Admin.EFCore.Tests;
#else
using Bing.Biz.FileManagement.FreeSql;
namespace Bing.Admin.FreeSQL.Tests;
#endif

/// <summary>两个真实 Admin 控制器程序集共用的业务、审计与运维接入验收。</summary>
[Collection("AdminFileHostIsolation")]
public sealed class AdminFileHostIntegrationTests
{
    [AdminFileManagementMySqlFact]
    public async Task DeploymentRealHost_ResolvesPermissionInitializerWithoutRedisOrMessageBroker()
    {
        await using var database = await AdminFileMySqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable("BING_FILE_MANAGEMENT_MYSQL_TEST_CONNECTION")!);
        using var host = Bing.Admin.Program.CreateFileDeploymentHost(new[]
        {
            "--FileManagement:Enabled=true", "--FileManagement:RunWorker=false",
            "--FileManagement:Stores:local-v1:Provider=FileSystem",
            "--FileManagement:Stores:local-v1:RootPath=" + Path.Combine(Path.GetTempPath(), "bing-file-deploy-unused"),
            "--ConnectionStrings:DefaultConnection=" + database.DatabaseConnectionString
        });
        using var scope = host.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAdminFilePermissionResourceInitializer>());
        Assert.IsType<AdminUserInfoFileAuthorizer>(scope.ServiceProvider.GetRequiredService<IFileAccessAuthorizer>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<FileManagementService>());
        Assert.Null(scope.ServiceProvider.GetService<CSRedis.CSRedisClient>());
        Assert.False(host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.IsCancellationRequested);
        Assert.DoesNotContain(host.Services.GetServices<IHostedService>(), service =>
            service.GetType().FullName == "Bing.Biz.FileManagement.AspNetCore.FileManagementWorker" ||
            service.GetType().Namespace?.StartsWith("DotNetCore.CAP", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task RealModule_RegistersTenantBusinessAuditMetricsAndOrmSchema()
    {
        await using var environment = new AdminFileHttpEnvironment();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FileManagement:Enabled"] = "true", ["FileManagement:RunWorker"] = "false",
            ["FileManagement:Stores:local-v1:Provider"] = "FileSystem",
            ["FileManagement:Stores:local-v1:RootPath"] = Path.Combine(environment.Root, "local"),
            ["FileManagement:Operations:MetricsEnabled"] = "true",
            ["FileManagement:Console:Enabled"] = "true",
            ["ConnectionStrings:DefaultConnection"] = "Server=127.0.0.1;Port=1;Database=unused;User ID=unused;Password=unused;"
        }).Build();
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<ICurrentUser>(environment.Identity);
        new FileStorageModule().AddServices(services);
        Assert.Equal(typeof(AdminUserInfoFileAuthorizer), services.Last(d => d.ServiceType == typeof(IFileAccessAuthorizer)).ImplementationType);
        Assert.Contains(services, d => d.ServiceType == typeof(IFileLifecycleEventHandler) && d.ImplementationType == typeof(AdminFileLifecycleAuditHandler));
        await using var provider = services.BuildServiceProvider();
        Assert.True(provider.GetRequiredService<FileManagementConsoleOptions>().Enabled);
        using var scope = provider.CreateScope();
        var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        Assert.Equal(environment.Identity.Tenant, tenant.Id);
        using (tenant.Change(null!)) { await Task.Yield(); Assert.False(tenant.IsAvailable); }
        Assert.Equal(environment.Identity.Tenant, tenant.Id);
        Assert.NotNull(provider.GetRequiredService<AdminFileMetricsExporter>());
#if BING_ADMIN_EFCORE_TESTS
        var script = await provider.GetRequiredService<AdminFileSchemaExporter>().ExportAsync(scope.ServiceProvider, CancellationToken.None);
        Assert.Contains("BingFileRecords", script);
        Assert.Contains("BingFileOutboxEvents", script);
#else
        // FreeSQL 导出的是目标库差异，真实 MySQL 导出在下方专用环境测试中验证。
        Assert.NotNull(provider.GetRequiredService<AdminFileSchemaExporter>());
#endif
    }

    [Fact]
    public async Task OperationsHttp_ProtectsGlobalState_ExportsRealMetrics_AndReportsWorkerReadiness()
    {
        await using var environment = new AdminFileHttpEnvironment();
        var permissions = new HostPermissions();
        var options = new AdminFileOperationsOptions { HealthEnabled = true, MetricsEnabled = true };
        using var server = new TestServer(new WebHostBuilder().ConfigureServices(services =>
        {
            environment.Configure(services);
            services.AddSingleton<IFilePermissionChecker>(permissions);
            services.AddSingleton(options);
            services.AddSingleton(_ => new AdminFileMetricsExporter(new[] { "admin-test-local" }));
        }).Configure(app =>
        {
            app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(endpoints => endpoints.MapControllers());
        }));
        await environment.InitializeDatabaseAsync(server.Services);
        using var client = server.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/internal/metrics/files")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "admin-file-test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/internal/metrics/files")).StatusCode);
        environment.Identity.Tenant = null!;
        permissions.Manage = false;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/internal/health/files")).StatusCode);
        permissions.Manage = true; permissions.Host = false;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/internal/health/files")).StatusCode);
        permissions.Host = true;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/internal/health/files")).StatusCode);
        server.Services.GetRequiredService<FileManagementHealthOptions>().ExpectWorker = true;
        var degraded = await client.GetAsync("/internal/health/files");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, degraded.StatusCode);
        Assert.DoesNotContain(environment.Root, await degraded.Content.ReadAsStringAsync());

        using var scope = server.Services.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<FileManagementService>();
        var session = await files.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "metrics.txt", Length = 0 });
        var pending = await client.GetStringAsync("/internal/metrics/files");
        Assert.Contains("bing_files_queue_pending 1\n", pending);
        await environment.RunOperationsAsync(server.Services);
        await files.CompleteUploadAsync(session.Id);
        await environment.RunOperationsAsync(server.Services);
        var response = await client.GetAsync("/internal/metrics/files");
        Assert.Equal("text/plain", response.Content.Headers.ContentType!.MediaType);
        var metrics = await response.Content.ReadAsStringAsync();
        Assert.Contains("bing_files_queue_pending 0\n", metrics);
        Assert.Contains("bing_files_events_pending 1\n", metrics);
        Assert.Contains("bing_files_operations_completed_total{kind=\"Complete\"}", metrics);
        Assert.Contains("bing_files_operations_duration_seconds_bucket{kind=\"Complete\",le=\"+Inf\"}", metrics);
        Assert.DoesNotContain(session.FileId.ToString(), metrics);
        options.MetricsEnabled = false; options.HealthEnabled = false;
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/internal/metrics/files")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/internal/health/files")).StatusCode);
    }

    [Fact]
    public void Metrics_UsesBoundedLabels_AndCumulativeHistogram()
    {
        using var exporter = new AdminFileMetricsExporter(new[] { "local" });
        using var meter = new Meter("Bing.FileManagement");
        var counter = meter.CreateCounter<long>("bing.files.bytes");
        counter.Add(4, new KeyValuePair<string, object?>("store", "local")); counter.Add(5, new KeyValuePair<string, object?>("store", "secret-tenant"));
        var histogram = meter.CreateHistogram<double>("bing.files.operations.duration");
        histogram.Record(.2, new KeyValuePair<string, object?>("kind", "Delete")); histogram.Record(2, new KeyValuePair<string, object?>("kind", "Delete"));
        var text = exporter.Export(new());
        Assert.Contains("bing_files_bytes_total{store=\"local\"} 4\n", text);
        Assert.Contains("bing_files_bytes_total{store=\"other\"} 5\n", text);
        Assert.DoesNotContain("secret-tenant", text);
        Assert.Contains("bing_files_operations_duration_seconds_bucket{kind=\"Delete\",le=\"0.5\"} 1\n", text);
        Assert.Contains("bing_files_operations_duration_seconds_bucket{kind=\"Delete\",le=\"+Inf\"} 2\n", text);
    }

    [Fact]
    public Task LifecycleAudit_PersistsRealTransitions_RetriesMissingSchema_AndDeduplicates() => AuditCycleAsync(null);

    [AdminFileManagementMySqlFact]
    public async Task LifecycleAudit_MySql_PersistsAndDeduplicates()
    {
        await using var database = await AdminFileMySqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable("BING_FILE_MANAGEMENT_MYSQL_TEST_CONNECTION")!);
        await AuditCycleAsync(database.DatabaseConnectionString);
    }

    private static async Task AuditCycleAsync(string? mysql)
    {
        await using var environment = new AdminFileHttpEnvironment(mysql);
        environment.Identity.Tenant = null!;
        var dialect = mysql == null ? AuditLogDatabase.Sqlite : AuditLogDatabase.MySql;
        Func<DbConnection> connection = mysql == null
            ? () => new SqliteConnection($"Data Source={Path.Combine(environment.Root, "audit.db")};Pooling=False")
            : () => new MySqlConnection(mysql);
        var services = new ServiceCollection(); environment.Configure(services);
        services.AddSingleton<IFilePermissionChecker>(new HostPermissions());
        services.AddSingleton(new AdminFileAuditDatabase(dialect, connection));
        services.AddScoped<IFileLifecycleEventHandler, AdminFileLifecycleAuditHandler>();
        await using var provider = services.BuildServiceProvider();
        await environment.InitializeDatabaseAsync(provider);
        using var scope = provider.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<FileManagementService>();
        var store = scope.ServiceProvider.GetRequiredService<IFileManagementStore>();
        var session = await files.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "audit.txt", Length = 0 });
        await environment.RunOperationsAsync(provider);
        await files.CompleteUploadAsync(session.Id);
        await environment.RunOperationsAsync(provider);
        Assert.Equal(FileState.Ready, (await store.FindAsync(session.FileId))!.File.State);
        Assert.Equal(1, (await store.GetOperationalSnapshotAsync()).PendingEvents);
        var pending = Assert.Single(await store.ListDueEventsAsync(long.MaxValue, 10));
        var message = new FileLifecycleEvent { Id = pending.Id, FileId = pending.FileId, ScopeKey = pending.ScopeKey,
            FileVersion = pending.FileVersion, Kind = pending.Kind, CreatedAt = pending.CreatedAt };
        var handler = new AdminFileLifecycleAuditHandler(provider.GetRequiredService<AdminFileAuditDatabase>(), store);
        await Assert.ThrowsAnyAsync<DbException>(() => handler.HandleAsync(message, CancellationToken.None));
        await using var db = connection(); await db.OpenAsync();
        foreach (var sql in AuditLogSchema.GetCreateStatements(dialect))
        { await using var command = db.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync(); }
        await handler.HandleAsync(message, CancellationToken.None);
        await new AdminFileLifecycleAuditHandler(provider.GetRequiredService<AdminFileAuditDatabase>(), store).HandleAsync(message, CancellationToken.None);
        // 模拟消费成功后进程在投递确认前重启，后续 Outbox 重试只能保留一条审计。
        var delay = pending.NextAttemptAt - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (delay > 0) await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(delay + 30, 5000)));
        await scope.ServiceProvider.GetRequiredService<FileOutboxDispatcher>().DispatchAsync();
        Assert.Equal(0, (await store.GetOperationalSnapshotAsync()).PendingEvents);
        await files.DeleteAsync(session.FileId);
        await environment.RunOperationsAsync(provider);
        await scope.ServiceProvider.GetRequiredService<FileOutboxDispatcher>().DispatchAsync();
        Assert.Equal(FileState.Deleted, (await store.FindAsync(session.FileId))!.File.State);
        await using var query = db.CreateCommand();
        query.CommandText = "SELECT HttpMethod, DetailsJson, UserId FROM bing_audit_log ORDER BY ExecutionTimeUtc";
        await using var reader = await query.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync()); Assert.Equal("FileReady", reader.GetString(0));
        Assert.Contains(session.FileId.ToString("N"), reader.GetString(1)); Assert.True(reader.IsDBNull(2) || reader.GetString(2) == "");
        Assert.True(await reader.ReadAsync()); Assert.Equal("FileDeleted", reader.GetString(0));
        Assert.False(await reader.ReadAsync());
    }

    [Fact]
    public async Task Deployment_RequiresApply_ExportsUtf8_AndNeverStartsHost()
    {
        await using var environment = new AdminFileHttpEnvironment();
        var path = Path.Combine(environment.Root, "schema.sql");
        var started = new StartProbe();
        IHost Build(string[] args) => new HostBuilder().ConfigureServices(services =>
        {
            environment.Configure(services);
            services.AddSingleton<IHostedService>(started);
            services.AddSingleton(new AdminFileSchemaExporter(async (provider, ct) =>
            {
#if BING_ADMIN_EFCORE_TESTS
                await using var db = await provider.GetRequiredService<IDbContextFactory<FileManagementDbContext>>().CreateDbContextAsync(ct);
                return db.Database.GenerateCreateScript();
#else
                await Task.CompletedTask;
                return FileManagementFreeSqlSchema.GenerateScript(provider.GetRequiredService<IFreeSql>());
#endif
            }));
        }).Build();
        await Assert.ThrowsAsync<ArgumentException>(() => AdminFileDeployment.TryRunAsync(new[] { "files", "initialize-permissions" }, _ => throw new Exception("must not build")));
        Assert.True(await AdminFileDeployment.TryRunAsync(new[] { "files", "schema", "--output", path }, Build));
        Assert.False(started.Started);
        var sql = await File.ReadAllTextAsync(path, System.Text.Encoding.UTF8);
        Assert.Contains("BingFileRecords", sql);
        await Assert.ThrowsAsync<IOException>(() => AdminFileDeployment.TryRunAsync(new[] { "files", "schema", "--output", path }, Build));
        // 执行本次导出的 SQLite schema，验证导出的真实表结构可被实际仓储使用。
        await using var connection = new SqliteConnection($"Data Source={Path.Combine(environment.Root, "schema-check.db")};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync();
        command.CommandText = "SELECT COUNT(*) FROM BingFileRecords"; Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    [AdminFileManagementMySqlFact]
    public async Task RealModule_MySql_SchemaExporterProducesDeployableScript()
    {
        await using var database = await AdminFileMySqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable("BING_FILE_MANAGEMENT_MYSQL_TEST_CONNECTION")!);
        await using var environment = new AdminFileHttpEnvironment();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FileManagement:Enabled"] = "true", ["FileManagement:RunWorker"] = "false",
            ["FileManagement:Stores:local-v1:Provider"] = "FileSystem",
            ["FileManagement:Stores:local-v1:RootPath"] = Path.Combine(environment.Root, "local"),
            ["ConnectionStrings:DefaultConnection"] = database.DatabaseConnectionString
        }).Build();
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton<IConfiguration>(config);
        new FileStorageModule().AddServices(services);
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sql = await provider.GetRequiredService<AdminFileSchemaExporter>().ExportAsync(scope.ServiceProvider, CancellationToken.None);
        await using var connection = new MySqlConnection(database.DatabaseConnectionString); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync();
        var snapshot = await scope.ServiceProvider.GetRequiredService<IFileManagementStore>().GetOperationalSnapshotAsync();
        Assert.Equal(0, snapshot.PendingOperations); Assert.Equal(0, snapshot.PendingEvents);
    }

    private sealed class StartProbe : IHostedService
    {
        public bool Started { get; private set; }
        public Task StartAsync(CancellationToken ct) { Started = true; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class HostPermissions : IFilePermissionChecker
    {
        public bool Host { get; set; } = true;
        public bool Manage { get; set; } = true;
        public Task<bool> IsGrantedAsync(string permission, CancellationToken ct = default) => Task.FromResult(permission switch
            { "files.host" => Host, "files.manage" => Manage, "files.upload" => true, _ => false });
    }
}

/// <summary>精简宿主会初始化现有静态对象映射器，Meter 也属于进程级，避免与其他夹具并发。</summary>
[CollectionDefinition("AdminFileHostIsolation", DisableParallelization = true)]
public sealed class AdminFileHostIsolationCollection { }

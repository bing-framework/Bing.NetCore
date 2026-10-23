#nullable enable

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bing.Data;
using Bing.Data.Sql;
using Bing.Samples.SqlUnitOfWork.Application;
using Bing.Samples.SqlUnitOfWork.Controllers;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Database;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Scenarios;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Bing.Tracing;
using Xunit;

namespace Bing.Samples.SqlUnitOfWork.Tests;

[Collection(SqlUnitOfWorkHostCollection.Name)]
public sealed class OperationEntryPointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void OperationContextSnapshot_ShouldCopyTraceAndDatabaseScalarsAndRestoreParents()
    {
        var previousTrace = new TraceIdContext("parent-trace", "parent-root", "parent-parent", "parent-child");
        TraceIdContext.Current = previousTrace;
        var accessor = new AsyncLocalDatabaseContextAccessor
        {
            Current = new DatabaseContext
            {
                DbKey = "reporting",
                TenantId = "tenant-17",
                MappingProfile = "profile-a",
                ReadPreference = SqlReadPreference.Primary
            }
        };

        try
        {
            var snapshot = OperationContextSnapshot.Capture("operation-17", accessor);
            TraceIdContext.Current = new TraceIdContext("other-trace");
            accessor.Current = new DatabaseContext { DbKey = "orders" };

            using (snapshot.Enter(accessor))
            {
                Assert.NotSame(previousTrace, TraceIdContext.Current);
                Assert.Equal("parent-trace", TraceIdContext.Current!.TraceId);
                Assert.Equal("parent-root", TraceIdContext.Current.RootId);
                Assert.Equal("parent-parent", TraceIdContext.Current.ParentId);
                Assert.Equal("parent-child", TraceIdContext.Current.ChildId);
                Assert.Equal("reporting", accessor.Current!.DbKey);
                Assert.Equal("tenant-17", accessor.Current.TenantId);
                Assert.Equal("profile-a", accessor.Current.MappingProfile);
                Assert.Equal(SqlReadPreference.Primary, accessor.Current.ReadPreference);
            }

            Assert.Equal("other-trace", TraceIdContext.Current!.TraceId);
            Assert.Equal("orders", accessor.Current!.DbKey);
            Assert.Null(accessor.Current.TenantId);
        }
        finally
        {
            TraceIdContext.Current = null;
        }
    }

    [Fact]
    public async Task OperationsDisabled_ShouldKeepLegacyHttpAndHideOperationRoute()
    {
        await using var host = await OperationTestHost.StartAsync("Dapper", operationsEnabled: false);

        using var unavailable = await host.Client.PostAsync(
            $"/api/operation-scenarios/{SqlUnitOfWorkScenarios.OperationSuccess}", content: null);
        Assert.Equal(HttpStatusCode.NotFound, unavailable.StatusCode);
        Assert.False(host.Options.OperationsEnabled);

        using var response = await host.Client.PostAsync(
            $"/api/scenarios/{SqlUnitOfWorkScenarios.CrossLayerCommit}", content: null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ScenarioExecutionResult>(JsonOptions);
        Assert.NotNull(result);
        await AssertRowsAsync(host.DatabaseFiles,
            new OrderRow($"{result!.OperationId}:application", result.OperationId, "application", 1,
                "application"),
            new OrderRow($"{result.OperationId}:repository", result.OperationId, "repository", 2,
                "repository"));
    }

    [Theory]
    [InlineData("Dapper")]
    [InlineData("EfBridge")]
    public async Task HttpOperation_ShouldCommitOnlyAfterSuccessAndRollbackFailures(string storageMode)
    {
        await using var host = await OperationTestHost.StartAsync(storageMode, operationsEnabled: true);
        Assert.True(host.Options.OperationsEnabled);
        var expected = new List<OrderRow>();

        SetContext(host, "http-success");
        var first = await RunOperationAsync(host, SqlUnitOfWorkScenarios.OperationSuccess);
        AssertExecutionIdentity(first, "http-success");
        expected.AddRange(OperationRows(first.OperationId, storageMode));
        await AssertRowsAsync(host.DatabaseFiles, expected.ToArray());
        Assert.NotEqual(Guid.Empty, first.ScopeId);

        if (storageMode == "EfBridge")
        {
            SetContext(host, "http-tracked");
            var trackedOnly = await RunOperationAsync(host, SqlUnitOfWorkScenarios.OperationEfTrackedOnly);
            AssertExecutionIdentity(trackedOnly, "http-tracked");
            expected.Add(new OrderRow($"{trackedOnly.OperationId}:ef", trackedOnly.OperationId,
                "ef-operation", 2, "tracked-only"));
            await AssertRowsAsync(host.DatabaseFiles, expected.ToArray());
        }

        SetContext(host, "http-failure");
        var failedId = await RunFailingOperationAsync(host, SqlUnitOfWorkScenarios.OperationFailure);
        var failed = await ReadOperationAsync(host.Client, failedId);
        Assert.Equal("Failed", failed.Status);
        Assert.Equal("InvalidOperationException", failed.ErrorType);
        AssertExecutionIdentity(failed, "http-failure");
        await AssertRowsAsync(host.DatabaseFiles, expected.ToArray());

        SetContext(host, "http-after-failure");
        var afterFailure = await RunOperationAsync(host, SqlUnitOfWorkScenarios.OperationSuccess);
        AssertExecutionIdentity(afterFailure, "http-after-failure");
        AssertIndependentExecutionIdentities(first.ExecutionIdentity, afterFailure.ExecutionIdentity);
        expected.AddRange(OperationRows(afterFailure.OperationId, storageMode));
        await AssertRowsAsync(host.DatabaseFiles, expected.ToArray());

        if (storageMode == "EfBridge")
        {
            using var legacyResponse = await host.Client.PostAsync(
                $"/api/scenarios/{SqlUnitOfWorkScenarios.EfCompleteWithoutSave}", content: null);
            legacyResponse.EnsureSuccessStatusCode();
            var legacyResult = await legacyResponse.Content.ReadFromJsonAsync<ScenarioExecutionResult>(JsonOptions);
            Assert.NotNull(legacyResult);
            expected.Add(new OrderRow($"{legacyResult!.OperationId}:dapper", legacyResult.OperationId,
                "dapper", 2, "written-by-dapper"));
            expected.Add(new OrderRow($"{legacyResult.OperationId}:ef-unsaved", legacyResult.OperationId,
                "ef-unsaved", 1, "not-flushed"));
            await AssertRowsAsync(host.DatabaseFiles, expected.ToArray());
        }
    }

    [Theory]
    [InlineData("Dapper")]
    [InlineData("EfBridge")]
    public async Task BackgroundWorker_ShouldUseIndependentOperationsAndContinueAfterFailure(string storageMode)
    {
        await using var host = await OperationTestHost.StartAsync(storageMode, operationsEnabled: true);
        var expected = new List<OrderRow>();

        SetContext(host, "worker-first");
        var first = await SubmitAsync(host.Client, "/api/worker/scenarios",
            SqlUnitOfWorkScenarios.OperationSuccess);
        var firstOperation = await WaitForOperationAsync(host.Client, first.OperationId);
        Assert.Equal("Completed", firstOperation.Status);
        AssertExecutionIdentity(firstOperation, "worker-first");
        expected.AddRange(OperationRows(first.OperationId, storageMode));

        SetContext(host, "worker-second");
        var second = await SubmitAsync(host.Client, "/api/worker/scenarios",
            SqlUnitOfWorkScenarios.OperationSuccess);
        var secondOperation = await WaitForOperationAsync(host.Client, second.OperationId);
        Assert.Equal("Completed", secondOperation.Status);
        AssertExecutionIdentity(secondOperation, "worker-second");
        AssertIndependentExecutionIdentities(firstOperation.ExecutionIdentity, secondOperation.ExecutionIdentity);
        expected.AddRange(OperationRows(second.OperationId, storageMode));

        SetContext(host, "worker-failure");
        var failure = await SubmitAsync(host.Client, "/api/worker/scenarios",
            SqlUnitOfWorkScenarios.OperationFailure);
        var failedOperation = await WaitForOperationAsync(host.Client, failure.OperationId);
        Assert.Equal("Failed", failedOperation.Status);
        AssertExecutionIdentity(failedOperation, "worker-failure");

        SetContext(host, "worker-after-failure");
        var afterFailure = await SubmitAsync(host.Client, "/api/worker/scenarios",
            SqlUnitOfWorkScenarios.OperationSuccess);
        var recoveredOperation = await WaitForOperationAsync(host.Client, afterFailure.OperationId);
        Assert.Equal("Completed", recoveredOperation.Status);
        AssertExecutionIdentity(recoveredOperation, "worker-after-failure");
        AssertIndependentExecutionIdentities(failedOperation.ExecutionIdentity,
            recoveredOperation.ExecutionIdentity);
        expected.AddRange(OperationRows(afterFailure.OperationId, storageMode));
        await AssertRowsAsync(host.DatabaseFiles, expected.ToArray());
    }

    [Theory]
    [InlineData("Dapper")]
    [InlineData("EfBridge")]
    public async Task HangfireServer_ShouldExecuteIsolatedOperationsAndContinueAfterFailure(string storageMode)
    {
        await using var host = await OperationTestHost.StartAsync(storageMode, operationsEnabled: true);
        var expected = new List<OrderRow>();

        SetContext(host, "hangfire-first");
        var first = await SubmitAsync(host.Client, "/api/jobs/scenarios",
            SqlUnitOfWorkScenarios.OperationSuccess);
        Assert.False(string.IsNullOrWhiteSpace(first.JobId));
        var firstOperation = await WaitForOperationAsync(host.Client, first.OperationId);
        Assert.Equal("Completed", firstOperation.Status);
        AssertExecutionIdentity(firstOperation, "hangfire-first");
        expected.AddRange(OperationRows(first.OperationId, storageMode));

        SetContext(host, "hangfire-second");
        var second = await SubmitAsync(host.Client, "/api/jobs/scenarios",
            SqlUnitOfWorkScenarios.OperationSuccess);
        Assert.False(string.IsNullOrWhiteSpace(second.JobId));
        var secondOperation = await WaitForOperationAsync(host.Client, second.OperationId);
        Assert.Equal("Completed", secondOperation.Status);
        AssertExecutionIdentity(secondOperation, "hangfire-second");
        AssertIndependentExecutionIdentities(firstOperation.ExecutionIdentity, secondOperation.ExecutionIdentity);
        expected.AddRange(OperationRows(second.OperationId, storageMode));

        SetContext(host, "hangfire-failure");
        var failure = await SubmitAsync(host.Client, "/api/jobs/scenarios",
            SqlUnitOfWorkScenarios.OperationFailure);
        var failedOperation = await WaitForOperationAsync(host.Client, failure.OperationId);
        Assert.Equal("Failed", failedOperation.Status);
        Assert.Equal(failure.JobId, failedOperation.JobId);
        AssertExecutionIdentity(failedOperation, "hangfire-failure");

        SetContext(host, "hangfire-after-failure");
        var afterFailure = await SubmitAsync(host.Client, "/api/jobs/scenarios",
            SqlUnitOfWorkScenarios.OperationSuccess);
        var recoveredOperation = await WaitForOperationAsync(host.Client, afterFailure.OperationId);
        Assert.Equal("Completed", recoveredOperation.Status);
        AssertExecutionIdentity(recoveredOperation, "hangfire-after-failure");
        AssertIndependentExecutionIdentities(failedOperation.ExecutionIdentity,
            recoveredOperation.ExecutionIdentity);
        expected.AddRange(OperationRows(afterFailure.OperationId, storageMode));
        await AssertRowsAsync(host.DatabaseFiles, expected.ToArray());
    }

    [Theory]
    [InlineData("Dapper")]
    [InlineData("EfBridge")]
    public async Task HttpRequestCancellation_ShouldCancelOperationAndRollbackStagedRows(string storageMode)
    {
        await using var host = await OperationTestHost.StartAsync(storageMode, operationsEnabled: true);
        SetContext(host, "http-cancel");
        using var cancellation = new CancellationTokenSource();
        var request = host.Client.PostAsync(
            $"/api/operation-scenarios/{SqlUnitOfWorkScenarios.OperationCancellation}", null,
            cancellation.Token);

        var operation = await WaitForAnyProcessStepAsync(host.Operations,
            SqlUnitOfWorkScenarios.OperationCancellation, "operation-waiting-for-cancellation");
        AssertExecutionIdentity(operation, "http-cancel");
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await request);

        var cancelled = await WaitForStoreStatusAsync(host.Operations, operation.OperationId, "Cancelled");
        Assert.Equal("Cancelled", cancelled.Status);
        await AssertRowsAsync(host.DatabaseFiles);
    }

    [Theory]
    [InlineData("Dapper")]
    [InlineData("EfBridge")]
    public async Task BackgroundWorkerShutdown_ShouldCancelCurrentOperationAndRollbackRows(string storageMode)
    {
        await using var host = await OperationTestHost.StartAsync(storageMode, operationsEnabled: true);
        SetContext(host, "worker-cancel");
        var submission = await SubmitAsync(host.Client, "/api/worker/scenarios",
            SqlUnitOfWorkScenarios.OperationCancellation);
        var running = await WaitForProcessStepAsync(host.Operations, submission.OperationId,
            "operation-waiting-for-cancellation");
        AssertExecutionIdentity(running, "worker-cancel");

        await host.StopAsync();
        var cancelled = await WaitForStoreStatusAsync(host.Operations, submission.OperationId, "Cancelled");
        Assert.Equal("Cancelled", cancelled.Status);
        await AssertRowsAsync(host.DatabaseFiles);
    }

    [Theory]
    [InlineData("Dapper")]
    [InlineData("EfBridge")]
    public async Task HangfireShutdown_ShouldPropagateCancellationAndRollbackRows(string storageMode)
    {
        await using var host = await OperationTestHost.StartAsync(storageMode, operationsEnabled: true);
        SetContext(host, "hangfire-cancel");
        var submission = await SubmitAsync(host.Client, "/api/jobs/scenarios",
            SqlUnitOfWorkScenarios.OperationCancellation);
        var running = await WaitForProcessStepAsync(host.Operations, submission.OperationId,
            "operation-waiting-for-cancellation");
        AssertExecutionIdentity(running, "hangfire-cancel");

        await host.StopAsync();
        var cancelled = await WaitForStoreStatusAsync(host.Operations, submission.OperationId, "Cancelled");
        Assert.Equal("Cancelled", cancelled.Status);
        await AssertRowsAsync(host.DatabaseFiles);
    }

    private static async Task<ScenarioExecutionResult> RunOperationAsync(OperationTestHost host, string scenario)
    {
        using var response = await host.Client.PostAsync($"/api/operation-scenarios/{scenario}", content: null);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonSerializer.Deserialize<ScenarioExecutionResult>(body, JsonOptions)
               ?? throw new Xunit.Sdk.XunitException($"业务操作响应无法解析：{body}");
    }

    private static async Task<string> RunFailingOperationAsync(OperationTestHost host, string scenario)
    {
        using var response = await host.Client.PostAsync($"/api/operation-scenarios/{scenario}", content: null);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var failure = JsonSerializer.Deserialize<ScenarioController.ScenarioFailure>(body, JsonOptions)
                      ?? throw new Xunit.Sdk.XunitException($"失败响应无法解析：{body}");
        return failure.OperationId;
    }

    private static void SetContext(OperationTestHost host, string name)
    {
        SetHeader(host.Client, "X-Test-Trace", $"trace-{name}");
        SetHeader(host.Client, "X-Test-Root", $"root-{name}");
        SetHeader(host.Client, "X-Test-Parent", $"parent-{name}");
        SetHeader(host.Client, "X-Test-Child", $"child-{name}");
        SetHeader(host.Client, "X-Test-Tenant", $"tenant-{name}");
    }

    private static void SetHeader(HttpClient client, string name, string value)
    {
        client.DefaultRequestHeaders.Remove(name);
        client.DefaultRequestHeaders.TryAddWithoutValidation(name, value);
    }

    private static void AssertExecutionIdentity(ScenarioExecutionResult result, string contextName) =>
        AssertExecutionIdentity(result.ExecutionIdentity, result.ScopeId, contextName);

    private static void AssertExecutionIdentity(ScenarioOperationSnapshot operation, string contextName) =>
        AssertExecutionIdentity(operation.ExecutionIdentity, operation.Result?.ScopeId, contextName);

    private static void AssertExecutionIdentity(ScenarioOperationIdentityEvidence? identity, Guid? scopeId,
        string contextName)
    {
        Assert.NotNull(identity);
        var evidence = identity!;
        Assert.All(new[]
        {
            evidence.ServiceIdentity,
            evidence.ScopeManagerIdentity,
            evidence.DbContextIdentity,
            evidence.QueryIdentity
        }, value => Assert.True(value > 0));
        Assert.Equal(4, new[]
        {
            evidence.ServiceIdentity,
            evidence.ScopeManagerIdentity,
            evidence.DbContextIdentity,
            evidence.QueryIdentity
        }.Distinct().Count());
        Assert.NotEqual(Guid.Empty, evidence.RootScopeId);
        if (scopeId.HasValue)
            Assert.Equal(scopeId.Value, evidence.RootScopeId);
        Assert.True(evidence.ManagerCurrentIsRoot);
        Assert.True(evidence.RepositoryUsesSameContext);
        Assert.Equal($"trace-{contextName}", evidence.TraceId);
        Assert.Equal($"root-{contextName}", evidence.TraceRootId);
        Assert.Equal($"parent-{contextName}", evidence.TraceParentId);
        Assert.Equal($"child-{contextName}", evidence.TraceChildId);
        Assert.Equal($"tenant-{contextName}", evidence.TenantId);
    }

    private static void AssertIndependentExecutionIdentities(ScenarioOperationIdentityEvidence? first,
        ScenarioOperationIdentityEvidence? second)
    {
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first!.ServiceIdentity, second!.ServiceIdentity);
        Assert.NotEqual(first.ScopeManagerIdentity, second.ScopeManagerIdentity);
        Assert.NotEqual(first.DbContextIdentity, second.DbContextIdentity);
        Assert.NotEqual(first.QueryIdentity, second.QueryIdentity);
        Assert.NotEqual(first.RootScopeId, second.RootScopeId);
    }

    private static async Task<OperationSubmission> SubmitAsync(HttpClient client, string path, string scenario)
    {
        using var response = await client.PostAsync($"{path}/{scenario}", content: null);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return JsonSerializer.Deserialize<OperationSubmission>(body, JsonOptions)
               ?? throw new Xunit.Sdk.XunitException($"后台操作响应无法解析：{body}");
    }

    private static async Task<ScenarioOperationSnapshot> WaitForOperationAsync(HttpClient client, string operationId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        ScenarioOperationSnapshot? last = null;
        while (true)
        {
            using var response = await client.GetAsync($"/api/operations/{operationId}", timeout.Token);
            response.EnsureSuccessStatusCode();
            last = await response.Content.ReadFromJsonAsync<ScenarioOperationSnapshot>(JsonOptions,
                timeout.Token);
            if (last?.Status is "Completed" or "Failed" or "Cancelled")
                return last;
            await Task.Delay(TimeSpan.FromMilliseconds(25), timeout.Token);
        }
    }

    private static async Task<ScenarioOperationSnapshot> ReadOperationAsync(HttpClient client, string operationId)
    {
        using var response = await client.GetAsync($"/api/operations/{operationId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ScenarioOperationSnapshot>(JsonOptions)
               ?? throw new Xunit.Sdk.XunitException($"操作 {operationId} 状态无法解析。");
    }

    private static async Task<ScenarioOperationSnapshot> WaitForAnyProcessStepAsync(ScenarioOperationStore operations,
        string scenario, string step)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (true)
        {
            var operation = operations.Snapshot().FirstOrDefault(item => item.Scenario == scenario &&
                (item.ProcessSteps ?? Array.Empty<ScenarioProcessStepEvidence>()).Any(evidence => evidence.Step == step));
            if (operation != null)
                return operation;
            await Task.Delay(TimeSpan.FromMilliseconds(25), timeout.Token);
        }
    }

    private static async Task<ScenarioOperationSnapshot> WaitForProcessStepAsync(ScenarioOperationStore operations,
        string operationId, string step)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (true)
        {
            if (operations.TryGet(operationId, out var operation) &&
                (operation.ProcessSteps ?? Array.Empty<ScenarioProcessStepEvidence>())
                .Any(evidence => evidence.Step == step))
                return operation;
            await Task.Delay(TimeSpan.FromMilliseconds(25), timeout.Token);
        }
    }

    private static async Task<ScenarioOperationSnapshot> WaitForStoreStatusAsync(ScenarioOperationStore operations,
        string operationId, string status)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (true)
        {
            if (operations.TryGet(operationId, out var operation) && operation.Status == status)
                return operation;
            await Task.Delay(TimeSpan.FromMilliseconds(25), timeout.Token);
        }
    }

    private static IReadOnlyList<OrderRow> OperationRows(string operationId, string storageMode)
    {
        var rows = new List<OrderRow>
        {
            new($"{operationId}:operation", operationId, "operation", 1, "operation")
        };
        if (storageMode == "EfBridge")
            rows.Add(new OrderRow($"{operationId}:ef", operationId, "ef-operation", 2, "tracked-only"));
        return rows;
    }

    private static async Task AssertRowsAsync(SampleDatabaseFiles files, params OrderRow[] expectedOrders)
    {
        var orders = await ReadOrderRowsAsync(files.OrdersPath);
        var reports = await ReadReportRowsAsync(files.ReportingPath);
        Assert.Equal(expectedOrders.OrderBy(row => row.Id).ToArray(), orders.OrderBy(row => row.Id).ToArray());
        Assert.Empty(reports);
    }

    private static async Task<OrderRow[]> ReadOrderRowsAsync(string path)
    {
        await using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, OperationId, Layer, Sequence, Value FROM OrderRecords ORDER BY Id";
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<OrderRow>();
        while (await reader.ReadAsync())
            rows.Add(new OrderRow(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetInt32(3), reader.GetString(4)));
        return rows.ToArray();
    }

    private static async Task<ReportRow[]> ReadReportRowsAsync(string path)
    {
        await using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, OperationId, Value FROM ReportRecords ORDER BY Id";
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<ReportRow>();
        while (await reader.ReadAsync())
            rows.Add(new ReportRow(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        return rows.ToArray();
    }

    private sealed record OperationSubmission(string OperationId, string? JobId, string Status);
    private sealed record OrderRow(string Id, string OperationId, string Layer, int Sequence, string Value);
    private sealed record ReportRow(string Id, string OperationId, string Value);

    private sealed class OperationTestHost : IAsyncDisposable
    {
        private readonly IHost _host;
        private readonly int _port;
        private bool _stopped;

        private OperationTestHost(IHost host, HttpClient client, int port,
            SampleDatabaseFiles databaseFiles, SqlUnitOfWorkSampleOptions options,
            ScenarioOperationStore operations)
        {
            _host = host;
            Client = client;
            _port = port;
            DatabaseFiles = databaseFiles;
            Options = options;
            Operations = operations;
        }

        public HttpClient Client { get; }
        public SampleDatabaseFiles DatabaseFiles { get; }
        public SqlUnitOfWorkSampleOptions Options { get; }
        public ScenarioOperationStore Operations { get; }

        public static async Task<OperationTestHost> StartAsync(string storageMode, bool operationsEnabled)
        {
            var host = Program.CreateHostBuilder(Array.Empty<string>(), storageMode,
                    webBuilder => webBuilder.UseUrls("http://127.0.0.1:0"),
                    services => services.AddSingleton<IStartupFilter, OperationTestContextStartupFilter>())
                .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["SqlUow:OperationsEnabled"] = operationsEnabled.ToString(),
                        ["SqlUow:ProcessEvidencePath"] = string.Empty
                    }))
                .Build();
            try
            {
                await host.StartAsync();
                var address = host.Services.GetRequiredService<IServer>()
                    .Features.Get<IServerAddressesFeature>()?.Addresses
                    .Single(item => item.StartsWith("http://127.0.0.1:", StringComparison.Ordinal));
                Assert.False(string.IsNullOrWhiteSpace(address));
                var endpoint = new Uri(address!);
                return new OperationTestHost(host, new HttpClient { BaseAddress = endpoint }, endpoint.Port,
                    host.Services.GetRequiredService<SampleDatabaseFiles>(),
                    host.Services.GetRequiredService<SqlUnitOfWorkSampleOptions>(),
                    host.Services.GetRequiredService<ScenarioOperationStore>());
            }
            catch
            {
                host.Dispose();
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (_stopped)
                return;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await _host.StopAsync(timeout.Token);
            _stopped = true;
            await AssertPortIsClosedAsync(_port);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await StopAsync();
            }
            finally
            {
                Client.Dispose();
                _host.Dispose();
            }
        }

        private static async Task AssertPortIsClosedAsync(int port)
        {
            using var client = new System.Net.Sockets.TcpClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try
            {
                await client.ConnectAsync(System.Net.IPAddress.Loopback, port, timeout.Token);
                throw new Xunit.Sdk.XunitException($"宿主停止后端口 {port} 仍可连接。");
            }
            catch (System.Net.Sockets.SocketException)
            {
            }
            catch (OperationCanceledException)
            {
                throw new Xunit.Sdk.XunitException($"检查期限内未确认端口 {port} 已关闭。");
            }
        }
    }

    private sealed class OperationTestContextStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                var previousTrace = TraceIdContext.Current;
                var databaseContextAccessor = context.RequestServices.GetRequiredService<IDatabaseContextAccessor>();
                var previousDatabase = databaseContextAccessor.Current;
                var headers = context.Request.Headers;
                TraceIdContext.Current = new TraceIdContext(
                    headers["X-Test-Trace"].FirstOrDefault() ?? "trace-default",
                    headers["X-Test-Root"].FirstOrDefault() ?? "root-default",
                    headers["X-Test-Parent"].FirstOrDefault() ?? "parent-default",
                    headers["X-Test-Child"].FirstOrDefault() ?? "child-default");
                databaseContextAccessor.Current = new DatabaseContext
                {
                    DbKey = "orders",
                    TenantId = headers["X-Test-Tenant"].FirstOrDefault() ?? "tenant-default"
                };
                try
                {
                    await nextMiddleware(context);
                }
                finally
                {
                    databaseContextAccessor.Current = previousDatabase;
                    TraceIdContext.Current = previousTrace;
                }
            });
            next(app);
        };
    }
}

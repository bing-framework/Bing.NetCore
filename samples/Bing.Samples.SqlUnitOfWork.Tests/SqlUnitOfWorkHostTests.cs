#nullable enable

using System.Net;
using System.Net.Sockets;
using System.Data;
using System.Net.Http.Json;
using System.Text.Json;
using Bing.Data.Sql;
using Bing.Events;
using Bing.Samples.SqlUnitOfWork;
using Bing.Samples.SqlUnitOfWork.Application;
using Bing.Samples.SqlUnitOfWork.Controllers;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Background;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Database;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Diagnostics;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Events;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Scenarios;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Testing;
using Bing.Samples.SqlUnitOfWork.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Bing.Uow;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Samples.SqlUnitOfWork.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SqlUnitOfWorkHostCollection
{
    public const string Name = "SQL unit of work sample hosts";
}

[Collection(SqlUnitOfWorkHostCollection.Name)]
public sealed class SqlUnitOfWorkHostTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string ApplicationLayerQuerySql = string.Join(Environment.NewLine,
        "Select `Id`,`OperationId`,`Layer`,`Sequence`,`Value` ",
        "From `OrderRecords` ",
        "Where `OperationId`=@_p_0 And `Layer`=@_p_1 And `Sequence`=@_p_2 And `Value`=@_p_3");
    private static readonly string RepositoryLayerQuerySql = string.Join(Environment.NewLine,
        "Select `Id`,`OperationId`,`Layer`,`Sequence`,`Value` ",
        "From `OrderRecords` ",
        "Where `OperationId`=@_p_0 And `Id`=@_p_1 And `Sequence`=@_p_2 And `Value`=@_p_3");
    private readonly ITestOutputHelper _output;

    public SqlUnitOfWorkHostTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task MemoryEvent_ShouldRunInsideEachRequestScopeAndPropagateFailures()
    {
        await using var host = await RunningSampleHost.StartAsync("Dapper");
        var first = await RunScenarioAsync(host.Client, SqlUnitOfWorkScenarios.MemoryEventImmediate);
        var second = await RunScenarioAsync(host.Client, SqlUnitOfWorkScenarios.MemoryEventImmediate);

        Assert.True(host.MemoryEvents.TryGet(first.OperationId, out var firstObservation));
        Assert.True(host.MemoryEvents.TryGet(second.OperationId, out var secondObservation));
        Assert.NotEqual(firstObservation.HandlerInstanceId, secondObservation.HandlerInstanceId);
        Assert.Contains("当前依赖注入 scope", first.Detail, StringComparison.Ordinal);

        var failure = await RunFailingScenarioAsync(host.Client, SqlUnitOfWorkScenarios.MemoryEventFailure);
        Assert.Equal("InvalidOperationException", failure.ErrorType);
        Assert.False(host.MemoryEvents.TryGet(failure.OperationId, out _));
    }

    [Fact]
    public async Task MemoryEventAfterComplete_ShouldUseCurrentScopeAndSkipRollback()
    {
        await using var host = await RunningSampleHost.StartAsync("Dapper");
        var handlerIds = new List<Guid>();
        foreach (var entryPoint in new[] { "http", "worker", "hangfire" })
        {
            var committed = entryPoint switch
            {
                "http" => await RunScenarioAsync(host.Client, SqlUnitOfWorkScenarios.MemoryEventAfterComplete),
                "worker" => await CompleteWorkerScenarioAsync(host.Client,
                    SqlUnitOfWorkScenarios.MemoryEventAfterComplete),
                "hangfire" => await CompleteJobScenarioAsync(host.Client,
                    SqlUnitOfWorkScenarios.MemoryEventAfterComplete),
                _ => throw new ArgumentOutOfRangeException(nameof(entryPoint))
            };
            Assert.True(host.MemoryEvents.TryGet(committed.OperationId, out var observation));
            Assert.True(observation.UnitOfWorkCompleted);
            handlerIds.Add(observation.HandlerInstanceId);

            var rolledBack = entryPoint switch
            {
                "http" => await RunScenarioAsync(host.Client,
                    SqlUnitOfWorkScenarios.MemoryEventAfterCompleteRollback),
                "worker" => await CompleteWorkerScenarioAsync(host.Client,
                    SqlUnitOfWorkScenarios.MemoryEventAfterCompleteRollback),
                "hangfire" => await CompleteJobScenarioAsync(host.Client,
                    SqlUnitOfWorkScenarios.MemoryEventAfterCompleteRollback),
                _ => throw new ArgumentOutOfRangeException(nameof(entryPoint))
            };
            Assert.False(host.MemoryEvents.TryGet(rolledBack.OperationId, out _));
        }

        Assert.Equal(3, handlerIds.Distinct().Count());
    }

    [Fact]
    public async Task DiagnosticsApi_WhenCapIsNotConfigured_ShouldReportUnknownSources()
    {
        await using var host = await RunningSampleHost.StartAsync("Dapper");

        var snapshot = await host.Client.GetFromJsonAsync<CapEventsDiagnosticsSnapshot>(
            "/api/diagnostics/events", JsonOptions);

        Assert.NotNull(snapshot);
        Assert.Equal("unknown", snapshot.Status);
        Assert.Empty(snapshot.Events);
        Assert.Equal(new[] { "orders", "reporting" }, snapshot.DataSources.Select(source => source.Source));
        Assert.All(snapshot.DataSources, source => Assert.Equal("not-configured", source.Status));
    }

    [Theory]
    [InlineData("Dapper")]
    [InlineData("EfBridge")]
    public async Task HttpHost_ShouldVerifyTransactionsFactoryLifetimeAndExactRows(string storageMode)
    {
        await using var host = await RunningSampleHost.StartAsync(storageMode);
        var expectedOrders = new List<OrderRow>();
        var expectedReports = new List<ReportRow>();

        var crossLayerCommit = await RunScenarioAsync(host.Client, SqlUnitOfWorkScenarios.CrossLayerCommit);
        Assert.True(crossLayerCommit.QueryInstancesAreIndependent);
        Assert.Equal(2, crossLayerCommit.ApplicationVisibleOrderCount);
        Assert.Equal(2, crossLayerCommit.RepositoryVisibleOrderCount);
        Assert.Equal(0, crossLayerCommit.ReportingVisibleRecordCount);
        var committedLayerObservation = host.Probe.GetObservation(crossLayerCommit.OperationId);
        AssertCrossLayerBuilderState(committedLayerObservation, crossLayerCommit.OperationId);
        AssertCrossLayerBuilderRows(committedLayerObservation, crossLayerCommit.OperationId);
        expectedOrders.Add(Order(crossLayerCommit, "application", "application", 1, "application"));
        expectedOrders.Add(Order(crossLayerCommit, "repository", "repository", 2, "repository"));
        await AssertRowsAsync(host.DatabaseFiles, expectedOrders, expectedReports);

        var multiDatabaseCommit = await RunScenarioAsync(host.Client, SqlUnitOfWorkScenarios.MultiDatabaseCommit);
        Assert.Equal(1, multiDatabaseCommit.ApplicationVisibleOrderCount);
        Assert.Equal(1, multiDatabaseCommit.ReportingVisibleRecordCount);
        expectedOrders.Add(Order(multiDatabaseCommit, "application", "application", 1, "orders"));
        expectedReports.Add(new ReportRow(
            $"{multiDatabaseCommit.OperationId}:reporting", multiDatabaseCommit.OperationId, "reporting"));
        await AssertRowsAsync(host.DatabaseFiles, expectedOrders, expectedReports);

        var crossLayerRollback = await RunScenarioAsync(host.Client, SqlUnitOfWorkScenarios.CrossLayerRollback);
        Assert.Equal(2, crossLayerRollback.ApplicationVisibleOrderCount);
        Assert.Equal(2, crossLayerRollback.RepositoryVisibleOrderCount);
        var rolledBackLayerObservation = host.Probe.GetObservation(crossLayerRollback.OperationId);
        AssertCrossLayerBuilderState(rolledBackLayerObservation, crossLayerRollback.OperationId);
        AssertCrossLayerBuilderRows(rolledBackLayerObservation, crossLayerRollback.OperationId);
        await AssertRowsAsync(host.DatabaseFiles, expectedOrders, expectedReports);

        var multiDatabaseRollback = await RunScenarioAsync(host.Client, SqlUnitOfWorkScenarios.MultiDatabaseRollback);
        Assert.Equal(1, multiDatabaseRollback.ApplicationVisibleOrderCount);
        Assert.Equal(1, multiDatabaseRollback.ReportingVisibleRecordCount);
        await AssertRowsAsync(host.DatabaseFiles, expectedOrders, expectedReports);

        var failure = await RunFailingScenarioAsync(host.Client, SqlUnitOfWorkScenarios.CaughtSqlException);
        Assert.Equal("InvalidOperationException", failure.ErrorType);
        Assert.Contains("rollback-only=True", failure.Error, StringComparison.Ordinal);
        await AssertRowsAsync(host.DatabaseFiles, expectedOrders, expectedReports);

        var factoryLifetime = await RunScenarioAsync(host.Client, SqlUnitOfWorkScenarios.FactoryQueryLifetime);
        Assert.Equal(100, factoryLifetime.FactoryQueriesExecuted);
        Assert.Equal(1, factoryLifetime.ApplicationVisibleOrderCount);
        expectedOrders.Add(Order(factoryLifetime, "factory-after", "application", 1, "root-still-open"));
        await AssertRowsAsync(host.DatabaseFiles, expectedOrders, expectedReports);

        if (storageMode == "EfBridge")
        {
            var efCommit = await RunScenarioAsync(host.Client, SqlUnitOfWorkScenarios.EfDapperCommit);
            Assert.Equal(1, efCommit.EfSaveChangesAffected);
            Assert.Equal(2, efCommit.ApplicationVisibleOrderCount);
            expectedOrders.Add(Order(efCommit, "ef", "ef", 1, "saved-by-ef"));
            expectedOrders.Add(Order(efCommit, "dapper", "dapper", 2, "written-by-dapper"));
            await AssertRowsAsync(host.DatabaseFiles, expectedOrders, expectedReports);

            var efRollback = await RunScenarioAsync(host.Client, SqlUnitOfWorkScenarios.EfDapperRollback);
            Assert.Equal(1, efRollback.EfSaveChangesAffected);
            await AssertRowsAsync(host.DatabaseFiles, expectedOrders, expectedReports);

            var completeWithoutSave = await RunScenarioAsync(host.Client,
                SqlUnitOfWorkScenarios.EfCompleteWithoutSave);
            Assert.Equal(0, completeWithoutSave.EfSaveChangesAffected);
            Assert.Contains("未调用 SaveChanges", completeWithoutSave.Detail, StringComparison.Ordinal);
            expectedOrders.Add(Order(completeWithoutSave, "dapper", "dapper", 2, "written-by-dapper"));
            await AssertRowsAsync(host.DatabaseFiles, expectedOrders, expectedReports);
        }
        else
        {
            var unavailableEfBridge = await RunFailingScenarioAsync(host.Client,
                SqlUnitOfWorkScenarios.EfDapperCommit);
            Assert.Contains("SqlUow:Storage=EfBridge", unavailableEfBridge.Error, StringComparison.Ordinal);
            await AssertRowsAsync(host.DatabaseFiles, expectedOrders, expectedReports);
        }
    }

    [Theory]
    [InlineData("Dapper")]
    [InlineData("EfBridge")]
    public async Task HttpHost_ShouldKeepConcurrentReadOnlyScopesIndependent(string storageMode)
    {
        await using var host = await RunningSampleHost.StartAsync(storageMode);
        var seed = await RunScenarioAsync(host.Client, SqlUnitOfWorkScenarios.CrossLayerCommit);
        var expectedOrders = new[]
        {
            Order(seed, "application", "application", 1, "application"),
            Order(seed, "repository", "repository", 2, "repository")
        };
        var expectedReports = Array.Empty<ReportRow>();
        await AssertRowsAsync(host.DatabaseFiles, expectedOrders, expectedReports);

        host.Probe.BeginConcurrentReadWindow(requestedConcurrency: 2);
        var path = $"/api/scenarios/{SqlUnitOfWorkScenarios.ScopeIsolationProbe}" +
                   $"?targetOperationId={Uri.EscapeDataString(seed.OperationId)}";
        Task<ScenarioExecutionResult>? firstRequest = null;
        Task<ScenarioExecutionResult>? secondRequest = null;

        try
        {
            firstRequest = RunScenarioPathAsync(host.Client, path);
            secondRequest = RunScenarioPathAsync(host.Client, path);
            var observations = await host.Probe.WaitForConcurrentReadScopesAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(2, observations.Count);
            var metrics = host.Probe.GetConcurrencyMetrics();
            _output.WriteLine(
                $"requestedConcurrency={metrics.RequestedConcurrency}; " +
                $"maxActualParallelism={metrics.MaxActualParallelism}");
            Assert.Equal(2, metrics.RequestedConcurrency);
            Assert.True(metrics.MaxActualParallelism >= 2);

            var first = observations[0];
            var second = observations[1];
            AssertDistinctScopeResources(first, second);
            Assert.Equal(ConnectionState.Open, first.OrdersConnection!.State);
            Assert.Equal(ConnectionState.Open, second.OrdersConnection!.State);
            AssertReadOnlyQuerySnapshots(first, seed.OperationId);
            AssertReadOnlyQuerySnapshots(second, seed.OperationId);
            AssertReadOnlyQueryRows(first, seed.OperationId, expectedStages: new[] { "before-gate" });
            AssertReadOnlyQueryRows(second, seed.OperationId, expectedStages: new[] { "before-gate" });

            host.Probe.ReleaseReadOnlyScope(first.OperationId);
            var firstFinished = await Task.WhenAny(firstRequest, secondRequest)
                .WaitAsync(TimeSpan.FromSeconds(10));
            var firstResult = await firstFinished;
            Assert.Equal(first.OperationId, firstResult.OperationId);
            await WaitForScopeDisposedAsync(first);
            AssertScopeResourcesDisposed(first);
            Assert.False(second.IsUnitOfWorkScopeDisposed);
            Assert.False(second.IsDependencyScopeDisposed);
            Assert.Equal(ConnectionState.Open, second.OrdersConnection!.State);
            AssertReadOnlyQueryRows(first, seed.OperationId);
            AssertReadOnlyQueryRows(second, seed.OperationId, expectedStages: new[] { "before-gate" });
            await AssertRowsAsync(host.DatabaseFiles, expectedOrders, expectedReports);

            host.Probe.ReleaseReadOnlyScope(second.OperationId);
            var secondResult = await (ReferenceEquals(firstFinished, firstRequest) ? secondRequest : firstRequest)
                .WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(second.OperationId, secondResult.OperationId);
            await WaitForScopeDisposedAsync(second);
            AssertScopeResourcesDisposed(second);
            AssertReadOnlyQueryRows(second, seed.OperationId);
            await AssertRowsAsync(host.DatabaseFiles, expectedOrders, expectedReports);
        }
        finally
        {
            host.Probe.ReleaseAllReadOnlyScopes();
            var pending = new[] { firstRequest, secondRequest }
                .Where(task => task != null)
                .Cast<Task<ScenarioExecutionResult>>()
                .ToArray();
            if (pending.Length > 0)
            {
                try
                {
                    await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(10));
                }
                catch
                {
                }
            }
        }
    }

    [Theory]
    [InlineData("Dapper")]
    [InlineData("EfBridge")]
    public async Task BackgroundWorker_ShouldReportFailuresAndUseFreshScopes(string storageMode)
    {
        await using var host = await RunningSampleHost.StartAsync(storageMode);
        var expectedOrders = new List<OrderRow>();

        var commit = await SubmitWorkerAsync(host.Client, SqlUnitOfWorkScenarios.CrossLayerCommit);
        var committed = await WaitForOperationAsync(host.Client, commit.OperationId);
        Assert.Equal("Completed", committed.Status);
        expectedOrders.Add(Order(committed.Result!, "application", "application", 1, "application"));
        expectedOrders.Add(Order(committed.Result!, "repository", "repository", 2, "repository"));
        await AssertRowsAsync(host.DatabaseFiles, expectedOrders, Array.Empty<ReportRow>());

        var rollback = await SubmitWorkerAsync(host.Client, SqlUnitOfWorkScenarios.CrossLayerRollback);
        var rolledBack = await WaitForOperationAsync(host.Client, rollback.OperationId);
        Assert.Equal("Completed", rolledBack.Status);
        await AssertRowsAsync(host.DatabaseFiles, expectedOrders, Array.Empty<ReportRow>());

        var probeOne = await SubmitWorkerAsync(host.Client, SqlUnitOfWorkScenarios.ScopeIsolationProbe);
        var probeTwo = await SubmitWorkerAsync(host.Client, SqlUnitOfWorkScenarios.ScopeIsolationProbe);
        var firstScope = await WaitForOperationAsync(host.Client, probeOne.OperationId);
        var secondScope = await WaitForOperationAsync(host.Client, probeTwo.OperationId);
        Assert.NotEqual(firstScope.Result!.ScopeId, secondScope.Result!.ScopeId);
        var firstObservation = host.Probe.GetObservation(firstScope.Result.OperationId);
        var secondObservation = host.Probe.GetObservation(secondScope.Result.OperationId);
        await WaitForScopeDisposedAsync(firstObservation);
        await WaitForScopeDisposedAsync(secondObservation);
        AssertScopeResourcesDisposed(firstObservation);
        AssertScopeResourcesDisposed(secondObservation);
        AssertDistinctScopeResources(firstObservation, secondObservation);
        AssertReadOnlyQueryRows(firstObservation, firstScope.Result.OperationId, expectSeedRows: false);
        await AssertRowsAsync(host.DatabaseFiles, expectedOrders, Array.Empty<ReportRow>());
        AssertReadOnlyQueryRows(secondObservation, secondScope.Result.OperationId, expectSeedRows: false);
        await AssertRowsAsync(host.DatabaseFiles, expectedOrders, Array.Empty<ReportRow>());

        var failed = await SubmitWorkerAsync(host.Client, SqlUnitOfWorkScenarios.CaughtSqlException);
        var failedOperation = await WaitForOperationAsync(host.Client, failed.OperationId);
        Assert.Equal("Failed", failedOperation.Status);
        Assert.Equal("InvalidOperationException", failedOperation.ErrorType);
        Assert.Contains("rollback-only=True", failedOperation.Error, StringComparison.Ordinal);
        await AssertRowsAsync(host.DatabaseFiles, expectedOrders, Array.Empty<ReportRow>());
    }

    [Theory]
    [InlineData("Dapper")]
    [InlineData("EfBridge")]
    public async Task HangfireServer_ShouldRunRealJobsReportFailuresAndUseFreshScopes(string storageMode)
    {
        await using var host = await RunningSampleHost.StartAsync(storageMode);
        var expectedOrders = new List<OrderRow>();

        var commit = await SubmitJobAsync(host.Client, SqlUnitOfWorkScenarios.CrossLayerCommit);
        Assert.False(string.IsNullOrWhiteSpace(commit.JobId));
        var committed = await WaitForOperationAsync(host.Client, commit.OperationId);
        Assert.Equal("Completed", committed.Status);
        expectedOrders.Add(Order(committed.Result!, "application", "application", 1, "application"));
        expectedOrders.Add(Order(committed.Result!, "repository", "repository", 2, "repository"));
        await AssertRowsAsync(host.DatabaseFiles, expectedOrders, Array.Empty<ReportRow>());

        var rollback = await SubmitJobAsync(host.Client, SqlUnitOfWorkScenarios.CrossLayerRollback);
        var rolledBack = await WaitForOperationAsync(host.Client, rollback.OperationId);
        Assert.Equal("Completed", rolledBack.Status);
        await AssertRowsAsync(host.DatabaseFiles, expectedOrders, Array.Empty<ReportRow>());

        var probeOne = await SubmitJobAsync(host.Client, SqlUnitOfWorkScenarios.ScopeIsolationProbe);
        var probeTwo = await SubmitJobAsync(host.Client, SqlUnitOfWorkScenarios.ScopeIsolationProbe);
        var firstScope = await WaitForOperationAsync(host.Client, probeOne.OperationId);
        var secondScope = await WaitForOperationAsync(host.Client, probeTwo.OperationId);
        Assert.NotEqual(firstScope.Result!.ScopeId, secondScope.Result!.ScopeId);
        var firstObservation = host.Probe.GetObservation(firstScope.Result.OperationId);
        var secondObservation = host.Probe.GetObservation(secondScope.Result.OperationId);
        await WaitForScopeDisposedAsync(firstObservation);
        await WaitForScopeDisposedAsync(secondObservation);
        AssertScopeResourcesDisposed(firstObservation);
        AssertScopeResourcesDisposed(secondObservation);
        AssertDistinctScopeResources(firstObservation, secondObservation);
        AssertReadOnlyQueryRows(firstObservation, firstScope.Result.OperationId, expectSeedRows: false);
        await AssertRowsAsync(host.DatabaseFiles, expectedOrders, Array.Empty<ReportRow>());
        AssertReadOnlyQueryRows(secondObservation, secondScope.Result.OperationId, expectSeedRows: false);
        await AssertRowsAsync(host.DatabaseFiles, expectedOrders, Array.Empty<ReportRow>());

        var failed = await SubmitJobAsync(host.Client, SqlUnitOfWorkScenarios.CaughtSqlException);
        var failedOperation = await WaitForOperationAsync(host.Client, failed.OperationId);
        Assert.Equal("Failed", failedOperation.Status);
        Assert.False(string.IsNullOrWhiteSpace(failedOperation.JobId));
        var diagnostics = await host.Client.GetFromJsonAsync<CapEventsDiagnosticsSnapshot>(
            $"/api/diagnostics/events?operationId={Uri.EscapeDataString(failed.OperationId)}", JsonOptions);
        var diagnosticsSnapshot = Assert.IsType<CapEventsDiagnosticsSnapshot>(diagnostics);
        Assert.Equal(failedOperation.JobId, Assert.Single(diagnosticsSnapshot.Operations).JobId);
        Assert.Equal("InvalidOperationException", failedOperation.ErrorType);
        Assert.Contains("rollback-only=True", failedOperation.Error, StringComparison.Ordinal);
        await AssertRowsAsync(host.DatabaseFiles, expectedOrders, Array.Empty<ReportRow>());
    }

    private static async Task<ScenarioExecutionResult> RunScenarioAsync(HttpClient client, string scenario)
    {
        return await RunScenarioPathAsync(client, $"/api/scenarios/{scenario}");
    }

    private static async Task<ScenarioExecutionResult> RunScenarioPathAsync(HttpClient client, string path)
    {
        using var response = await client.PostAsync(path, content: null);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"请求 {path} 失败：HTTP {(int)response.StatusCode}；响应：{body}");
        return JsonSerializer.Deserialize<ScenarioExecutionResult>(body, JsonOptions)
               ?? throw new Xunit.Sdk.XunitException($"场景响应无法解析：{body}");
    }

    private static void AssertCrossLayerBuilderState(SampleScopeObservation observation, string operationId)
    {
        AssertQueryStates(observation, "application", ApplicationLayerQuerySql,
            ExpectedQueryParameters(operationId, "application", 1, "application"));
        AssertQueryStates(observation, "repository", RepositoryLayerQuerySql,
            ExpectedRepositoryParameters(operationId));
    }

    private static Dictionary<string, object?> ExpectedQueryParameters(string operationId, string layer,
        int sequence, string value) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["@_p_0"] = operationId,
        ["@_p_1"] = layer,
        ["@_p_2"] = sequence,
        ["@_p_3"] = value
    };

    private static Dictionary<string, object?> ExpectedRepositoryParameters(string operationId) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["@_p_0"] = operationId,
            ["@_p_1"] = $"{operationId}:repository",
            ["@_p_2"] = 2,
            ["@_p_3"] = "repository"
        };

    private static void AssertQueryStates(SampleScopeObservation observation, string role, string expectedSql,
        IReadOnlyDictionary<string, object?> expectedParameters)
    {
        var states = observation.QueryStates.Where(item => item.Role == role).ToArray();
        Assert.NotEmpty(states);
        foreach (var state in states)
        {
            Assert.True(string.Equals(expectedSql, state.Sql, StringComparison.Ordinal),
                $"{role}/{state.Stage} 的实际 SQL：{state.Sql}");
            Assert.Equal(
                expectedParameters.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase).ToArray(),
                state.Parameters.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase).ToArray());
        }
    }

    private static void AssertCrossLayerBuilderRows(SampleScopeObservation observation, string operationId)
    {
        var applicationRows = observation.QueryRows.Single(item =>
            item.Role == "application" && item.Stage == "cross-layer").Rows;
        var repositoryRows = observation.QueryRows.Single(item =>
            item.Role == "repository" && item.Stage == "cross-layer").Rows;

        Assert.Equal(new[]
        {
            new OrderRow($"{operationId}:application", operationId, "application", 1, "application")
        }, ConvertRows(applicationRows));
        Assert.Equal(new[]
        {
            new OrderRow($"{operationId}:repository", operationId, "repository", 2, "repository")
        }, ConvertRows(repositoryRows));
    }

    private static void AssertReadOnlyQuerySnapshots(SampleScopeObservation observation, string targetOperationId)
    {
        AssertQueryStates(observation, "application", ApplicationLayerQuerySql,
            ExpectedQueryParameters(targetOperationId, "application", 1, "application"));
        AssertQueryStates(observation, "repository", RepositoryLayerQuerySql,
            ExpectedRepositoryParameters(targetOperationId));
    }

    private static void AssertReadOnlyQueryRows(SampleScopeObservation observation, string targetOperationId,
        bool expectSeedRows = true, params string[] expectedStages)
    {
        if (expectedStages.Length == 0)
            expectedStages = new[] { "before-gate", "after-release" };

        var applicationRows = observation.QueryRows.Where(item => item.Role == "application").ToArray();
        var repositoryRows = observation.QueryRows.Where(item => item.Role == "repository").ToArray();
        Assert.Equal(expectedStages.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            applicationRows.Select(item => item.Stage).Distinct().OrderBy(item => item, StringComparer.Ordinal));
        Assert.Equal(expectedStages.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            repositoryRows.Select(item => item.Stage).Distinct().OrderBy(item => item, StringComparer.Ordinal));

        var expectedApplication = expectSeedRows
            ? new[] { new OrderRow($"{targetOperationId}:application", targetOperationId, "application", 1, "application") }
            : Array.Empty<OrderRow>();
        var expectedRepository = expectSeedRows
            ? new[] { new OrderRow($"{targetOperationId}:repository", targetOperationId, "repository", 2, "repository") }
            : Array.Empty<OrderRow>();

        Assert.All(applicationRows, item => Assert.Equal(expectedApplication, ConvertRows(item.Rows)));
        Assert.All(repositoryRows, item => Assert.Equal(expectedRepository, ConvertRows(item.Rows)));
    }

    private static OrderRow[] ConvertRows(IEnumerable<OrderQueryRow> rows) => rows
        .Select(row => new OrderRow(row.Id, row.OperationId, row.Layer, row.Sequence, row.Value))
        .ToArray();

    private static void AssertDistinctScopeResources(SampleScopeObservation first,
        SampleScopeObservation second)
    {
        Assert.NotEqual(first.ScopeId, second.ScopeId);
        Assert.True(first.CurrentScopeMatched);
        Assert.True(second.CurrentScopeMatched);
        Assert.NotSame(first.Scope, second.Scope);
        Assert.NotSame(first.ScopeManager, second.ScopeManager);
        Assert.NotSame(first.ApplicationQuery, first.RepositoryQuery);
        Assert.NotSame(second.ApplicationQuery, second.RepositoryQuery);
        Assert.NotSame(first.ApplicationQuery, second.ApplicationQuery);
        Assert.NotSame(first.RepositoryQuery, second.RepositoryQuery);
        Assert.NotSame(first.DbContext, second.DbContext);
        Assert.NotSame(first.OrdersConnection, second.OrdersConnection);
        if (first.EfBridgeEnabled)
            Assert.Same(first.ContextConnection, first.OrdersConnection);
        if (second.EfBridgeEnabled)
            Assert.Same(second.ContextConnection, second.OrdersConnection);
    }

    private static async Task WaitForScopeDisposedAsync(SampleScopeObservation observation)
    {
        try
        {
            await Task.WhenAll(observation.UnitOfWorkScopeDisposedTask, observation.DependencyScopeDisposedTask)
                .WaitAsync(TimeSpan.FromSeconds(15));
        }
        catch (TimeoutException exception)
        {
            throw new Xunit.Sdk.XunitException(
                $"操作 {observation.OperationId} 的 scope 未在 15 秒内完整释放：" +
                $"unitOfWorkDisposed={observation.IsUnitOfWorkScopeDisposed}; " +
                $"dependencyScopeDisposed={observation.IsDependencyScopeDisposed}。", exception);
        }
    }

    private static void AssertScopeResourcesDisposed(SampleScopeObservation observation)
    {
        Assert.True(observation.IsUnitOfWorkScopeDisposed);
        Assert.True(observation.IsDependencyScopeDisposed);
        Assert.NotNull(observation.OrdersConnection);
        Assert.Equal(ConnectionState.Closed, observation.OrdersConnection!.State);
        Assert.Throws<ObjectDisposedException>(() => observation.ApplicationQuery.Query());
        Assert.Throws<ObjectDisposedException>(() => observation.RepositoryQuery.Query());
        Assert.Throws<ObjectDisposedException>(() => observation.DbContext.Entry(new OrderRecord()));
    }

    private static async Task<ScenarioController.ScenarioFailure> RunFailingScenarioAsync(HttpClient client,
        string scenario)
    {
        using var response = await client.PostAsync($"/api/scenarios/{scenario}", content: null);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        return JsonSerializer.Deserialize<ScenarioController.ScenarioFailure>(body, JsonOptions)
               ?? throw new Xunit.Sdk.XunitException($"失败响应无法解析：{body}");
    }

    private static async Task<ScenarioSubmission> SubmitWorkerAsync(HttpClient client, string scenario) =>
        await SubmitAsync(client, $"/api/worker/scenarios/{scenario}");

    private static async Task<ScenarioSubmission> SubmitJobAsync(HttpClient client, string scenario) =>
        await SubmitAsync(client, $"/api/jobs/scenarios/{scenario}");

    private static async Task<ScenarioExecutionResult> CompleteWorkerScenarioAsync(HttpClient client,
        string scenario)
    {
        var submission = await SubmitWorkerAsync(client, scenario);
        var operation = await WaitForOperationAsync(client, submission.OperationId);
        Assert.Equal("Completed", operation.Status);
        return operation.Result ?? throw new Xunit.Sdk.XunitException("Worker 没有返回场景结果。");
    }

    private static async Task<ScenarioExecutionResult> CompleteJobScenarioAsync(HttpClient client, string scenario)
    {
        var submission = await SubmitJobAsync(client, scenario);
        var operation = await WaitForOperationAsync(client, submission.OperationId);
        Assert.Equal("Completed", operation.Status);
        Assert.False(string.IsNullOrWhiteSpace(operation.JobId));
        return operation.Result ?? throw new Xunit.Sdk.XunitException("Hangfire job 没有返回场景结果。");
    }

    private static async Task<ScenarioSubmission> SubmitAsync(HttpClient client, string path)
    {
        using var response = await client.PostAsync(path, content: null);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return JsonSerializer.Deserialize<ScenarioSubmission>(body, JsonOptions)
               ?? throw new Xunit.Sdk.XunitException($"后台操作响应无法解析：{body}");
    }

    private static async Task<ScenarioOperationSnapshot> WaitForOperationAsync(HttpClient client,
        string operationId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        ScenarioOperationSnapshot? last = null;
        try
        {
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
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new Xunit.Sdk.XunitException(
                $"操作 {operationId} 未在期限内结束；最后状态：{JsonSerializer.Serialize(last, JsonOptions) ?? "未创建"}。");
        }
    }

    private static OrderRow Order(ScenarioExecutionResult result, string suffix, string layer, int sequence,
        string value) => new($"{result.OperationId}:{suffix}", result.OperationId, layer, sequence, value);

    private static async Task AssertRowsAsync(SampleDatabaseFiles files, IEnumerable<OrderRow> expectedOrders,
        IEnumerable<ReportRow> expectedReports)
    {
        var orders = await ReadOrderRowsAsync(files.OrdersPath);
        var reports = await ReadReportRowsAsync(files.ReportingPath);
        Assert.Equal(expectedOrders.OrderBy(row => row.Id).ToArray(), orders.OrderBy(row => row.Id).ToArray());
        Assert.Equal(expectedReports.OrderBy(row => row.Id).ToArray(), reports.OrderBy(row => row.Id).ToArray());
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

    public sealed record ScenarioSubmission(string OperationId, string? JobId, string Status);
    private sealed record OrderRow(string Id, string OperationId, string Layer, int Sequence, string Value);
    private sealed record ReportRow(string Id, string OperationId, string Value);

    private sealed class RunningSampleHost : IAsyncDisposable
    {
        private readonly IHost _host;
        private readonly HttpClient _client;
        private readonly ScenarioBackgroundWorker _worker;
        private readonly SampleScopeIsolationProbe _probe;
        private readonly string _directoryPath;
        private readonly int _port;
        private bool _disposed;

        private RunningSampleHost(IHost host, HttpClient client, SampleDatabaseFiles databaseFiles,
            ScenarioBackgroundWorker worker, SampleScopeIsolationProbe probe, int port)
        {
            _host = host;
            _client = client;
            DatabaseFiles = databaseFiles;
            _worker = worker;
            _probe = probe;
            _directoryPath = databaseFiles.DirectoryPath;
            _port = port;
        }

        public HttpClient Client => _client;
        public SampleDatabaseFiles DatabaseFiles { get; }
        public SampleScopeIsolationProbe Probe => _probe;
        public SqlUnitOfWorkMemoryEventStore MemoryEvents =>
            _host.Services.GetRequiredService<SqlUnitOfWorkMemoryEventStore>();

        public static async Task<RunningSampleHost> StartAsync(string storageMode)
        {
            var probe = new SampleScopeIsolationProbe();
            var host = Program.CreateHostBuilder(Array.Empty<string>(), storageMode,
                    webBuilder => webBuilder.UseUrls("http://127.0.0.1:0"),
                    services =>
                    {
                        services.AddSingleton(probe);
                        services.AddScoped<SampleScopeLifetime>();
                        services.RemoveAll<SqlDbConnectionFactoryRegistration>();
                        services.AddSingleton(new SqlDbConnectionFactoryRegistration
                        {
                            ProviderKey = "bing.sqlite",
                            Factory = probe.CreateSqliteConnection
                        });
                        services.RemoveAll<SqlUnitOfWorkApplicationService>();
                        services.AddScoped(provider =>
                        {
                            var scopeLifetime = provider.GetRequiredService<SampleScopeLifetime>();
                            return new SqlUnitOfWorkApplicationService(
                                provider.GetRequiredService<ISqlQuery>(),
                                provider.GetRequiredService<ISqlExecutor>(),
                                provider.GetRequiredService<ISqlQueryFactory>(),
                                provider.GetRequiredService<IUnitOfWorkScopeManager>(),
                                provider.GetRequiredService<OrdersRepository>(),
                                provider.GetRequiredService<ReportingRepository>(),
                                provider.GetRequiredService<SqlUnitOfWorkSampleOptions>(),
                                probe,
                                scopeLifetime,
                                null,
                                null,
                                provider.GetRequiredService<ISimpleEventBus>());
                        });
                    })
                .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["SqlUow:ProcessEvidencePath"] = string.Empty }))
                .Build();
            try
            {
                await host.StartAsync();
                var address = host.Services.GetRequiredService<IServer>()
                    .Features.Get<IServerAddressesFeature>()?.Addresses
                    .Single(item => item.StartsWith("http://127.0.0.1:", StringComparison.Ordinal));
                Assert.False(string.IsNullOrWhiteSpace(address));
                var endpoint = new Uri(address!);
                var client = new HttpClient { BaseAddress = endpoint };
                var databaseFiles = host.Services.GetRequiredService<SampleDatabaseFiles>();
                var worker = host.Services.GetRequiredService<ScenarioBackgroundWorker>();
                return new RunningSampleHost(host, client, databaseFiles, worker, probe, endpoint.Port);
            }
            catch
            {
                host.Dispose();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;
            _disposed = true;

            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await _host.StopAsync(timeout.Token);
                Assert.True(_worker.IsStopped, "BackgroundService 在 StopAsync 返回前应已退出。");
                await AssertPortIsClosedAsync(_port);
            }
            finally
            {
                _client.Dispose();
                _host.Dispose();
            }

            Assert.False(Directory.Exists(_directoryPath),
                $"宿主停止并 Dispose 后仍残留数据库目录：{_directoryPath}");
        }
    }

    private static async Task AssertPortIsClosedAsync(int port)
    {
        using var client = new TcpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
            throw new Xunit.Sdk.XunitException($"宿主停止后端口 {port} 仍可连接。");
        }
        catch (SocketException)
        {
        }
        catch (OperationCanceledException)
        {
            throw new Xunit.Sdk.XunitException($"检查期限内未确认端口 {port} 已关闭。");
        }
    }
}

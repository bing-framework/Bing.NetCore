using Bing.Samples.SqlUnitOfWork.Application;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Database;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Diagnostics;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Bing.Samples.SqlUnitOfWork.Tests;

public sealed class ScenarioOperationStoreTests
{
    [Fact]
    public async Task ProcessEvidence_ShouldSurviveRestartAndProjectCallbackFailuresAndRollbackFilters()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sql-uow-evidence-{Guid.NewGuid():N}.jsonl");
        try
        {
            var options = new SqlUnitOfWorkSampleOptions(false, ProcessEvidencePath: path);
            var store = new ScenarioOperationStore(options);
            var operationId = Guid.NewGuid().ToString("N");
            var eventId = $"{operationId}:orders-event";
            var reportingEventId = $"{operationId}:reporting-event";
            store.Create(operationId, "cap-multi-source-rollback");
            store.SetJobId(operationId, "hangfire-job-42");
            store.RecordCapEvent(operationId, "cap-multi-source-rollback", eventId, "trace-42",
                "root-42", "orders", "sqluow.orders.changed");
            store.MarkPublisherCallStarted(operationId, eventId, "orders");
            store.RecordProcessStep(operationId, eventId, "orders", "business-event-sql-started");
            store.RecordCapEvent(operationId, "cap-multi-source-rollback", reportingEventId, "trace-42",
                "root-42", "reporting", "sqluow.reporting.changed");

            var ordersFailure = CreateCallbackFailure("orders flush failed after commit", "orders", 0);
            var reportingFailure = CreateCallbackFailure("reporting flush failed after commit", "reporting", 1);
            store.MarkFailed(operationId, new AggregateException(ordersFailure, reportingFailure));

            var rollbackOperationId = $"rollback-{Guid.NewGuid():N}";
            var rollbackEventId = $"{rollbackOperationId}:event";
            store.Create(rollbackOperationId, "cap-multi-source-rollback");
            store.RecordCapEvent(rollbackOperationId, "cap-multi-source-rollback", rollbackEventId,
                "rollback-trace-7", "rollback-root-7", "orders", "sqluow.orders.changed");
            store.RecordProcessStep(rollbackOperationId, rollbackEventId, "orders", "publisher-call-started");
            store.MarkSucceeded(rollbackOperationId, new ScenarioExecutionResult(rollbackOperationId,
                "cap-multi-source-rollback", Guid.NewGuid(), true, 0, 0, 0, Outcome: "RolledBack"));

            var recovered = new ScenarioOperationStore(options);
            Assert.True(recovered.TryGet(operationId, out var snapshot));
            Assert.Equal("hangfire-job-42", snapshot.JobId);
            Assert.Equal("trace-42", Assert.Single(snapshot.ProcessEvents!, item => item.Source == "orders").TraceId);
            Assert.Contains(snapshot.ProcessSteps!, item => item.Step == "publisher-call-started");
            Assert.Collection(snapshot.CallbackFailures!,
                failure =>
                {
                    Assert.Equal("orders", failure.Source);
                    Assert.Equal(0, failure.ResourceSequence);
                    Assert.Equal("CommittedCallbackFailed", failure.Outcome);
                },
                failure =>
                {
                    Assert.Equal("reporting", failure.Source);
                    Assert.Equal(1, failure.ResourceSequence);
                    Assert.Equal("CommittedCallbackFailed", failure.Outcome);
                });

            var diagnostics = new CapEventDiagnosticsService(options, recovered,
                new ConfigurationBuilder().Build());
            var byOperation = await diagnostics.ReadAsync(rollbackOperationId, null, null, null, null,
                CancellationToken.None);
            var rollbackOperation = Assert.Single(byOperation.Operations);
            Assert.Equal("rolled-back", rollbackOperation.Status);
            Assert.Equal("unknown", rollbackOperation.OrdersCommitStatus);
            Assert.Equal("unknown", rollbackOperation.ReportingCommitStatus);
            Assert.Equal("RolledBack", rollbackOperation.ExecutionOutcome);

            var byEvent = await diagnostics.ReadAsync(null, rollbackEventId, null, null, null,
                CancellationToken.None);
            var byTrace = await diagnostics.ReadAsync(null, null, "rollback-trace-7", null, null,
                CancellationToken.None);
            var bySource = await diagnostics.ReadAsync(rollbackOperationId, null, null, "orders", null,
                CancellationToken.None);
            foreach (var filtered in new[] { byOperation, byEvent, byTrace, bySource })
            {
                var processEvent = Assert.Single(filtered.Events);
                Assert.Equal(rollbackOperationId, processEvent.OperationId);
                Assert.Equal(rollbackEventId, processEvent.EventId);
                Assert.Equal("process-journal", processEvent.EvidenceSource);
                Assert.Equal("unknown", processEvent.DatabaseCommitStatus);
                Assert.Equal("publisher-call-started", Assert.Single(processEvent.ProcessSteps).Step);
            }

            var callbackSnapshot = await diagnostics.ReadAsync(operationId, null, null, null, null,
                CancellationToken.None);
            var callbackOperation = Assert.Single(callbackSnapshot.Operations);
            Assert.Equal("hangfire-job-42", callbackOperation.JobId);
            Assert.Equal("failed", callbackOperation.Status);
            Assert.Collection(callbackOperation.CallbackFailures,
                failure => Assert.Equal(("orders", 0, "CommittedCallbackFailed"),
                    (failure.Source, failure.ResourceSequence, failure.Outcome)),
                failure => Assert.Equal(("reporting", 1, "CommittedCallbackFailed"),
                    (failure.Source, failure.ResourceSequence, failure.Outcome)));
            Assert.All(callbackSnapshot.Events, processEvent =>
            {
                Assert.Equal("process-journal", processEvent.EvidenceSource);
                Assert.Equal("unknown", processEvent.DatabaseCommitStatus);
            });
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static InvalidOperationException CreateCallbackFailure(string message, string source, int sequence)
    {
        var failure = new InvalidOperationException(message);
        failure.Data["Bing.Uow.ResourceOutcome"] = "CommittedCallbackFailed";
        failure.Data["Bing.Uow.ResourceSequence"] = sequence;
        failure.Data["Bing.Cap.DataSourceKey"] = source;
        return failure;
    }
}

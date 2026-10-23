using Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;
using Bing.Data.Sql;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Database;
using Bing.Samples.SqlUnitOfWork.Jobs;
using Hangfire;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.SqlUnitOfWork.Controllers;

[ApiController]
[Route("api/jobs/scenarios")]
public sealed class JobsController : ControllerBase
{
    private readonly IBackgroundJobClient _jobs;
    private readonly ScenarioOperationStore _operations;
    private readonly IDatabaseContextAccessor _databaseContextAccessor;
    private readonly SqlUnitOfWorkSampleOptions _options;

    public JobsController(IBackgroundJobClient jobs, ScenarioOperationStore operations,
        IDatabaseContextAccessor databaseContextAccessor, SqlUnitOfWorkSampleOptions options)
    {
        _jobs = jobs;
        _operations = operations;
        _databaseContextAccessor = databaseContextAccessor;
        _options = options;
    }

    [HttpPost("{scenario}")]
    public IActionResult Enqueue(string scenario)
    {
        var operationId = Guid.NewGuid().ToString("N");
        _operations.Create(operationId, scenario);
        try
        {
            var context = _options.OperationsEnabled
                ? OperationContextSnapshot.Capture(operationId, _databaseContextAccessor)
                : null;
            var jobId = _options.OperationsEnabled
                ? _jobs.Enqueue<SqlUnitOfWorkJob>(job => job.ExecuteAsync(operationId, scenario,
                    context, CancellationToken.None))
                : _jobs.Enqueue<SqlUnitOfWorkJob>(job => job.ExecuteAsync(operationId, scenario));
            _operations.SetJobId(operationId, jobId);
            return Accepted($"/api/operations/{operationId}", new { operationId, jobId, status = "Queued" });
        }
        catch (Exception exception)
        {
            _operations.MarkFailed(operationId, exception);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { operationId, error = exception.Message });
        }
    }
}

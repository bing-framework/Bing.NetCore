using Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.SqlUnitOfWork.Controllers;

[ApiController]
[Route("api/operations")]
public sealed class OperationsController : ControllerBase
{
    private readonly ScenarioOperationStore _operations;

    public OperationsController(ScenarioOperationStore operations) => _operations = operations;

    [HttpGet("{operationId}")]
    public ActionResult<ScenarioOperationSnapshot> Get(string operationId) =>
        _operations.TryGet(operationId, out var operation) ? Ok(operation) : NotFound();
}

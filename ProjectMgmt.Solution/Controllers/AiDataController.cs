using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.AiDataOps.Contracts;

namespace ProjectMgmt.Solution.Controllers;

[Route("api/ai-data")]
public class AiDataController : ApiControllerBase
{
    private readonly IEvaluationSetCatalog _evaluationSets;

    public AiDataController(IEvaluationSetCatalog evaluationSets)
    {
        _evaluationSets = evaluationSets;
    }

    [HttpGet("evaluation-set/current")]
    public async Task<ActionResult<EvaluationSetDescriptor>> GetCurrentEvaluationSet(
        CancellationToken cancellationToken)
    {
        var result = await _evaluationSets.GetCurrentAsync(cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}

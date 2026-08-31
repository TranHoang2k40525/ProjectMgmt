using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.AiDataOps.Contracts;

namespace ProjectMgmt.Solution.Api;

[Route("api/ai-data")]
public sealed class AiDataController(IEvaluationSetCatalog evaluationSets) : ApiControllerBase
{
    [HttpGet("evaluation-set/current")]
    public async Task<ActionResult<EvaluationSetDescriptor>> GetCurrentEvaluationSet(
        CancellationToken cancellationToken)
    {
        var result = await evaluationSets.GetCurrentAsync(cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}

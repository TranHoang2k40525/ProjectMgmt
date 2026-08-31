using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.BuildingBlocks.Results;

namespace ProjectMgmt.Solution.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<T> FromResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return StatusCode(StatusCodeFor(result.Error.Type), ToProblem(result.Error));
    }

    protected IActionResult FromResult(Result result)
    {
        if (result.IsSuccess)
        {
            return NoContent();
        }

        return StatusCode(StatusCodeFor(result.Error.Type), ToProblem(result.Error));
    }

    private ProblemDetails ToProblem(Error error) => new()
    {
        Status = StatusCodeFor(error.Type),
        Title = error.Message,
        Type = $"urn:projectmgmt:error:{error.Code}",
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = error.Code,
            ["traceId"] = HttpContext.TraceIdentifier,
            ["errors"] = error.Details
        }
    };

    private static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError
    };
}

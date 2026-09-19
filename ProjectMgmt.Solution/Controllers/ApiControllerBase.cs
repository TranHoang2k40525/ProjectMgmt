using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.BuildingBlocks.Results;

namespace ProjectMgmt.Solution.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult FromResult(Result result)
    {
        if (result.IsSuccess)
        {
            return Ok();
        }

        return HandleFailure(result.Error);
    }

    protected ActionResult<T> FromResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return HandleFailure(result.Error);
    }

    private ObjectResult HandleFailure(Error error)
    {
        return error.Type switch
        {
            ErrorType.Validation => BadRequest(CreateProblemDetails("Validation Error", error)),
            ErrorType.NotFound => NotFound(CreateProblemDetails("Not Found", error)),
            ErrorType.Conflict => Conflict(CreateProblemDetails("Conflict", error)),
            ErrorType.Unauthorized => Unauthorized(CreateProblemDetails("Unauthorized", error)),
            ErrorType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, CreateProblemDetails("Forbidden", error)),
            _ => BadRequest(CreateProblemDetails("Bad Request", error))
        };
    }

    private static ProblemDetails CreateProblemDetails(string title, Error error) => new()
    {
        Title = title,
        Detail = error.Description,
        Status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest
        },
        Extensions = { { "code", error.Code } }
    };
}

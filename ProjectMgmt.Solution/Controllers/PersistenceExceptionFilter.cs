using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace ProjectMgmt.Solution.Controllers;

// Translate only expected constraint failures; never expose SQL or connection details.
public class PersistenceExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not DbUpdateException { InnerException: MySqlException mysql }) return;
        var (status, code, detail) = mysql.Number switch
        {
            1062 => (409, "DATA_CONFLICT", "A record with these unique values already exists."),
            1451 => (409, "DATA_IN_USE", "This record is still referenced by other data."),
            1452 => (400, "REFERENCE_INVALID", "A referenced record does not exist."),
            3819 or 4025 => (400, "CONSTRAINT_INVALID", "The supplied values violate a data constraint."),
            1406 => (400, "VALUE_TOO_LONG", "A supplied value exceeds the allowed length."),
            _ => (0, "", "")
        };
        if (status == 0) return;
        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = status,
            Title = status == 409 ? "Conflict" : "Validation Error",
            Detail = detail,
            Extensions = { ["code"] = code }
        }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}

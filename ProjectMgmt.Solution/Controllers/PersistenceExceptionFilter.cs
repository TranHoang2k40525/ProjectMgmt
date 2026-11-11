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
            1062 => (409, "DATA_CONFLICT", "Dữ liệu với giá trị duy nhất này đã tồn tại trong hệ thống."),
            1451 => (409, "DATA_IN_USE", "Dữ liệu này đang được tham chiếu và sử dụng bởi các mục khác."),
            1452 => (400, "REFERENCE_INVALID", "Dữ liệu tham chiếu không tồn tại trong hệ thống."),
            3819 or 4025 => (400, "CONSTRAINT_INVALID", "Dữ liệu cung cấp vi phạm ràng buộc dữ liệu của hệ thống."),
            1406 => (400, "VALUE_TOO_LONG", "Độ dài của dữ liệu vượt quá giới hạn cho phép."),
            _ => (0, "", "")
        };
        if (status == 0) return;
        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = status,
            Title = status == 409 ? "Xung đột dữ liệu" : "Dữ liệu không hợp lệ",
            Detail = detail,
            Extensions = { ["code"] = code }
        }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}

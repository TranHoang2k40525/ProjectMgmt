using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure;

namespace ProjectMgmt.Solution.Security;

public sealed class ProjectAuthorizationFilter(PlanningDbContext planning, ProjectAccess access) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        Guid? Id(string key) => context.ActionArguments.TryGetValue(key, out var value) && value is Guid id ? id : null;
        var projectId = Id("projectId");
        if (projectId is null && Id("boardId") is Guid boardId)
            projectId = await planning.Boards.Where(b => b.Id == boardId).Select(b => (Guid?)b.ProjectId).SingleOrDefaultAsync();
        if (projectId is null && Id("columnId") is Guid columnId)
            projectId = await (from c in planning.BoardColumns join b in planning.Boards on c.BoardId equals b.Id where c.Id == columnId select (Guid?)b.ProjectId).SingleOrDefaultAsync();
        if (projectId is null && Id("transitionId") is Guid transitionId)
            projectId = await planning.WorkflowTransitions.Where(t => t.Id == transitionId).Select(t => (Guid?)t.ProjectId).SingleOrDefaultAsync();
        if (projectId is null)
        {
            if (Id("boardId") is not null || Id("columnId") is not null || Id("transitionId") is not null)
            { context.Result = new NotFoundResult(); return; }
            await next(); return; // Project list/create have their own scoped checks.
        }
        var method = context.HttpContext.Request.Method;
        var path = context.HttpContext.Request.Path.Value ?? "";
        var permission = HttpMethods.IsGet(method) ? "project.read"
            : path.Contains("/boards", StringComparison.Ordinal) || path.Contains("/workflow", StringComparison.Ordinal) ? "project.settings.manage"
            : HttpMethods.IsDelete(method) ? "project.delete" : "project.update";
        if (!await access.AllowsAsync(projectId.Value, permission))
        {
            context.Result = new ObjectResult(new { code = "PROJECT_PERMISSION_DENIED", message = "Bạn không có quyền thực hiện thao tác trong dự án này." }) { StatusCode = 403 };
            return;
        }
        await next();
    }
}

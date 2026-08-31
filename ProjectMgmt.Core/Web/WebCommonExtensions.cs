using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProjectMgmt.BuildingBlocks.Common;
using ProjectMgmt.BuildingBlocks.Exceptions;
using ProjectMgmt.BuildingBlocks.Security;
using Serilog;
using Serilog.Context;

namespace ProjectMgmt.BuildingBlocks.Web;

public static class WebCommonExtensions
{
    public static IServiceCollection AddProjectMgmtWebCommon(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        return services;
    }

    public static IApplicationBuilder UseProjectMgmtWebCommon(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging();
        app.UseExceptionHandler();
        return app;
    }
}

public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = IsAcceptable(supplied) ? supplied! : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static bool IsAcceptable(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128 && value.All(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
}

public class GlobalExceptionHandler : IExceptionHandler
{
    private static readonly Action<Microsoft.Extensions.Logging.ILogger, string, Exception?> LogUnhandledException =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1000, nameof(GlobalExceptionHandler)),
            "Unhandled exception with code {ErrorCode}");

    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, code) = exception switch
        {
            ValidationException validation => (StatusCodes.Status400BadRequest, "Validation failed", validation.Code),
            NotFoundException notFound => (StatusCodes.Status404NotFound, "Resource not found", notFound.Code),
            ConflictException conflict => (StatusCodes.Status409Conflict, "Conflict", conflict.Code),
            UnauthorizedException unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized", unauthorized.Code),
            ForbiddenException forbidden => (StatusCodes.Status403Forbidden, "Forbidden", forbidden.Code),
            DomainRuleException domainRule => (StatusCodes.Status422UnprocessableEntity, "Domain rule violated", domainRule.Code),
            ProjectMgmtException projectException => (StatusCodes.Status500InternalServerError, "Application failure", projectException.Code),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error", "server.unexpected")
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status >= 500 ? "An unexpected error occurred." : exception.Message,
            Instance = httpContext.Request.Path,
            Type = $"https://httpstatuses.com/{status}"
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(_logger, code, exception);
        }

        if (exception is ValidationException validationException)
        {
            problem.Extensions["errors"] = validationException.Errors;
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}

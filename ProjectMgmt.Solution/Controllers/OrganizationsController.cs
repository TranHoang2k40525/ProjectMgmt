using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.Modules.Planning.ProjectManagement.Application.IServices;
using ProjectMgmt.Solution.Services;

namespace ProjectMgmt.Solution.Controllers;

[Route("api/v1/organizations")]
[Route("api/organizations")]
public class OrganizationsController : ApiControllerBase
{
    private static readonly Action<ILogger, Exception?> LogCreateFailure =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2101, "OrganizationCreateFailure"),
            "Lỗi không xử lý khi tạo tổ chức.");

    private readonly IProjectManagementService _service;
    private readonly IWorkspaceProvisioningService _provisioning;
    private readonly ICurrentUserContext _currentUser;
    private readonly IPermissionEvaluator _permissions;
    private readonly ILogger<OrganizationsController> _logger;

    public OrganizationsController(
        IProjectManagementService service,
        IWorkspaceProvisioningService provisioning,
        ICurrentUserContext currentUser,
        IPermissionEvaluator permissions,
        ILogger<OrganizationsController> logger)
    {
        _service = service;
        _provisioning = provisioning;
        _currentUser = currentUser;
        _permissions = permissions;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrganizationDto>>> GetAll()
    {
        var result = await _service.GetOrganizationsAsync();
        if (!result.IsSuccess) return FromResult(result);
        var visible = new List<OrganizationDto>();
        foreach (var org in result.Value)
            if (org.OwnerId == _currentUser.UserId || (_currentUser.UserId.HasValue && await _permissions.HasPermissionAsync(_currentUser.UserId.Value, "project.create", "Organization", org.Id))) visible.Add(org);
        return Ok(visible);
    }

    [HttpPost]
    public async Task<ActionResult<OrganizationDto>> Create(
        [FromBody] CreateOrganizationDto request)
    {
        try
        {
            if (!_currentUser.UserId.HasValue)
            {
                return Unauthorized(new
                {
                    success = false,
                    errorCode = "AUTH_UNAUTHENTICATED",
                    message = "Phiên đăng nhập không hợp lệ."
                });
            }

            var result = await _provisioning.CreateOrganizationAsync(
                request,
                _currentUser.UserId.Value);
            return result.IsSuccess
                ? StatusCode(StatusCodes.Status201Created, result.Value)
                : FromResult(result);
        }
        catch (Exception exception)
        {
            LogCreateFailure(_logger, exception);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    success = false,
                    errorCode = "ORGANIZATION_CREATE_FAILED",
                    message = "Không thể tạo tổ chức. Vui lòng thử lại sau."
                });
        }
    }
}

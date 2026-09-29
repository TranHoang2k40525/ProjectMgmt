using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.Modules.Planning.ProjectManagement.Application.IServices;
using ProjectMgmt.Solution.Services;

namespace ProjectMgmt.Solution.Controllers;

[Route("api/v1/organizations")]
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
    private readonly ILogger<OrganizationsController> _logger;

    public OrganizationsController(
        IProjectManagementService service,
        IWorkspaceProvisioningService provisioning,
        ICurrentUserContext currentUser,
        ILogger<OrganizationsController> logger)
    {
        _service = service;
        _provisioning = provisioning;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrganizationDto>>> GetAll(CancellationToken cancellationToken) =>
        FromResult(await _service.GetOrganizationsAsync(cancellationToken));

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

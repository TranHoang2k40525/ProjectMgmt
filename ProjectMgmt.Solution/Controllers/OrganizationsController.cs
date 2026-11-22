using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Planning.Infrastructure;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.Modules.Planning.ProjectManagement.Application.IServices;
using ProjectMgmt.Solution.Services;

namespace ProjectMgmt.Solution.Controllers;

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
    public async Task<ActionResult<IReadOnlyList<OrganizationDto>>> GetAll([FromServices] MySqlConnector.MySqlConnection db)
    {
        var result = await _service.GetOrganizationsAsync();
        if (!result.IsSuccess) return FromResult(result);

        var memberOrgIds = new HashSet<Guid>();
        if (_currentUser.UserId.HasValue)
        {
            try
            {
                if (db.State != System.Data.ConnectionState.Open) await db.OpenAsync();
                await using var cmd = db.CreateCommand();
                cmd.CommandText = @"
                    SELECT OrgId FROM OrganizationMember WHERE UserId = @userId
                    UNION
                    SELECT p.OrgId FROM UserRole ur JOIN Project p ON ur.ScopeId = p.Id WHERE ur.UserId = @userId AND ur.ScopeType = 'Project';";
                cmd.Parameters.AddWithValue("@userId", _currentUser.UserId.Value.ToString());
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (Guid.TryParse(reader.GetValue(0)?.ToString(), out var mOrgId))
                    {
                        memberOrgIds.Add(mOrgId);
                    }
                }
            }
            catch
            {
                // Ignore fallback query error
            }
        }

        var visible = new List<OrganizationDto>();
        foreach (var org in result.Value)
        {
            if (org.OwnerId == _currentUser.UserId
                || memberOrgIds.Contains(org.Id)
                || (_currentUser.UserId.HasValue && await _permissions.HasPermissionAsync(_currentUser.UserId.Value, "project.create", "Organization", org.Id)))
            {
                visible.Add(org);
            }
        }
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

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromServices] PlanningDbContext planning)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Unauthorized(new { success = false, message = "Phiên đăng nhập không hợp lệ." });
        }

        var org = await planning.Organizations.FirstOrDefaultAsync(o => o.Id == id);
        if (org is null) return NotFound(new { success = false, message = "Không tìm thấy tổ chức." });

        // Làm chặt quyền: Chỉ Chủ tổ chức (Owner) hoặc Admin toàn hệ thống mới được phép xóa!
        var isOwner = org.OwnerId == _currentUser.UserId.Value;
        var isSysAdmin = await _permissions.HasPermissionAsync(_currentUser.UserId.Value, "system.admin", "System", Guid.Empty);
        if (!isOwner && !isSysAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                success = false,
                errorCode = "FORBIDDEN_ORG_DELETE",
                message = "Chỉ Chủ tổ chức (Org Owner) mới có quyền hạn tối cao để xóa tổ chức này."
            });
        }

        var orgName = org.Name;
        var strategy = planning.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync<IActionResult>(async () =>
            {
                planning.ChangeTracker.Clear();
                var conn = planning.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                {
                    await conn.OpenAsync();
                }

                await using var trans = await planning.Database.BeginTransactionAsync();
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = trans.GetDbTransaction();

                var param = cmd.CreateParameter();
                param.ParameterName = "@orgId";
                param.Value = id.ToString();
                cmd.Parameters.Add(param);

                cmd.CommandText = @"
                    SET FOREIGN_KEY_CHECKS = 0;
                    DELETE FROM OrganizationMember WHERE OrgId = @orgId;
                    DELETE FROM OrganizationInvitation WHERE OrgId = @orgId;
                    DELETE FROM OrganizationInviteLink WHERE OrgId = @orgId;
                    DELETE FROM organizationworkflowtemplate WHERE OrgId = @orgId;
                    DELETE FROM projectinvitation WHERE OrgId = @orgId;
                    DELETE FROM UserRole WHERE ScopeType = 'Organization' AND ScopeId = @orgId;
                    DELETE FROM UserRole WHERE ScopeType = 'Project' AND ScopeId IN (SELECT Id FROM Project WHERE OrgId = @orgId);
                    DELETE FROM Project WHERE OrgId = @orgId;
                    DELETE FROM Organization WHERE Id = @orgId;
                    SET FOREIGN_KEY_CHECKS = 1;
                ";
                await cmd.ExecuteNonQueryAsync();
                await trans.CommitAsync();

                return Ok(new { success = true, message = $"Đã xóa thành công tổ chức '{orgName}'." });
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xóa tổ chức {OrgId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Lỗi hệ thống khi xóa tổ chức: " + ex.Message
            });
        }
    }
}

using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ProjectMgmt.Solution.Controller;

[Route("api/v1/auth")]
[ApiController]
[AllowAnonymous]
public class AccountController : ControllerBase
{
    private readonly IAccountServices _accountServices;

    public AccountController(IAccountServices accountServices)
    {
        _accountServices = accountServices;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(RegisterResult), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(RegisterResult), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] AccountDto request,
        CancellationToken cancellationToken)
    {
        var result = await _accountServices.RegisterAsync(request, cancellationToken);
        if (result.Success)
        {
            return Accepted(result);
        }

        return result.ErrorCode is "AUTH_EMAIL_ALREADY_EXISTS"
            or "AUTH_EMAIL_VERIFICATION_PENDING"
            or "AUTH_PHONE_ALREADY_EXISTS"
            or "AUTH_REGISTRATION_CONFLICT"
            ? Conflict(result)
            : BadRequest(result);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(ResultLogin), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResultLogin), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] AccountDto request,
        CancellationToken cancellationToken)
    {
        var result = await _accountServices.LoginAsync(request, cancellationToken);
        return result.Success ? Ok(result) : Unauthorized(result);
    }
}

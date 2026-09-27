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
            : result.ErrorCode == "AUTH_EMAIL_DELIVERY_FAILED"
                ? StatusCode(StatusCodes.Status503ServiceUnavailable, result)
            : BadRequest(result);
    }

    [HttpPost("otp/send")]
    [ProducesResponseType(typeof(OtpResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(OtpResult), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(OtpResult), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(OtpResult), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(OtpResult), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(OtpResult), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SendOtp(
        [FromBody] AccountDto request,
        CancellationToken cancellationToken)
    {
        var result = await _accountServices.SendOtpAsync(request, cancellationToken);
        if (result.Success)
        {
            return Accepted(result);
        }

        if (result.ErrorCode == "AUTH_OTP_RATE_LIMITED")
        {
            Response.Headers.RetryAfter = result.ResendAfterSeconds?.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            return StatusCode(StatusCodes.Status429TooManyRequests, result);
        }

        return result.ErrorCode switch
        {
            "AUTH_ACCOUNT_NOT_FOUND" => NotFound(result),
            "AUTH_EMAIL_ALREADY_VERIFIED" => Conflict(result),
            "AUTH_EMAIL_DELIVERY_FAILED" => StatusCode(StatusCodes.Status503ServiceUnavailable, result),
            _ => BadRequest(result)
        };
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

using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ProjectMgmt.Solution.Controllers;

[Route("api/v1/auth")]
[ApiController]
public class AccountController : ControllerBase
{
    private static readonly Action<ILogger, string, Exception?> LogEndpointFailure =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(2001, "AuthEndpointFailure"),
            "Lỗi không xử lý tại endpoint xác thực {Endpoint}.");

    private readonly IAccountServices _accountServices;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IAccountServices accountServices,
        ILogger<AccountController> logger)
    {
        _accountServices = accountServices;
        _logger = logger;
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth-otp")]
    public async Task<IActionResult> Register([FromBody] AccountDto request)
    {
        try
        {
            var result = await _accountServices.RegisterAsync(
                request.Email,
                request.Password,
                request.FullName,
                request.PhoneNumber);
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
        catch (Exception exception)
        {
            return InternalError("register", exception);
        }
    }

    [HttpPost("otp/send")]
    [EnableRateLimiting("auth-otp")]
    public async Task<IActionResult> SendOtp([FromBody] AccountDto request)
    {
        try
        {
            var result = await _accountServices.SendOtpAsync(request.Email, request.Purpose);
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
        catch (Exception exception)
        {
            return InternalError("otp/send", exception);
        }
    }

    [HttpPost("otp/verify")]
    [EnableRateLimiting("auth-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] AccountDto request)
    {
        try
        {
            var otpCode = request.Code ?? request.OtpCode;
            var result = await _accountServices.VerifyOtpAsync(
                request.Email,
                otpCode,
                request.Purpose);
            if (result.Success)
            {
                return Ok(result);
            }

            return result.ErrorCode switch
            {
                "AUTH_ACCOUNT_NOT_FOUND" => NotFound(result),
                "AUTH_ACCOUNT_DISABLED" => Conflict(result),
                "AUTH_OTP_EXPIRED" => StatusCode(StatusCodes.Status410Gone, result),
                "AUTH_OTP_ATTEMPTS_EXCEEDED" => StatusCode(StatusCodes.Status429TooManyRequests, result),
                _ => BadRequest(result)
            };
        }
        catch (Exception exception)
        {
            return InternalError("otp/verify", exception);
        }
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth-login")]
    public async Task<IActionResult> Login([FromBody] AccountDto request)
    {
        try
        {
            var result = await _accountServices.LoginAsync(
                request.Email,
                request.Password,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString());
            if (result.Success)
            {
                return Ok(result);
            }

            return result.ErrorCode is "AUTH_ACCOUNT_DISABLED" or "AUTH_EMAIL_NOT_VERIFIED"
                ? StatusCode(StatusCodes.Status403Forbidden, result)
                : Unauthorized(result);
        }
        catch (Exception exception)
        {
            return InternalError("login", exception);
        }
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] AccountDto request)
    {
        try
        {
            var result = await _accountServices.RefreshTokenAsync(
                request.RefreshToken,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString());
            if (result.Success)
            {
                return Ok(result);
            }

            return result.ErrorCode is "AUTH_ACCOUNT_DISABLED" or "AUTH_EMAIL_NOT_VERIFIED"
                ? StatusCode(StatusCodes.Status403Forbidden, result)
                : Unauthorized(result);
        }
        catch (Exception exception)
        {
            return InternalError("refresh-token", exception);
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] AccountDto request)
    {
        try
        {
            var result = await _accountServices.LogoutAsync(request.RefreshToken);
            return Ok(result);
        }
        catch (Exception exception)
        {
            return InternalError("logout", exception);
        }
    }

    private ObjectResult InternalError(string endpoint, Exception exception)
    {
        LogEndpointFailure(_logger, endpoint, exception);
        return StatusCode(
            StatusCodes.Status500InternalServerError,
            new Result
            {
                Success = false,
                ErrorCode = "AUTH_INTERNAL_ERROR",
                Message = "Hệ thống xác thực đang gặp lỗi. Vui lòng thử lại sau."
            });
    }
}

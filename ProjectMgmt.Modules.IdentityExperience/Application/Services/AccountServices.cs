using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.IRepositories;
using PhoneNumbers;

namespace IdentityExperience.Application.Services;

public class AccountServices : IAccountServices
{
    private const string VerifyEmailPurpose = "VerifyEmail";
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    private const int OtpResendAfterSeconds = 60;
    private static readonly Regex PasswordPattern = new(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,128}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IIdentityRepository _identityRepository;
    private readonly IPasswordService _passwordService;
    private readonly IOtpCodeService _otpCodeService;
    private readonly TimeProvider _timeProvider;

    public AccountServices(
        IIdentityRepository identityRepository,
        IPasswordService passwordService,
        IOtpCodeService otpCodeService,
        TimeProvider timeProvider)
    {
        _identityRepository = identityRepository;
        _passwordService = passwordService;
        _otpCodeService = otpCodeService;
        _timeProvider = timeProvider;
    }

    public async Task<RegisterResult> RegisterAsync(
        AccountDto account,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateRegistration(account);
        if (validationError is not null)
        {
            return validationError;
        }

        var email = account.Email!.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        var existingUser = await _identityRepository.GetUserByNormalizedEmailAsync(
            normalizedEmail,
            cancellationToken: cancellationToken);

        if (existingUser is not null)
        {
            return new RegisterResult
            {
                Success = false,
                ErrorCode = existingUser.IsEmailVerified
                    ? "AUTH_EMAIL_ALREADY_EXISTS"
                    : "AUTH_EMAIL_VERIFICATION_PENDING",
                Message = existingUser.IsEmailVerified
                    ? "Email đã được sử dụng."
                    : "Email đã đăng ký nhưng chưa xác minh. Hãy yêu cầu gửi lại OTP.",
                UserId = existingUser.Id,
                Email = existingUser.Email,
                Status = existingUser.IsEmailVerified ? "Active" : "PendingVerification"
            };
        }

        if (!TryNormalizePhone(account.PhoneNumber!, out var phoneNumber))
        {
            return Failure("AUTH_PHONE_INVALID", "Số điện thoại không hợp lệ.");
        }

        if (await _identityRepository.PhoneNumberExistsAsync(phoneNumber, cancellationToken))
        {
            return Failure("AUTH_PHONE_ALREADY_EXISTS", "Số điện thoại đã được sử dụng.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var userId = Guid.NewGuid();
        var otpCode = _otpCodeService.GenerateCode();
        var otpExpiresAt = now.Add(OtpLifetime);

        var user = new User
        {
            Id = userId,
            Email = email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = _passwordService.Hash(account.Password!),
            IsEmailVerified = false,
            IsActive = true,
            SecurityStamp = Guid.NewGuid(),
            CreatedAt = now
        };

        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DisplayName = account.FullName!.Trim(),
            PhoneNumber = phoneNumber,
            Timezone = "Asia/Ho_Chi_Minh",
            CreatedAt = now
        };

        var otp = new OtpCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CodeHash = _otpCodeService.Hash(normalizedEmail, VerifyEmailPurpose, otpCode),
            Purpose = VerifyEmailPurpose,
            ExpiresAt = otpExpiresAt,
            IsUsed = false,
            AttemptCount = 0,
            CreatedAt = now
        };

        var created = await _identityRepository.CreatePendingRegistrationAsync(
            user,
            profile,
            otp,
            cancellationToken);

        if (!created)
        {
            return Failure(
                "AUTH_REGISTRATION_CONFLICT",
                "Email hoặc số điện thoại vừa được sử dụng bởi một yêu cầu khác.");
        }

        return new RegisterResult
        {
            Success = true,
            Message = "Đã tạo tài khoản chờ xác minh email.",
            UserId = userId,
            Email = email,
            Status = "PendingVerification",
            OtpExpiresAt = otpExpiresAt,
            ResendAfterSeconds = OtpResendAfterSeconds
        };
    }

    public Task<ResultLogin> LoginAsync(
        AccountDto account,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ResultLogin
        {
            Success = false,
            ErrorCode = "AUTH_LOGIN_NOT_IMPLEMENTED",
            Message = "Đăng nhập sẽ được hoàn thiện ở mốc JWT và refresh token."
        });
    }

    public Task<OtpResult> SendOtpAsync(
        AccountDto account,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new OtpResult
        {
            Success = false,
            ErrorCode = "AUTH_OTP_DELIVERY_NOT_IMPLEMENTED",
            Message = "Gửi OTP qua email sẽ được hoàn thiện ở mốc SMTP."
        });
    }

    public Task<OtpResult> VerifyOtpAsync(
        AccountDto account,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new OtpResult
        {
            Success = false,
            ErrorCode = "AUTH_OTP_VERIFICATION_NOT_IMPLEMENTED",
            Message = "Xác minh OTP sẽ được hoàn thiện ở mốc kích hoạt tài khoản."
        });
    }

    public Task<ResultLogin> RefreshTokenAsync(
        string refreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ResultLogin
        {
            Success = false,
            ErrorCode = "AUTH_REFRESH_NOT_IMPLEMENTED",
            Message = "Làm mới token sẽ được hoàn thiện ở mốc JWT và refresh token."
        });
    }

    public Task<Result> LogoutAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new Result
        {
            Success = false,
            ErrorCode = "AUTH_LOGOUT_NOT_IMPLEMENTED",
            Message = "Đăng xuất sẽ được hoàn thiện ở mốc JWT và refresh token."
        });
    }

    private static RegisterResult? ValidateRegistration(AccountDto account)
    {
        if (string.IsNullOrWhiteSpace(account.Email)
            || !new EmailAddressAttribute().IsValid(account.Email.Trim()))
        {
            return Failure("AUTH_EMAIL_INVALID", "Email không hợp lệ.");
        }

        if (account.Email.Trim().Length > 256)
        {
            return Failure("AUTH_EMAIL_TOO_LONG", "Email không được vượt quá 256 ký tự.");
        }

        if (string.IsNullOrWhiteSpace(account.FullName)
            || account.FullName.Trim().Length is < 2 or > 150)
        {
            return Failure("AUTH_FULL_NAME_INVALID", "Họ tên phải có từ 2 đến 150 ký tự.");
        }

        if (string.IsNullOrWhiteSpace(account.PhoneNumber))
        {
            return Failure("AUTH_PHONE_REQUIRED", "Số điện thoại là bắt buộc.");
        }

        if (string.IsNullOrEmpty(account.Password) || !PasswordPattern.IsMatch(account.Password))
        {
            return Failure(
                "AUTH_PASSWORD_WEAK",
                "Mật khẩu phải dài 8-128 ký tự và có chữ hoa, chữ thường, số, ký tự đặc biệt.");
        }

        return null;
    }

    private static bool TryNormalizePhone(string rawPhoneNumber, out string phoneNumber)
    {
        phoneNumber = string.Empty;
        try
        {
            var phoneUtil = PhoneNumberUtil.GetInstance();
            var parsed = phoneUtil.Parse(rawPhoneNumber.Trim(), "VN");
            if (!phoneUtil.IsValidNumber(parsed))
            {
                return false;
            }

            phoneNumber = phoneUtil.Format(parsed, PhoneNumberFormat.E164);
            return true;
        }
        catch (NumberParseException)
        {
            return false;
        }
    }

    private static RegisterResult Failure(string errorCode, string message)
    {
        return new RegisterResult
        {
            Success = false,
            ErrorCode = errorCode,
            Message = message
        };
    }
}

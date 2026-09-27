using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.IRepositories;
using IdentityExperience.Domain.Models;
using PhoneNumbers;

namespace IdentityExperience.Application.Services;

public class AccountServices : IAccountServices
{
    private const string VerifyEmailPurpose = "VerifyEmail";
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    private const int OtpResendAfterSeconds = 60;
    private const int OtpMaximumAttempts = 5;
    private static readonly Regex PasswordPattern = new(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,128}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IIdentityRepository _identityRepository;
    private readonly IPasswordService _passwordService;
    private readonly IOtpCodeService _otpCodeService;
    private readonly IEmailService _emailService;
    private readonly ITokenService _tokenService;
    private readonly TimeProvider _timeProvider;

    public AccountServices(
        IIdentityRepository identityRepository,
        IPasswordService passwordService,
        IOtpCodeService otpCodeService,
        IEmailService emailService,
        ITokenService tokenService,
        TimeProvider timeProvider)
    {
        _identityRepository = identityRepository;
        _passwordService = passwordService;
        _otpCodeService = otpCodeService;
        _emailService = emailService;
        _tokenService = tokenService;
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

        var emailDelivered = await _emailService.SendOtpAsync(
            email,
            profile.DisplayName,
            otpCode,
            otpExpiresAt,
            cancellationToken);

        if (!emailDelivered)
        {
            return new RegisterResult
            {
                Success = false,
                ErrorCode = "AUTH_EMAIL_DELIVERY_FAILED",
                Message = "Tài khoản đã được tạo ở trạng thái chờ xác minh nhưng chưa gửi được email OTP. Hãy thử gửi lại.",
                UserId = userId,
                Email = email,
                Status = "PendingVerification",
                OtpExpiresAt = otpExpiresAt,
                ResendAfterSeconds = OtpResendAfterSeconds
            };
        }

        return new RegisterResult
        {
            Success = true,
            Message = "Đã tạo tài khoản và gửi mã OTP xác minh email.",
            UserId = userId,
            Email = email,
            Status = "PendingVerification",
            OtpExpiresAt = otpExpiresAt,
            ResendAfterSeconds = OtpResendAfterSeconds
        };
    }

    public async Task<ResultLogin> LoginAsync(
        AccountDto account,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeEmail(account.Email, out _, out var normalizedEmail)
            || string.IsNullOrEmpty(account.Password)
            || account.Password.Length > 128)
        {
            return LoginFailure("AUTH_INVALID_CREDENTIALS", "Email hoặc mật khẩu không đúng.");
        }

        var user = await _identityRepository.GetUserByNormalizedEmailAsync(
            normalizedEmail,
            cancellationToken: cancellationToken);
        if (user?.PasswordHash is null
            || !_passwordService.Verify(account.Password, user.PasswordHash))
        {
            return LoginFailure("AUTH_INVALID_CREDENTIALS", "Email hoặc mật khẩu không đúng.");
        }

        if (!user.IsActive)
        {
            return LoginFailure("AUTH_ACCOUNT_DISABLED", "Tài khoản đã bị vô hiệu hóa.");
        }

        if (!user.IsEmailVerified)
        {
            return LoginFailure(
                "AUTH_EMAIL_NOT_VERIFIED",
                "Email chưa được xác minh. Hãy xác minh OTP trước khi đăng nhập.");
        }

        var profile = await _identityRepository.GetUserProfileAsync(
            user.Id,
            cancellationToken: cancellationToken);
        var roles = await _identityRepository.GetSystemRoleNamesAsync(user.Id, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var tokenSet = _tokenService.CreateTokenSet(user, profile, roles, now);
        var refreshToken = BuildRefreshToken(user.Id, tokenSet, ipAddress, userAgent, now);

        var sessionStatus = await _identityRepository.CreateLoginSessionAsync(
            user.Id,
            refreshToken,
            now,
            cancellationToken);
        if (sessionStatus != LoginSessionStatus.Created)
        {
            return sessionStatus switch
            {
                LoginSessionStatus.AccountDisabled =>
                    LoginFailure("AUTH_ACCOUNT_DISABLED", "Tài khoản đã bị vô hiệu hóa."),
                LoginSessionStatus.EmailNotVerified =>
                    LoginFailure("AUTH_EMAIL_NOT_VERIFIED", "Email chưa được xác minh."),
                _ => LoginFailure("AUTH_INVALID_CREDENTIALS", "Không thể tạo phiên đăng nhập.")
            };
        }

        return BuildLoginSuccess(user, profile, roles, tokenSet, now);
    }

    public async Task<OtpResult> SendOtpAsync(
        AccountDto account,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeEmail(account.Email, out var email, out var normalizedEmail))
        {
            return OtpFailure("AUTH_EMAIL_INVALID", "Email không hợp lệ.");
        }

        var purpose = string.IsNullOrWhiteSpace(account.Purpose)
            ? VerifyEmailPurpose
            : account.Purpose.Trim();
        if (!string.Equals(purpose, VerifyEmailPurpose, StringComparison.Ordinal))
        {
            return OtpFailure(
                "AUTH_OTP_PURPOSE_INVALID",
                "Endpoint này chỉ hỗ trợ mục đích VerifyEmail.");
        }

        var user = await _identityRepository.GetUserByNormalizedEmailAsync(
            normalizedEmail,
            cancellationToken: cancellationToken);
        if (user is null)
        {
            return OtpFailure("AUTH_ACCOUNT_NOT_FOUND", "Không tìm thấy tài khoản với email này.");
        }

        if (user.IsEmailVerified)
        {
            return OtpFailure("AUTH_EMAIL_ALREADY_VERIFIED", "Email đã được xác minh.");
        }

        var profile = await _identityRepository.GetUserProfileAsync(
            user.Id,
            cancellationToken: cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var otpCode = _otpCodeService.GenerateCode();
        var otp = new OtpCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CodeHash = _otpCodeService.Hash(normalizedEmail, VerifyEmailPurpose, otpCode),
            Purpose = VerifyEmailPurpose,
            ExpiresAt = now.Add(OtpLifetime),
            IsUsed = false,
            AttemptCount = 0,
            CreatedAt = now
        };

        var issueResult = await _identityRepository.ReplaceEmailVerificationOtpAsync(
            user.Id,
            otp,
            now,
            TimeSpan.FromSeconds(OtpResendAfterSeconds),
            cancellationToken);

        if (issueResult.Status == OtpIssueStatus.RateLimited)
        {
            return new OtpResult
            {
                Success = false,
                ErrorCode = "AUTH_OTP_RATE_LIMITED",
                Message = "OTP vừa được cấp. Vui lòng chờ trước khi yêu cầu mã mới.",
                Email = email,
                Status = "PendingVerification",
                OtpExpiresAt = issueResult.ExpiresAt,
                ResendAfterSeconds = issueResult.RetryAfterSeconds
            };
        }

        if (issueResult.Status == OtpIssueStatus.EmailAlreadyVerified)
        {
            return OtpFailure("AUTH_EMAIL_ALREADY_VERIFIED", "Email đã được xác minh.");
        }

        if (issueResult.Status == OtpIssueStatus.UserNotFound)
        {
            return OtpFailure("AUTH_ACCOUNT_NOT_FOUND", "Tài khoản không còn tồn tại.");
        }

        var emailDelivered = await _emailService.SendOtpAsync(
            email,
            profile?.DisplayName ?? email,
            otpCode,
            otp.ExpiresAt,
            cancellationToken);
        if (!emailDelivered)
        {
            return new OtpResult
            {
                Success = false,
                ErrorCode = "AUTH_EMAIL_DELIVERY_FAILED",
                Message = "Đã tạo OTP mới nhưng chưa gửi được email. Hãy thử lại sau thời gian chờ.",
                Email = email,
                Status = "PendingVerification",
                OtpExpiresAt = otp.ExpiresAt,
                ResendAfterSeconds = OtpResendAfterSeconds
            };
        }

        return new OtpResult
        {
            Success = true,
            Message = "Đã gửi mã OTP mới.",
            Email = email,
            Status = "PendingVerification",
            OtpExpiresAt = otp.ExpiresAt,
            ResendAfterSeconds = OtpResendAfterSeconds
        };
    }

    public async Task<OtpResult> VerifyOtpAsync(
        AccountDto account,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeEmail(account.Email, out var email, out var normalizedEmail))
        {
            return OtpFailure("AUTH_EMAIL_INVALID", "Email không hợp lệ.");
        }

        var purpose = string.IsNullOrWhiteSpace(account.Purpose)
            ? VerifyEmailPurpose
            : account.Purpose.Trim();
        if (!string.Equals(purpose, VerifyEmailPurpose, StringComparison.Ordinal))
        {
            return OtpFailure(
                "AUTH_OTP_PURPOSE_INVALID",
                "Endpoint này chỉ hỗ trợ mục đích VerifyEmail.");
        }

        var otpCode = account.OtpCode?.Trim() ?? string.Empty;
        if (otpCode.Length != 6 || otpCode.Any(character => character is < '0' or > '9'))
        {
            return OtpFailure("AUTH_OTP_INVALID_FORMAT", "OTP phải gồm đúng 6 chữ số.");
        }

        var user = await _identityRepository.GetUserByNormalizedEmailAsync(
            normalizedEmail,
            cancellationToken: cancellationToken);
        if (user is null)
        {
            return OtpFailure("AUTH_ACCOUNT_NOT_FOUND", "Không tìm thấy tài khoản với email này.");
        }

        var expectedHash = _otpCodeService.Hash(normalizedEmail, VerifyEmailPurpose, otpCode);
        var verification = await _identityRepository.VerifyEmailOtpAsync(
            user.Id,
            VerifyEmailPurpose,
            expectedHash,
            _timeProvider.GetUtcNow().UtcDateTime,
            OtpMaximumAttempts,
            cancellationToken);

        return verification.Status switch
        {
            OtpVerificationStatus.Verified => new OtpResult
            {
                Success = true,
                Message = "Xác minh email thành công. Tài khoản đã sẵn sàng đăng nhập.",
                Email = email,
                Status = "Active",
                AttemptsRemaining = verification.AttemptsRemaining
            },
            OtpVerificationStatus.AlreadyVerified => new OtpResult
            {
                Success = true,
                Message = "Email đã được xác minh trước đó.",
                Email = email,
                Status = "Active"
            },
            OtpVerificationStatus.UserNotFound =>
                OtpFailure("AUTH_ACCOUNT_NOT_FOUND", "Tài khoản không còn tồn tại."),
            OtpVerificationStatus.AccountDisabled =>
                OtpFailure("AUTH_ACCOUNT_DISABLED", "Tài khoản đã bị vô hiệu hóa."),
            OtpVerificationStatus.NoActiveCode =>
                OtpFailure("AUTH_OTP_NOT_FOUND", "Không có OTP đang hoạt động. Hãy yêu cầu mã mới."),
            OtpVerificationStatus.Expired =>
                OtpFailure("AUTH_OTP_EXPIRED", "OTP đã hết hạn. Hãy yêu cầu mã mới."),
            OtpVerificationStatus.AttemptsExceeded => new OtpResult
            {
                Success = false,
                ErrorCode = "AUTH_OTP_ATTEMPTS_EXCEEDED",
                Message = "OTP đã bị khóa sau quá nhiều lần nhập sai. Hãy yêu cầu mã mới.",
                Email = email,
                Status = "PendingVerification",
                AttemptsRemaining = 0
            },
            _ => new OtpResult
            {
                Success = false,
                ErrorCode = "AUTH_OTP_INVALID",
                Message = "OTP không đúng.",
                Email = email,
                Status = "PendingVerification",
                AttemptsRemaining = verification.AttemptsRemaining
            }
        };
    }

    public async Task<ResultLogin> RefreshTokenAsync(
        string refreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > 512)
        {
            return LoginFailure("AUTH_REFRESH_TOKEN_INVALID", "Refresh token không hợp lệ.");
        }

        var currentTokenHash = _tokenService.HashRefreshToken(refreshToken.Trim());
        var context = await _identityRepository.GetRefreshSessionContextAsync(
            currentTokenHash,
            cancellationToken);
        if (context is null)
        {
            return LoginFailure("AUTH_REFRESH_TOKEN_INVALID", "Refresh token không hợp lệ.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var tokenSet = _tokenService.CreateTokenSet(
            context.User,
            context.Profile,
            context.SystemRoles,
            now);
        var replacementToken = BuildRefreshToken(
            context.User.Id,
            tokenSet,
            ipAddress,
            userAgent,
            now);

        var rotationStatus = await _identityRepository.RotateRefreshTokenAsync(
            currentTokenHash,
            replacementToken,
            now,
            cancellationToken);
        if (rotationStatus != TokenRotationStatus.Rotated)
        {
            return rotationStatus switch
            {
                TokenRotationStatus.TokenExpired =>
                    LoginFailure("AUTH_REFRESH_TOKEN_EXPIRED", "Refresh token đã hết hạn."),
                TokenRotationStatus.ReuseDetected =>
                    LoginFailure(
                        "AUTH_REFRESH_TOKEN_REUSE_DETECTED",
                        "Phát hiện refresh token đã bị tái sử dụng; các phiên liên quan đã bị thu hồi."),
                TokenRotationStatus.AccountDisabled =>
                    LoginFailure("AUTH_ACCOUNT_DISABLED", "Tài khoản đã bị vô hiệu hóa."),
                TokenRotationStatus.EmailNotVerified =>
                    LoginFailure("AUTH_EMAIL_NOT_VERIFIED", "Email chưa được xác minh."),
                _ => LoginFailure("AUTH_REFRESH_TOKEN_INVALID", "Refresh token không còn hiệu lực.")
            };
        }

        return BuildLoginSuccess(
            context.User,
            context.Profile,
            context.SystemRoles,
            tokenSet,
            now);
    }

    public async Task<Result> LogoutAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(refreshToken) && refreshToken.Length <= 512)
        {
            var refreshTokenHash = _tokenService.HashRefreshToken(refreshToken.Trim());
            await _identityRepository.RevokeRefreshTokenAsync(refreshTokenHash, cancellationToken);
        }

        return new Result
        {
            Success = true,
            Message = "Đã thu hồi phiên đăng nhập an toàn."
        };
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

    private static bool TryNormalizeEmail(
        string? rawEmail,
        out string email,
        out string normalizedEmail)
    {
        email = rawEmail?.Trim() ?? string.Empty;
        normalizedEmail = string.Empty;
        if (email.Length is 0 or > 256 || !new EmailAddressAttribute().IsValid(email))
        {
            return false;
        }

        normalizedEmail = email.ToUpperInvariant();
        return true;
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

    private static OtpResult OtpFailure(string errorCode, string message)
    {
        return new OtpResult
        {
            Success = false,
            ErrorCode = errorCode,
            Message = message
        };
    }

    private static RefreshToken BuildRefreshToken(
        Guid userId,
        TokenSet tokenSet,
        string? ipAddress,
        string? userAgent,
        DateTime nowUtc)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenSet.RefreshTokenHash,
            ExpiresAt = tokenSet.RefreshTokenExpiresAt,
            IsRevoked = false,
            CreatedByIp = Truncate(ipAddress, 45),
            UserAgent = Truncate(userAgent, 400),
            CreatedAt = nowUtc
        };
    }

    private static ResultLogin BuildLoginSuccess(
        User user,
        UserProfile? profile,
        List<string> roles,
        TokenSet tokenSet,
        DateTime nowUtc)
    {
        return new ResultLogin
        {
            Success = true,
            Message = "Đăng nhập thành công.",
            AccessToken = tokenSet.AccessToken,
            RefreshToken = tokenSet.RefreshToken,
            AccessTokenExpiresAt = tokenSet.AccessTokenExpiresAt,
            RefreshTokenExpiresAt = tokenSet.RefreshTokenExpiresAt,
            ExpiresInSeconds = Math.Max(
                0,
                (int)(tokenSet.AccessTokenExpiresAt - nowUtc).TotalSeconds),
            UserId = user.Id,
            Email = user.Email,
            FullName = profile?.DisplayName,
            Roles = roles
        };
    }

    private static ResultLogin LoginFailure(string errorCode, string message)
    {
        return new ResultLogin
        {
            Success = false,
            ErrorCode = errorCode,
            Message = message
        };
    }

    private static string? Truncate(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maximumLength ? trimmed : trimmed[..maximumLength];
    }
}

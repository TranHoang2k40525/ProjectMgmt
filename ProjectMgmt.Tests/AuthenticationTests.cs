using System.Security.Cryptography;
using System.Text;
using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using IdentityExperience.Application.Services;
using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.IRepositories;
using IdentityExperience.Domain.Models;
using IdentityExperience.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace ProjectMgmt.Tests;

public class AuthenticationTests
{
    private static readonly DateTime TestNowUtc = new(2026, 9, 28, 2, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PasswordHashCanOnlyBeVerifiedByCorrectPassword()
    {
        var passwordService = new PasswordService();

        var passwordHash = passwordService.Hash("StrongPassword@123");

        Assert.NotEqual("StrongPassword@123", passwordHash);
        Assert.True(passwordService.Verify("StrongPassword@123", passwordHash));
        Assert.False(passwordService.Verify("WrongPassword@123", passwordHash));
    }

    [Fact]
    public void OtpUsesSixDigitsAndContextBoundHmac()
    {
        var otpService = CreateOtpService();

        var code = otpService.GenerateCode();
        var codeHash = otpService.Hash("USER@EXAMPLE.COM", "VerifyEmail", code);

        Assert.Equal(6, code.Length);
        Assert.All(code, character => Assert.InRange(character, '0', '9'));
        Assert.True(otpService.Verify("USER@EXAMPLE.COM", "VerifyEmail", code, codeHash));
        Assert.False(otpService.Verify("OTHER@EXAMPLE.COM", "VerifyEmail", code, codeHash));
        Assert.False(otpService.Verify("USER@EXAMPLE.COM", "ResetPassword", code, codeHash));
    }

    [Fact]
    public void JwtServiceCreatesSignedAccessTokenAndHashedRefreshToken()
    {
        var tokenService = CreateRealTokenService();
        var user = CreateUser(isEmailVerified: true);
        var profile = CreateProfile(user.Id);

        var tokens = tokenService.CreateTokenSet(user, profile, ["Admin"], TestNowUtc);

        Assert.Equal(3, tokens.AccessToken.Split('.').Length);
        Assert.NotEqual(tokens.RefreshToken, tokens.RefreshTokenHash);
        Assert.Equal(64, tokens.RefreshTokenHash.Length);
        Assert.Equal(TestNowUtc.AddMinutes(15), tokens.AccessTokenExpiresAt);
        Assert.Equal(TestNowUtc.AddDays(30), tokens.RefreshTokenExpiresAt);
    }

    [Fact]
    public async Task RegistrationCreatesPendingAggregateAndSendsOtp()
    {
        var repository = new FakeIdentityRepository();
        var emailService = new FakeEmailService();
        var otpService = CreateOtpService();
        var service = CreateAccountService(repository, emailService, otpService);

        var result = await service.RegisterAsync(
            "member@example.com",
            "StrongPassword@123",
            "Nguyễn Văn Thành Viên",
            "0901234567");

        Assert.True(result.Success);
        Assert.Equal("PendingVerification", result.Status);
        Assert.NotNull(repository.CreatedUser);
        Assert.NotNull(repository.CreatedProfile);
        Assert.NotNull(repository.CreatedOtp);
        Assert.False(repository.CreatedUser.IsEmailVerified);
        Assert.Equal(repository.CreatedUser.Id, repository.CreatedProfile.UserId);
        Assert.Equal(repository.CreatedUser.Id, repository.CreatedOtp.UserId);
        Assert.Equal("VerifyEmail", repository.CreatedOtp.Purpose);
        Assert.Equal("member@example.com", emailService.RecipientEmail);
        Assert.True(otpService.Verify(
            repository.CreatedUser.NormalizedEmail,
            repository.CreatedOtp.Purpose,
            emailService.OtpCode!,
            repository.CreatedOtp.CodeHash));
    }

    [Fact]
    public async Task RegistrationKeepsPendingAccountWhenSmtpDeliveryFails()
    {
        var repository = new FakeIdentityRepository();
        var emailService = new FakeEmailService { DeliverySucceeds = false };
        var service = CreateAccountService(repository, emailService, CreateOtpService());

        var result = await service.RegisterAsync(
            "member@example.com",
            "StrongPassword@123",
            "Nguyễn Văn Thành Viên",
            "0901234567");

        Assert.False(result.Success);
        Assert.Equal("AUTH_EMAIL_DELIVERY_FAILED", result.ErrorCode);
        Assert.Equal("PendingVerification", result.Status);
        Assert.NotNull(repository.CreatedUser);
        Assert.NotNull(repository.CreatedOtp);
        Assert.False(repository.CreatedUser.IsEmailVerified);
    }

    [Fact]
    public async Task ResendOtpReturnsCooldownFromRepositoryTransaction()
    {
        var user = CreateUser(isEmailVerified: false);
        var repository = new FakeIdentityRepository
        {
            User = user,
            OtpIssue = new OtpIssueResult
            {
                Status = OtpIssueStatus.RateLimited,
                ExpiresAt = TestNowUtc.AddMinutes(4),
                RetryAfterSeconds = 42
            }
        };
        var service = CreateAccountService(repository, new FakeEmailService(), CreateOtpService());

        var result = await service.SendOtpAsync(user.Email, "VerifyEmail");

        Assert.False(result.Success);
        Assert.Equal("AUTH_OTP_RATE_LIMITED", result.ErrorCode);
        Assert.Equal(42, result.ResendAfterSeconds);
    }

    [Fact]
    public async Task VerificationReturnsActiveOnlyAfterRepositoryTransactionSucceeds()
    {
        var user = CreateUser(isEmailVerified: false);
        var repository = new FakeIdentityRepository
        {
            User = user,
            OtpVerification = new OtpVerificationResult
            {
                Status = OtpVerificationStatus.Verified,
                AttemptsRemaining = 5
            }
        };
        var service = CreateAccountService(repository, new FakeEmailService(), CreateOtpService());

        var result = await service.VerifyOtpAsync(
            user.Email,
            "123456",
            "VerifyEmail");

        Assert.True(result.Success);
        Assert.Equal("Active", result.Status);
        Assert.Equal(5, result.AttemptsRemaining);
    }

    [Fact]
    public async Task LoginRejectsUnverifiedEmailBeforeCreatingSession()
    {
        var passwordService = new PasswordService();
        var user = CreateUser(isEmailVerified: false);
        user.PasswordHash = passwordService.Hash("StrongPassword@123");
        var repository = new FakeIdentityRepository { User = user };
        var service = CreateAccountService(
            repository,
            new FakeEmailService(),
            CreateOtpService(),
            passwordService);

        var result = await service.LoginAsync(
            user.Email,
            "StrongPassword@123",
            "127.0.0.1",
            "test-agent");

        Assert.False(result.Success);
        Assert.Equal("AUTH_EMAIL_NOT_VERIFIED", result.ErrorCode);
        Assert.Null(repository.CreatedRefreshToken);
    }

    [Fact]
    public async Task LoginCreatesRefreshSessionForVerifiedAccount()
    {
        var passwordService = new PasswordService();
        var user = CreateUser(isEmailVerified: true);
        user.PasswordHash = passwordService.Hash("StrongPassword@123");
        var repository = new FakeIdentityRepository
        {
            User = user,
            Profile = CreateProfile(user.Id),
            SystemRoles = ["Admin"]
        };
        var service = CreateAccountService(
            repository,
            new FakeEmailService(),
            CreateOtpService(),
            passwordService,
            CreateRealTokenService());

        var result = await service.LoginAsync(
            user.Email,
            "StrongPassword@123",
            "127.0.0.1",
            "test-agent");

        Assert.True(result.Success);
        Assert.NotNull(result.AccessToken);
        Assert.NotNull(result.RefreshToken);
        Assert.Equal(900, result.ExpiresInSeconds);
        Assert.Equal(["Admin"], result.Roles);
        Assert.NotNull(repository.CreatedRefreshToken);
        Assert.NotEqual(result.RefreshToken, repository.CreatedRefreshToken.TokenHash);
    }

    [Fact]
    public async Task ForgotPasswordDoesNotRevealWhetherAccountExists()
    {
        var service = CreateAccountService(
            new FakeIdentityRepository(),
            new FakeEmailService(),
            CreateOtpService());

        var existingShape = await service.ForgotPasswordAsync("missing@example.com");
        var invalidShape = await service.ForgotPasswordAsync("not-an-email");

        Assert.True(existingShape.Success);
        Assert.True(invalidShape.Success);
        Assert.Equal(existingShape.Message, invalidShape.Message);
    }

    [Fact]
    public async Task ForgotPasswordCreatesPurposeBoundOtpBeforeSendingEmail()
    {
        var user = CreateUser(isEmailVerified: true);
        var repository = new FakeIdentityRepository
        {
            User = user,
            Profile = CreateProfile(user.Id)
        };
        var emailService = new FakeEmailService();
        var otpService = CreateOtpService();
        var service = CreateAccountService(repository, emailService, otpService);

        var result = await service.ForgotPasswordAsync(user.Email);

        Assert.True(result.Success);
        Assert.NotNull(repository.CreatedOtp);
        Assert.Equal("ResetPassword", repository.CreatedOtp.Purpose);
        Assert.Equal(user.Email, emailService.PasswordResetRecipientEmail);
        Assert.True(otpService.Verify(
            user.NormalizedEmail,
            "ResetPassword",
            emailService.PasswordResetOtpCode!,
            repository.CreatedOtp.CodeHash));
    }

    [Fact]
    public async Task ResetPasswordReturnsSuccessOnlyAfterAtomicRepositoryChange()
    {
        var passwordService = new PasswordService();
        var user = CreateUser(isEmailVerified: true);
        user.PasswordHash = passwordService.Hash("OldPassword@123");
        var repository = new FakeIdentityRepository
        {
            User = user,
            Profile = CreateProfile(user.Id),
            PasswordReset = new PasswordResetResult
            {
                Status = PasswordResetStatus.Changed,
                AttemptsRemaining = 4
            }
        };
        var emailService = new FakeEmailService();
        var service = CreateAccountService(
            repository,
            emailService,
            CreateOtpService(),
            passwordService);

        var result = await service.ResetPasswordAsync(
            user.Email,
            "123456",
            "NewPassword@123");

        Assert.True(result.Success);
        Assert.True(repository.ResetPasswordCalled);
        Assert.Equal(user.Email, emailService.PasswordChangedRecipientEmail);
    }

    [Fact]
    public async Task ChangePasswordRequiresCurrentPasswordAndUsesAtomicRepositoryChange()
    {
        var passwordService = new PasswordService();
        var user = CreateUser(isEmailVerified: true);
        user.PasswordHash = passwordService.Hash("OldPassword@123");
        var repository = new FakeIdentityRepository
        {
            User = user,
            Profile = CreateProfile(user.Id)
        };
        var service = CreateAccountService(
            repository,
            new FakeEmailService(),
            CreateOtpService(),
            passwordService);

        var rejected = await service.ChangePasswordAsync(
            user.Id,
            "WrongPassword@123",
            "NewPassword@123");
        var changed = await service.ChangePasswordAsync(
            user.Id,
            "OldPassword@123",
            "NewPassword@123");

        Assert.False(rejected.Success);
        Assert.Equal("AUTH_CURRENT_PASSWORD_INVALID", rejected.ErrorCode);
        Assert.True(changed.Success);
        Assert.True(repository.ChangePasswordCalled);
    }

    [Fact]
    public async Task ChangedPasswordPublishesPersistedSecurityNotification()
    {
        var passwordService = new PasswordService();
        var user = CreateUser(isEmailVerified: true);
        user.PasswordHash = passwordService.Hash("OldPassword@123");
        var repository = new FakeIdentityRepository
        {
            User = user,
            Profile = CreateProfile(user.Id)
        };
        var notifications = new CapturingNotificationServices();
        var service = CreateAccountService(
            repository,
            new FakeEmailService(),
            CreateOtpService(),
            passwordService,
            notificationServices: notifications);

        var result = await service.ChangePasswordAsync(
            user.Id,
            "OldPassword@123",
            "NewPassword@123");

        Assert.True(result.Success);
        Assert.NotNull(notifications.Published);
        Assert.Equal(NotificationTypes.PasswordChanged, notifications.Published.Type);
        Assert.Equal(user.Id, notifications.Published.UserId);
        Assert.True(notifications.Published.SendEmail);
    }

    private static AccountServices CreateAccountService(
        FakeIdentityRepository repository,
        FakeEmailService emailService,
        IOtpCodeService otpService,
        IPasswordService? passwordService = null,
        ITokenService? tokenService = null,
        INotificationServices? notificationServices = null)
    {
        return new AccountServices(
            repository,
            passwordService ?? new PasswordService(),
            otpService,
            emailService,
            tokenService ?? new FakeTokenService(),
            new FixedTimeProvider(TestNowUtc),
            notificationServices);
    }

    private static OtpCodeService CreateOtpService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Otp:HashKey"] = "unit-test-otp-key-with-at-least-32-characters"
            })
            .Build();
        return new OtpCodeService(configuration);
    }

    private static TokenService CreateRealTokenService()
    {
        return new TokenService(Options.Create(new JwtOptions
        {
            Issuer = "ProjectMgmt.Tests",
            Audience = "ProjectMgmt.Tests.Client",
            SigningKey = "unit-test-jwt-key-with-at-least-32-characters",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 30
        }));
    }

    private static User CreateUser(bool isEmailVerified)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = "member@example.com",
            NormalizedEmail = "MEMBER@EXAMPLE.COM",
            IsEmailVerified = isEmailVerified,
            IsActive = true,
            SecurityStamp = Guid.NewGuid(),
            CreatedAt = TestNowUtc
        };
    }

    private static UserProfile CreateProfile(Guid userId)
    {
        return new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DisplayName = "Thành viên kiểm thử",
            Timezone = "Asia/Ho_Chi_Minh",
            CreatedAt = TestNowUtc
        };
    }
}

public class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _utcNow;

    public FixedTimeProvider(DateTime utcNow)
    {
        _utcNow = new DateTimeOffset(utcNow);
    }

    public override DateTimeOffset GetUtcNow()
    {
        return _utcNow;
    }
}

public class FakeEmailService : IEmailService
{
    public bool DeliverySucceeds { get; set; } = true;
    public string? RecipientEmail { get; private set; }
    public string? OtpCode { get; private set; }
    public string? PasswordResetRecipientEmail { get; private set; }
    public string? PasswordResetOtpCode { get; private set; }
    public string? PasswordChangedRecipientEmail { get; private set; }

    public Task<bool> SendOtpAsync(
        string recipientEmail,
        string recipientName,
        string otpCode,
        DateTime expiresAtUtc)
    {
        RecipientEmail = recipientEmail;
        OtpCode = otpCode;
        return Task.FromResult(DeliverySucceeds);
    }

    public Task<bool> SendPasswordResetOtpAsync(
        string recipientEmail,
        string recipientName,
        string otpCode,
        DateTime expiresAtUtc)
    {
        PasswordResetRecipientEmail = recipientEmail;
        PasswordResetOtpCode = otpCode;
        return Task.FromResult(DeliverySucceeds);
    }

    public Task<bool> SendPasswordChangedAsync(
        string recipientEmail,
        string recipientName,
        DateTime changedAtUtc)
    {
        PasswordChangedRecipientEmail = recipientEmail;
        return Task.FromResult(DeliverySucceeds);
    }

    public Task<bool> SendProjectInvitationAsync(
        string recipientEmail,
        string recipientName,
        Guid projectId,
        string? roleName)
    {
        RecipientEmail = recipientEmail;
        return Task.FromResult(DeliverySucceeds);
    }

    public Task<bool> SendProjectRoleChangedAsync(
        string recipientEmail,
        string recipientName,
        Guid projectId,
        string? roleName)
    {
        RecipientEmail = recipientEmail;
        return Task.FromResult(DeliverySucceeds);
    }

    public Task<bool> SendProjectRoleRevokedAsync(
        string recipientEmail,
        string recipientName,
        Guid projectId)
    {
        RecipientEmail = recipientEmail;
        return Task.FromResult(DeliverySucceeds);
    }
}

public class FakeTokenService : ITokenService
{
    public TokenSet CreateTokenSet(
        User user,
        UserProfile? profile,
        IReadOnlyCollection<string> systemRoles,
        DateTime nowUtc)
    {
        return new TokenSet
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            RefreshTokenHash = HashRefreshToken("refresh-token"),
            AccessTokenExpiresAt = nowUtc.AddMinutes(15),
            RefreshTokenExpiresAt = nowUtc.AddDays(30)
        };
    }

    public string HashRefreshToken(string refreshToken)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
    }
}

public class FakeIdentityRepository : IIdentityRepository
{
    public User? User { get; set; }
    public UserProfile? Profile { get; set; }
    public List<string> SystemRoles { get; set; } = [];
    public OtpIssueResult OtpIssue { get; set; } = new() { Status = OtpIssueStatus.Issued };
    public OtpVerificationResult OtpVerification { get; set; } = new()
    {
        Status = OtpVerificationStatus.InvalidCode,
        AttemptsRemaining = 4
    };
    public LoginSessionStatus LoginSession { get; set; } = LoginSessionStatus.Created;
    public TokenRotationStatus Rotation { get; set; } = TokenRotationStatus.Rotated;
    public PasswordResetResult PasswordReset { get; set; } = new()
    {
        Status = PasswordResetStatus.Changed
    };
    public PasswordChangeResult PasswordChange { get; set; } = new()
    {
        Status = PasswordChangeStatus.Changed
    };
    public AuthSessionContext? RefreshContext { get; set; }
    public User? CreatedUser { get; private set; }
    public UserProfile? CreatedProfile { get; private set; }
    public OtpCode? CreatedOtp { get; private set; }
    public RefreshToken? CreatedRefreshToken { get; private set; }
    public bool ResetPasswordCalled { get; private set; }
    public bool ChangePasswordCalled { get; private set; }

    public Task<User?> GetUserByNormalizedEmailAsync(
        string normalizedEmail,
        bool tracking = false)
    {
        return Task.FromResult(User);
    }

    public Task<User?> GetUserByIdAsync(Guid userId, bool tracking = false)
    {
        return Task.FromResult(User);
    }

    public Task<UserProfile?> GetUserProfileAsync(
        Guid userId,
        bool tracking = false)
    {
        return Task.FromResult(Profile);
    }

    public Task<bool> PhoneNumberExistsAsync(string phoneNumber)
    {
        return Task.FromResult(false);
    }

    public Task<bool> CreatePendingRegistrationAsync(
        User user,
        UserProfile profile,
        OtpCode otpCode)
    {
        CreatedUser = user;
        CreatedProfile = profile;
        CreatedOtp = otpCode;
        return Task.FromResult(true);
    }

    public Task<OtpIssueResult> ReplaceEmailVerificationOtpAsync(
        Guid userId,
        OtpCode newOtpCode,
        DateTime nowUtc,
        TimeSpan minimumInterval)
    {
        CreatedOtp = newOtpCode;
        return Task.FromResult(OtpIssue);
    }

    public Task<OtpVerificationResult> VerifyEmailOtpAsync(
        Guid userId,
        string purpose,
        string expectedCodeHash,
        DateTime nowUtc,
        int maximumAttempts)
    {
        return Task.FromResult(OtpVerification);
    }

    public Task<OtpIssueResult> ReplacePasswordResetOtpAsync(
        Guid userId,
        OtpCode newOtpCode,
        DateTime nowUtc,
        TimeSpan minimumInterval)
    {
        CreatedOtp = newOtpCode;
        return Task.FromResult(OtpIssue);
    }

    public Task<PasswordResetResult> ResetPasswordAsync(
        Guid userId,
        string purpose,
        string expectedCodeHash,
        string newPasswordHash,
        Guid newSecurityStamp,
        DateTime nowUtc,
        int maximumAttempts)
    {
        ResetPasswordCalled = true;
        return Task.FromResult(PasswordReset);
    }

    public Task<PasswordChangeResult> ChangePasswordAsync(
        Guid userId,
        string expectedCurrentPasswordHash,
        string newPasswordHash,
        Guid newSecurityStamp,
        DateTime nowUtc)
    {
        ChangePasswordCalled = true;
        return Task.FromResult(PasswordChange);
    }

    public Task<List<string>> GetSystemRoleNamesAsync(Guid userId)
    {
        return Task.FromResult(SystemRoles);
    }

    public Task<LoginSessionStatus> CreateLoginSessionAsync(
        Guid userId,
        RefreshToken refreshToken,
        DateTime nowUtc)
    {
        CreatedRefreshToken = refreshToken;
        return Task.FromResult(LoginSession);
    }

    public Task<AuthSessionContext?> GetRefreshSessionContextAsync(string refreshTokenHash)
    {
        return Task.FromResult(RefreshContext);
    }

    public Task<TokenRotationStatus> RotateRefreshTokenAsync(
        string currentTokenHash,
        RefreshToken replacementToken,
        DateTime nowUtc)
    {
        CreatedRefreshToken = replacementToken;
        return Task.FromResult(Rotation);
    }

    public Task<bool> RevokeRefreshTokenAsync(string refreshTokenHash)
    {
        return Task.FromResult(true);
    }
}

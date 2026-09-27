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

        var result = await service.RegisterAsync(new AccountDto
        {
            Email = "member@example.com",
            Password = "StrongPassword@123",
            FullName = "Nguyễn Văn Thành Viên",
            PhoneNumber = "0901234567"
        });

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

        var result = await service.VerifyOtpAsync(new AccountDto
        {
            Email = user.Email,
            Code = "123456",
            Purpose = "VerifyEmail"
        });

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
            new AccountDto { Email = user.Email, Password = "StrongPassword@123" },
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
            new AccountDto { Email = user.Email, Password = "StrongPassword@123" },
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

    private static AccountServices CreateAccountService(
        FakeIdentityRepository repository,
        FakeEmailService emailService,
        IOtpCodeService otpService,
        IPasswordService? passwordService = null,
        ITokenService? tokenService = null)
    {
        return new AccountServices(
            repository,
            passwordService ?? new PasswordService(),
            otpService,
            emailService,
            tokenService ?? new FakeTokenService(),
            new FixedTimeProvider(TestNowUtc));
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

    public Task<bool> SendOtpAsync(
        string recipientEmail,
        string recipientName,
        string otpCode,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        RecipientEmail = recipientEmail;
        OtpCode = otpCode;
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
    public AuthSessionContext? RefreshContext { get; set; }
    public User? CreatedUser { get; private set; }
    public UserProfile? CreatedProfile { get; private set; }
    public OtpCode? CreatedOtp { get; private set; }
    public RefreshToken? CreatedRefreshToken { get; private set; }

    public Task<User?> GetUserByNormalizedEmailAsync(
        string normalizedEmail,
        bool tracking = false,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(User);
    }

    public Task<UserProfile?> GetUserProfileAsync(
        Guid userId,
        bool tracking = false,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Profile);
    }

    public Task<bool> PhoneNumberExistsAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<bool> CreatePendingRegistrationAsync(
        User user,
        UserProfile profile,
        OtpCode otpCode,
        CancellationToken cancellationToken = default)
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
        TimeSpan minimumInterval,
        CancellationToken cancellationToken = default)
    {
        CreatedOtp = newOtpCode;
        return Task.FromResult(OtpIssue);
    }

    public Task<OtpVerificationResult> VerifyEmailOtpAsync(
        Guid userId,
        string purpose,
        string expectedCodeHash,
        DateTime nowUtc,
        int maximumAttempts,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(OtpVerification);
    }

    public Task<List<string>> GetSystemRoleNamesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SystemRoles);
    }

    public Task<LoginSessionStatus> CreateLoginSessionAsync(
        Guid userId,
        RefreshToken refreshToken,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        CreatedRefreshToken = refreshToken;
        return Task.FromResult(LoginSession);
    }

    public Task<AuthSessionContext?> GetRefreshSessionContextAsync(
        string refreshTokenHash,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(RefreshContext);
    }

    public Task<TokenRotationStatus> RotateRefreshTokenAsync(
        string currentTokenHash,
        RefreshToken replacementToken,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        CreatedRefreshToken = replacementToken;
        return Task.FromResult(Rotation);
    }

    public Task<bool> RevokeRefreshTokenAsync(
        string refreshTokenHash,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}

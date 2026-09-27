using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.IRepositories;
using IdentityExperience.Domain.Models;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Security.Cryptography;

namespace IdentityExperience.Infrastructure.Repository;

public class IdentityRepository : IIdentityRepository
{
    private readonly IdentityExperienceDbContext _context;

    public IdentityRepository(IdentityExperienceDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetUserByNormalizedEmailAsync(
        string normalizedEmail,
        bool tracking = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<User> query = _context.Users;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(
            user => user.NormalizedEmail == normalizedEmail,
            cancellationToken);
    }

    public Task<UserProfile?> GetUserProfileAsync(
        Guid userId,
        bool tracking = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<UserProfile> query = _context.UserProfiles;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(
            profile => profile.UserId == userId,
            cancellationToken);
    }

    public Task<bool> PhoneNumberExistsAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        return _context.UserProfiles
            .AsNoTracking()
            .AnyAsync(profile => profile.PhoneNumber == phoneNumber, cancellationToken);
    }

    public async Task<bool> CreatePendingRegistrationAsync(
        User user,
        UserProfile profile,
        OtpCode otpCode,
        CancellationToken cancellationToken = default)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();
        var created = false;

        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await _context.Users.AddAsync(user, cancellationToken);
                await _context.UserProfiles.AddAsync(profile, cancellationToken);
                await _context.OtpCodes.AddAsync(otpCode, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                created = true;
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is MySqlException { Number: 1062 })
            {
                await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                created = false;
            }
        });

        return created;
    }

    public async Task<OtpIssueResult> ReplaceEmailVerificationOtpAsync(
        Guid userId,
        OtpCode newOtpCode,
        DateTime nowUtc,
        TimeSpan minimumInterval,
        CancellationToken cancellationToken = default)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var lockedUsers = await _context.Users
                .FromSqlInterpolated($"SELECT * FROM `User` WHERE `Id` = {userId} FOR UPDATE")
                .ToListAsync(cancellationToken);
            var user = lockedUsers.SingleOrDefault();

            if (user is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new OtpIssueResult { Status = OtpIssueStatus.UserNotFound };
            }

            if (user.IsEmailVerified)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new OtpIssueResult { Status = OtpIssueStatus.EmailAlreadyVerified };
            }

            var otpCodes = await _context.OtpCodes
                .Where(otp => otp.UserId == userId && otp.Purpose == newOtpCode.Purpose)
                .OrderByDescending(otp => otp.CreatedAt)
                .ToListAsync(cancellationToken);

            var latestOtp = otpCodes.FirstOrDefault();
            if (latestOtp is not null)
            {
                var elapsed = nowUtc - latestOtp.CreatedAt;
                if (elapsed < minimumInterval)
                {
                    var retryAfter = (int)Math.Ceiling((minimumInterval - elapsed).TotalSeconds);
                    await transaction.RollbackAsync(cancellationToken);
                    return new OtpIssueResult
                    {
                        Status = OtpIssueStatus.RateLimited,
                        ExpiresAt = latestOtp.ExpiresAt,
                        RetryAfterSeconds = Math.Max(1, retryAfter)
                    };
                }
            }

            foreach (var otpCode in otpCodes.Where(otp => !otp.IsUsed))
            {
                otpCode.IsUsed = true;
            }

            await _context.OtpCodes.AddAsync(newOtpCode, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new OtpIssueResult
            {
                Status = OtpIssueStatus.Issued,
                ExpiresAt = newOtpCode.ExpiresAt,
                RetryAfterSeconds = (int)minimumInterval.TotalSeconds
            };
        });
    }

    public async Task<OtpVerificationResult> VerifyEmailOtpAsync(
        Guid userId,
        string purpose,
        string expectedCodeHash,
        DateTime nowUtc,
        int maximumAttempts,
        CancellationToken cancellationToken = default)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var lockedUsers = await _context.Users
                .FromSqlInterpolated($"SELECT * FROM `User` WHERE `Id` = {userId} FOR UPDATE")
                .ToListAsync(cancellationToken);
            var user = lockedUsers.SingleOrDefault();

            if (user is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return VerificationResult(OtpVerificationStatus.UserNotFound);
            }

            if (!user.IsActive)
            {
                await transaction.RollbackAsync(cancellationToken);
                return VerificationResult(OtpVerificationStatus.AccountDisabled);
            }

            if (user.IsEmailVerified)
            {
                await transaction.RollbackAsync(cancellationToken);
                return VerificationResult(OtpVerificationStatus.AlreadyVerified);
            }

            var otpCode = await _context.OtpCodes
                .Where(otp => otp.UserId == userId && otp.Purpose == purpose && !otp.IsUsed)
                .OrderByDescending(otp => otp.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (otpCode is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return VerificationResult(OtpVerificationStatus.NoActiveCode);
            }

            if (otpCode.ExpiresAt <= nowUtc)
            {
                otpCode.IsUsed = true;
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return VerificationResult(OtpVerificationStatus.Expired, 0);
            }

            if (otpCode.AttemptCount >= maximumAttempts)
            {
                otpCode.IsUsed = true;
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return VerificationResult(OtpVerificationStatus.AttemptsExceeded, 0);
            }

            if (!HashesMatch(expectedCodeHash, otpCode.CodeHash))
            {
                otpCode.AttemptCount++;
                var attemptsRemaining = Math.Max(0, maximumAttempts - otpCode.AttemptCount);
                if (attemptsRemaining == 0)
                {
                    otpCode.IsUsed = true;
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return VerificationResult(
                    attemptsRemaining == 0
                        ? OtpVerificationStatus.AttemptsExceeded
                        : OtpVerificationStatus.InvalidCode,
                    attemptsRemaining);
            }

            otpCode.IsUsed = true;
            user.IsEmailVerified = true;
            user.SecurityStamp = Guid.NewGuid();
            user.UpdatedAt = nowUtc;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return VerificationResult(OtpVerificationStatus.Verified, maximumAttempts - otpCode.AttemptCount);
        });
    }

    public Task<List<string>> GetSystemRoleNamesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return (from userRole in _context.UserRoles.AsNoTracking()
                join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userRole.UserId == userId
                      && userRole.ScopeType == "System"
                      && userRole.ScopeId == null
                select role.Name)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<LoginSessionStatus> CreateLoginSessionAsync(
        Guid userId,
        RefreshToken refreshToken,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var lockedUsers = await _context.Users
                .FromSqlInterpolated($"SELECT * FROM `User` WHERE `Id` = {userId} FOR UPDATE")
                .ToListAsync(cancellationToken);
            var user = lockedUsers.SingleOrDefault();

            if (user is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return LoginSessionStatus.UserNotFound;
            }

            if (!user.IsActive)
            {
                await transaction.RollbackAsync(cancellationToken);
                return LoginSessionStatus.AccountDisabled;
            }

            if (!user.IsEmailVerified)
            {
                await transaction.RollbackAsync(cancellationToken);
                return LoginSessionStatus.EmailNotVerified;
            }

            user.LastLoginAt = nowUtc;
            await _context.RefreshTokens.AddAsync(refreshToken, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return LoginSessionStatus.Created;
        });
    }

    public async Task<AuthSessionContext?> GetRefreshSessionContextAsync(
        string refreshTokenHash,
        CancellationToken cancellationToken = default)
    {
        var token = await _context.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(
                refreshToken => refreshToken.TokenHash == refreshTokenHash,
                cancellationToken);
        if (token is null)
        {
            return null;
        }

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == token.UserId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        return new AuthSessionContext
        {
            User = user,
            Profile = await _context.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(profile => profile.UserId == user.Id, cancellationToken),
            SystemRoles = await GetSystemRoleNamesAsync(user.Id, cancellationToken)
        };
    }

    public async Task<TokenRotationStatus> RotateRefreshTokenAsync(
        string currentTokenHash,
        RefreshToken replacementToken,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var lockedTokens = await _context.RefreshTokens
                .FromSqlInterpolated($"SELECT * FROM `RefreshToken` WHERE `TokenHash` = {currentTokenHash} FOR UPDATE")
                .ToListAsync(cancellationToken);
            var currentToken = lockedTokens.SingleOrDefault();

            if (currentToken is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return TokenRotationStatus.TokenNotFound;
            }

            if (currentToken.IsRevoked)
            {
                if (currentToken.ReplacedByTokenId is not null)
                {
                    var activeTokens = await _context.RefreshTokens
                        .Where(token => token.UserId == currentToken.UserId && !token.IsRevoked)
                        .ToListAsync(cancellationToken);
                    foreach (var activeToken in activeTokens)
                    {
                        activeToken.IsRevoked = true;
                    }

                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return TokenRotationStatus.ReuseDetected;
                }

                await transaction.RollbackAsync(cancellationToken);
                return TokenRotationStatus.TokenRevoked;
            }

            if (currentToken.ExpiresAt <= nowUtc)
            {
                currentToken.IsRevoked = true;
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return TokenRotationStatus.TokenExpired;
            }

            var lockedUsers = await _context.Users
                .FromSqlInterpolated($"SELECT * FROM `User` WHERE `Id` = {currentToken.UserId} FOR UPDATE")
                .ToListAsync(cancellationToken);
            var user = lockedUsers.SingleOrDefault();
            if (user is null || !user.IsActive)
            {
                currentToken.IsRevoked = true;
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return TokenRotationStatus.AccountDisabled;
            }

            if (!user.IsEmailVerified)
            {
                currentToken.IsRevoked = true;
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return TokenRotationStatus.EmailNotVerified;
            }

            replacementToken.UserId = currentToken.UserId;
            await _context.RefreshTokens.AddAsync(replacementToken, cancellationToken);
            currentToken.IsRevoked = true;
            currentToken.ReplacedByTokenId = replacementToken.Id;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return TokenRotationStatus.Rotated;
        });
    }

    public async Task<bool> RevokeRefreshTokenAsync(
        string refreshTokenHash,
        CancellationToken cancellationToken = default)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var lockedTokens = await _context.RefreshTokens
                .FromSqlInterpolated($"SELECT * FROM `RefreshToken` WHERE `TokenHash` = {refreshTokenHash} FOR UPDATE")
                .ToListAsync(cancellationToken);
            var refreshToken = lockedTokens.SingleOrDefault();
            if (refreshToken is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            if (!refreshToken.IsRevoked)
            {
                refreshToken.IsRevoked = true;
                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    private static bool HashesMatch(string expectedHash, string storedHash)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expectedHash),
                Convert.FromHexString(storedHash));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static OtpVerificationResult VerificationResult(
        OtpVerificationStatus status,
        int? attemptsRemaining = null)
    {
        return new OtpVerificationResult
        {
            Status = status,
            AttemptsRemaining = attemptsRemaining
        };
    }
}

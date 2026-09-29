using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.IRepositories;
using IdentityExperience.Domain.Models;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Security.Cryptography;
using System.Text;

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
        bool tracking = false)
    {
        IQueryable<User> query = _context.Users;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail);
    }

    public Task<User?> GetUserByIdAsync(
        Guid userId,
        bool tracking = false)
    {
        IQueryable<User> query = _context.Users;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(user => user.Id == userId);
    }

    public Task<UserProfile?> GetUserProfileAsync(
        Guid userId,
        bool tracking = false)
    {
        IQueryable<UserProfile> query = _context.UserProfiles;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(profile => profile.UserId == userId);
    }

    public Task<bool> PhoneNumberExistsAsync(string phoneNumber)
    {
        return _context.UserProfiles
            .AsNoTracking()
            .AnyAsync(profile => profile.PhoneNumber == phoneNumber);
    }

    public async Task<bool> CreatePendingRegistrationAsync(
        User user,
        UserProfile profile,
        OtpCode otpCode)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();
        var created = false;

        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.Users.AddAsync(user);
                await _context.UserProfiles.AddAsync(profile);
                await _context.OtpCodes.AddAsync(otpCode);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                created = true;
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is MySqlException { Number: 1062 })
            {
                await transaction.RollbackAsync();
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
        TimeSpan minimumInterval)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var lockedUsers = await _context.Users
                .FromSqlInterpolated($"SELECT * FROM `User` WHERE `Id` = {userId} FOR UPDATE")
                .ToListAsync();
            var user = lockedUsers.SingleOrDefault();

            if (user is null)
            {
                await transaction.RollbackAsync();
                return new OtpIssueResult { Status = OtpIssueStatus.UserNotFound };
            }

            if (user.IsEmailVerified)
            {
                await transaction.RollbackAsync();
                return new OtpIssueResult { Status = OtpIssueStatus.EmailAlreadyVerified };
            }

            var otpCodes = await _context.OtpCodes
                .Where(otp => otp.UserId == userId && otp.Purpose == newOtpCode.Purpose)
                .OrderByDescending(otp => otp.CreatedAt)
                .ToListAsync();

            var latestOtp = otpCodes.FirstOrDefault();
            if (latestOtp is not null)
            {
                var elapsed = nowUtc - latestOtp.CreatedAt;
                if (elapsed < minimumInterval)
                {
                    var retryAfter = (int)Math.Ceiling((minimumInterval - elapsed).TotalSeconds);
                    await transaction.RollbackAsync();
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

            await _context.OtpCodes.AddAsync(newOtpCode);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

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
        int maximumAttempts)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var lockedUsers = await _context.Users
                .FromSqlInterpolated($"SELECT * FROM `User` WHERE `Id` = {userId} FOR UPDATE")
                .ToListAsync();
            var user = lockedUsers.SingleOrDefault();

            if (user is null)
            {
                await transaction.RollbackAsync();
                return VerificationResult(OtpVerificationStatus.UserNotFound);
            }

            if (!user.IsActive)
            {
                await transaction.RollbackAsync();
                return VerificationResult(OtpVerificationStatus.AccountDisabled);
            }

            if (user.IsEmailVerified)
            {
                await transaction.RollbackAsync();
                return VerificationResult(OtpVerificationStatus.AlreadyVerified);
            }

            var otpCode = await _context.OtpCodes
                .Where(otp => otp.UserId == userId && otp.Purpose == purpose && !otp.IsUsed)
                .OrderByDescending(otp => otp.CreatedAt)
                .FirstOrDefaultAsync();

            if (otpCode is null)
            {
                await transaction.RollbackAsync();
                return VerificationResult(OtpVerificationStatus.NoActiveCode);
            }

            if (otpCode.ExpiresAt <= nowUtc)
            {
                otpCode.IsUsed = true;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return VerificationResult(OtpVerificationStatus.Expired, 0);
            }

            if (otpCode.AttemptCount >= maximumAttempts)
            {
                otpCode.IsUsed = true;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
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

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
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

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return VerificationResult(OtpVerificationStatus.Verified, maximumAttempts - otpCode.AttemptCount);
        });
    }

    public async Task<OtpIssueResult> ReplacePasswordResetOtpAsync(
        Guid userId,
        OtpCode newOtpCode,
        DateTime nowUtc,
        TimeSpan minimumInterval)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var lockedUsers = await _context.Users
                .FromSqlInterpolated($"SELECT * FROM `User` WHERE `Id` = {userId} FOR UPDATE")
                .ToListAsync();
            var user = lockedUsers.SingleOrDefault();

            if (user is null)
            {
                await transaction.RollbackAsync();
                return new OtpIssueResult { Status = OtpIssueStatus.UserNotFound };
            }

            if (!user.IsActive)
            {
                await transaction.RollbackAsync();
                return new OtpIssueResult { Status = OtpIssueStatus.AccountDisabled };
            }

            if (!user.IsEmailVerified)
            {
                await transaction.RollbackAsync();
                return new OtpIssueResult { Status = OtpIssueStatus.EmailNotVerified };
            }

            var otpCodes = await _context.OtpCodes
                .Where(otp => otp.UserId == userId && otp.Purpose == newOtpCode.Purpose)
                .OrderByDescending(otp => otp.CreatedAt)
                .ToListAsync();
            var latestOtp = otpCodes.FirstOrDefault();

            if (latestOtp is not null)
            {
                var elapsed = nowUtc - latestOtp.CreatedAt;
                if (elapsed < minimumInterval)
                {
                    var retryAfter = (int)Math.Ceiling((minimumInterval - elapsed).TotalSeconds);
                    await transaction.RollbackAsync();
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

            await _context.OtpCodes.AddAsync(newOtpCode);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new OtpIssueResult
            {
                Status = OtpIssueStatus.Issued,
                ExpiresAt = newOtpCode.ExpiresAt,
                RetryAfterSeconds = (int)minimumInterval.TotalSeconds
            };
        });
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(
        Guid userId,
        string purpose,
        string expectedCodeHash,
        string newPasswordHash,
        Guid newSecurityStamp,
        DateTime nowUtc,
        int maximumAttempts)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var lockedUsers = await _context.Users
                .FromSqlInterpolated($"SELECT * FROM `User` WHERE `Id` = {userId} FOR UPDATE")
                .ToListAsync();
            var user = lockedUsers.SingleOrDefault();

            if (user is null)
            {
                await transaction.RollbackAsync();
                return PasswordResetResult(PasswordResetStatus.UserNotFound);
            }

            if (!user.IsActive)
            {
                await transaction.RollbackAsync();
                return PasswordResetResult(PasswordResetStatus.AccountDisabled);
            }

            if (!user.IsEmailVerified)
            {
                await transaction.RollbackAsync();
                return PasswordResetResult(PasswordResetStatus.EmailNotVerified);
            }

            var lockedOtpCodes = await _context.OtpCodes
                .FromSqlInterpolated($"""
                    SELECT * FROM `OtpCode`
                    WHERE `UserId` = {userId} AND `Purpose` = {purpose} AND `IsUsed` = 0
                    ORDER BY `CreatedAt` DESC LIMIT 1 FOR UPDATE
                    """)
                .ToListAsync();
            var otpCode = lockedOtpCodes.SingleOrDefault();

            if (otpCode is null)
            {
                await transaction.RollbackAsync();
                return PasswordResetResult(PasswordResetStatus.NoActiveCode);
            }

            if (otpCode.ExpiresAt <= nowUtc)
            {
                otpCode.IsUsed = true;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return PasswordResetResult(PasswordResetStatus.Expired, 0);
            }

            if (otpCode.AttemptCount >= maximumAttempts)
            {
                otpCode.IsUsed = true;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return PasswordResetResult(PasswordResetStatus.AttemptsExceeded, 0);
            }

            if (!HashesMatch(expectedCodeHash, otpCode.CodeHash))
            {
                otpCode.AttemptCount++;
                var attemptsRemaining = Math.Max(0, maximumAttempts - otpCode.AttemptCount);
                if (attemptsRemaining == 0)
                {
                    otpCode.IsUsed = true;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return PasswordResetResult(
                    attemptsRemaining == 0
                        ? PasswordResetStatus.AttemptsExceeded
                        : PasswordResetStatus.InvalidCode,
                    attemptsRemaining);
            }

            otpCode.IsUsed = true;
            user.PasswordHash = newPasswordHash;
            user.SecurityStamp = newSecurityStamp;
            user.UpdatedAt = nowUtc;
            await RevokeAllRefreshTokensAsync(userId);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return PasswordResetResult(
                PasswordResetStatus.Changed,
                maximumAttempts - otpCode.AttemptCount);
        });
    }

    public async Task<PasswordChangeResult> ChangePasswordAsync(
        Guid userId,
        string expectedCurrentPasswordHash,
        string newPasswordHash,
        Guid newSecurityStamp,
        DateTime nowUtc)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var lockedUsers = await _context.Users
                .FromSqlInterpolated($"SELECT * FROM `User` WHERE `Id` = {userId} FOR UPDATE")
                .ToListAsync();
            var user = lockedUsers.SingleOrDefault();

            if (user is null)
            {
                await transaction.RollbackAsync();
                return new PasswordChangeResult { Status = PasswordChangeStatus.UserNotFound };
            }

            if (!user.IsActive)
            {
                await transaction.RollbackAsync();
                return new PasswordChangeResult { Status = PasswordChangeStatus.AccountDisabled };
            }

            if (!user.IsEmailVerified)
            {
                await transaction.RollbackAsync();
                return new PasswordChangeResult { Status = PasswordChangeStatus.EmailNotVerified };
            }

            if (user.PasswordHash is null
                || !TextMatches(user.PasswordHash, expectedCurrentPasswordHash))
            {
                await transaction.RollbackAsync();
                return new PasswordChangeResult { Status = PasswordChangeStatus.CurrentPasswordChanged };
            }

            user.PasswordHash = newPasswordHash;
            user.SecurityStamp = newSecurityStamp;
            user.UpdatedAt = nowUtc;
            await RevokeAllRefreshTokensAsync(userId);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return new PasswordChangeResult { Status = PasswordChangeStatus.Changed };
        });
    }

    public Task<List<string>> GetSystemRoleNamesAsync(Guid userId)
    {
        return (from userRole in _context.UserRoles.AsNoTracking()
                join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userRole.UserId == userId
                      && userRole.ScopeType == "System"
                      && userRole.ScopeId == null
                select role.Name)
            .Distinct()
            .ToListAsync();
    }

    public async Task<LoginSessionStatus> CreateLoginSessionAsync(
        Guid userId,
        RefreshToken refreshToken,
        DateTime nowUtc)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var lockedUsers = await _context.Users
                .FromSqlInterpolated($"SELECT * FROM `User` WHERE `Id` = {userId} FOR UPDATE")
                .ToListAsync();
            var user = lockedUsers.SingleOrDefault();

            if (user is null)
            {
                await transaction.RollbackAsync();
                return LoginSessionStatus.UserNotFound;
            }

            if (!user.IsActive)
            {
                await transaction.RollbackAsync();
                return LoginSessionStatus.AccountDisabled;
            }

            if (!user.IsEmailVerified)
            {
                await transaction.RollbackAsync();
                return LoginSessionStatus.EmailNotVerified;
            }

            user.LastLoginAt = nowUtc;
            await _context.RefreshTokens.AddAsync(refreshToken);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return LoginSessionStatus.Created;
        });
    }

    public async Task<AuthSessionContext?> GetRefreshSessionContextAsync(string refreshTokenHash)
    {
        var token = await _context.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(refreshToken => refreshToken.TokenHash == refreshTokenHash);
        if (token is null)
        {
            return null;
        }

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == token.UserId);
        if (user is null)
        {
            return null;
        }

        return new AuthSessionContext
        {
            User = user,
            Profile = await _context.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(profile => profile.UserId == user.Id),
            SystemRoles = await GetSystemRoleNamesAsync(user.Id)
        };
    }

    public async Task<TokenRotationStatus> RotateRefreshTokenAsync(
        string currentTokenHash,
        RefreshToken replacementToken,
        DateTime nowUtc)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var lockedTokens = await _context.RefreshTokens
                .FromSqlInterpolated($"SELECT * FROM `RefreshToken` WHERE `TokenHash` = {currentTokenHash} FOR UPDATE")
                .ToListAsync();
            var currentToken = lockedTokens.SingleOrDefault();

            if (currentToken is null)
            {
                await transaction.RollbackAsync();
                return TokenRotationStatus.TokenNotFound;
            }

            if (currentToken.IsRevoked)
            {
                if (currentToken.ReplacedByTokenId is not null)
                {
                    var activeTokens = await _context.RefreshTokens
                        .Where(token => token.UserId == currentToken.UserId && !token.IsRevoked)
                        .ToListAsync();
                    foreach (var activeToken in activeTokens)
                    {
                        activeToken.IsRevoked = true;
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return TokenRotationStatus.ReuseDetected;
                }

                await transaction.RollbackAsync();
                return TokenRotationStatus.TokenRevoked;
            }

            if (currentToken.ExpiresAt <= nowUtc)
            {
                currentToken.IsRevoked = true;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return TokenRotationStatus.TokenExpired;
            }

            var lockedUsers = await _context.Users
                .FromSqlInterpolated($"SELECT * FROM `User` WHERE `Id` = {currentToken.UserId} FOR UPDATE")
                .ToListAsync();
            var user = lockedUsers.SingleOrDefault();
            if (user is null || !user.IsActive)
            {
                currentToken.IsRevoked = true;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return TokenRotationStatus.AccountDisabled;
            }

            if (!user.IsEmailVerified)
            {
                currentToken.IsRevoked = true;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return TokenRotationStatus.EmailNotVerified;
            }

            replacementToken.UserId = currentToken.UserId;
            await _context.RefreshTokens.AddAsync(replacementToken);
            currentToken.IsRevoked = true;
            currentToken.ReplacedByTokenId = replacementToken.Id;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return TokenRotationStatus.Rotated;
        });
    }

    public async Task<bool> RevokeRefreshTokenAsync(string refreshTokenHash)
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var lockedTokens = await _context.RefreshTokens
                .FromSqlInterpolated($"SELECT * FROM `RefreshToken` WHERE `TokenHash` = {refreshTokenHash} FOR UPDATE")
                .ToListAsync();
            var refreshToken = lockedTokens.SingleOrDefault();
            if (refreshToken is null)
            {
                await transaction.RollbackAsync();
                return false;
            }

            if (!refreshToken.IsRevoked)
            {
                refreshToken.IsRevoked = true;
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
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

    private Task<int> RevokeAllRefreshTokensAsync(Guid userId)
    {
        return _context.RefreshTokens
            .Where(token => token.UserId == userId && !token.IsRevoked)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.IsRevoked, true));
    }

    private static bool TextMatches(string left, string right)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left),
            Encoding.UTF8.GetBytes(right));
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

    private static PasswordResetResult PasswordResetResult(
        PasswordResetStatus status,
        int? attemptsRemaining = null)
    {
        return new PasswordResetResult
        {
            Status = status,
            AttemptsRemaining = attemptsRemaining
        };
    }
}

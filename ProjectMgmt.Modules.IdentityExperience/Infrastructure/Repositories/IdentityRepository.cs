using IdentityExperience.Domain.Entities;
using IdentityExperience.Domain.IRepositories;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

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
}

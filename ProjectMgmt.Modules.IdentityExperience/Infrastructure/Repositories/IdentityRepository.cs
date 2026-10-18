
using System.Linq;
using IdentityExperience.Domain.Entities;
using System.Linq.Expressions;
using IdentityExperience.Infrastructure.IRepository;
using Microsoft.EntityFrameworkCore;
namespace IdentityExperience.Infrastructure.Repository;

public class IdentityRepository : IIdentityRepository
{
private readonly IdentityExperienceDbContext _context;
    public IdentityRepository(IdentityExperienceDbContext context) { _context = context; }
    public async Task<(string, bool)> DeleteAsync<T>(T entity)
    {
        throw new NotImplementedException();
    }



    public async Task<List<TEntity>> Gets<TEntity>(Expression<Func<TEntity, bool>> parame) where TEntity : class
    {
        return await _context.Set<TEntity>().Where(parame).ToListAsync();
    }
    public async Task<TEntity> GetBy<TEntity>(Expression<Func<TEntity, bool>> parame) where TEntity : class
    {
        return await _context.Set<TEntity>().FirstOrDefaultAsync(parame);
    }
    public async Task<(string, bool)> PostAsync<TEntity>(TEntity entity)
    {
        return (null, true); //await _context.Set<TEntity>().AddAsync(entity);
    }

    public async Task<(string, bool)> UpdateAsync<T>(T entity)
    {
        throw new NotImplementedException();
    }
}

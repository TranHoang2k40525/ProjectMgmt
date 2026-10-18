using System.Linq.Expressions;
using IdentityExperience.Domain.Entities;
namespace IdentityExperience.Infrastructure.IRepository;

public interface IIdentityRepository
{

    Task<List<TEntity>> Gets<TEntity>(Expression<Func<TEntity, bool>> parame) where TEntity : class;
    Task<TEntity> GetBy<TEntity>(Expression<Func<TEntity, bool>> parame) where TEntity : class;

    Task<(string, bool)> DeleteAsync<T>(T entity);
    Task<(string, bool)> UpdateAsync<T>(T entity);
    Task<(string, bool)> PostAsync<T>(T entity);
}

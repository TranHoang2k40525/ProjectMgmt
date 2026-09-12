using System;
using System.Collections.Generic;
using System.Text;
using IdentityExperience.Application.Dto;

namespace IdentityExperience.Application.IServices
{
    public interface IAccountServices
    {
        Task<ResultLogin> LoginAsync(string username, string password);
        Task<Result> RegisterAsync (AccountDto.Register registerDto);
        Task<Result> VerifyAsync<TEntity,T>(TEntity entity, T m);
    }
}

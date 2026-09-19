using System;
using System.Collections.Generic;
using System.Text;
using BCrypt.Net;
using System.Linq;
using IdentityExperience.Domain.Entities;
using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using  IdentityExperience.Infrastructure.IRepository;
using System.Xml.Serialization;

namespace IdentityExperience.Application.Services
{
    

    public class AccountServices : IAccountServices
    {
        private readonly IIdentityRepository _identityRepository;
        public AccountServices(IIdentityRepository identityRepository) {
             _identityRepository = identityRepository;
        }

        public async Task<ResultLogin> LoginAsync(string username, string password)
        {
            var Data = await _identityRepository.GetBy<User>(u => u.Email == username);
            if(Data.Email == null || Data.PasswordHash == null) {
            return new ResultLogin{
            Message = "Tài khoản và mật khẩu không tồn tại",
            Success = false
            };
            }
            var verify = HasPassword(password, Data.PasswordHash);
            if (verify == false) {
                return new ResultLogin
                {
                    Message = "Mật khẩu không đúng, vui lòng nhập lại mật khẩu",
                    Success = false
                };

            }
            return new ResultLogin
            {
                Message = " đăng nhập thành công",
                Success = true
            };


        }

        public async Task<Result> RegisterAsync(AccountDto.Register registerDto)
        {
            var Data = await _identityRepository.GetBy<UserProfile>(u => u.DisplayName == $"{registerDto.FirstName} {registerDto.FirstName}" );
            if(Data.DisplayName.Any())
            {
                return new Result
                {
                    Message = "Tên người dùng đã tồn tại",
                    Success = false
                };
            }
            //var PostData = await _identityRepository.PostAsync<User>(registerDto);
            return new Result{
            Message = "đăng ký thành công",
            Success = true
            };
        }

        public async Task<Result> VerifyAsync<TEntity, T>(TEntity entity, T m)
        {
            throw new NotImplementedException();
        }
        private bool HasPassword(string password, string passwordhash) {
            var hashpass = BCrypt.Net.BCrypt.Verify(password, passwordhash);
            return hashpass;
        }
    }
}

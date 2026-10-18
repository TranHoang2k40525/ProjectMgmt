using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using IdentityExperience.Application.Dto;
using System.Text.RegularExpressions;
using IdentityExperience.Application.IServices;
namespace ProjectMgmt.Solution.Controller

{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IAccountServices _accountServices;
        public AccountController(IAccountServices accountServices) {
            _accountServices = accountServices;
        }
        [HttpPost("Login")]
        public async Task<IActionResult> Login(string username, string password)
        {

            if (string.IsNullOrEmpty(username))
            {
                return BadRequest(new Result
                {
                    Message = "Vui lòng nhập tên đăng nhập",
                    Success = false

                });
            }
            if (string.IsNullOrEmpty(password))
            {
                return BadRequest(new Result
                {
                    Message = "Vui lòng nhập mật khẩu",
                    Success = false

                });
            }
            var pattern = @"^[a-zA-Z0-9_]+$";
            if (!Regex.IsMatch(username, pattern)) return BadRequest(new Result { Message = "Username không hợp lệ. Chỉ được chứa chữ cái, số và dấu gạch dưới.\"", Success = false });


            var data = _accountServices.LoginAsync(username, password);
            return Ok(data);

        }
        [HttpPost("Register")]
        public async Task<IActionResult> Register(AccountDto.Register request)
        {
            var data = "đăng nhập thành công";
            return Ok(data);
        }
        [HttpPost("PostOtp")]
        public async Task<IActionResult> PostOtp(AccountDto.Register request, string OtpRequest)
        {
            return Ok("xác thực thành công");
        }
    }
}

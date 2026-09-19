using System;
using System.Collections.Generic;
using System.Text;

namespace IdentityExperience.Application.Dto
{
    public class Result
    {
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Verify { get; set; }
    }


    public class ResultLogin
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Verify { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
    }
}

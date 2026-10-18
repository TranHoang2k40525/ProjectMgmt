using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;

namespace IdentityExperience.Application.Dto
{
    public class AccountDto
    {
        public class Register
        {

            [Required]
            public string Email { get; set; } = null!;
            [Required]
            public string Password { get; set; } = null!;
            public string FirstName { get; set; } = null!;

            public string LastName { get; set; } = null!;
            public string PhoneNumber { get; set; } = string.Empty;
            public string IsEmailVerified { get; set; } = string.Empty;
        }
    }
}

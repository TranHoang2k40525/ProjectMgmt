using System;
using System.Collections.Generic;
using System.Text;
using PhoneNumbers;
using System.Net.Mail;
namespace ProjectMgmt.Core.Validation
{
	public class Validate
	{
        public static bool ValidPhoneNumber(string phoneNumber) {
            if (string.IsNullOrEmpty(phoneNumber))
            {
                return false;
            }
            var phoneUtil = PhoneNumberUtil.GetInstance(phoneNumber);
            var number = phoneUtil.Parse(phoneNumber, "VN");
            return phoneUtil.IsValidNumber(number);
        }
        public static bool ValidMail(string email)
        {
            if (string.IsNullOrEmpty(email)) return false;
            MailAddress address;
            return MailAddress.TryCreate(email, out address)
           && address.Address == email;
        }
    }
}

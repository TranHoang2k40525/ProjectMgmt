namespace IdentityExperience.Application.IServices;

public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public interface IOtpCodeService
{
    string GenerateCode();
    string Hash(string normalizedEmail, string purpose, string code);
    bool Verify(string normalizedEmail, string purpose, string code, string codeHash);
}

namespace ProjectMgmt.BuildingBlocks.Exceptions;

public class ProjectMgmtException : Exception
{
    public ProjectMgmtException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class NotFoundException(string code, string message) : ProjectMgmtException(code, message);

public sealed class ConflictException(string code, string message) : ProjectMgmtException(code, message);

public sealed class ForbiddenException(string code, string message) : ProjectMgmtException(code, message);

public sealed class UnauthorizedException(string code, string message) : ProjectMgmtException(code, message);

public sealed class DomainRuleException(string code, string message) : ProjectMgmtException(code, message);

public sealed class ValidationException : ProjectMgmtException
{
    public ValidationException(string code, string message, IReadOnlyDictionary<string, string[]> errors)
        : base(code, message)
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

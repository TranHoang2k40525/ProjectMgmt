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

public class NotFoundException : ProjectMgmtException
{
    public NotFoundException(string code, string message)
        : base(code, message)
    {
    }
}

public class ConflictException : ProjectMgmtException
{
    public ConflictException(string code, string message)
        : base(code, message)
    {
    }
}

public class ForbiddenException : ProjectMgmtException
{
    public ForbiddenException(string code, string message)
        : base(code, message)
    {
    }
}

public class UnauthorizedException : ProjectMgmtException
{
    public UnauthorizedException(string code, string message)
        : base(code, message)
    {
    }
}

public class DomainRuleException : ProjectMgmtException
{
    public DomainRuleException(string code, string message)
        : base(code, message)
    {
    }
}

public class ValidationException : ProjectMgmtException
{
    public ValidationException(string code, string message, IReadOnlyDictionary<string, string[]> errors)
        : base(code, message)
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

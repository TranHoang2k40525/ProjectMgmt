using ProjectMgmt.BuildingBlocks.Exceptions;

namespace ProjectMgmt.BuildingBlocks.Common;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed record PageRequest
{
    public const int MaximumPageSize = 200;

    public PageRequest(int pageNumber = 1, int pageSize = 20)
    {
        if (pageNumber < 1)
        {
            throw new ValidationException(
                "pagination.page_number",
                "Page number must be at least 1.",
                new Dictionary<string, string[]> { [nameof(pageNumber)] = ["Must be at least 1."] });
        }

        if (pageSize is < 1 or > MaximumPageSize)
        {
            throw new ValidationException(
                "pagination.page_size",
                $"Page size must be between 1 and {MaximumPageSize}.",
                new Dictionary<string, string[]> { [nameof(pageSize)] = [$"Must be between 1 and {MaximumPageSize}."] });
        }

        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    public int PageNumber { get; }

    public int PageSize { get; }

    public int Skip => (PageNumber - 1) * PageSize;
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    long TotalCount)
{
    public long TotalPages => PageSize == 0 ? 0 : (long)Math.Ceiling(TotalCount / (double)PageSize);
}

public static class Guard
{
    public static string Required(string? value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();

    public static T NotNull<T>(T? value, string parameterName)
        where T : class => value ?? throw new ArgumentNullException(parameterName);

    public static int InRange(int value, int minimum, int maximum, string parameterName) =>
        value < minimum || value > maximum
            ? throw new ArgumentOutOfRangeException(parameterName, value, $"Value must be between {minimum} and {maximum}.")
            : value;
}

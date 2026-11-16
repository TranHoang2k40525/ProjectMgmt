using System.Globalization;
using System.Text.RegularExpressions;

namespace DeliveryIntelligence.Domain.UseCases;

/// <summary>
/// Mã hiển thị của Issue dạng "PROJ-123": ProjectKey của Planning ghép với IssueNumber cấp theo Project.
/// </summary>
public static class IssueKey
{
    private static readonly Regex ProjectKeyPattern = new("^[A-Z][A-Z0-9]{1,9}$", RegexOptions.Compiled);

    public static string Format(string projectKey, int issueNumber)
    {
        if (!ProjectKeyPattern.IsMatch(projectKey ?? string.Empty))
        {
            throw new ArgumentException("ProjectKey không hợp lệ.", nameof(projectKey));
        }

        if (issueNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(issueNumber), "IssueNumber phải lớn hơn 0.");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{projectKey}-{issueNumber}");
    }

    public static bool TryParse(string? value, out string projectKey, out int issueNumber)
    {
        projectKey = string.Empty;
        issueNumber = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        var separator = text.LastIndexOf('-');
        if (separator <= 0 || separator == text.Length - 1)
        {
            return false;
        }

        var key = text[..separator].ToUpperInvariant();
        if (!ProjectKeyPattern.IsMatch(key)
            || !int.TryParse(text[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || number <= 0)
        {
            return false;
        }

        projectKey = key;
        issueNumber = number;
        return true;
    }
}

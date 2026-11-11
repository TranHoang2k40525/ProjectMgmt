namespace ProjectMgmt.Core.Security;

using System.Text.RegularExpressions;

public sealed record InputGuardrailResult(string SanitizedText, int MaskedSecretCount, bool InjectionNeutralized);

/// <summary>
/// Input Guardrail: Scans and masks sensitive secrets (API keys, JWTs, passwords, connection strings)
/// as [MASKED_SECRET] and neutralizes prompt injection directives before sending text to external LLMs.
/// </summary>
public static partial class InputGuardrail
{
    private static readonly Regex SecretPattern = new(
        @"(AIza[0-9A-Za-z\-_]{20,}|AQ\.[0-9A-Za-z\-_]{20,}|sk-[0-9A-Za-z\-_]{20,}|Bearer\s+[0-9A-Za-z\-_\.]{20,}|(?:password|passwd|pwd|secret|apikey|api_key)\s*[:=]\s*[^\s,;""']+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex InjectionPattern = new(
        @"(?:ignore\s+all\s+previous\s+instructions|disregard\s+system\s+prompt|system\s+override\s*:)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static InputGuardrailResult Sanitize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new InputGuardrailResult(string.Empty, 0, false);
        }

        var maskedCount = 0;
        var sanitized = SecretPattern.Replace(input, match =>
        {
            maskedCount++;
            return "[MASKED_SECRET]";
        });

        var injectionFound = InjectionPattern.IsMatch(sanitized);
        if (injectionFound)
        {
            sanitized = InjectionPattern.Replace(sanitized, "[GUARDRAIL_FILTERED]");
        }

        return new InputGuardrailResult(sanitized, maskedCount, injectionFound);
    }
}


namespace ProjectMgmt.Core.Security;

using System.Text.RegularExpressions;

public sealed record InputGuardrailResult(string SanitizedText, int MaskedSecretCount, bool InjectionNeutralized);

/// <summary>
/// Input Guardrail: Scans and masks sensitive secrets (API keys, JWTs, passwords, connection strings)
/// as [MASKED_SECRET], detects/blocks prompt injection, secret/cross-tenant exfiltration, and unsafe HTML/script payloads.
/// </summary>
public static partial class InputGuardrail
{
    private static readonly Regex SecretPattern = new(
        @"(AIza[0-9A-Za-z\-_]{20,}|AQ\.[0-9A-Za-z\-_]{20,}|sk-[0-9A-Za-z\-_]{20,}|Bearer\s+[0-9A-Za-z\-_\.]{20,}|(?:password|passwd|pwd|secret|apikey|api_key|signingkey|hashkey|connectionstring)\s*[:=]\s*[^\s,;""']+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex InjectionPattern = new(
        @"(?:ignore\s+(?:all\s+)?(?:previous|prior|above)\s+instructions|disregard\s+(?:all\s+)?(?:previous\s+)?system\s+prompt|system\s+override\s*:|you\s+are\s+no\s+longer\s+an?\s+ai|jailbreak|dan\s+mode|bỏ\s+qua\s+(?:mọi|tất\s+cả)?\s*(?:hướng\s+dẫn|chỉ\s+thị|quy\s+tắc)\s*(?:trước|trên|hệ\s+thống)?|quên\s+(?:hết|toàn\s+bộ)\s+(?:hướng\s+dẫn|chỉ\s+dẫn)\s*(?:trước|cũ)?|bạn\s+không\s+còn\s+là\s+trợ\s+lý)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SecretOrCrossTenantExfilPattern = new(
        @"(?:(?:xuất|in|tiết\s+lộ|cho\s+xem|hiển\s+thị|show|reveal|print|dump|leak|output|đọc|trích\s+xuất)\s+(?:ra\s+)?(?:toàn\s+bộ\s+|nguyên\s+văn\s+|chi\s+tiết\s+|danh\s+sách\s+|chuỗi\s+|các\s+|của\s+|the\s+|entire\s+|all\s+)?(?:system\s*prompt|prompt\s*hệ\s+thống|api\s*key|secret\s*key|signing\s*key|connection\s*string|chuỗi\s+kết\s+nối|mật\s+khẩu|password|hash\s*mật\s+khẩu|mã\s*băm|jwt|appsettings|\.env|dữ\s+liệu\s+(?:của\s+)?(?:tổ\s+chức|dự\s+án|công\s+ty)\s+khác|danh\s+sách\s+(?:email|tài\s+khoản|mật\s+khẩu).*tổ\s+chức\s+khác)|(?:tất\s+cả|mọi|các)\s+(?:tổ\s+chức|org|tenant)\s+khác|(?:select\s+\*\s+from|union\s+select|drop\s+table|xp_cmdshell))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UnsafeHtmlScriptPattern = new(
        @"(?:<\s*/?\s*script\b[^>]*>|<\s*iframe\b[^>]*>|<\s*object\b[^>]*>|javascript\s*:|\bon(?:error|load|click|mouseover|focus)\s*=)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex IndirectDocInjectionPattern = new(
        @"(?:<\s*/?\s*(?:system|developer|assistant_override|instructions|untrusted_document_context|untrusted_spechub_context)\b[^>]*>|<\|im_(?:start|end)\|>|\[/?\s*(?:SYSTEM|ADMIN|DEVELOPER)\s*(?:OVERRIDE|INSTRUCTION|PROMPT|MESSAGE)?\s*\]|###\s*(?:SYSTEM|OVERRIDE|NEW\s+INSTRUCTIONS)\b|(?:new|updated|override)\s+system\s+(?:instructions?|rules?|prompt)|(?:act|behave)\s+as\s+(?:system\s+admin|root|unrestricted\s+ai)|(?:ghi\s+đè|thay\s+đổi|vô\s+hiệu\s+hóa)\s+(?:quy\s+tắc|chỉ\s+dẫn|quy\s+chuẩn)\s*(?:hệ\s+thống|bảo\s+mật)?|(?:thực\s+thi|làm\s+theo)\s+(?:mệnh\s+lệnh|lệnh)\s*(?:ẩn|này|bên\s+dưới)?\s*:)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool ContainsPromptInjectionOrExfiltration(string? input, out string violationCode)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            violationCode = string.Empty;
            return false;
        }

        if (InjectionPattern.IsMatch(input))
        {
            violationCode = "PROMPT_INJECTION";
            return true;
        }

        if (SecretOrCrossTenantExfilPattern.IsMatch(input))
        {
            violationCode = "SECRET_OR_CROSS_TENANT_EXFILTRATION";
            return true;
        }

        violationCode = string.Empty;
        return false;
    }

    public static bool ContainsUnsafeScript(string? input)
        => !string.IsNullOrWhiteSpace(input) && UnsafeHtmlScriptPattern.IsMatch(input);

    public static string StripUnsafeHtmlOrScript(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var cleaned = Regex.Replace(
            input,
            @"<\s*script\b[^>]*>[\s\S]*?<\s*/\s*script\s*>",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        cleaned = UnsafeHtmlScriptPattern.Replace(cleaned, string.Empty);
        return cleaned.Trim();
    }

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

        var injectionFound = InjectionPattern.IsMatch(sanitized) || SecretOrCrossTenantExfilPattern.IsMatch(sanitized);
        if (injectionFound)
        {
            sanitized = InjectionPattern.Replace(sanitized, "[GUARDRAIL_FILTERED]");
        }

        if (UnsafeHtmlScriptPattern.IsMatch(sanitized))
        {
            injectionFound = true;
            sanitized = StripUnsafeHtmlOrScript(sanitized);
        }

        return new InputGuardrailResult(sanitized, maskedCount, injectionFound);
    }

    /// <summary>
    /// Lọc sạch các chỉ dẫn tiêm nhiễm gián tiếp (Indirect Prompt Injection), thẻ giả mạo hệ thống/ngữ cảnh
    /// và khóa bí mật bên trong văn bản trích xuất từ tài liệu đính kèm hoặc Spec Hub.
    /// </summary>
    public static InputGuardrailResult SanitizeDocumentContent(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new InputGuardrailResult(string.Empty, 0, false);
        }

        var maskedCount = 0;
        var sanitized = SecretPattern.Replace(input, _ =>
        {
            maskedCount++;
            return "[MASKED_SECRET]";
        });

        var injectionFound = InjectionPattern.IsMatch(sanitized)
            || SecretOrCrossTenantExfilPattern.IsMatch(sanitized)
            || IndirectDocInjectionPattern.IsMatch(sanitized);

        if (injectionFound)
        {
            sanitized = IndirectDocInjectionPattern.Replace(sanitized, "[Cảnh báo bảo mật: chỉ dẫn khả nghi trong tài liệu đã bị vô hiệu hóa]");
            sanitized = InjectionPattern.Replace(sanitized, "[Cảnh báo bảo mật: chỉ dẫn khả nghi trong tài liệu đã bị vô hiệu hóa]");
            sanitized = SecretOrCrossTenantExfilPattern.Replace(sanitized, "[Cảnh báo bảo mật: truy vấn ngoài phạm vi trong tài liệu đã bị vô hiệu hóa]");
        }

        return new InputGuardrailResult(sanitized, maskedCount, injectionFound);
    }
}


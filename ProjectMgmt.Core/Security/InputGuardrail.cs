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
        @"(?:(?:ignore|disregard|forget|override|bypass|disable|turn\s+off|drop|reset)\s+(?:all\s+|any\s+|every\s+|the\s+|your\s+)?(?:previous|prior|above|earlier|original|system|safety|security|existing)?\s*(?:instructions?|rules?|prompts?|guidelines?|guardrails?|filters?|restrictions?|security\s+filter)|system\s+override\s*:|you\s+are\s+no\s+longer\s+an?\s+ai|you\s+have\s+no\s+restrictions|pretend\s+you\s+have\s+no\s+rules|(?:act|switch|behave|enter)\s+(?:as\s+|to\s+)?(?:dan\b|unrestricted|uncensored|jailbroken|developer\s+mode)|jailbreak|(?:bật\s+)?dan\s+mode|(?:bỏ\s+qua|phớt\s+lờ|quên\s+(?:hết|toàn\s+bộ|mọi|tất\s+cả)|vô\s+hiệu\s+hóa|vượt\s+(?:rào|qua)|tắt|gỡ\s+bỏ)\s+(?:mọi\s+|tất\s+cả\s+|các\s+|hết\s+)?(?:quy\s+tắc|nguyên\s+tắc|quy\s+chuẩn|hướng\s+dẫn|chỉ\s+dẫn|chỉ\s+thị|giới\s+hạn|bộ\s+lọc|rào\s+chắn|guardrail)(?:\s+(?:trước(?:\s+đó)?|trên|cũ|hệ\s+thống|bảo\s+mật|an\s+toàn))*|ghi\s+đè\s+(?:mọi\s+|các\s+)?(?:chỉ\s+dẫn|quy\s+tắc|prompt)\s*(?:hệ\s+thống)?|bạn\s+không\s+còn\s+là\s+trợ\s+lý|(?:đóng\s+vai|vào\s+vai|giả\s+vờ(?:\s+bạn)?\s+là)\s+(?:tin\s+tặc|hacker|ai\s+không\s+giới\s+hạn|admin\s+tối\s+cao))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SecretOrCrossTenantExfilPattern = new(
        @"(?:(?:xuất|in|tiết\s+lộ|cho\s+(?:tôi\s+)?xem|hiển\s+thị|gửi(?:\s+cho\s+tôi)?|đưa(?:\s+cho\s+tôi|\s+tôi)?|lấy(?:\s+cho\s+tôi)?|show|reveal|print|dump|leak|output|display|expose|đọc|trích\s+xuất|liệt\s+kê)\s+(?:ra\s+|giúp\s+tôi\s+)?(?:(?:toàn\s+bộ|nguyên\s+văn|chi\s+tiết|danh\s+sách|nội\s+dung|thông\s+tin|tất\s+cả|tệp|file|bảng|khóa|chuỗi|các|của|the|entire|all|user|email(?:s)?\s+and)\s+)*(?:system\s*prompt|prompt\s*(?:hệ\s+thống|gốc|nội\s+bộ)|internal\s+instructions?|(?:gemini|openai|claude|qdrant|langfuse)?\s*(?:api\s*)?keys?|redis\s*password|(?:jwt\s*)?(?:signing|hash|secret)\s*key|khóa\s+ký\s+token|connection\s*strings?|chuỗi\s+kết\s+nối|(?:hash\s*)?(?:mật\s+khẩu|passwords?)(?:\s+hash(?:es)?)?|tài\s+khoản\s+mật\s+khẩu|mã\s*băm|appsettings(?:\.local)?\.json|\.env\b|biến\s+môi\s+trường|environment\s+variables?|dữ\s+liệu\s+(?:mật\s+)?(?:của\s+)?(?:tổ\s+chức|dự\s+án|công\s+ty|tenant|org|khách\s+hàng)\s+(?:khác|đối\s+thủ)|(?:(?:email|tài\s+khoản|mật\s+khẩu).*?(?:tổ\s+chức|org|toàn\s+hệ\s+thống|database)))|(?:lấy|xem|truy\s+cập|truy\s+vấn|đọc|liệt\s+kê)\s+[\p{L}\p{N}\s,._-]{0,45}?\b(?:của\s+)?(?:tổ\s+chức|dự\s+án|công\s+ty|tenant|org)\s+(?:khác|đối\s+thủ)\b|(?:tất\s+cả|mọi|các|truy\s+cập|truy\s+vấn|lấy\s+dữ\s+liệu|xem\s+dữ\s+liệu)\s+(?:của\s+)?(?:tổ\s+chức|org|tenant|công\s+ty)\s+khác|(?:truy\s+cập|đọc|lấy|xem)\s+(?:dữ\s+liệu|backlog|sprint|tài\s+liệu)\s+(?:của\s+)?dự\s+án\s+khác|(?:nâng\s+quyền\s+admin|cấp\s+quyền\s+admin\s+cho\s+tôi)|(?<!chống\s+)(?<!kiểm\s+thử\s+chống\s+)(?:bypass|vượt\s+qua)\s+(?:phân\s+quyền|rbac|xác\s+thực|jwt|bảo\s+mật)|(?:select\s+\*\s+from\b|union\s+(?:all\s+)?select\b|drop\s+(?:table|database)\b|;\s*delete\s+from\b|xp_cmdshell|rm\s+-rf\b|/etc/passwd|\.\./\.\./|\.\.\\\.\.\\|powershell\s+-enc\b|'\s*or\s*'1'\s*=\s*'1))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UnsafeHtmlScriptPattern = new(
        @"(?:<\s*/?\s*script\b[^>]*>|<\s*iframe\b[^>]*>|<\s*object\b[^>]*>|javascript\s*:|\bon(?:error|load|click|mouseover|focus)\s*=|document\.cookie|window\.location)",
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


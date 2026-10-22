using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace DeliveryIntelligence.Domain.UseCases;

/// <summary>Giá trị hợp lệ của DataOps theo CHECK constraint và comment schema.</summary>
public static partial class DatasetRules
{
    public static readonly IReadOnlySet<string> TaskTypes = new HashSet<string> { "Breakdown", "Assignment" };
    public static readonly IReadOnlySet<string> SplitTypes = new HashSet<string> { "Train", "Validation", "Test" };
    public static readonly IReadOnlySet<string> SourceTypes = new HashSet<string>
    {
        "AiSuggestedTask", "AiAssignmentDecision", "BreakdownFeedback", "AssignmentFeedback", "ManualCurated", "Synthetic"
    };

    public static class Quality
    {
        public const string Raw = "Raw";
        public const string Cleaned = "Cleaned";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
    }

    public static class Review
    {
        public const string NotReviewed = "NotReviewed";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string NeedsFix = "NeedsFix";
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{1,79}$")]
    public static partial Regex DatasetCodePattern();

    [GeneratedRegex(@"^v\d{1,3}\.\d{1,3}(\.\d{1,3})?$")]
    public static partial Regex VersionTagPattern();
}

/// <summary>
/// ContentHash = SHA-256 (hex thường) của Instruction + InputJson + OutputJson sau chuẩn hóa:
/// cắt khoảng trắng đầu/cuối, đổi xuống dòng về \n, JSON viết gọn (giữ thứ tự thuộc tính).
/// Cùng nội dung ⇒ cùng hash ⇒ UQ_AiDatasetSample_Dedup chặn trùng trong một version.
/// </summary>
public static class SampleHasher
{
    // Giữ nguyên tiếng Việt (không mã hóa thành \u) để người duyệt đọc được và JSONL export gọn; vẫn escape ký tự nhạy cảm HTML.
    private static readonly JsonSerializerOptions CanonicalOptions = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

    public static string Compute(string instruction, string inputJson, string outputJson)
    {
        var canonical = string.Join('\u001f', Normalize(instruction), CanonicalJson(inputJson), CanonicalJson(outputJson));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    /// <summary>JSON hợp lệ thì trả bản viết gọn; không hợp lệ thì ném <see cref="JsonException"/>.</summary>
    public static string CanonicalJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement, CanonicalOptions);
    }

    public static bool IsValidJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
}

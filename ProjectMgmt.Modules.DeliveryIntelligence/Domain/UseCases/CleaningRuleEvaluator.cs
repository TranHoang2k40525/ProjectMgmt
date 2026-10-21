using System.Text.Json;
using System.Text.RegularExpressions;
using DeliveryIntelligence.Domain.Entities;

namespace DeliveryIntelligence.Domain.UseCases;

public record CleaningFinding(string Message, string? FieldPath);

/// <summary>
/// Đánh giá rule làm sạch dạng cấu hình (AiDataCleaningRule.Config JSON) trên một sample. Các loại hỗ trợ:
/// LengthCheck, SchemaValidation, Heuristic, Deduplication, PiiDetection. Loại khác (vd LanguageCheck, chấm điểm bằng mô hình)
/// chưa được hỗ trợ và bị bỏ qua — đó là phần AI để lại cho bước sau.
/// </summary>
public static partial class CleaningRuleEvaluator
{
    public static readonly IReadOnlySet<string> SupportedTypes = new HashSet<string>
    {
        "LengthCheck", "SchemaValidation", "Heuristic", "Deduplication", "PiiDetection"
    };

    public static readonly IReadOnlySet<string> Severities = new HashSet<string> { "Info", "Warning", "Error" };

    /// <param name="sameInstructionCount">Số sample KHÁC trong cùng version có instruction (đã chuẩn hóa) trùng sample này.</param>
    public static IReadOnlyList<CleaningFinding> Evaluate(AiDataCleaningRule rule, AiDatasetSample sample, int sameInstructionCount = 0)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(sample);
        using var config = JsonDocument.Parse(string.IsNullOrWhiteSpace(rule.Config) ? "{}" : rule.Config);
        var c = config.RootElement;
        return rule.RuleType switch
        {
            "LengthCheck" => LengthCheck(c, sample),
            "SchemaValidation" => SchemaValidation(c, sample),
            "Heuristic" => GenericItems(c, sample),
            "Deduplication" => sameInstructionCount > 0
                ? [new CleaningFinding($"Instruction trùng với {sameInstructionCount} mẫu khác trong version.", "$.instruction")]
                : [],
            "PiiDetection" => DetectPii(c, sample),
            _ => []
        };
    }

    public static string NormalizeInstruction(string instruction) =>
        WhitespaceRun().Replace(instruction.Trim().ToLowerInvariant(), " ");

    private static List<CleaningFinding> LengthCheck(JsonElement c, AiDatasetSample sample)
    {
        var field = Text(c, "field") ?? "Instruction";
        var value = FieldValue(sample, field);
        var length = value.Trim().Length;
        var min = Int(c, "min") ?? 1;
        var max = Int(c, "max") ?? int.MaxValue;
        if (length < min) return [new($"{field} quá ngắn ({length} < {min} ký tự).", Path(field))];
        if (length > max) return [new($"{field} quá dài ({length} > {max} ký tự).", Path(field))];
        return [];
    }

    private static List<CleaningFinding> SchemaValidation(JsonElement c, AiDatasetSample sample)
    {
        var findings = new List<CleaningFinding>();
        if (!SampleHasher.IsValidJson(sample.InputJson)) findings.Add(new("InputJson không phải JSON hợp lệ.", "$.input"));
        if (!SampleHasher.IsValidJson(sample.OutputJson))
        {
            findings.Add(new("OutputJson không phải JSON hợp lệ.", "$.output"));
            return findings;
        }

        var required = Text(c, "requiredArray");
        if (required is null) return findings;
        using var output = JsonDocument.Parse(sample.OutputJson);
        if (output.RootElement.ValueKind != JsonValueKind.Object
            || !output.RootElement.TryGetProperty(required, out var array)
            || array.ValueKind != JsonValueKind.Array
            || array.GetArrayLength() == 0)
        {
            findings.Add(new($"OutputJson phải có mảng \"{required}\" không rỗng.", $"$.output.{required}"));
        }

        return findings;
    }

    /// <summary>Phần tử chung chung: tiêu đề quá ít từ hoặc nằm trong danh sách cụm từ vô nghĩa.</summary>
    private static List<CleaningFinding> GenericItems(JsonElement c, AiDatasetSample sample)
    {
        var arrayPath = Text(c, "arrayPath") ?? "subTasks";
        var field = Text(c, "field") ?? "title";
        var minWords = Int(c, "minWords") ?? 3;
        var phrases = c.TryGetProperty("phrases", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Select(p => p.GetString()?.Trim().ToLowerInvariant()).Where(p => !string.IsNullOrEmpty(p)).ToHashSet()
            : [];
        if (!SampleHasher.IsValidJson(sample.OutputJson)) return [];

        using var output = JsonDocument.Parse(sample.OutputJson);
        if (output.RootElement.ValueKind != JsonValueKind.Object
            || !output.RootElement.TryGetProperty(arrayPath, out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var findings = new List<CleaningFinding>();
        var index = 0;
        foreach (var item in items.EnumerateArray())
        {
            var title = item.ValueKind == JsonValueKind.Object && item.TryGetProperty(field, out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString() ?? string.Empty
                : string.Empty;
            var normalized = NormalizeInstruction(title).TrimEnd('.', '!', ':');
            var words = normalized.Length == 0 ? 0 : normalized.Split(' ').Length;
            if (words < minWords || phrases.Contains(normalized))
            {
                findings.Add(new($"Mục \"{title}\" quá chung chung.", $"$.output.{arrayPath}[{index}].{field}"));
            }

            index++;
        }

        return findings;
    }

    private static List<CleaningFinding> DetectPii(JsonElement c, AiDatasetSample sample)
    {
        var detectors = c.TryGetProperty("detectors", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Select(d => d.GetString()).ToHashSet()
            : ["email", "phone", "secret"];
        var findings = new List<CleaningFinding>();
        foreach (var (field, text) in new[] { ("instruction", sample.Instruction), ("input", sample.InputJson), ("output", sample.OutputJson) })
        {
            // Không đưa giá trị nhạy cảm vào message: chỉ nêu loại và vị trí.
            if (detectors.Contains("email") && EmailPattern().IsMatch(text)) findings.Add(new($"Nghi có email trong {field}.", $"$.{field}"));
            if (detectors.Contains("phone") && PhonePattern().IsMatch(text)) findings.Add(new($"Nghi có số điện thoại trong {field}.", $"$.{field}"));
            if (detectors.Contains("secret") && SecretPattern().IsMatch(text)) findings.Add(new($"Nghi có khóa/token bí mật trong {field}.", $"$.{field}"));
        }

        return findings;
    }

    private static string FieldValue(AiDatasetSample sample, string field) => field switch
    {
        "InputJson" => sample.InputJson,
        "OutputJson" => sample.OutputJson,
        _ => sample.Instruction
    };

    private static string Path(string field) => field switch
    {
        "InputJson" => "$.input",
        "OutputJson" => "$.output",
        _ => "$.instruction"
    };

    private static string? Text(JsonElement c, string name) =>
        c.ValueKind == JsonValueKind.Object && c.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement c, string name) =>
        c.ValueKind == JsonValueKind.Object && c.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : null;

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailPattern();

    // Di động VN: +84/84/0 rồi 9 chữ số (cho phép dấu cách, chấm, gạch).
    [GeneratedRegex(@"(?<!\d)(?:\+?84|0)(?:[\s.-]?\d){9}(?!\d)")]
    private static partial Regex PhonePattern();

    // Khóa API phổ biến, JWT, chuỗi "password=..." / "token: ...".
    [GeneratedRegex(@"(?:sk-[A-Za-z0-9_-]{20,}|AKIA[0-9A-Z]{16}|gh[pousr]_[A-Za-z0-9]{30,}|eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}|(?:password|passwd|secret|token|api[_-]?key)\s*[:=]\s*\S{6,})", RegexOptions.IgnoreCase)]
    private static partial Regex SecretPattern();
}

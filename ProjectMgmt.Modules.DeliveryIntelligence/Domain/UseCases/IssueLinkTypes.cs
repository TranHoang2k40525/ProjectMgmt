namespace DeliveryIntelligence.Domain.UseCases;

/// <summary>
/// Loại liên kết theo CK_IssueLink_Type. Mỗi quan hệ lưu hai bản ghi đối xứng (A Blocks B ↔ B IsBlockedBy A)
/// để màn hình mỗi Issue chỉ cần đọc liên kết đi ra.
/// </summary>
public static class IssueLinkTypes
{
    public const string Blocks = "Blocks";
    public const string IsBlockedBy = "IsBlockedBy";
    public const string Relates = "Relates";
    public const string Duplicates = "Duplicates";
    public const string IsDuplicatedBy = "IsDuplicatedBy";

    private static readonly Dictionary<string, string> Inverses = new(StringComparer.OrdinalIgnoreCase)
    {
        [Blocks] = IsBlockedBy,
        [IsBlockedBy] = Blocks,
        [Relates] = Relates,
        [Duplicates] = IsDuplicatedBy,
        [IsDuplicatedBy] = Duplicates
    };

    /// <summary>Chuẩn hóa tên loại (không phân biệt hoa thường); null nếu không hợp lệ.</summary>
    public static string? Normalize(string? linkType) =>
        linkType is not null && Inverses.ContainsKey(linkType.Trim())
            ? Inverses.Keys.First(key => key.Equals(linkType.Trim(), StringComparison.OrdinalIgnoreCase))
            : null;

    public static string InverseOf(string linkType) => Inverses[linkType];
}

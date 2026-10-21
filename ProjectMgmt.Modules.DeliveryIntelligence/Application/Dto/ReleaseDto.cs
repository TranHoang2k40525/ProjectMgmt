namespace DeliveryIntelligence.Application.Dto;

public class IssueComponentDto
{
    public Guid ComponentId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class IssueVersionDto
{
    public Guid VersionId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>FixVersion | AffectsVersion (CK_IssueVersion_LinkType).</summary>
    public string LinkType { get; set; } = string.Empty;
    public DateOnly? ReleaseDate { get; set; }
    public bool IsReleased { get; set; }
}

public class ReleaseOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly? ReleaseDate { get; set; }
    public bool IsReleased { get; set; }
}

/// <summary>Liên kết hiện có của Issue và danh sách Component/Version của Project để chọn thêm.</summary>
public class IssueReleaseLinksDto
{
    public IReadOnlyList<IssueComponentDto> Components { get; set; } = [];
    public IReadOnlyList<IssueVersionDto> FixVersions { get; set; } = [];
    public IReadOnlyList<IssueVersionDto> AffectsVersions { get; set; } = [];
    public IReadOnlyList<ReleaseOptionDto> AvailableComponents { get; set; } = [];
    public IReadOnlyList<ReleaseOptionDto> AvailableVersions { get; set; } = [];
}

public class AddIssueComponentDto
{
    public Guid ComponentId { get; set; }
}

public class AddIssueVersionDto
{
    public Guid VersionId { get; set; }
    public string LinkType { get; set; } = "FixVersion";
}

namespace ProjectMgmt.Solution.Dtos.Ai;

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Request model for initiating real-time AI sprint decomposition via multipart/form-data.
/// </summary>
public sealed class AiDecomposeStreamRequest
{
    /// <summary>
    /// Identifier of the target Project.
    /// </summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Optional target Sprint identifier. If null, plans for the next upcoming planned sprint.
    /// </summary>
    public Guid? TargetSprintId { get; set; }

    /// <summary>
    /// Optional user prompt or specific decomposition guidance (e.g. "Ưu tiên phân rã các task bảo mật").
    /// </summary>
    [MaxLength(4000)]
    public string? Prompt { get; set; }

    /// <summary>
    /// Optional text pasted directly into the UI (e.g. meeting notes, SRS excerpt, user stories).
    /// </summary>
    [MaxLength(100000)]
    public string? Text { get; set; }

    /// <summary>
    /// Optional uploaded document file (.docx, .pdf, .txt, .md, .xlsx). Max 30MB.
    /// </summary>
    public IFormFile? File { get; set; }
}

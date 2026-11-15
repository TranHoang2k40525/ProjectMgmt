namespace ProjectMgmt.Solution.Dtos.Ai;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Request to regenerate a single task based on user feedback without affecting other tasks.
/// </summary>
public sealed class AiRegenerateTaskRequest
{
    [Required]
    public string SessionId { get; set; } = string.Empty;

    [Required]
    public string TaskId { get; set; } = string.Empty;

    [Required, MinLength(2), MaxLength(2000)]
    public string Feedback { get; set; } = string.Empty;
}

public sealed class AiClearCacheRequest
{
    public string? SessionId { get; set; }
    public Guid? ProjectId { get; set; }
}

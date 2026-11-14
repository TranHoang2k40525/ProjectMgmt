namespace ProjectMgmt.Solution.Dtos.Ai;

public static class AiSseEventTypes
{
    public const string SessionInit = "session_init";
    public const string StepProgress = "step_progress";
    public const string ToolCall = "tool_call";
    public const string ThoughtStream = "thought_stream";
    public const string ProposalReady = "proposal_ready";
    public const string Completed = "completed";
    public const string Error = "error";
}

public sealed record AiSessionInitPayload(
    string SessionId,
    Guid ProjectId,
    string ActorName,
    DateTime TimestampUtc);

public sealed record AiStepProgressPayload(
    int StepIndex,
    int TotalSteps,
    string StepName,
    string LabelVi,
    int Percent,
    string? Detail);

public sealed record AiToolCallPayload(
    string ToolName,
    object? ToolArguments,
    string SummaryVi,
    long? DurationMs = null,
    string? ActionVerb = null,
    string? TargetLabel = null,
    string? DetailPreview = null);

public sealed record AiThoughtDeltaPayload(
    string Delta,
    int TotalLength);

public sealed record AiErrorPayload(
    string Code,
    string Message,
    int? StepIndex = null);

public sealed record AiCompletedPayload(
    string SessionId,
    long TotalDurationMs,
    int TaskCount,
    decimal TotalStoryPoints);

namespace ProjectMgmt.Core.Ai;

using ProjectMgmt.Core.Security;

/// <summary>
/// Abstraction for invoking AI models (Gemini, Claude, OpenAI, Local/Ollama) with streaming and completion support.
/// </summary>
public interface IAiModelClient
{
    /// <summary>
    /// Identifier of the active AI provider (e.g. "Gemini", "Claude", "OpenAI", "Ollama").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Executes a standard (non-streaming) completion request within a verified project scope.
    /// </summary>
    /// <param name="prompt">Prompt containing messages, tools, and execution parameters.</param>
    /// <param name="scope">Verified tenant boundary context (OrgId, ProjectId, UserId).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>AiCompletionResult containing content, thought reasoning, tool calls, and usage metrics.</returns>
    Task<AiCompletionResult> GenerateAsync(
        AiPrompt prompt,
        IProjectScopeContext scope,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a streaming completion request, invoking onChunk for each incremental token or event.
    /// </summary>
    /// <param name="prompt">Prompt containing messages, tools, and execution parameters.</param>
    /// <param name="scope">Verified tenant boundary context (OrgId, ProjectId, UserId).</param>
    /// <param name="onChunk">Async delegate invoked whenever a chunk (content, thought, tool call, done) arrives.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StreamGenerateAsync(
        AiPrompt prompt,
        IProjectScopeContext scope,
        Func<AiStreamChunk, Task> onChunk,
        CancellationToken cancellationToken = default);
}

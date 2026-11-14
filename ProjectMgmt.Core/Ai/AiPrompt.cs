namespace ProjectMgmt.Core.Ai;

/// <summary>
/// Encapsulates the execution parameters, conversation history, and tool definitions sent to the AI model.
/// </summary>
public class AiPrompt
{
    /// <summary>
    /// Ordered sequence of conversation messages (System, User, Assistant, Tool).
    /// </summary>
    public IReadOnlyList<AiMessage> Messages { get; set; } = Array.Empty<AiMessage>();

    /// <summary>
    /// Optional tool definitions available for function calling.
    /// </summary>
    public IReadOnlyList<AiToolDefinition>? Tools { get; set; }

    /// <summary>
    /// Optional system instruction overriding or prepended to conversation context.
    /// </summary>
    public string? SystemInstruction { get; set; }

    /// <summary>
    /// Temperature for sampling (0.0 to 2.0). Lower values are more deterministic.
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// Maximum number of tokens to generate in the completion.
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Specific model name override (e.g. "gemini-2.5-flash", "claude-3-7-sonnet", "gpt-4o").
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Nucleus sampling probability cutoff (0.0 to 1.0).
    /// </summary>
    public double? TopP { get; set; }

    /// <summary>
    /// Optional list of stop sequences that halt generation.
    /// </summary>
    public IReadOnlyList<string>? StopSequences { get; set; }

    /// <summary>
    /// Additional provider-specific or tracing metadata (e.g. SessionId, TraceId).
    /// </summary>
    public IDictionary<string, object?>? Metadata { get; set; }

    /// <summary>
    /// Convenience factory method creating an AiPrompt from a single user message.
    /// </summary>
    public static AiPrompt FromUserMessage(
        string userMessage,
        string? systemInstruction = null,
        double? temperature = null,
        int? maxTokens = null)
    {
        var messages = new List<AiMessage>();
        if (!string.IsNullOrWhiteSpace(systemInstruction))
        {
            messages.Add(AiMessage.CreateSystem(systemInstruction));
        }
        messages.Add(AiMessage.CreateUser(userMessage));

        return new AiPrompt
        {
            Messages = messages,
            SystemInstruction = systemInstruction,
            Temperature = temperature,
            MaxTokens = maxTokens
        };
    }
}

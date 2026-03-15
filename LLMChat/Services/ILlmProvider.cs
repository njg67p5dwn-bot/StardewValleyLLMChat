using LLMChat.Data;

namespace LLMChat.Services;

public interface ILlmProvider
{
    /// <summary>
    /// Generate a response from the LLM.
    /// </summary>
    Task<string> GenerateResponseAsync(
        string systemPrompt,
        List<ChatMessage> history,
        string userMessage,
        int maxTokens,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Generate a streaming response. Calls onToken for each token received.
    /// </summary>
    Task<string> GenerateStreamingResponseAsync(
        string systemPrompt,
        List<ChatMessage> history,
        string userMessage,
        int maxTokens,
        Action<string> onToken,
        CancellationToken cancellationToken = default
    );
}

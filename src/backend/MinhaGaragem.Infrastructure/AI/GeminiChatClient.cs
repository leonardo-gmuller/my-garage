using Google.GenAI.Types;
using Microsoft.Extensions.AI;
using MinhaGaragem.Application.AI;

namespace MinhaGaragem.Infrastructure.AI;

/// <summary>The SDK adapter is contained here; application code only sees IChatClient.</summary>
public sealed class GeminiChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options = options?.Clone() ?? new();
        // The SDK normally resumes truncated generations with further billable requests.
        options.RawRepresentationFactory = _ => new GenerateContentConfig
        {
            AutomaticContinuation = false,
        };
        try
        {
            return await base.GetResponseAsync(messages, options, cancellationToken);
        }
        catch (Google.GenAI.ClientError error) when (error.StatusCode == 429)
        {
            throw new MaintenanceAiException(AiFailure.RateLimited);
        }
        catch (Google.GenAI.ApiException)
        {
            throw new MaintenanceAiException(AiFailure.Unavailable);
        }
        catch (HttpRequestException)
        {
            throw new MaintenanceAiException(AiFailure.Unavailable);
        }
    }
}

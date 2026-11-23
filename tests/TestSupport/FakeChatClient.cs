using Microsoft.Extensions.AI;

namespace MinhaGaragem.TestSupport;

/// <summary>Test-only client. Production registration never returns simulated content.</summary>
public sealed class FakeChatClient : IChatClient
{
    public int Calls { get; private set; }
    public IReadOnlyList<ChatMessage> Messages { get; private set; } = [];
    public ChatOptions? Options { get; private set; }
    public Func<CancellationToken, Task<ChatResponse>> Respond { get; set; } =
        _ => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "{}")));

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        Calls++;
        Messages = messages.ToArray();
        Options = options;
        return Respond(cancellationToken);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}

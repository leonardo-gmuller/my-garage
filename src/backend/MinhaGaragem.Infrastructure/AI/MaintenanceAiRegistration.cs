using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MinhaGaragem.Application.AI;

namespace MinhaGaragem.Infrastructure.AI;

public static class MaintenanceAiRegistration
{
    public static IServiceCollection AddMaintenanceAi(
        this IServiceCollection services,
        IConfiguration config
    )
    {
        string? Get(string flat, string nested) =>
            string.IsNullOrWhiteSpace(config[flat]) ? config[nested] : config[flat];
        int Limit(string name, int fallback, int max) =>
            int.TryParse(config[name], out var value) ? Math.Clamp(value, 1, max) : fallback;
        var enabled = bool.TryParse(Get("AI_ENABLED", "AI:Enabled"), out var on) && on;
        var key = Get("GEMINI_API_KEY", "Gemini:ApiKey");
        var model = Get("GEMINI_MODEL", "Gemini:Model") ?? "gemini-3.5-flash-lite";
        var provider = Get("AI_PROVIDER", "AI:Provider") ?? "Gemini";
        var available =
            enabled
            && !string.IsNullOrWhiteSpace(key)
            && provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(model);
        var options = new MaintenanceAiOptions(
            available,
            model,
            Limit("AI_TIMEOUT_SECONDS", 30, 60),
            Limit("AI_MAX_INPUT_CHARS", 4000, 8000),
            Limit("AI_MAX_OUTPUT_TOKENS", 2048, 4096),
            Limit("AI_REQUESTS_PER_MINUTE", 3, 10),
            Limit("AI_REQUESTS_PER_USER_PER_DAY", 20, 100),
            Limit("AI_REQUESTS_PER_DAY", 100, 500)
        );
        services.AddSingleton(options);
        services.AddSingleton<MaintenanceAiGate>();
        if (available)
        {
            services.AddSingleton(_ => new Client(
                apiKey: key,
                vertexAI: false,
                httpOptions: new HttpOptions
                {
                    Timeout = options.TimeoutSeconds * 1000,
                    RetryOptions = new HttpRetryOptions { Attempts = 1 },
                }
            ));
            services.AddSingleton<IChatClient>(sp => new GeminiChatClient(
                sp.GetRequiredService<Client>().AsIChatClient(model)
            ));
        }
        else
            services.AddSingleton<IChatClient, UnavailableChatClient>();
        services.AddScoped<MaintenanceInterpreter>();
        return services;
    }

    private sealed class UnavailableChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        ) => throw new MaintenanceAiException(AiFailure.Unavailable);

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public object? GetService(System.Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}

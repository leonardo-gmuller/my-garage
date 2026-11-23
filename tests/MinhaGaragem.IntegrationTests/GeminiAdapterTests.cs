using System.Text.Json.Nodes;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinhaGaragem.Application.AI;
using MinhaGaragem.Infrastructure.AI;

namespace MinhaGaragem.IntegrationTests;

[Trait("Category", "AI")]
public sealed class GeminiAdapterTests
{
    [Theory]
    [InlineData("STOP")]
    [InlineData("CONTINUATION")]
    [InlineData("QUOTA")]
    public async Task OfficialSdkSendsSchemaAndHonorsSingleRequestLimitsAgainstLocalTransport(
        string finish
    )
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        JsonNode? payload = null;
        var calls = 0;
        app.MapPost("/{**path}", async (HttpContext context) =>
        {
            calls++;
            payload = JsonNode.Parse(await new StreamReader(context.Request.Body).ReadToEndAsync());
            if (finish == "QUOTA")
                return Results.Json(
                    new
                    {
                        error = new
                        {
                            code = 429,
                            message = "test-only quota",
                            status = "RESOURCE_EXHAUSTED",
                        },
                    },
                    statusCode: 429
                );
            return Results.Json(
                new
                {
                    candidates = new[]
                    {
                        new
                        {
                            content = new { role = "model", parts = new[] { new { text = "{}" } } },
                            finishReason = calls == 1 ? finish : "STOP",
                            continuationToken = finish == "CONTINUATION"
                                ? "dGVzdC1jb250aW51YXRpb24="
                                : null,
                        },
                    },
                    usageMetadata = new
                    {
                        promptTokenCount = 10,
                        candidatesTokenCount = 20,
                        totalTokenCount = 30,
                    },
                }
            );
        });
        await app.StartAsync();
        var url = app
            .Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();
        using var google = new Client(
            apiKey: "test-only-not-a-real-key",
            vertexAI: false,
            httpOptions: new HttpOptions
            {
                BaseUrl = url,
                Timeout = 5000,
                RetryOptions = new HttpRetryOptions { Attempts = 1 },
            }
        );
        using var chat = new GeminiChatClient(google.AsIChatClient("test-model"));
        var options = new ChatOptions
        {
            MaxOutputTokens = 512,
            ResponseFormat = ChatResponseFormat.ForJsonSchema<MaintenanceExtraction>(
                MaintenanceAiJson.Options
            ),
        };
        if (finish == "QUOTA")
            Assert.Equal(
                AiFailure.RateLimited,
                (
                    await Assert.ThrowsAsync<MaintenanceAiException>(() =>
                        chat.GetResponseAsync("test message", options)
                    )
                ).Failure
            );
        else
        {
            var response = await chat.GetResponseAsync("test message", options);
            Assert.Equal("{}", response.Text);
            Assert.Equal(10, response.Usage!.InputTokenCount);
        }
        Assert.Equal(1, calls);
        var generation = payload!["generationConfig"]!;
        Assert.Equal(512, generation["maxOutputTokens"]!.GetValue<int>());
        Assert.Equal("application/json", generation["responseMimeType"]!.GetValue<string>());
        var schema = generation["responseJsonSchema"] ?? generation["responseSchema"];
        Assert.NotNull(schema);
        Assert.NotNull(schema!["properties"]!["components"]);
        Assert.Null(payload["tools"]);
        await app.StopAsync();
    }

    [Fact]
    public void EmptyEnvironmentKeyDoesNotHideAUserSecretsKeyAndUnsupportedProvidersStayUnavailable()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AI_ENABLED"] = "true",
                    ["GEMINI_API_KEY"] = "",
                    ["Gemini:ApiKey"] = "test-only-key",
                }
            )
            .Build();
        var services = new ServiceCollection();
        services.AddMaintenanceAi(config);
        using var provider = services.BuildServiceProvider();
        Assert.True(provider.GetRequiredService<MaintenanceAiOptions>().Available);
        config["AI_PROVIDER"] = "unknown";
        var disabled = new ServiceCollection();
        disabled.AddMaintenanceAi(config);
        using var fallback = disabled.BuildServiceProvider();
        Assert.False(fallback.GetRequiredService<MaintenanceAiOptions>().Available);
    }
}

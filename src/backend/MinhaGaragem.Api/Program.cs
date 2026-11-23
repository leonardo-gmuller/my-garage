using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MinhaGaragem.Api;
using MinhaGaragem.Application;
using MinhaGaragem.Application.AI;
using MinhaGaragem.Domain;
using MinhaGaragem.Infrastructure;
using MinhaGaragem.Infrastructure.AI;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter())
);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<GarageService>();
builder.Services.AddScoped<MechanicService>();
builder.Services.AddMaintenanceAi(builder.Configuration);
var connection = builder.Configuration.GetConnectionString("Garage")
    ?? throw new InvalidOperationException(
        "Defina ConnectionStrings__Garage. Consulte .env.example."
    );
builder.Services.AddDbContext<GarageDbContext>(o =>
    o.UseNpgsql(connection, p => p.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
);
builder.Services.AddScoped<IGarageDb>(s => s.GetRequiredService<GarageDbContext>());
builder.Services.AddScoped<AccountProvisioner>();
builder
    .Services.AddIdentity<User, IdentityRole<Guid>>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 10;
        o.Password.RequireNonAlphanumeric = false;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<GarageDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "garage.session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy =
        builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c =>
    {
        c.Response.StatusCode = 401;
        return Task.CompletedTask;
    };
    o.Events.OnRedirectToAccessDenied = c =>
    {
        c.Response.StatusCode = 403;
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.Name = "garage.csrf";
    o.Cookie.SameSite = SameSiteMode.Strict;
});
var storageRoot = builder.Configuration["Storage:Path"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "uploads");
builder.Services.AddSingleton(new FileStorage(storageRoot));
var keys = builder.Configuration["DataProtection:Path"];
if (!string.IsNullOrEmpty(keys))
    builder
        .Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keys))
        .SetApplicationName("MinhaGaragem");
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 9 * 1024 * 1024);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await Results
            .Problem(
                statusCode: 429,
                title: "Limite de solicitações atingido. Aguarde antes de tentar novamente."
            )
            .ExecuteAsync(context.HttpContext);
    };
    o.AddPolicy("ai", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = context
                    .RequestServices.GetRequiredService<MaintenanceAiOptions>()
                    .RequestsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }
        )
    );
    o.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "local",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue<int?>("Auth:PermitLimit") ?? 15,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }
        )
    );
});
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgresql", tags: ["ready"]);
var telemetry = builder
    .Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("MinhaGaragem.Api"))
    .WithTracing(t =>
        t.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource(MaintenanceAiTelemetry.Name)
    )
    .WithMetrics(m =>
        m.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter("System.Runtime", MaintenanceAiTelemetry.Name)
    );
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
    telemetry.UseOtlpExporter();
var app = builder.Build();
app.UseExceptionHandler();
app.Use(
    async (ctx, next) =>
    {
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Response.Headers["Referrer-Policy"] = "same-origin";
        ctx.Response.Headers.CacheControl = "no-store";
        await next(ctx);
    }
);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(
    async (ctx, next) =>
    {
        if (
            ctx.Request.Path.StartsWithSegments("/api")
            && !HttpMethods.IsGet(ctx.Request.Method)
            && !HttpMethods.IsHead(ctx.Request.Method)
            && !HttpMethods.IsOptions(ctx.Request.Method)
        )
            await ctx.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(ctx);
        await next(ctx);
    }
);
app.UseAntiforgery();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = r => r.Tags.Contains("ready") });
app.MapOpenApi().RequireAuthorization();
var auth = app.MapGroup("/api/auth").WithTags("Authentication");
auth.MapGet("/csrf", (IAntiforgery af, HttpContext ctx) =>
    Results.Ok(new { token = af.GetAndStoreTokens(ctx).RequestToken })
);
auth.MapPost("/login", async (Credentials input, SignInManager<User> signIn) =>
    {
        Rules.Text(input.Email, "E-mail", 254, true);
        Rules.Text(input.Password, "Senha", 128, true);
        var result = await signIn.PasswordSignInAsync(input.Email, input.Password, false, true);
        return result.Succeeded
            ? Results.Ok(new { email = input.Email })
            : Results.Problem(
                statusCode: 401,
                title: "E-mail ou senha inválidos, ou acesso temporariamente bloqueado."
            );
    })
    .RequireRateLimiting("auth");
auth.MapGet("/me", (ClaimsPrincipal principal) =>
        Results.Ok(new { email = principal.FindFirstValue(ClaimTypes.Email) })
    )
    .RequireAuthorization();
auth.MapPost("/logout", async (SignInManager<User> signIn) =>
    {
        await signIn.SignOutAsync();
        return Results.NoContent();
    })
    .RequireAuthorization();
app.MapGarageEndpoints();
app.MapMechanicEndpoints();
app.MapMaintenanceAiEndpoints();
if (builder.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<GarageDbContext>().Database.MigrateAsync();
}
if (builder.Configuration.GetValue("Bootstrap:Enabled", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    var created = await scope
        .ServiceProvider.GetRequiredService<AccountProvisioner>()
        .InitializeFirstUser(
            builder.Configuration["Bootstrap:Email"],
            builder.Configuration["Bootstrap:Password"]
        );
    if (created)
        app.Logger.LogInformation(
            "Acesso inicial da garagem criado. A senha não será redefinida nas próximas inicializações."
        );
}
app.Run();

public sealed record Credentials(string Email, string Password);

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid Id =>
        Guid.TryParse(
            accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var id
        )
            ? id
            : Guid.Empty;
}

public sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger,
    IProblemDetailsService problems
) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx,
        Exception exception,
        CancellationToken ct
    )
    {
        var (status, title) = exception switch
        {
            MaintenanceAiException e => e.Failure switch
            {
                AiFailure.RateLimited => (
                    429,
                    "Limite da IA atingido. Aguarde e tente novamente ou registre manualmente."
                ),
                AiFailure.Timeout => (
                    504,
                    "A interpretação demorou mais que o esperado. Tente novamente ou registre manualmente."
                ),
                AiFailure.InvalidResponse => (
                    502,
                    "A IA retornou uma sugestão inválida ou sem evidência suficiente. Revise o relato ou registre manualmente."
                ),
                _ => (503, "IA indisponível. O registro manual continua disponível."),
            },
            DomainException e => (400, e.Message),
            NotFoundException => (404, "Recurso não encontrado."),
            AntiforgeryValidationException => (
                400,
                "Sessão de formulário inválida. Atualize a página e tente novamente."
            ),
            DbUpdateConcurrencyException => (
                409,
                "O veículo foi alterado. Atualize os dados e tente novamente."
            ),
            BadHttpRequestException e => (e.StatusCode, "Requisição inválida."),
            _ => (500, "Não foi possível concluir a operação."),
        };
        if (status == 500)
            logger.LogError(exception, "Request failed: {TraceId}", ctx.TraceIdentifier);
        ctx.Response.StatusCode = status;
        return await problems.TryWriteAsync(
            new()
            {
                HttpContext = ctx,
                ProblemDetails = new()
                {
                    Status = status,
                    Title = title,
                    Extensions = { ["traceId"] = ctx.TraceIdentifier },
                },
            }
        );
    }
}

public sealed class PostgresHealthCheck(GarageDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    ) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("PostgreSQL indisponível.");
}

public partial class Program;

# Aprendendo C# e .NET 10 neste projeto

## Dependency Injection

`Program.cs` é o composition root. O container nativo recebe serviços com seus ciclos de vida reais:

```csharp
builder.Services.AddScoped<GarageService>();
builder.Services.AddScoped<IGarageDb>(s => s.GetRequiredService<GarageDbContext>());
builder.Services.AddSingleton(TimeProvider.System);
```

`Scoped` cria uma instância por requisição HTTP: o serviço e Identity compartilham a mesma unidade transacional EF. `Singleton` serve para o relógio, que não guarda estado da requisição. `GarageService(IGarageDb db, ICurrentUser user, TimeProvider clock)` usa primary constructor de C#. Evite singleton que dependa de DbContext ou usuário atual.

## EF Core e migrations

`GarageDbContext` herda de `IdentityDbContext<User, IdentityRole<Guid>, Guid>`. Identity e negócio ficam no mesmo PostgreSQL. Fluent API define relacionamentos, índices, constraints e precisão:

```csharp
e.Property(r => r.LaborCost).HasPrecision(16, 2);
e.HasIndex(r => new { r.VehicleId, r.Date });
```

Uma migration versiona mudanças do modelo; a migration inicial cria as tabelas Identity e do negócio. Gere uma nova após alterar entidades/configuração:

```bash
dotnet ef migrations add NomeDaMudanca --project src/backend/MinhaGaragem.Infrastructure --startup-project src/backend/MinhaGaragem.Api
dotnet ef database update --project src/backend/MinhaGaragem.Infrastructure --startup-project src/backend/MinhaGaragem.Api
```

A `DesignTimeFactory` permite criar o contexto na CLI sem precisar de uma requisição autenticada. `SaveChangesAsync` envolve o conjunto de comandos em transação. A criação de conta usa transação explícita porque inclui o `CreateAsync` de Identity e o posterior SaveChanges da Saveiro.

## LINQ: consulta e cálculo

`GarageService.Records` começa com `IQueryable`, compõe os filtros e só executa SQL no `CountAsync`/`ToListAsync`:

```csharp
if (category.HasValue) q = q.Where(r => r.Category == category);
var total = await q.CountAsync(ct);
```

`AsNoTracking` evita rastrear entidades de consultas somente de leitura. `Include` carrega peças e vínculos; split queries configuradas no provider evitam multiplicação de linhas entre coleções. A projeção para DTO ocorre depois da carga. `FinancialSummary.Calculate` trabalha em memória com `IEnumerable`, agrupando por mês/ano/categoria. Um vínculo de componente não duplica o serviço na soma.

## async/await e CancellationToken

Endpoints recebem `CancellationToken ct` do ASP.NET Core e o propagam por serviço, EF e arquivos:

```csharp
var v = await s.Vehicle(vehicleId, ct);
await db.SaveChangesAsync(ct);
```

`await` libera a thread durante I/O; não transforme I/O em `Task.Run`. O token representa cancelamento da requisição e permite interromper operações que o suportam. As APIs de SignInManager/UserManager usadas aqui não expõem esse parâmetro. Em uma aplicação real, evite bloquear async com `.Result` ou `.Wait()`.

## Middleware

A pipeline de `Program.cs` tem tratamento de exceções, cabeçalhos, autenticação, autorização, rate limiting e proteção CSRF antes dos endpoints. A ordem importa: a identidade precisa estar disponível antes da autorização e da validação dos tokens associados à sessão. `ApiExceptionHandler` implementa `IExceptionHandler`; transforma erros conhecidos em ProblemDetails com 400/404/409 e preserva detalhes internos apenas nos logs. O traceId conecta a resposta ao log.

## Minimal APIs e Controllers

Escolhemos Minimal APIs porque os recursos desta primeira versão têm contratos explícitos, DI por parâmetro e agrupamento por rota. `MapGroup("/api/vehicles").RequireAuthorization()` aplica a política ao grupo e `MapGet`/`MapPost` descrevem a operação diretamente. As regras ficam no serviço, não no transporte. DTOs `record` representam os contratos de entrada e saída.

Controllers seriam válidos para uma equipe que já usa convenções MVC, filtros e classes por recurso. Não são necessários para demonstrar orientação a objetos ou separação de responsabilidades. A escolha não altera a necessidade de validação, autorização e testes. O OpenAPI nativo documenta os contratos de Minimal APIs.

## Autenticação e autorização

Identity fornece hash de senha, normalização de e-mail, lockout e cookies assinados/criptografados. `SignInManager.PasswordSignInAsync(..., lockoutOnFailure: true)` é usado no login. O navegador guarda o cookie HttpOnly, sem token acessível ao JavaScript. O cliente obtém o token antiforgery e o envia no cabeçalho das mutações.

Autenticar responde quem é o usuário; autorizar decide quais dados ele pode acessar. `CurrentUser` extrai o UUID de `ClaimTypes.NameIdentifier`. `GarageService.Vehicle` exige ID e UserId corretos; os query filters de contexto reforçam a restrição nos filhos. Não basta esconder um botão Vue. Os testes executam requisições com usuários diferentes e exigem 404 ao acessar dados alheios.

## Testes automatizados

`BusinessRulesTests` usa xUnit para regras puras, sem banco. Por exemplo, o mesmo serviço ligado a dois componentes soma seu custo uma vez. `[Theory]`/`[InlineData]` cobrem limites de alertas e quilometragem.

`ApiTests` usa `WebApplicationFactory<Program>` com a pipeline real, cookies e antiforgery. `ApiFixture` cria um banco PostgreSQL exclusivo com nome aleatório e aplica a migration real, verificando constraints e tradução LINQ com o provider de produção. O banco é removido ao final. Não usamos EF InMemory para provar comportamento SQL. Os testes de navegador complementam a API exercitando os formulários e a cena; não substituem os testes de autorização.

## OpenTelemetry

`AddOpenTelemetry` configura o nome do serviço, instrumentação de requisições ASP.NET Core, chamadas HttpClient e métricas do runtime. Se `OTEL_EXPORTER_OTLP_ENDPOINT` estiver presente, `UseOtlpExporter` exporta os sinais para um collector local ou remoto. Não configuramos um fornecedor pago nem exportador obrigatório. Logs JSON permanecem úteis sem collector; health checks distinguem processo vivo de banco acessível. Ao investigar uma falha, procure o traceId retornado em ProblemDetails nos logs da API.

## Escolhas idiomáticas

Valores monetários são `decimal`, identificadores são `Guid`, datas civis são `DateOnly`, instantes de criação são `DateTimeOffset` em UTC e os enums têm nomes explícitos. Nullable reference types e warnings como erros fazem parte da build. A propriedade nula de quilometragem expressa desconhecimento; zero é um valor conhecido. Propriedades calculadas não são persistidas de forma redundante. O domínio não depende de HTTP, Identity ou Three.js.

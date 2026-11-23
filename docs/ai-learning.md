# IA na Minha Garagem: interpretar, revisar e registrar

Esta entrega implementa somente **Registrar com IA**. O usuário descreve um serviço, recebe um rascunho e revisa o formulário existente. A interpretação não escreve registros, peças, pagamentos ou quilometragem. A gravação usa o endpoint normal de manutenção, após **Confirmar e salvar serviço**.

## Executar

No `.env` local, configure:

```dotenv
AI_ENABLED=true
AI_PROVIDER=Gemini
GEMINI_API_KEY=
GEMINI_MODEL=gemini-3.5-flash-lite
AI_TIMEOUT_SECONDS=30
AI_MAX_INPUT_CHARS=4000
AI_MAX_OUTPUT_TOKENS=2048
AI_REQUESTS_PER_MINUTE=3
AI_REQUESTS_PER_USER_PER_DAY=20
AI_REQUESTS_PER_DAY=100
```

Preencha sua chave somente no backend e reinicie `make dev` ou reconstrua com `make up`. Nenhuma variável de IA usa o prefixo `VITE_`. Sem chave, com `AI_ENABLED=false` ou provedor desconhecido, a aplicação permanece utilizável e informa que a IA está indisponível.

O modelo padrão, `gemini-3.5-flash-lite`, consta como estável e com entrada/saída gratuitas na documentação consultada em 23/11/2026. Disponibilidade, cotas e preços podem mudar: confira [o modelo](https://ai.google.dev/gemini-api/docs/models/gemini-3.5-flash-lite) e [os preços](https://ai.google.dev/gemini-api/docs/pricing#gemini-3.5-flash-lite). Selecionar esse nome não transforma um projeto com faturamento habilitado em projeto gratuito. A aplicação não contrata serviços, habilita faturamento ou cria contas no Google.

### User Secrets

O projeto da API tem `UserSecretsId`. Para armazenar a chave fora do repositório, em um terminal privado:

```sh
./scripts/dotnet-local.sh user-secrets set 'Gemini:ApiKey' '<sua-chave>' \
  --project src/backend/MinhaGaragem.Api
```

Não publique o comando com uma chave real nem a coloque em screenshots. Deixe `GEMINI_API_KEY` vazio no `.env` ao usar User Secrets. Uma chave não vazia no ambiente tem precedência. Os nomes estruturados `AI:Enabled`, `AI:Provider` e `Gemini:Model` também são aceitos. User Secrets é um recurso local de desenvolvimento; no Render, configure as variáveis privadas do serviço da API. A pasta de segredos pode variar conforme `DOTNET_CLI_HOME`; use o mesmo wrapper/ambiente ao configurar e iniciar a aplicação.

## 1. O que é um LLM

Um LLM gera texto a partir de padrões aprendidos. Ele pode reconhecer que “troquei os cabos e as velas” se refere à ignição, mas essa capacidade não o torna uma fonte confiável de datas, preços ou fatos ausentes. Aqui ele organiza um relato fornecido pelo usuário; não diagnostica o carro nem recomenda intervenções mecânicas.

A separação é concreta: [MaintenanceInterpreter](../src/backend/MinhaGaragem.Application/AI/MaintenanceInterpreter.cs) só consulta o veículo autorizado e o catálogo. Não chama `SaveRecord`, `SaveChangesAsync`, SQL ou ferramentas. A API de interpretação retorna um DTO transitório.

## 2. Como a chamada chega ao Gemini

O fluxo é:

```text
Histórico → Registrar com IA → POST /api/ai/maintenance/interpret
  → autenticação, CSRF e limite por usuário
  → autorização do veículo e catálogo existente
  → MaintenanceInterpreter → IChatClient → GeminiChatClient → Google.GenAI
  → validação da resposta → rascunho editável
  → confirmação humana → endpoint normal de manutenção
```

O SDK oficial `Google.GenAI` 2.2.0 já inclui `AsIChatClient`. [MaintenanceAiRegistration](../src/backend/MinhaGaragem.Infrastructure/AI/MaintenanceAiRegistration.cs) compõe o cliente por DI. Não há um cliente HTTP improvisado, adaptador comunitário obrigatório ou SDK do Google dentro do serviço de aplicação. A integração usa `Microsoft.Extensions.AI` 10.11.0, compatível com o alvo .NET 10 do projeto. Veja o [adaptador oficial](https://googleapis.github.io/dotnet-genai/api/Microsoft.Extensions.AI.GoogleGenAIExtensions.html).

## 3. Tokens e limites

Tokens são unidades de texto processadas pelo modelo; não correspondem exatamente a palavras ou caracteres. O consumo inclui instruções, catálogo, relato e resposta. O sistema limita o relato a 4.000 caracteres por padrão e solicita no máximo 2.048 tokens de saída. O catálogo também tem tamanho limitado pelas entidades atuais.

Os limites são configuráveis, com tetos no código: até 8.000 caracteres e 4.096 tokens. O timeout padrão é 30 segundos, com máximo de 60. Duas interpretações podem estar em andamento por processo; novas solicitações excedentes são recusadas, sem fila ilimitada.

## 4. Prompt de sistema versionado

[MaintenancePromptV1](../src/backend/MinhaGaragem.Application/AI/Prompts/MaintenancePromptV1.cs) define a função de interpretação, os dados que podem ser extraídos e a proibição de inventar valores ou executar ações. A versão `maintenance-v1` acompanha o rascunho e os eventos de falha.

O prompt é enviado em `ChatRole.System`. Contexto do veículo e relato são serializados como dados JSON em outra mensagem. Instruções dentro do relato não recebem autoridade administrativa. Não existe ferramenta que permita ao modelo consultar segredos ou modificar o banco.

Um prompt não elimina sozinho prompt injection ou alucinação. A proteção também depende do escopo enxuto, validação no servidor, ausência de ferramentas e confirmação humana. O texto gerado é exibido como texto, sem executar HTML ou comandos.

## 5. Structured output

Uma resposta em prosa exigiria interpretar novamente frases como “o custo parece ser...”. Structured output solicita um objeto JSON com campos definidos em [MaintenanceExtraction](../src/backend/MinhaGaragem.Application/AI/MaintenanceAiContracts.cs).

Campos desconhecidos ficam `null`, inclusive data, total e quantidade. “Paguei R$ 850” não autoriza dividir o gasto entre velas, cabos e mão de obra. O rascunho usa a modalidade já existente de **valor total fechado**. Se o usuário escolher valores detalhados, preços individuais e mão de obra não informados também permanecem em branco, exigindo preenchimento antes de salvar.

## 6. JSON Schema e validação

`ChatResponseFormat.ForJsonSchema<MaintenanceExtraction>()` gera o schema a partir do tipo C#. O adaptador oficial traduz essa solicitação para a configuração JSON do Gemini. Isso melhora o formato da resposta, mas não prova que seus fatos estão corretos. [Documentação do Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.chatresponseformat.forjsonschema).

[MaintenanceDraftValidator](../src/backend/MinhaGaragem.Application/AI/MaintenanceDraftValidator.cs) faz uma segunda verificação com `System.Text.Json`: exige os campos, rejeita propriedades extras, enum desconhecido, JSON incompleto, custos inválidos, data futura, quilometragem incompatível e chaves fora do catálogo.

O modelo retorna chaves técnicas, como `engine` e `spark-plugs`, nunca UUIDs. O servidor resolve os IDs reais e verifica o componente pai. Quantidades, preços e quilometragem precisam de trechos literais com os números correspondentes; oficinas, nomes de peças e observações também devem vir do relato. Data exige dia, mês e ano explícitos. Datas relativas, ano omitido ou quantidades não sustentadas ficam pendentes para revisão.

Essas verificações reduzem invenções, mas não comprovam a semântica de todo resumo ou aviso. O usuário deve conferir a sugestão. Regras de dinheiro, data e hodômetro também reutilizam `Rules.Record`, sem persistir os valores provisórios usados nessa validação.

## 7. O contrato IChatClient

`IChatClient` descreve como enviar mensagens, receber uma resposta e acessar metadados de uso. O serviço conhece `ChatMessage`, `ChatOptions` e `ChatResponse`; não conhece `Google.GenAI.Client`.

A aplicação pede schema, modelo e limite de tokens. [GeminiChatClient](../src/backend/MinhaGaragem.Infrastructure/AI/GeminiChatClient.cs) traduz erros do fornecedor e desliga a continuação automática de respostas. O SDK também usa `HttpRetryOptions.Attempts=1`: cada interpretação faz uma tentativa, sem retentativas ou continuações faturáveis ocultas.

## 8. Por que desacoplar o provedor

A fronteira permite trocar a implementação de `IChatClient` e testar sem uma conta externa. Não significa que todos os provedores terão idêntico suporte a schema, limites ou erros. Um novo provedor deve comprovar esses contratos antes de ser habilitado.

Só Gemini está implementado. Não foram criados roteadores, agentes, armazenamento vetorial ou interfaces para fases futuras sem uso nesta entrega.

## 9. Testar sem consumir tokens

```sh
make ai-test       # somente testes com Trait Category=AI
make test          # suíte completa, PostgreSQL temporário
make format
make format-check
make lint
make build
```

[FakeChatClient](../tests/TestSupport/FakeChatClient.cs) existe somente nos projetos de teste. Ele permite devolver JSON inválido, interromper por cancelamento e simular quota ou indisponibilidade. Os testes verificam autorização, associação real dos componentes, ausência de gravação ao interpretar e gravação somente após o POST normal confirmado.

[GeminiAdapterTests](../tests/MinhaGaragem.IntegrationTests/GeminiAdapterTests.cs) exercita o SDK oficial contra um servidor HTTP local, conferindo schema e limites sem acessar o Google. Isso valida o transporte e a adaptação, mas **não equivale a uma chamada real ao Gemini**. A interface de produção não usa respostas simuladas.

No fechamento da Fase 1, em 23/11/2026, os 130 testes de backend, o lint e o build passaram. A tentativa de validação real ficou limitada por um HTTP 504 do provedor, reproduzido também com uma pergunta mínima fora do fluxo de manutenção. Portanto, essa verificação não comprova uma inferência real bem-sucedida. Disponibilidade e cotas do Gemini precisam ser conferidas na instalação em uso.

## 10. Prompt engineering, tool calling e RAG

- **Prompt engineering:** instruções, contexto e formato para esta extração. É o que Registrar com IA utiliza.
- **Tool calling:** o modelo solicita uma função do backend, que precisaria autorizar cada consulta. O [assistente da Fase 2](ai-assistant.md) implementa consultas de leitura com execução controlada.
- **RAG:** busca de trechos de documentos para compor contexto e citar referências. Não há ingestão de manual ou banco vetorial nesta fase.

Leitura de notas e manual inteligente continuam fora do escopo. O assistente de histórico está documentado na [Fase 2](ai-assistant.md). Anexos existentes não são enviados à IA.

## 11. Privacidade, observabilidade e custos

São enviados apenas o relato digitado, fabricante, modelo, ano/versão cadastrados e nomes/chaves do catálogo. O contexto montado pela aplicação não inclui UUIDs, placa, apelido, e-mail da conta, histórico de serviços, fotos, contatos ou comprovantes. O texto livre pode conter dados pessoais: a interface informa o envio ao Google e pede que esses dados sejam omitidos.

A camada gratuita do Gemini pode usar conteúdo para melhoria dos produtos, conforme [as condições do fornecedor](https://ai.google.dev/gemini-api/docs/pricing). Revise essas condições antes de enviar informações confidenciais.

O OpenTelemetry existente recebe a fonte e o meter `MinhaGaragem.AI`. Métricas: `garage.ai.requests`, `garage.ai.failures`, `garage.ai.duration`, `garage.ai.provider.duration`, `garage.ai.tokens.input` e `garage.ai.tokens.output`. Tokens são registrados somente quando informados pelo provedor. Logs de falha guardam motivo e versão do prompt; não registram relato, resposta bruta, exceção original do SDK ou chave.

Os limites padrão são três solicitações por minuto por usuário, vinte por dia por usuário e cem por dia por processo. Falhas após iniciar uma chamada também contam no orçamento diário. Contadores diários usam UTC e memória: reiniciar a API os reinicia; várias réplicas têm contadores próprios. Eles não substituem um limite de gastos durável no projeto Google. Use cotas do provedor e acompanhe o uso no AI Studio; não dependa apenas das métricas da aplicação para faturamento.

A chave fica no serviço da API. Não publique `.env`, User Secrets, `.local/vscode.env` ou logs. O verificador `scripts/check-public.mjs` também procura a chave local nos arquivos versionáveis.

## 12. Depurar no VS Code

Use o compound existente **Minha Garagem — Full Stack Debug**. `debug-prepare` mantém as variáveis de IA em `.local/vscode.env`, privado e ignorado pelo Git, e o SDK é composto pelo mesmo registro de DI da execução normal.

Coloque breakpoints em:

1. [MaintenanceAiEndpoints.cs](../src/backend/MinhaGaragem.Api/MaintenanceAiEndpoints.cs), antes de `Interpret`.
2. [MaintenanceInterpreter.cs](../src/backend/MinhaGaragem.Application/AI/MaintenanceInterpreter.cs), no envio a `IChatClient`.
3. [MaintenanceDraftValidator.cs](../src/backend/MinhaGaragem.Application/AI/MaintenanceDraftValidator.cs), na validação e resolução das chaves.
4. [GeminiChatClient.cs](../src/backend/MinhaGaragem.Infrastructure/AI/GeminiChatClient.cs), para observar erro/timeout do provedor.

Na aba Network, observe `/api/ai/maintenance/interpret` e, somente ao confirmar, o POST normal de manutenção. Não compartilhe capturas de Variables/Watch contendo credenciais ou conteúdo pessoal. Para testar disponibilidade sem consumir tokens, consulte o endpoint autenticado `GET /api/ai/maintenance/status`.

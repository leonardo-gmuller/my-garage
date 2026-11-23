# Desenvolvimento e debug

## Pré-requisitos

Use .NET SDK 10, Node.js 20.19+ (ou 22+), npm, Docker Engine/Desktop com Compose e GNU Make. O Makefile é compatível com o Make 3.81 fornecido no macOS e com GNU Make no Linux. Docker precisa estar iniciado; nenhum comando instala ou altera o Docker do sistema.

- macOS: as Command Line Tools (`xcode-select --install`) incluem Make; alternativamente, instale GNU Make pelo Homebrew.
- Ubuntu/Debian: `sudo apt install make`. Instale .NET 10 e Node pela distribuição oficial escolhida e configure acesso ao Docker.
- Windows: use WSL2 com Ubuntu, `sudo apt install make`, Docker Desktop com integração WSL e VS Code com a extensão WSL. Abra o repositório dentro do WSL (`code .`) e instale C#/Vue no ambiente remoto. Os scripts utilizam Bash e semântica de processos POSIX; não foi implementado suporte a Make/PowerShell nativo.

O wrapper `scripts/dotnet-local.sh` usa `.tools/dotnet` quando existe, ou o SDK no PATH. Ele mantém os caches em `.local`. Para a extensão C# encontrar um SDK instalado apenas nesta pasta, abra o VS Code a partir de um terminal com `export PATH="$PWD/.tools/dotnet:$PATH"`; em outras máquinas, prefira instalar o SDK 10 normalmente. O debug recebe o `DOTNET_ROOT` correto pelo ambiente gerado.

## Primeiro uso

```bash
make setup
# Edite .env e defina BOOTSTRAP_EMAIL e BOOTSTRAP_PASSWORD.
make dev
```

`setup` cria `.env` a partir do exemplo quando necessário, preenche uma senha aleatória do PostgreSQL se estiver vazia e instala as dependências .NET/npm. Um `.env` existente com senha é preservado. Defina também o e-mail e a senha de acesso (`BOOTSTRAP_EMAIL` e `BOOTSTRAP_PASSWORD`) antes de iniciar a API. O primeiro usuário só é criado em banco sem usuários; não há cadastro público ou redefinição de senha ao reiniciar. A senha não é impressa. A instalação inicial precisa de acesso a NuGet/npm; as demais execuções reutilizam dependências até que o manifesto/lockfile mude ou os artefatos sejam limpos.

`dev` inicia PostgreSQL no Docker, aguarda prontidão, aplica migrations e executa **API e Vite em paralelo no host**, com hot reload. Mantenha o terminal aberto. Ctrl+C encerra os processos de host pertencentes à execução e preserva banco e volumes. A alternativa completa fica em `make up`.

| Comando | Uso |
| --- | --- |
| `make help` | Lista todos os targets |
| `make setup` | Prepara `.env` e dependências |
| `make up` | Constrói e inicia API, banco e frontend no Docker |
| `make down` | Para os projetos Docker completo e de desenvolvimento, sem `-v` |
| `make dev` | API/Vite no host e PostgreSQL no Docker |
| `make dev-api` | Somente API com `dotnet watch`, preparando o banco |
| `make dev-front` | Somente Vite, com reutilização segura quando já está pronto |
| `make build` | Build .NET e Vue; Release por padrão |
| `make test` | xUnit e integração em PostgreSQL temporário |
| `make lint` | Formatação C#, TypeScript e estrutura das configurações de debug |
| `make migrate` | Aplica migrations no banco de desenvolvimento |
| `make logs` | Logs Docker; prefere o projeto dev quando está em execução |
| `make clean` | Limpa bin/obj/dist e relatórios; preserva dados e dependências |
| `make test-e2e` | Playwright com API/Vite já em execução |

`dev-api` e `dev-front` podem ser usados em dois terminais. O frontend usa proxy `/api`, `/health` e `/openapi`: o navegador acessa uma única origem, sem precisar abrir CORS ou expor tokens.

## Portas e variáveis

| Variável | Padrão | Finalidade |
| --- | --- | --- |
| `API_PORT` | 5081 | API no host |
| `VITE_PORT` | 5175 | Frontend com hot reload |
| `DB_PORT` | 55432 | PostgreSQL dev publicado apenas em 127.0.0.1 |
| `FRONTEND_PORT` | 8080 | Frontend da stack Docker completa |
| `COMPOSE_PROJECT_NAME` | my-garage | Projeto da stack completa |
| `DEV_COMPOSE_PROJECT_NAME` | my-garage-dev | Projeto do banco de desenvolvimento |
| `BOOTSTRAP_EMAIL`, `BOOTSTRAP_PASSWORD` | `.env` | Acesso inicial em banco vazio |
| `GARAGE_DISABLE_LOCAL_MODELS` | false | Forçar picape demonstrativa, mesmo com GLB local |
| `CONFIGURATION` | Release | Build, teste e migration pelo Make |
| `API_PROJECT`, `INFRA_PROJECT`, `FRONTEND_DIR` | Caminhos atuais | Caminhos usados pelo executor local |

As portas são configuradas no Makefile, no ambiente ou pela linha de comando. O `.env` guarda as credenciais do PostgreSQL:

```bash
make dev API_PORT=5090 VITE_PORT=5175 DB_PORT=55433
make build CONFIGURATION=Debug
```

O F5 sempre compila a API em **Debug**. `API_PORT` é carregada automaticamente no ambiente do debugger e no proxy Vite. Ao alterar `VITE_PORT` para o F5, ajuste também as URLs das duas configurações Vue em `.vscode/launch.json`; a preparação rejeita uma URL divergente em vez de abrir o navegador no endereço errado.

## Bancos e arquivos preservados

`compose.dev.yaml` complementa `compose.yaml` com publicação da porta e um volume exclusivo. O projeto Docker de desenvolvimento usa `postgres_dev_data`; a stack completa usa `postgres_data`. Assim, testar no host não mistura o histórico da aplicação Docker com anexos de outro diretório.

No host, uploads ficam em `.local/dev-uploads` e chaves em `.local/dev-keys`. No Docker completo, usam os volumes `uploads` e `data_protection`. `.env`, SDK local, caches e ambiente de debug estão ignorados pelo Git. `make clean` não apaga esses diretórios, modelos GLB/JSON ou volumes. `make down` não chama `docker compose down -v`. Ctrl+C no host não para automaticamente o banco Docker; use `make down` depois de encerrar as sessões.

## F5 no VS Code

1. Abra a raiz do projeto e instale as extensões recomendadas em `.vscode/extensions.json`: C# oficial Microsoft, .NET Install Tool, Vue — Official, Container Tools e EditorConfig. O depurador JavaScript/TypeScript é integrado ao VS Code; Chrome precisa estar instalado.
2. Abra **Run and Debug**.
3. Selecione **Minha Garagem — Full Stack Debug** e pressione F5.

A tarefa inicial verifica a porta da API, inicia o PostgreSQL se necessário, compila a API em Debug, aplica migrations e grava `.local/vscode.env` com permissão privada. O arquivo contém a conexão local e o caminho do runtime; não há senha em `launch.json`, `tasks.json` ou `launchSettings.json`.

O debugger C# inicia a DLL em `src/backend/MinhaGaragem.Api/bin/Debug/net10.0/MinhaGaragem.Api.dll`, com cwd no projeto da API e ambiente Development. Ele não executa `dotnet watch` ao mesmo tempo. A configuração Vue inicia ou reutiliza o Vite deste repositório, aguarda `/health/ready` da API e então abre o Chrome. A tarefa background libera o debugger somente após a linha `FRONTEND_READY`, conferida por HTTP. Encerrar o compound para as duas sessões e limpa o Vite que o debug iniciou; um Vite reutilizado de `make dev-front` é preservado.

Não execute `make dev` e F5 da API simultaneamente na mesma porta. A preparação detecta a instância existente e aborta, sem matar o processo. Para trocar de modo, encerre primeiro a execução no terminal ou o debugger.

Também há configurações individuais para **API (.NET)** e **Vue (Chrome)**. A configuração Vue individual inicia o Vite; para usar os dados, mantenha a API aberta em outro terminal (`make dev-api`) ou use o compound. As duas configurações internas do compound ficam ocultas no seletor.

## Breakpoints em C# e endpoints

Abra `src/backend/MinhaGaragem.Api/GarageEndpoints.cs` e coloque um breakpoint em uma lambda de endpoint, por exemplo na chamada `s.UpdateMileage`. Abra `MinhaGaragem.Application/GarageService.cs` e marque `UpdateMileage` ou `SaveRecord`. Com F5 ativo, atualize a quilometragem ou salve um serviço no navegador.

Quando a execução parar, use Variables/Watch para inspecionar DTOs, entidades e o usuário atual. F10 executa a próxima linha; F11 entra no método; Shift+F11 retorna; F5 continua. Call Stack mostra o caminho até o endpoint. O projeto usa Minimal APIs, portanto não há Controllers artificiais para esta configuração.

Para testar a conectividade sem login, consulte `http://127.0.0.1:5081/health/ready`. Rotas de negócio exigem cookie Identity e mutações exigem antiforgery; o cliente Vue já envia ambos. Um 401 anônimo não indica falha no debugger.

## Breakpoints em Vue/TypeScript

Abra `src/frontend/src/stores/garage.ts`, `services/api.ts` ou o bloco `<script setup>` de `components/RecordDialog.vue`. Marque o breakpoint e acione a ação correspondente no navegador lançado pelo VS Code. A configuração usa `pwa-chrome`, webRoot do frontend e source maps, permitindo trabalhar sobre `.vue` e `.ts`.

O Vite continua com HMR. O build também gera `.map` e CSS usa `devSourcemap`. O depurador mostra estado reativo/Pinia nas variáveis e o fetch em `api.ts`; o painel Network do navegador permite conferir códigos e corpos HTTP. Instalar Vue Devtools é opcional e não é requisito para os breakpoints.

Se um breakpoint estiver cinza, confira se abriu a raiz correta, se o módulo já foi carregado (as views são lazy), se está no Chrome iniciado pelo debugger e se o servidor é o Vite deste projeto. Limpe sessões antigas e reinicie F5 após trocar build/configuração.

## Exceções e logs

A API usa logs JSON no terminal ou Debug Console. O tratamento centralizado retorna ProblemDetails com traceId. Consulte esse traceId no log para investigar a falha sem habilitar logs de senha ou parâmetros sensíveis.

No painel Breakpoints do VS Code, habilite pausa em exceções .NET não tratadas. Pausar também em exceções lançadas é útil para investigar `DomainException` e `DbUpdateException`, mas pode interromper validações esperadas antes de o middleware tratá-las. O Call Stack e a janela de exceções mostram o contexto. Na sessão Chrome, use pausa em exceções JavaScript para inspecionar falhas no cliente.

Em desenvolvimento, API e Vite mostram seus logs nos terminais do host. `make logs` acompanha containers; nesse modo ele mostra o PostgreSQL. Na stack completa, acompanha os serviços do Docker.

## Migrations

```bash
make migrate
# Para criar uma nova migration (não apenas aplicá-la):
./scripts/dotnet-local.sh ef migrations add NomeDaMudanca \
  --project src/backend/MinhaGaragem.Infrastructure \
  --startup-project src/backend/MinhaGaragem.Api
make migrate
```

O EF gera migrations UTF-8 com BOM, preservadas pela regra correspondente do `.editorconfig`. Não edite uma migration já aplicada para mudar o schema: crie outra. O banco da stack completa aplica migrations quando a API Docker inicia.

## Diferenças entre os comandos .NET

- `dotnet build`: compila a solução e produz DLLs/PDBs; não inicia a API.
- `dotnet run`: compila quando necessário e inicia uma vez; não acompanha mudanças de arquivos.
- `dotnet watch`: observa arquivos e aplica hot reload ou reinicia a aplicação quando necessário. É o modo dos targets de terminal; não é usado para iniciar a API dentro do debugger.
- `dotnet test`: compila/executa os testes. As integrações deste projeto criam e removem um banco `garage_test_<uuid>` no servidor de desenvolvimento; o usuário precisa de CREATEDB. `make test` fornece a conexão sem imprimi-la.

## Solução de problemas

**Porta ocupada:** use `lsof -nP -iTCP:5081 -sTCP:LISTEN` no macOS/Linux, ou `ss -ltnp` no Linux. Encerre apenas a instância que você reconhece ou escolha outra porta. Vite usa `strictPort`, sem migrar silenciosamente para uma URL diferente. As URLs utilizam 127.0.0.1, evitando ambiguidade entre IPv4 e localhost/IPv6.

**Docker não responde:** inicie Docker Desktop/Engine e execute `docker info`. Nenhum script tenta instalar ou abrir o Docker por conta própria.

**Senha do banco não confere:** variáveis `POSTGRES_*` inicializam um volume novo; alterar `.env` não troca a senha de um volume existente. Restaure a configuração correspondente ou altere a senha pelo PostgreSQL. Não remova volumes para resolver isso sem decidir explicitamente descartar os dados.

**API não fica pronta no compound:** consulte o terminal da tarefa e Debug Console. O frontend espera `/health/ready`; confirme que a API iniciou e que o PostgreSQL está acessível. A migração usa o mesmo ambiente privado da API.

**SDK 10 não localizado:** confira `dotnet --list-sdks`, `global.json` e `./scripts/dotnet-local.sh --version`. Use SDK 10 no PATH para o serviço de linguagem; o wrapper e o arquivo de debug gerado resolvem o SDK privado deste checkout.

## Inteligência artificial e formatação

A IA é opcional e usa credenciais apenas do backend. Configuração, User Secrets, testes fake e breakpoints estão em [ai-learning.md](ai-learning.md). Use `make ai-test` para testes focados, `make format` para aplicar CSharpier e `make format-check` para conferir. `make lint` também exige CSharpier.

## Testes de navegador

Use uma instalação isolada, com credenciais de demonstração e sem dados pessoais. Inicie a aplicação, instale o Chromium do Playwright e execute:

```sh
cd src/frontend
npx playwright install chromium
cd ../..
make test-e2e
```

Por padrão, o login de teste usa `BOOTSTRAP_EMAIL` e `BOOTSTRAP_PASSWORD` do `.env`; `GARAGE_E2E_EMAIL` e `GARAGE_E2E_PASSWORD` permitem outro acesso configurado. `make test-e2e` usa a porta `VITE_PORT`. Os cenários criam veículos próprios, removem esses veículos e arquivam somente os contatos que criaram. Não executam limpeza geral da conta.

A distribuição pública não inclui o GLB da Saveiro. Os cenários específicos desse asset são pulados quando ele não existe; os fluxos de garagem, formulário, pagamentos e o teste de GLTFLoader com GLB original mínimo continuam disponíveis. Para conferir a distribuição mesmo tendo os arquivos locais, inicie API/Vite e Playwright com `GARAGE_DISABLE_LOCAL_MODELS=true`.

## Limites conhecidos

- A configuração documentada é local, sem implantação pública ou cadastro aberto.
- O script de bootstrap não é um mecanismo de redefinição de senha.
- O build pode avisar sobre o tamanho do chunk Three.js. O visualizador limita DPR, pausa fora de tela e renderiza sob demanda; o custo inicial da biblioteca permanece.
- Os custos são agregados em memória por veículo. Para históricos muito grandes, a evolução prevista é usar projeções SQL.
- O debug usa configurações oficiais do VS Code, mas exige Chrome, extensões e SDK compatíveis instalados na máquina.

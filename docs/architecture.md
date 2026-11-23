# Arquitetura e decisões

## Monólito modular pragmático

As quatro camadas mantêm dependências em uma direção: Domain não conhece infraestrutura; Application usa Domain e os contratos EF de consulta; Infrastructure implementa o contexto e Identity; Api é o ponto de composição. Veículos, manutenção, agenda e finanças são funcionalidades internas do mesmo processo e transação. Não há microserviços, mediador, CQRS ou repositório genérico. `IGarageDb` descreve os conjuntos concretos e SaveChanges; permite usar LINQ/EF nos casos de uso sem depender do contexto Identity.

`GarageService` organiza os casos de uso desta versão. `FinancialSummary`, `Rules`, `ComponentCatalog` e os métodos de entidade isolam regras puras. Os DTOs evitam serializar grafos EF e propriedades de Identity. As rotas são agrupadas por recurso em `GarageEndpoints`. Crescimento de escopo permite dividir o serviço por funcionalidade sem trocar o contrato HTTP.

## Propriedade e integridade

A identidade vem do cookie assinado pelo ASP.NET Core Identity. Não há IDs de usuário aceitos nos DTOs de negócio. Todas as operações primeiro resolvem o veículo da conta autenticada. Query filters EF protegem também tabelas filhas. Recursos de outra conta retornam 404. Componentes possuem chaves alternativas compostas `(Id, VehicleId)` e FKs compostas nos vínculos de serviço/agendamento: o próprio banco impede vincular componentes de outro veículo.

UUIDs identificam os registros. Índices cobrem proprietário/criação, veículo/data, veículo/componente e prazos. Constraints rejeitam custos e hodômetros negativos e agendas sem meta. A alteração do hodômetro e seu histórico são persistidos no mesmo SaveChanges transacional. O veículo tem um token de concorrência trocado a cada modificação; atualizações simultâneas de quilometragem não sobrescrevem silenciosamente umas às outras (409 exige recarga).

## Financeiro

Um serviço contém mão de obra e uma lista de peças (`Quantity × UnitCost`). Componentes são apenas vínculos, não entidades financeiras. `PartsCost` e `TotalCost` são calculados, não duplicados no banco. Indicadores percorrem registros uma única vez por consulta e nunca somam joins por componente. Valores persistidos usam `numeric(16,2)` e C# `decimal`, com validação de centavos. A primeira versão materializa os serviços do veículo para agregação clara e testável; para históricos muito grandes, mover agregações para projeções SQL é o ponto de evolução.

## Segurança e arquivos

Identity gerencia hash de senha, cookies HttpOnly/SameSite Strict, bloqueio por tentativas e logout. Não há tokens em localStorage. Antiforgery valida TODAS as mutações `/api`, inclusive login e upload; o uso de `DisableAntiforgery` nos endpoints multipart desliga apenas a validação de metadados padrão, pois a verificação é feita pelo middleware comum antes do endpoint. Auth limita tentativas por IP (15/minuto, configurável por `Auth:PermitLimit`; testes usam limite maior). Sem CORS aberto: Vite/nginx oferecem a mesma origem.

Arquivos nunca são publicados como diretório estático. São identificados por UUID e acessados após autorização do veículo/serviço. O nome original é só metadado; o caminho de armazenamento é gerado no servidor. Tipo real é verificado por assinatura, com allowlist de PNG/JPEG/WebP/PDF; SVG/HTML e arquivos acima de 8 MB são rejeitados. Os downloads de anexos usam Content-Disposition; fotos usam tipo de imagem e nosniff. Se persistência falhar após upload, o arquivo é removido. Falhas de remoção após exclusão do banco podem deixar um arquivo órfão que precisa de limpeza operacional; não expõem o arquivo sem o registro autorizável.

A criação administrativa do primeiro usuário e de sua Saveiro é transacional. Não há rota pública de cadastro. O bootstrap lê as credenciais do ambiente somente quando o banco está vazio, sem redefinir contas existentes. Chaves Data Protection e arquivos têm volumes separados do banco. Logs JSON não habilitam conteúdo sensível de SQL. Dados de senha não são registrados. O tratamento centralizado usa ProblemDetails e traceId, sem retornar stack traces.

## Frontend e 3D

Pinia guarda sessão em memória e o veículo atual. O cliente fetch usa cookies de mesma origem e cabeçalho antiforgery. Vue Router divide telas em módulos carregados sob demanda. PrimeVue fornece os controles de formulário, diálogos, mensagens e tema; Tailwind 4 integra a build e CSS de produto define a composição visual. Sora e DM Sans são servidas localmente. Transition do Vue anima mudanças de rota e @vueuse/motion anima a entrada de cards, respeitando movimento reduzido.

A cena importa apenas um catálogo de componentes e o mapeamento visual. Não calcula finanças ou regras de prazo. A seleção externa oferece teclado e alternativa a WebGL. O Three.js é liberado ao sair da tela; ResizeObserver ajusta câmera e canvas, IntersectionObserver pausa render fora da área visível, pixel ratio é limitado a 2. Na distribuição pública, a cena usa a picape original criada por código. Quando um GLB autorizado e seu mapeamento estão disponíveis localmente, o GLTFLoader carrega a Saveiro; o JSON define os prefixos das malhas e um adaptador os relaciona às chaves estáveis de componentes. Raycasting usa a superfície visível mais próxima. A seleção de uma porta destaca só seu grupo e abre Carroceria / Portas no painel técnico. Cópias dos materiais preservam mapas, PBR e transparência; a troca de seleção restaura as mesmas referências originais. Box3 normaliza escala/origem e ajusta câmera à tela. Pontos auxiliares, inicialmente ocultos, atendem peças internas ou não identificadas. A renderização acontece quando a cena muda; sombras estáticas não são recalculadas em cada frame. No celular o painel fica abaixo da cena. Sem GSAP porque este produto não precisa de uma biblioteca adicional para as transições discretas implementadas.

## Observabilidade

`/health/live` testa o processo; `/health/ready` consulta PostgreSQL. Logs estruturados incluem falhas e identificador da requisição. OpenTelemetry instrumenta ASP.NET Core, HttpClient e medidores runtime; com `OTEL_EXPORTER_OTLP_ENDPOINT` configurado, exporta para um collector compatível. Sem endpoint configurado, a API não depende de um collector externo. Nenhum backend comercial obrigatório.

## Componentes e pintura

Conjuntos possuem VehicleSubcomponent por veículo. Registros guardam o componente principal, vínculos aos conjuntos e vínculos opcionais às peças, sem duplicar custos. FKs compostas exigem que cada subcomponente pertença ao conjunto envolvido e ao veículo correto. Agendamentos podem apontar a uma peça técnica. O resumo usa todos os serviços para o custo acumulado e mostra somente os cinco recentes. ColorHex e PaintFinish persistem junto de Color no veículo; um endpoint de pintura preserva seus demais campos. O frontend aplica clones PBR apenas aos slots elegíveis por grupo/material, reutilizando-os a cada prévia. Consulte [a documentação detalhada](component-details-and-paint.md).

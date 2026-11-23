# Agenda de mecânicos

Cadastro privado de contatos, sem login próprio para os profissionais. A agenda pertence à conta e pode ser usada em todos os seus veículos, inclusive antes de cadastrar um veículo.

## Fluxo

A página **Mecânicos** permite cadastrar, buscar, editar, arquivar e restaurar contatos. Apenas o nome é obrigatório. Oficina, especialidade, telefone/WhatsApp, e-mail, endereço e observações são opcionais. Telefones e e-mails exibidos nos cards abrem o discador/compositor somente quando o usuário escolhe o link.

No serviço, o Select PrimeVue **Mecânico responsável** é opcional. Os contatos ativos podem ser selecionados, e a oficina vazia é preenchida com a oficina do contato. O campo livre de oficina continua disponível. **Cadastrar mecânico sem sair do serviço** abre um formulário próprio, mantém o preenchimento da manutenção e seleciona o novo contato ao salvar.

A agenda apresenta a quantidade e a data do último serviço por contato, sem duplicar registros com vários componentes. **Ver serviços** reúne os vínculos de todos os veículos da conta, com paginação. O histórico do veículo, a lista de serviços e o painel do componente mostram o nome registrado.

Arquivar tira o contato das opções para novos serviços e preserva seus vínculos. O mecânico arquivado de um serviço existente continua visível e pode ser mantido ao editar esse serviço. Restaurar disponibiliza o contato novamente. `DELETE /api/mechanics/{id}` arquiva; não há exclusão física pela interface.

## Dados e proteção

`Mechanic` guarda o proprietário (`UserId`) e os dados do contato. As consultas usam filtros de propriedade e as rotas exigem autenticação. Escritas mantêm a proteção CSRF existente. O vínculo de manutenção rejeita IDs de outras contas e contatos arquivados em novas associações.

`MaintenanceRecord.MechanicId` é opcional. `MechanicName` registra uma cópia do nome ao associar o contato; `Workshop` mantém a oficina registrada no serviço. Alterações no contato não reescrevem esse histórico. Editar um serviço sem trocar o vínculo mantém seu nome/oficina antigos. A FK usa SET NULL caso o contato seja removido diretamente por administração; o nome registrado permanece no serviço.

A migration `20261119183638_MechanicsDirectory` cria a tabela e acrescenta duas colunas nulas nos registros existentes, sem inferir contatos a partir do campo livre de oficina. O endpoint de serviço continua aceitando os contratos anteriores, sem `mechanicId`.

## API

- `GET /api/mechanics?page=1&pageSize=20&search=texto&status=active`: busca e paginação; situações `active`, `archived`, `all`.
- `POST /api/mechanics`: novo contato.
- `GET /api/mechanics/{id}` e `PUT /api/mechanics/{id}`: consulta e edição.
- `DELETE /api/mechanics/{id}`: arquivamento.
- `POST /api/mechanics/{id}/restore`: restauração.
- `GET /api/mechanics/{id}/records?page=1&pageSize=10`: serviços privados vinculados ao contato em todos os veículos.
- `RecordInput` recebe `mechanicId` opcional; `RecordDto` acrescenta `mechanicId` e `mechanicName`.

## Interface

Vue 3, PrimeVue e os tokens Smoked Glass existentes. Cards e formulários responsivos, com links/ações identificados, mensagens de erro, estados vazios, navegação por teclado e transições que respeitam movimento reduzido. A rota de contatos é marcada como independente do veículo, evitando desmontar formulários durante a seleção inicial do carro. A sincronização entre cadastro rápido, registro de serviço e agenda usa uma revisão Pinia sem armazenar contatos entre sessões.

## Verificação

Testes .NET cobrem validação, privacidade/CSRF, vínculos entre contas, agenda sem veículo, busca/paginação, arquivar/restaurar, edição de serviços com contato arquivado, preservação do nome/oficina e custo único por serviço em vários veículos. O teste de upgrade mantém serviços e oficinas anteriores sem inventar contatos.

O cenário E2E em desktop/mobile cobre cadastro/edição persistidos, seleção no serviço, oficina preenchida, histórico preservado, arquivamento/restauração, edição do vínculo arquivado, cadastro rápido sem perder o formulário, busca e manutenção da agenda após excluir o próprio veículo de teste.

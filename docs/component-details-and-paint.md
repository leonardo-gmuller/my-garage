# Componentes detalhados e pintura

## Registrar serviços reais

Na Garagem 3D, selecione um conjunto pelo modelo ou pela lista. O detalhamento técnico apresenta os subcomponentes disponíveis mesmo que não exista uma malha interna. Suspensão inclui amortecedores, molas, batentes, bandejas, pivôs e buchas; Motor / ignição inclui velas, cabos de vela, bateria, filtros, correias e sistema de arrefecimento. Os demais conjuntos também possuem opções técnicas.

**Registrar neste componente** preenche o conjunto e, quando selecionado, o subcomponente. O formulário permite escolher o componente principal, outros conjuntos envolvidos e vários subcomponentes. Remover um conjunto limpa as escolhas de suas peças. Categoria, descrição, data, peças, valores e observações continuam editáveis.

As sugestões opcionais **Troca de amortecedores** e **Troca de cabos e velas** preenchem descrição, categoria, tipo e vínculos. Elas não criam registros nem preenchem preços, oficina, quilometragem ou histórico fictício. Informe os dados reais e salve. O catálogo é técnico; não presume versão do motor, código de peça ou compatibilidade de um produto comercial específico.

O painel mostra custo acumulado de todos os serviços vinculados, contagem, último serviço com peças/observações, últimos cinco registros e acesso ao histórico completo filtrado. Selecionar uma peça técnica filtra o painel por subcomponente. Portas e capô selecionados no GLB abrem os respectivos subcomponentes de Carroceria. O cadastro não distingue posições de roda ou lado de porta.

A próxima manutenção usa um agendamento real aberto do conjunto ou da peça. **Programar para este componente** permite informar data e/ou quilometragem. Sem agendamento, o painel informa a ausência de previsão e permite cadastrá-la. Não são inventados intervalos de troca sem conhecer a versão e o plano de manutenção do veículo.

## Modelagem e API

- `VehicleComponent`: conjunto do veículo, preservando os dez componentes anteriores.
- `VehicleSubcomponent`: peça técnica com chave única dentro do componente e vínculo ao mesmo veículo.
- `MaintenanceRecord.PrimaryComponentId`: componente principal, sempre um dos conjuntos envolvidos nas novas gravações.
- `MaintenanceRecordComponent`: conjuntos envolvidos no serviço.
- `MaintenanceRecordSubcomponent`: subcomponentes envolvidos; FKs compostas exigem o mesmo veículo e um vínculo do serviço com seu componente pai.
- `MaintenanceItem`: peça substituída, quantidade e valor unitário ligados ao serviço.
- `MaintenanceSchedule.VehicleSubcomponentId`: alvo opcional de um agendamento do conjunto.

Peças e mão de obra pertencem ao registro, nunca ao vínculo. Um serviço com velas e cabos tem um único total. Ele aparece em ambos os históricos, mas entra uma vez nos indicadores. Totais de diferentes componentes ou subcomponentes podem compartilhar o mesmo serviço e não devem ser somados como despesas independentes.

| Operação | Contrato |
| --- | --- |
| Catálogo | `GET /api/vehicles/{id}/components` retorna `subcomponents` em cada conjunto |
| Gravar serviço | `POST /PUT .../records` recebe `componentIds`, `primaryComponentId` e `subcomponentIds` opcional |
| Histórico | `GET .../records?component={uuid}&subcomponent={uuid}`; filtros também disponíveis em `timeline` |
| Resumo do componente | `GET .../components/{componentId}/summary?subcomponent={uuid}` |
| Agendamento | `POST /PUT .../schedules` aceita `subcomponentId` opcional |
| Pintura | `PUT /api/vehicles/{id}/paint` recebe `color`, `colorHex` e `paintFinish` |

As rotas exigem autenticação e antiforgery para mutações. IDs de outro veículo, peças de um sistema não selecionado e IDs duplicados são rejeitados. A API anterior continua aceitando registros apenas com componentes principais.

## Cor persistida e materiais

A Saveiro 2013 inicial usa **Prata**, `#C0C0C0`, acabamento `Metallic`. O veículo armazena `Color`, `ColorHex` e `PaintFinish` (Solid, Metallic, Pearlescent ou Matte). Cor hexadecimal exige `#RRGGBB`. O endpoint específico de pintura não altera hodômetro ou demais campos.

**Personalizar cor** abre campos PrimeVue de nome, ColorPicker/hex e acabamento. A prévia altera imediatamente o modelo; **Salvar cor** persiste a escolha. **Restaurar cor salva** descarta a prévia. O dashboard e uma nova abertura da garagem usam a cor persistida. Os acabamentos são aproximações PBR, sem simulação de partículas metálicas ou interferência óptica perolizada.

Na integração opcional do GLB local, `src/frontend/src/features/three/paintMapping.json` limita a pintura ao material `Diego` nos grupos `chassis`, `bonnet_ok`, `door_rf_01`, `boot_ok`, `bump_fro01` e `bump_rea01`: 29 malhas do arquivo atual. O nome do material isolado não basta, pois ele também é usado no interior. Não há busca genérica por superfícies brancas nem aplicação no GLB inteiro.

`VehiclePaint` cria um `MeshPhysicalMaterial` compartilhado entre os slots pintáveis, preservando os atributos Standard (texturas, lado e transparência). Atualizações reutilizam o clone; geometrias e texturas não são recriadas. Prata metálica usa metalness 0,78, roughness 0,30, envMapIntensity 1,1, clearcoat 0,55 e clearcoatRoughness 0,20. A camada independente `SaveiroSurfaces` ajusta plásticos, rodas, pneus, vidros, iluminação e rack por grupo/material e exceções explícitas de lentes; essas peças não recebem a cor personalizada. O destaque de seleção restaura a aparência atual ao sair da seleção. As camadas são liberadas em ordem inversa e os materiais importados permanecem intactos. Detalhes: [saveiro-materials.md](saveiro-materials.md).

## Atualização de bancos existentes

`ComponentDetailsAndVehiclePaint` acrescenta o catálogo técnico por veículo, preserva registros e seus valores e preenche o componente principal dos serviços anteriores a partir de um vínculo existente. Ela não tenta deduzir subcomponentes pela descrição: serviços antigos continuam no histórico do conjunto e podem ser editados para receber detalhes.

A cor prata é preenchida apenas em Volkswagen Saveiro 2013 sem cor informada ou já marcada como prata. Uma cor diferente conhecida é preservada. Não há remoção de dados nem criação automática dos serviços de exemplo. Use `make migrate` no desenvolvimento; a stack Docker aplica a migration ao iniciar a API.

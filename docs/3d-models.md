# Visualização 3D e modelo opcional

## Distribuição pública

O repositório funciona com `features/three/genericPickup.ts`, uma picape demonstrativa construída pelo código do projeto. Não exige baixar um modelo de terceiros e mantém câmera orbital, seleção de componentes, personalização de cor e painel técnico.

O GLB específico da Saveiro e seu JSON fornecido não são redistribuídos porque sua licença não foi confirmada. Não estão no repositório ou nas capturas públicas. A licença MIT do código não concede direitos sobre modelos adicionados localmente.

## Integração local opcional

Se você possui autorização para usar o asset, coloque ambos os arquivos em `src/frontend/public/models/`:

- `Saveiro_3D.glb`
- `Saveiro_3D_grupos.json`

Reinicie o Vite ou reconstrua o frontend. A configuração verifica a presença dos dois arquivos na inicialização da build; quando ausentes, usa diretamente a picape demonstrativa. `GARAGE_DISABLE_LOCAL_MODELS=true` força essa versão mesmo com arquivos locais. Se um asset habilitado falhar no carregamento, aparece um aviso e a cena usa a picape demonstrativa.

As seções seguintes descrevem o adaptador do GLB local. O asset usado durante o desenvolvimento tinha 649 malhas, 61.775 triângulos e 22 materiais; modelos diferentes exigem revisar o mapeamento.

## Como o mapeamento funciona

`features/three/modelConfig.ts` configura os caminhos do GLB/JSON e o adaptador de geometria para negócio. O JSON é carregado e validado antes de habilitar a seleção. Prefixos mais longos têm preferência para evitar colisões. O prefixo de cada grupo vem do JSON, sem depender de UUIDs de manutenção.

| Grupo do JSON | Componente de manutenção | Seleção |
| --- | --- | --- |
| `wheel_1` | Pneus e rodas | Malhas de rodas |
| `suspensao` | Suspensão | Malhas da suspensão |
| `farol`, `lanterna` | Faróis | Malhas de iluminação |
| `door_rf_01`, `bonnet_ok`, `chassis`, `boot_ok`, para-choques, racks, frisos e acabamento | Carroceria | Grupo clicado, com rótulo específico |
| `cofre` | Motor | Região do cofre; não identifica peças internas do motor |
| Sem malha identificada de bateria, freios, arrefecimento, transmissão e escapamento | Respectivo componente | Lista acessível e pontos auxiliares opcionais |

Não associamos a malha de uma roda aos freios nem o capô ao motor. Os grupos de painel, bancos e pedais permanecem com seus materiais e sem associação mecânica presumida. O componente de negócio continua o mesmo ao selecionar uma peça física: uma porta abre Carroceria / Portas e destaca apenas seu grupo. O capô abre Carroceria / Capô. O histórico pode ser filtrado pelo conjunto ou subcomponente técnico; não se armazena a posição específica de cada roda/porta.

## Interações

- Arrastar gira a câmera com OrbitControls; gesto de pinça/roda e botões controlam o zoom.
- O clique/toque lança um Raycaster. A superfície visível mais próxima deve ter um componente mapeado; não atravessamos uma porta para selecionar algo atrás dela.
- Hover com mouse identifica a peça. A lista funciona por teclado e toque e permite consultar componentes internos.
- Pontos auxiliares começam ocultos. O controle com ícone de mira mostra/oculta os pontos. Rodas, suspensão, faróis e carroceria não recebem hotspots no modelo da Saveiro.
- A seleção usa clones dos materiais, mantendo texturas, transparência, roughness e metalness. Ao trocar a seleção, o material da pintura atual é restaurado, ou o original nas partes sem pintura. O helper `MaterialSelection` gerencia e libera os clones.
- O painel lateral consulta a API por UUID do componente e subcomponente técnico opcional. Registrar neste componente abre o formulário com a seleção preenchida; o registro é persistido no PostgreSQL e o histórico é atualizado.

## Câmera, iluminação e desempenho

`Box3` mede o modelo real, centraliza X/Z, alinha o chão a Y=0 e normaliza o maior eixo para 4,7 unidades. O arquivo fornecido usa Y para cima e frente em +Z. O enquadramento considera os limites, FOV e aspecto da tela; restaurar câmera reaplica esse enquadramento. ResizeObserver recalcula ao mudar o tamanho da cena.

A iluminação usa hemisphere/directional lights, sombras estáticas e ambiente PMREM de 128 px gerado localmente com quatro softboxes de estúdio, sem HDR externo e sem reescrever materiais originais. A renderização é invalidada por câmera, seleção, carregamento ou resize; a cena parada não redesenha todos os meshes a cada frame. O pixel ratio é limitado a 2; a cena fora de tela pausa. Geometrias, materiais, texturas, controles, observers e ambiente são liberados na desmontagem.

## Trocar ou corrigir o asset

1. Preserve um arquivo GLB/GLTF utilizável localmente e os créditos/licença correspondentes ao asset usado. O projeto não presume que o arquivo fornecido seja um modelo oficial Volkswagen.
2. Substitua os caminhos `saveiroModel.url` e `mappingUrl` em `modelConfig.ts` se necessário. Para GLTF, inclua buffers e texturas relativos; prefira GLB sem Draco/KTX2 nesta versão.
3. Ajuste os grupos/prefixos no JSON de acordo com as malhas reais e o adaptador `componentGroups` para a categoria correta de manutenção.
4. Ajuste as coordenadas dos pontos auxiliares no espaço original do asset. Elas são transformadas pela raiz normalizada automaticamente. Não crie pontos para componentes que já têm uma superfície selecionável útil.
5. Para orientação diferente, ajuste a raiz no carregamento; o enquadramento de tamanho já é automático. Outros modelos de veículo mantêm a picape genérica, evitando representar todos os carros como Saveiro.
6. Execute TypeScript/build e os testes `e2e/saveiro.spec.ts`; valide também o toque, a restauração da câmera e a seleção em tela estreita.

Se o GLB ou JSON falhar, o visualizador informa o erro e usa a picape genérica original de `genericPickup.ts`. WebGL indisponível mantém a lista e o painel utilizáveis. O modelo não altera dados cadastrais, não cria serviços automaticamente e não presume quilometragem ou versão.

## Pintura e detalhes técnicos

A Saveiro inicial é prata metálica persistida. `paintMapping.json` combina grupos externos e nome de material para pintar 29 malhas sem alterar os materiais importados. Os clones da pintura são separados dos clones do destaque e da camada de acabamentos; vidros, pneus, rodas, iluminação, acabamentos e interior não recebem a cor personalizada. O acabamento por peça e o ambiente de estúdio estão descritos em [saveiro-materials.md](saveiro-materials.md). A lista técnica permite registrar peças internas sem inventar geometria. Estratégia, contratos e atualização de dados: [component-details-and-paint.md](component-details-and-paint.md).

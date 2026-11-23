# Minha Garagem · Dark Glass Premium

A identidade atual usa vidro fumê, carvão e prata, com cobre discreto nas ações. A composição mantém a estrutura funcional: Saveiro à esquerda, indicadores e cuidados à direita, despesas e histórico abaixo. A Saveiro ocupa sete das doze colunas no desktop e recebe o maior título; os três indicadores financeiros compartilham uma única placa de vidro, com divisórias leves. Textos ficam alinhados à esquerda. Descrições acessíveis dos gráficos incluem peças, mão de obra e total, além da legenda visual.

## Paleta e tipografia

| Papel | Cor |
| --- | --- |
| Carvão ambiente | `#080809` |
| Vidro neutro | `rgba(29, 29, 29, .46)` |
| Vidro elevado | `rgba(36, 35, 33, .74)` |
| Prata | `#CFCBC4` |
| Branco suave | `#F0EDE7` |
| Cobre | `#C7966A` |

Sora Variable define títulos e números, com pesos 400/500 e espaçamento editorial. DM Sans Variable mantém legibilidade em textos e formulários. Valores usam números tabulares. As fontes são locais. O cobre identifica ações e seleção; erros mantêm uma cor semântica própria. Não há azul/ciano decorativo nem efeitos neon. A cor de pintura escolhida para o veículo continua sendo um dado do usuário, independente da paleta da interface.

## Tokens reutilizáveis

A fonte de cores é `src/frontend/src/styles/tokens.css`:

| Material/estado | Token |
| --- | --- |
| Glass surface | `--glass-surface` |
| Elevated glass | `--elevated-glass` |
| Glass border/reflexo | `--glass-border`, `--glass-reflection` |
| Glass hover | `--glass-hover`, `--glass-hover-border` |
| Glass active | `--glass-active` |
| Ambient background | `--ambient-background`, `--ambient-softbox` |
| Accent color | `--accent-color`, `--accent-hover` |
| Fallbacks | `--glass-fallback`, `--elevated-fallback` |
| Intensidade óptica | `--glass-blur`, `--elevated-blur` |

`theme/primeTheme.ts` aponta PrimeVue para essas mesmas variáveis, inclusive nos portais de Select, DatePicker, Dialog e Drawer. Os aliases Tailwind são definidos por `@theme inline`. `theme/sceneTheme.ts` permite ao Three.js ler os tokens de iluminação/seleção. O CSS de estrutura permanece em `style.css`; material, hierarquia e estados ficam em `styles/glass.css`.

## Vidro e profundidade

Os painéis possuem preenchimento RGBA plano, sem imagem de fundo ou gradiente de preenchimento. O blur amostra realmente o ambiente atrás da superfície. A iluminação ambiental e os reflexos diagonais pertencem ao fundo, permitindo perceber sua continuidade através dos cards. Bordas de 1 px, reflexo superior e sombra suave definem a espessura.

Sem suporte a `backdrop-filter`/`-webkit-backdrop-filter`, o estilo base usa uma superfície opaca legível. Com suporte, `@supports` habilita a transparência e o blur reais. Preferências de alto contraste ou transparência reduzida também usam o fallback. O comportamento normal mantém Glassmorphism no desktop e no celular.

## Vitrine 3D e desempenho

A cena utiliza luzes de estúdio neutras, ambiente PMREM local e piso transparente que recebe apenas sombras. Foram retirados a grade azul e o disco de piso visível. A seleção usa cobre e mantém os clones/restauração dos materiais, sem alterar o GLB ou a pintura persistida.

O blur padrão é 14 px, 8 px nas superfícies móveis e na área maior do veículo. Overlays usam 20 px no desktop e 12 px no celular. Os indicadores compartilham um blur; seus três filhos não filtram novamente. Não se aplica blur ao canvas, ao modelo ou aos controles sobre a cena. Não há animação de blur, fundo ou luz. A cena continua renderizando somente quando invalidada e pausa fora de vista.

## Interação e acessibilidade

Hovers iluminam discretamente a borda, sem mover os cards. Botões têm estados normal, hover, pressionado, foco e desabilitado. O foco usa contorno visível. Transições usam tokens de duração; `prefers-reduced-motion` interrompe efeitos e mantém os cards visíveis. Alvos de ações e checkboxes possuem área de toque ampliada; os campos de peças reorganizam-se em telas pequenas.

Os tokens visuais são independentes dos contratos de negócio, seleção 3D e persistência.

## Marca aplicada

A identidade usa a logo ilustrada aprovada: Saveiro prata, garagem grafite e estrada cobre. `BrandLogo.vue` reutiliza o PNG original no cabeçalho, autenticação, carregamento e rodapé. O nome recebe a cor do tema por máscara alpha para leitura no vidro fumê, preservando o desenho das letras. A composição se adapta ao mobile e os favicons usam uma redução própria. Arquivos e aplicações: [brand/README.md](brand/README.md).

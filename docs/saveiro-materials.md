# Acabamento da Saveiro prata

Este documento descreve a integração opcional do GLB local, que não é redistribuído. A versão pública usa a picape original gerada por código.

A cor persistida continua **Prata / #C0C0C0 / Metallic**. O GLB fornecido permanece inalterado. O visual é construído em três camadas reversíveis: acabamentos por peça → pintura personalizada → destaque de seleção.

## Superfícies

`src/frontend/src/features/three/saveiroSurfaces.json` centraliza as cores, propriedades PBR e regras por grupo/material do modelo. A classificação não depende da luminosidade do material e não pinta o interior por compartilhar o material `Diego` com a carroceria.

| Superfície | Tratamento |
| --- | --- |
| Carroceria: 29 malhas externas `Diego` | MeshPhysicalMaterial, prata configurável, metalness 0,78, roughness 0,30, verniz 0,55 / roughness 0,20 |
| Para-choques inferiores, caixas de roda, frisos e grade | Chumbo #202225, pouca reflexão metálica, rugosidade 0,68 |
| Pneus | Borracha #141517, metalness 0, rugosidade 0,88 |
| Rodas | Liga #85898e, metalness 0,82, rugosidade 0,29 |
| Vidros | Fumê #171b1e, reflexão suave, opacidade 0,78, sem depthWrite |
| Rack / santo antônio | Metal escuro #53585d, metalness 0,72, rugosidade 0,34 |
| Faróis | Refletores metálicos e lentes translúcidas separados; indicadores âmbar |
| Lanternas | Lentes vermelhas #a90916 e elementos transparentes separados |

O grupo `windscre01` inclui também coberturas dos faróis e carcaças das lanternas. O JSON declara exceções por nome exato dessas malhas para evitar escurecer os faróis como se fossem vidros. Nenhuma malha é removida ou substituída.

## Estúdio e custo

`studioEnvironment.ts` gera uma única PMREM de 128 px com quatro softboxes e espaços escuros para dar profundidade às reflexões. Esse ambiente não aparece como geometria no visualizador. A iluminação direta usa uma luz principal com sombra de 1024 px, preenchimento, recorte e hemisfério discreto; exposição 0,95. As cores do estúdio vêm dos tokens CSS existentes e de `--scene-environment`.

Não há refração/transmission, pós-processamento, novos arquivos HDR/texturas nem luzes animadas. Os materiais são compartilhados por origem/perfil; a pintura reutiliza o mesmo material ao mudar a cor. O visualizador preserva a renderização sob demanda, pausa fora da tela, limite de DPR e controle por OrbitControls. A seleção restaura a pintura e os acabamentos correntes. Na desmontagem, as camadas restauram e liberam apenas seus clones antes de liberar os recursos originais.

## Verificação

O teste em `component-details.spec.ts` carrega o GLB real e verifica as 29 malhas pintáveis, plásticos, rack, pneus, rodas, vidros, faróis, lanternas e exceções de lentes. Confere ainda preservação do interior e dos materiais originais, compartilhamento, troca de cor, seleção e restauração de todas as camadas. A suíte existente cobre desktop/mobile, raycasting, persistência de cor e manutenção.

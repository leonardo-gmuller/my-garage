# Manutenção com valor total fechado

Uma visita à oficina pode reunir várias trocas e verificações em um único registro. Quando o proprietário conhece apenas o total cobrado, escolhe **Valor total fechado** e informa, por exemplo, **R$ 1.000**. Pode selecionar motor e suspensão, marcar amortecedores, cabos e velas e listar peças e quantidades sem informar seus preços individuais.

Novos registros começam nessa modalidade. **Valores detalhados** continua disponível para preencher preços de peças e mão de obra; registros anteriores abrem com a modalidade que já usavam. A modalidade pode ser alterada ao editar. Os valores ocultos não são enviados junto com o total fechado.

## Cálculo e histórico

- Um registro tem um único total, mesmo quando possui vários componentes/subcomponentes. Os totais financeiros não somam vínculos como gastos independentes.
- `MaintenanceRecord.PackageTotal` é opcional. Quando presente, define o total da visita; quando ausente, continua valendo a soma de peças e mão de obra.
- A API rejeita valores fechados negativos, acima do limite, com precisão inválida ou acompanhados de custos detalhados conflitantes.
- Na modalidade fechada, o contrato de leitura retorna `null` para custos de peças e mão de obra e para preços dos itens. A interface mostra **Incluído no total**, sem apresentar preço desconhecido como uma peça gratuita. Quantidades e nomes permanecem registrados.
- Os campos internos de preços detalhados ficam zerados nesse modo; a presença de `PackageTotal` distingue explicitamente ausência de detalhamento de preços detalhados iguais a zero. Valor fechado zero também é aceito, sem ser confundido com modalidade detalhada.
- O histórico, o painel do componente e os serviços do mecânico preservam os vínculos existentes. O painel identifica o custo como **Total dos serviços vinculados**, sem atribuir um preço individual ao componente.

## Relatórios

O financeiro e os gráficos mensais acrescentam **Sem detalhamento** (`Unallocated`). Com o suporte posterior a pagamentos, a identidade de valores é `Total = Parts + Labor + Unallocated + Interest`. A parcela sem detalhamento inclui valores totais fechados e não estima uma divisão desconhecida; juros ficam separados conforme [as regras de pagamentos](maintenance-payments.md). Totais anuais, categorias e indicadores incluem cada registro uma única vez. Categoria corresponde à categoria escolhida para a visita, sem repartir automaticamente o valor entre os componentes.

O histórico e o financeiro acompanham a revisão dos dados da garagem para atualizar imediatamente após salvar pelo botão global do cabeçalho.

## Banco e compatibilidade

A migration `20261119220812_PackageMaintenanceTotal` acrescenta uma coluna nullable e uma restrição de valores ao registro. Não converte custos antigos nem altera itens, oficinas ou mecânicos existentes. Clientes que omitem `packageTotal` continuam usando o cálculo detalhado anterior.

## Verificação

Os testes unitários e de integração cobrem cálculo misto, preços desconhecidos, limites, conflitos, custo único com múltiplos componentes, persistência, troca de modalidade e upgrade de dados antigos. O cenário `package-total.spec.ts` cobre o exemplo de R$ 1.000 pelo formulário, persistência após recarregar, preços incluídos, gráfico financeiro e detalhamento posterior no mesmo registro, em desktop e celular.

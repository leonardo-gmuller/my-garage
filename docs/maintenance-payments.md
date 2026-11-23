# Pagamentos, parcelas e juros da manutenção

Uma visita pode ter preços detalhados e cobranças separadas: peças pagas à loja e mão de obra paga ao mecânico. O formulário permite registrar um pagamento de cada tipo, com forma de pagamento, quantidade opcional de parcelas e total da cobrança com juros. Também é possível informar somente o acréscimo em reais; o formulário calcula o total da cobrança a partir do valor original.

Na modalidade de valor fechado, há um pagamento único associado ao total, mantendo os preços individuais desconhecidos.

## Valores conhecidos e pendentes

O preço das peças e o valor da mão de obra permanecem separados dos juros. `TotalCost = BaseCost + InterestCost`. `BaseCost` é a soma dos itens e da mão de obra, ou o valor fechado. Cada pagamento com total final informado acrescenta apenas `TotalCharged - Principal` ao custo. O valor principal já faz parte da manutenção e não é contado novamente.

Um pagamento com `TotalCharged = null` representa juros ainda não informados. A interface mostra **Total conhecido** e um aviso de pendência; não transforma desconhecimento em juros zero. Quantidade de parcelas também pode permanecer desconhecida. Informar um total igual ao principal registra explicitamente ausência de acréscimo.

Histórico, painel do componente, serviços do mecânico e indicadores mostram esses dados. O financeiro e o gráfico mensal acrescentam uma parcela de **Juros e acréscimos**. A identidade dos valores é `Total = Parts + Labor + Unallocated + Interest`.

As parcelas são metadados da cobrança. A despesa comprometida é contada uma única vez, no mês da manutenção; não há lançamento de parcelas futuras ou inferência de fatura/cartão. Não são calculadas taxas mensais ou anuais a partir de valores incompletos.

## Persistência e compatibilidade

A migration `20261119223704_MaintenancePayments` cria a tabela `Payments`, vinculada ao registro de manutenção e com índice único por registro/escopo. Os escopos são `Parts`, `Labor` e `Package`; formas de pagamento são `CreditCard`, `DebitCard`, `Pix`, `Cash` e `Other`.

Os pagamentos usam o filtro de proprietário do veículo. A API mantém autenticação/CSRF e não permite movimentar uma cobrança por um ID fornecido pelo cliente. Valores negativos, total final abaixo do principal, escopos duplicados ou incompatíveis com a modalidade e quantidades de parcelas fora de 1–120 são rejeitados.

A ausência do campo `payments` ao editar preserva cobranças existentes, para clientes anteriores. Uma lista explícita vazia remove as cobranças. Alterar custos exige que os pagamentos continuem válidos para o novo principal. Ao trocar a modalidade de preço, cobranças incompatíveis continuam visíveis e precisam ser removidas ou a modalidade revertida; seus dados não são descartados silenciosamente.

## Exemplo fictício de orçamento incompleto

Uma revisão contém R$ 1.380 em peças conhecidas e R$ 450 de mão de obra. A mão de obra foi cobrada em três parcelas, totalizando R$ 495: o acréscimo confirmado é R$ 45.

O orçamento das peças ainda não está completo. A cobrança de R$ 1.518 fica nas observações e o pagamento permanece com total final pendente, evitando transformar peças ainda não listadas em juros. O sistema apresenta R$ 1.875 como total conhecido e indica a pendência.

Esse exemplo é gerado pelo [seed fictício](demo-seed.md). Os testes cobrem cálculos, pendências, validação, privacidade, atualização e preservação de registros anteriores.

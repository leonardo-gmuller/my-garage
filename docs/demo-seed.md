# Demonstração com dados fictícios

O seed é explícito e opcional. Ele não roda na inicialização e não importa dados pessoais ou notas reais.

## Executar

1. Configure `.env` com `BOOTSTRAP_EMAIL=demo@minhagaragem.local` e sua própria `BOOTSTRAP_PASSWORD`.
2. Inicie uma instalação local com banco vazio para que o bootstrap crie esse acesso.
3. Execute `make seed` e entre com as mesmas credenciais.

Para apontar ao Vite, use `make seed SEED_BASE_URL=http://127.0.0.1:5175`. Somente endereços loopback e contas exclusivas de demonstração (`demo@minhagaragem.local` ou `demo+nome@minhagaragem.local`) são aceitos. Não há criação de conta por endpoint público.

## O que aparece

- A Saveiro inicial passa a ter o apelido **Saveiro · Demo**.
- Um contato fictício de mecânico, sem telefone, e-mail ou endereço real.
- Uma revisão com suspensão e motor, subcomponentes, sete itens e observações marcadas como fictícias.
- R$ 1.380 em peças conhecidas e R$ 450 de mão de obra.
- Pagamento das peças em seis parcelas, com juros pendentes por orçamento incompleto.
- Mão de obra em três parcelas, total de R$ 495 e acréscimo confirmado de R$ 45.
- Total conhecido de R$ 1.875 e um lembrete para completar o orçamento em sete dias.

Os valores estão em `scripts/seeds/garage-demo.json`. A data do serviço é a data da primeira execução, sem quilometragem ou compatibilidade comercial de peças presumidas.

## Reexecução e preservação

O script usa a API autenticada e guarda apenas identificadores e o estado da demonstração em `.local/seeds`, ignorado pelo Git. Não grava a senha nesse estado. A combinação de origem, conta e chave do fixture separa cada demonstração.

Reexecutar não duplica os objetos e preserva edições feitas na interface. O seed não limpa a garagem nem altera contas pessoais. Para experimentar do zero, crie outra instalação com banco e volumes separados; não remova volumes com dados importantes.

# Contribuir

Obrigado pelo interesse na Minha Garagem. Abra uma issue descrevendo o problema ou a proposta antes de mudanças grandes. Correções pequenas podem seguir diretamente por pull request.

## Preparar e validar

1. Siga o [guia de desenvolvimento](docs/development.md) e configure seu `.env` local.
2. Crie uma branch para a mudança.
3. Execute `make lint`, `make test`, `make build` e `node scripts/check-public.mjs`.
4. Para alterações de interface, confira desktop, celular, teclado e movimento reduzido. Execute os cenários Playwright afetados em uma instalação isolada.
5. Descreva o comportamento alterado e a validação no pull request.

Preserve a direção de dependências entre as camadas do backend e os tokens visuais do frontend. Mudanças de banco devem incluir uma nova migration; nunca reescreva uma migration já aplicada. Novas regras devem ter testes de domínio ou integração apropriados.

Não envie `.env`, credenciais, comprovantes, dados reais de veículos ou contatos, bancos, logs e assets sem licença de redistribuição. Utilize dados fictícios nos testes e capturas. O GLB opcional da Saveiro não faz parte da distribuição.

As contribuições de código são recebidas sob a licença MIT do projeto.

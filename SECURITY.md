# Segurança

Se encontrar uma vulnerabilidade, não publique credenciais, dados pessoais ou instruções de exploração em uma issue pública. Use o canal privado **Report a vulnerability** na aba Security do GitHub, quando habilitado. Se a opção estiver indisponível, abra uma issue solicitando um canal privado, sem detalhes sensíveis.

Inclua o commit afetado, ambiente, comportamento observado, impacto e um exemplo mínimo usando exclusivamente dados fictícios.

## Configuração local

- Configure suas próprias senhas em `.env`. O arquivo é ignorado pelo Git; `.env.example` não contém senhas prontas.
- O usuário inicial é criado somente se o banco não tiver usuários. Alterar a variável de bootstrap não redefine senhas existentes.
- O Compose publica o frontend apenas no loopback. Esta configuração documenta execução local; uma implantação pública exige configuração própria de HTTPS, cookies, proxy, backups e acesso ao banco.
- Arquivos anexados ficam em armazenamento privado e exigem autorização para leitura.
- Não use uma instalação com dados pessoais para testes E2E ou demonstrações públicas.

O projeto é mantido como portfólio, sem prazo contratual para correções. Correções de segurança são aplicadas à versão principal.

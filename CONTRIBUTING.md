# Como contribuir

## Fluxo de trabalho

1. Trabalhe em uma mudança pequena e revisável.
2. Preserve a direção de dependências descrita em `docs/architecture.md`.
3. Não inclua credenciais, bancos, backups ou dados corporativos reais.
4. Atualize testes e documentação junto com o código.
5. Execute a suíte de validação antes de propor um commit.

```powershell
.\V.cmd
.\V.cmd -Configuration Release
```

Para coletar cobertura no mesmo fluxo, execute `.\V.cmd -Coverage`. Cobertura é
um sinal de apoio: regras e fluxos críticos devem ser revisados mesmo quando o
percentual global subir.

Mudanças destinadas a publicação também devem passar pela auditoria local:

```powershell
.\scripts\audit-portfolio.cmd
```

Não use screenshots, seeds ou fixtures para reproduzir dados reais. O gerador de
portfólio deve continuar restrito a Development, loopback e token efêmero.

## Commits

Use mensagens curtas em português, com prefixos como `chore:`, `feat:`,
`fix:`, `test:`, `docs:` ou `refactor:`.

Exemplo:

```text
chore: inicializa solução e padrões do repositório
```

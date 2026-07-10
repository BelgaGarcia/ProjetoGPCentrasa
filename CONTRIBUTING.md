# Como contribuir

## Fluxo de trabalho

1. Trabalhe em uma mudança pequena e revisável.
2. Preserve a direção de dependências descrita em `docs/architecture.md`.
3. Não inclua credenciais, bancos, backups ou dados corporativos reais.
4. Atualize testes e documentação junto com o código.
5. Execute a suíte de validação antes de propor um commit.

```powershell
dotnet restore CentraSA.sln
dotnet build CentraSA.sln --no-restore
dotnet test CentraSA.sln --no-build
dotnet format CentraSA.sln --verify-no-changes
```

## Commits

Use mensagens curtas em português, com prefixos como `chore:`, `feat:`,
`fix:`, `test:`, `docs:` ou `refactor:`.

Exemplo:

```text
chore: inicializa solução e padrões do repositório
```

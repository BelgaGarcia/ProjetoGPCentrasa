# Arquitetura

## Estilo

A Central CentraSA será um monólito modular. A separação em projetos protege as
regras de negócio sem introduzir processos distribuídos ou padrões que não
tragam benefício para uma aplicação local de um usuário.

```text
CentraSA.Web ─────────────► CentraSA.Application
      │                              │
      └───────────────► CentraSA.Infrastructure
                                     │
CentraSA.Application ───────────────► CentraSA.Domain
CentraSA.Infrastructure ────────────► CentraSA.Application + CentraSA.Domain
```

## Responsabilidades

- **Domain:** entidades, enums, invariantes e regras puras.
- **Application:** casos de uso, consultas, DTOs e contratos de infraestrutura.
- **Infrastructure:** EF Core, SQLite, Identity, migrations, seed e backup.
- **Web:** controllers, Razor Views, view models e composição de dependências.

Os testes unitários referenciam Domain e Application. Os testes de integração
referenciam Web e Infrastructure para validar a composição completa.

## Restrições

- Controllers e views não recebem regras de negócio.
- Entidades de persistência não são usadas como view models.
- Não usar CQRS completo, mensageria, MediatR, AutoMapper ou repositório
  genérico sem uma nova decisão arquitetural documentada.
- Escrita do item e de seu histórico deve ocorrer na mesma transação.
- O provedor SQLite deve permanecer isolado em Infrastructure.

## Persistência e autenticação

- `CentraSaDbContext` herda de `IdentityUserContext<ApplicationUser, Guid>` para
  manter Identity e dados funcionais na mesma unidade transacional local.
- O SQLite fica fora do repositório, sob `%LOCALAPPDATA%\CentraSA`.
- Migrations são aplicadas no início; os seeders são idempotentes.
- Catálogos estruturais são sempre garantidos. Dados demonstrativos só entram
  em Development, quando não existe dado operacional.
- Identity Core fornece hash, cookies, lockout e tokens sem cadastro público,
  roles ou provedores externos.
- Toda rota exige autenticação por fallback policy, salvo ações explicitamente
  anônimas de login, configuração inicial e erro.

## Interface

- `_Layout.cshtml` escolhe automaticamente entre o shell autenticado e a
  experiência de primeiro acesso/login.
- `_PresentationLayout.cshtml` remove a navegação administrativa e oferece tela
  cheia e impressão sem dependência de gerador de PDF.
- O design system usa arquivos separados para tokens, base, layout,
  componentes, páginas e impressão.
- A navegação móvel é uma melhoria progressiva em JavaScript; as rotas e
  formulários continuam funcionais sem scripts.
- Os ícones são um sprite SVG local e não dependem de fontes ou CDN.

## Decisões registradas

- [ADR 0001 — Monólito modular](decisions/0001-modular-monolith.md)
- [ADR 0002 — .NET 8](decisions/0002-dotnet-8.md)
- [ADR 0003 — SQLite e Identity locais](decisions/0003-local-persistence-and-identity.md)
- [ADR 0004 — Design system local](decisions/0004-local-design-system.md)

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
- **Infrastructure:** EF Core, SQLite, Identity, migrations e seed.
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
- A recuperação desta versão é operacional: com a aplicação encerrada, o
  arquivo SQLite pode ser copiado ou reposto integralmente. Não existe serviço
  de importação, exportação ou retenção automática.
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

## Módulo de pendências

- `PendingTaskService` concentra validações, transições, ordenação e gravação
  do histórico na mesma unidade de trabalho.
- `IPendingTaskRepository` é específico do agregado e sua implementação EF
  projeta as listagens com filtros e paginação.
- O controller traduz HTTP e mensagens; view models com Data Annotations
  cuidam da entrada, sem expor entidades EF aos formulários.
- As regras de atraso e da janela inclusiva de sete dias são puras e usam
  `TimeProvider`, permitindo testes determinísticos.
- A atualização parcial dos filtros é uma melhoria progressiva sobre formulários
  GET; o modo de apresentação usa um layout Razor independente.

## Módulo de SMUDs

- `SmudService` concentra normalização do código, validações, transições de
  status, concorrência e histórico na mesma unidade de trabalho.
- O quadro é agrupado diretamente pelo `StatusDefinitionId` persistido; não há
  uma coluna paralela ou derivada, mantendo o status do banco como fonte única.
- Busca, área, pessoa, status, prazo e ação necessária são aplicados antes do
  agrupamento. O total do quadro é sempre a soma dos cartões das colunas.
- Códigos são normalizados para o formato `SMUDnnn` e protegidos contra
  duplicidade tanto pelo serviço quanto pelo índice único do SQLite.
- O módulo oferece CRUD com arquivamento lógico, detalhe com histórico e modo
  de apresentação em tela cheia.

## Módulo de chamados

- `SupportTicketService` mantém número normalizado, validações, histórico,
  concorrência e arquivamento na mesma unidade operacional.
- Categoria e equipe são referências independentes. O quadro agrupa por
  categoria, mas ambos os filtros são aplicados diretamente ao chamado.
- Cada item é projetado como a relação cartão–responsável–prazo. O CSS usa grid
  e pseudo-elementos no fluxo, sem coordenadas absolutas frágeis.
- Em telas estreitas, os cinco elementos da relação passam para uma coluna e os
  conectores mudam de orientação sem alterar o HTML.
- O índice único do SQLite complementa a verificação do serviço e protege o
  número do chamado inclusive em condições de concorrência.

## Módulo de reunião diária

- `DailyMeetingService` monta sugestões dos três módulos, aplica uma prioridade
  determinística e rejeita origens duplicadas independentemente da seção.
- `DailyMeeting` é o agregado da sessão. Seus itens guardam seção, ordem, notas,
  progresso e snapshots de título, status, prazo e responsável.
- Ações rápidas validam a versão da reunião. Concluir uma origem atualiza o item
  operacional, o histórico e a sessão na mesma unidade de trabalho do SQLite.
- Reuniões finalizadas são somente leitura; o snapshot continua consultável
  mesmo que a origem tenha mudado depois da preparação.
- O modo de apresentação usa grid responsivo e impressão do navegador, sem
  coordenadas absolutas nem serviço externo de PDF.

## Dashboard, histórico e lookups

- `InsightRepository` executa projeções `AsNoTracking` dos três módulos. Apenas
  campos de contagem, prazo, pesquisa e apresentação atravessam a camada de dados.
- O dashboard faz três consultas operacionais indexadas, uma para reuniões em
  rascunho e uma página curta de histórico; não carrega agregados nem coleções.
- Contadores e drill-down compartilham a mesma regra de data e a mesma projeção,
  evitando divergência entre o número e a listagem correspondente.
- `ActivityHistories` sustenta a timeline global e por entidade usando os índices
  `(EntityType, EntityId, OccurredAtUtc)` e `OccurredAtUtc` da migration inicial.
- Filtros são formulários GET com atualização parcial progressiva. A resposta
  HTML completa é o fallback oficial quando JavaScript não está disponível.
- `LookupService` gerencia áreas, pessoas, status e categorias com normalização,
  proteção de escopo em uso, ativação lógica e auditoria na mesma transação.

## Decisões registradas

- [ADR 0001 — Monólito modular](decisions/0001-modular-monolith.md)
- [ADR 0002 — .NET 8](decisions/0002-dotnet-8.md)
- [ADR 0003 — SQLite e Identity locais](decisions/0003-local-persistence-and-identity.md)
- [ADR 0004 — Design system local](decisions/0004-local-design-system.md)
- [ADR 0005 — Marco 9 dispensado e recuperação manual](decisions/0005-manual-recovery-scope.md)

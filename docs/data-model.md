# Modelo de dados

O esquema físico foi criado no Marco 2 pela migration `InitialCreate`.

## Agregados

- `PendingTask`: pendência ordenável e arquivável.
- `Smud`: customização com código normalizado e único.
- `SupportTicket`: chamado com número único, categoria e equipe responsável.
- `DailyMeeting`: sessão persistida com itens e snapshots.

## Cadastros e apoio

- `ApplicationUser`
- `TeamArea`
- `Person`
- `StatusDefinition`
- `Category`
- `ActivityHistory`
- `WorkItemReference`

Registros operacionais importantes usam arquivamento lógico. Datas de
negócio usam `DateOnly`; timestamps usam UTC.

## Tabelas e integridade

| Grupo | Tabelas | Garantias principais |
|---|---|---|
| Identity | `AspNetUsers`, claims, logins e tokens | Username normalizado único e senha tratada pelo Identity. |
| Cadastros | `TeamAreas`, `People`, `StatusDefinitions`, `Categories` | Nomes/códigos únicos; registros referenciados usam delete restritivo. |
| Operação | `PendingTasks`, `Smuds`, `SupportTickets` | Código SMUD e número de chamado únicos; versão de concorrência; arquivamento lógico. |
| Relações | `WorkItemReferences` | Uma pendência aponta para exatamente um SMUD ou chamado por relação. |
| Reuniões | `DailyMeetings`, `DailyMeetingItems` | Cada item possui exatamente uma origem e guarda snapshots. |
| Histórico | `ActivityHistories` | Índices por entidade e data; ator é opcional. |

Todos os itens operacionais possuem índices para status, prazo, área e
arquivamento. Status têm uma semântica de ciclo `Active`, `Completed` ou
`Cancelled`, separada do nome e da cor configuráveis.

## Concorrência

Pendências, SMUDs, chamados e reuniões possuem `Version` como concurrency
token. Uma segunda aba com versão desatualizada recebe conflito em vez de
sobrescrever silenciosamente a primeira alteração.

## Seed

- 8 áreas/equipes;
- 17 status;
- 7 categorias;
- em Development e banco vazio: 10 pendências, 7 SMUDs, 7 chamados, 6 vínculos
  e seus históricos de criação;
- nomes pessoais são fictícios e descrições foram sanitizadas.

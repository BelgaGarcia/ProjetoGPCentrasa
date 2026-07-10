# ADR 0003 — SQLite e Identity locais

- **Status:** aceito
- **Data:** 10/07/2026

## Decisão

Persistir dados funcionais e ASP.NET Core Identity em um banco SQLite fora do
repositório. Usar Identity Core com cookies, sem cadastro público, roles,
e-mail ou provedores externos.

## Motivo

SQLite oferece persistência simples e transacional para um único usuário local.
Identity evita implementar hash, lockout, cookies e tokens de senha manualmente.

## Consequências

- O banco padrão fica em `%LOCALAPPDATA%\CentraSA\Data`.
- O primeiro administrador é criado por uma rota disponível apenas sem usuários
  e a partir da máquina local.
- A conta e os hashes nunca entram no seed ou em futuros backups JSON.
- Troca de provedor de banco fica isolada em Infrastructure.

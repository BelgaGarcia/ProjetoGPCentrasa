# ADR 0002 — .NET 8

- **Status:** aceito com migração planejada
- **Data:** 10/07/2026

## Decisão

Usar `net8.0` e fixar o SDK pela configuração `global.json`, permitindo apenas
patches compatíveis da feature band selecionada.

## Motivo

O .NET SDK 8.0.414 é a única versão LTS instalada no ambiente inicial.

## Consequências

- Dependências Microsoft permanecerão na linha 8.0.
- O ambiente deve receber patches de manutenção durante a implementação.
- A migração para .NET 10 deve ocorrer antes de 10/11/2026.

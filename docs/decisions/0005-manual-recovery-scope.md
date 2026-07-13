# ADR 0005 — Marco 9 dispensado e recuperação manual

- **Status:** aceito
- **Data:** 13/07/2026

## Decisão

Dispensar do primeiro release o módulo de backup, importação e exportação
planejado no Marco 9. A recuperação suportada será a cópia integral do arquivo
SQLite, feita manualmente e somente com a aplicação encerrada.

## Motivo

A aplicação é local, de um usuário e não armazena anexos. O volume funcional
está concentrado em um único arquivo pequeno, e o responsável aceitou repor
manualmente dados recentes quando não houver uma cópia. Envelope versionado,
upload, prévia, restauração seletiva e retenção automática aumentariam a área
destrutiva e o custo operacional sem necessidade atual.

## Consequências

- a aplicação não cria cópias nem consome espaço de retenção automaticamente;
- o usuário decide onde e por quanto tempo guarda cada cópia;
- uma cópia fria do `centrasa.db` inclui dados funcionais e Identity no mesmo
  estado, inclusive o administrador existente naquele instante;
- não há restauração seletiva, validação de envelope ou importação entre versões;
- a reposição exige encerramento da aplicação e uma cópia preventiva do banco
  atual, conforme o manual local;
- implementar backup lógico no futuro exige novo marco e nova decisão de escopo.

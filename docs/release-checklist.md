# Checklist do release candidate

## Validação automatizada

Na raiz do repositório:

```powershell
.\V.cmd
.\V.cmd -Configuration Release
.\V.cmd -Coverage
```

Os dois primeiros comandos devem concluir restore, build sem warnings, suíte
completa e formatação, respectivamente em Debug e Release. O terceiro repete o
fluxo e grava `coverage.cobertura.xml` sob `TestResults`, pasta ignorada pelo Git.

## Registro do Marco 11 — 13/07/2026

- `V.cmd -Configuration Release`: aprovado com zero warnings, zero erros,
  33 testes unitários e 20 testes de integração.
- Cópia temporária sem `.git`, `bin`, `obj`, bancos ou dados locais: restore,
  build, testes e formatação aprovados em Release.
- Smoke test Production em loopback: configuração inicial respondeu, o seed
  permaneceu desligado e a rota descartável de screenshots exigiu autenticação.
- Auditoria de portfólio: nenhum banco, credencial, chave, referência corporativa
  bloqueada ou segredo de alta confiança foi encontrado nos arquivos publicáveis.
- Seis screenshots sanitizadas foram geradas a partir de banco e perfil de
  navegador temporários.

O gate de publicação `-RequireClean` deve ser executado somente depois do commit
final; até lá, `git status --short` lista legitimamente os Marcos 5 a 11 ainda
não consolidados no histórico.

## Banco novo e banco existente

Use uma raiz temporária para não tocar nos dados pessoais:

```powershell
$env:Storage__DataDirectory = Join-Path $env:TEMP "CentraSA-RC"
$env:SeedDemoData = "false"
dotnet run --project src/CentraSA.Web
```

1. No banco novo, crie o administrador, entre e cadastre um item de cada módulo.
2. Encerre com `Ctrl+C` e execute novamente usando a mesma raiz.
3. Confirme login, dados e históricos. A inicialização deve reaplicar somente o
   que estiver pendente e não duplicar cadastros estruturais.
4. Repita em Development com o seed demonstrativo e confirme 10 pendências,
   7 SMUDs e 7 chamados sem duplicação após reiniciar.

O teste de integração `NewAndAlreadyMigratedDatabaseInitializationIsIdempotentAndPersistent`
cobre automaticamente a criação, a reabertura do banco já migrado e a ausência
de migrations pendentes. Como ainda existe apenas `InitialCreate`, migração entre
duas versões históricas será validada quando uma segunda migration for criada.

## Roteiro manual de interface

- Entrar apenas após criar o primeiro administrador; validar troca de senha e
  conflito por edição em duas abas.
- Conferir dashboard e abrir cada contador no drill-down correspondente.
- Filtrar os três módulos e confirmar que totais e cartões permanecem iguais.
- Validar `SMUD081`, `SMUD083`, `SMUD077`, `SMUD084` e `SMUD085`, além dos sete
  chamados demonstrativos na composição cartão–responsável–prazo.
- Preparar, editar, apresentar, alterar e finalizar uma reunião; depois consultar
  os snapshots e o item original.
- Repetir filtros com JavaScript desabilitado.
- Navegar somente por teclado, usar o link de salto e observar foco visível.
- Testar desktop e viewport estreita; os conectores devem permanecer no fluxo.
- Acionar tela cheia e prévia de impressão; menus e ações não devem ser impressos.
- Conferir mensagens de sucesso, validação, duplicidade e concorrência.
- Abrir histórico global, detalhe e cadastros auxiliares pelos links da aplicação.

## Aprovação

O release candidate pode ser promovido quando `V` passar integralmente, os dois
cenários de banco e o roteiro manual não apresentarem falha bloqueante, e as
limitações conhecidas estiverem aceitas.

Antes da publicação pública, execute também:

```powershell
.\scripts\audit-portfolio.cmd -RequireClean
git status --short
```

O teste `ClosedDatabaseFileCanBeCopiedAndRestored` demonstra a recuperação por
cópia fria em banco temporário. O teste `ProductionDoesNotRegisterPortfolioCaptureLogin`
garante que o acesso descartável das imagens não existe em Production.

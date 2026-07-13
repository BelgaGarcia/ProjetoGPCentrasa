# Manual de execução local

## Preparar o projeto

Na raiz do repositório:

```powershell
dotnet restore CentraSA.sln
dotnet build CentraSA.sln --no-restore
```

## Executar

```powershell
dotnet run --project src/CentraSA.Web
```

Abra no navegador uma das URLs `localhost` informadas pelo ASP.NET Core.

Para simular a entrega local em Production, use uma raiz própria e mantenha o
servidor limitado ao loopback:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:Storage__DataDirectory = "$env:LOCALAPPDATA\CentraSA"
dotnet run --project src/CentraSA.Web -c Release --no-launch-profile --urls http://127.0.0.1:5180
```

Production não recebe seed demonstrativo e não registra a rota temporária usada
para capturar o portfólio.

No primeiro acesso:

1. Acesse qualquer página; você será direcionado ao login.
2. Como ainda não há usuário, o sistema abre a configuração inicial.
3. Defina um username e uma senha com no mínimo 12 caracteres, letras
   minúsculas e números.
4. Entre com as credenciais criadas.

A configuração inicial deixa de estar disponível após a criação do usuário.
Não existe cadastro público nem senha padrão.

## Fluxo de pendências

1. Entre na aplicação e selecione **Pendências** no menu lateral.
2. Use **Captura rápida** para registrar apenas título, área e prazo.
3. Use **Nova pendência** para informar prioridade, responsável, status,
   descrição, observações e um vínculo opcional com SMUD ou chamado.
4. Marque a caixa azul para concluir; a caixa verde reabre o item.
5. Use as setas para mudar a ordem da reunião e **Apresentar** para abrir o
   checklist azul em tela cheia.
6. Itens arquivados deixam a operação principal, mas podem ser restaurados em
   **Arquivadas**.

Todas as ações mutáveis exigem autenticação e antiforgery. Se o item tiver sido
alterado em outra aba, recarregue a página antes de tentar salvar novamente.

## Fluxo de SMUDs

1. Selecione **SMUDs** no menu lateral para abrir o quadro operacional.
2. Use **Novo SMUD** e informe código, título, área e status. Entradas como
   `smud-84` são normalizadas para `SMUD084` e não podem se repetir.
3. Use os filtros para restringir os cartões por texto, área, pessoa, status,
   prazo ou presença de ação necessária.
4. Abra um cartão para consultar todos os dados e seu histórico; use **Editar**
   para alterar também o status que define a coluna do cartão.
5. Use **Apresentar** para o quadro em tela cheia ou **Arquivados** para restaurar
   itens retirados da operação.

Os totais no cabeçalho e nas colunas são calculados sobre os mesmos cartões
resultantes dos filtros. O status persistido é a única fonte para o agrupamento.

## Fluxo de chamados

1. Selecione **Chamados** no menu lateral.
2. Use **Novo chamado** para registrar número, categoria, equipe, pessoa, status,
   prazo e ação pendente. O número aceita somente dígitos e deve ser único.
3. Combine os filtros de categoria e equipe conforme necessário; os dois
   cadastros são independentes.
4. Leia cada linha como **chamado → responsável → prazo**. Em viewport estreita,
   a relação passa automaticamente para uma coluna, com conectores verticais.
5. Abra o chamado para consultar o histórico, ou use **Apresentar** para exibir
   as relações em tela cheia.
6. Chamados arquivados saem do quadro principal e podem ser restaurados em
   **Arquivados**.

## Fluxo de reunião diária

1. Selecione **Reunião diária** e use **Preparar reunião**.
2. Revise as sugestões automáticas, marque os itens desejados e defina seção,
   ordem e notas. Uma origem possui apenas uma seleção e uma seção.
3. Salve o rascunho; ele pode ser reaberto em **Editar roteiro**.
4. Use **Apresentar** para tela cheia. Durante a sessão, salve notas, marque
   itens apresentados ou use **Concluir original** para atualizar a pendência,
   o SMUD ou o chamado na mesma transação.
5. Use **Imprimir / PDF** quando precisar de uma cópia sem controles de tela.
6. Finalize a reunião para torná-la somente leitura e mantê-la no histórico.

O título, status, prazo e responsável exibidos no roteiro são snapshots. A
situação atual do item original é mostrada separadamente quando houver mudança.

## Dashboard, histórico e cadastros auxiliares

1. Abra **Início** para consultar os contadores consolidados.
2. Selecione qualquer contador para abrir o drill-down com o filtro responsável
   pelo número exibido.
3. Use **Histórico** para pesquisar resumos e combinar tipo, ação e intervalo.
   O campo **Até** inclui todo o dia selecionado.
4. Abra **Detalhes** em um evento para consultar a timeline completa da origem.
5. Em **Cadastros auxiliares**, gerencie áreas/equipes, pessoas, status e
   categorias. Desativar preserva os vínculos e o histórico.

Filtros usam query string e botão de envio, portanto funcionam sem JavaScript.
Com JavaScript habilitado, apenas os resultados são substituídos e a URL continua
representando o filtro atual.

## Dados locais

Por padrão:

```text
%LOCALAPPDATA%\CentraSA\Data\centrasa.db
%LOCALAPPDATA%\CentraSA\Keys\
```

A pasta `Keys` protege cookies locais. O banco contém tanto os dados funcionais
quanto a conta do administrador. A aplicação não cria uma pasta de backups nem
mantém retenção automática.

Defina outra raiz somente quando necessário:

```powershell
$env:Storage__DataDirectory = "C:\dados\CentraSA"
dotnet run --project src/CentraSA.Web
```

## Cópia e recuperação manual

O Marco 9 foi dispensado por decisão de escopo. Para guardar uma cópia consistente:

1. Encerre a aplicação com `Ctrl+C` e feche qualquer outra instância da Central.
2. No Explorador, abra `%LOCALAPPDATA%\CentraSA\Data` ou a raiz definida em
   `Storage__DataDirectory`.
3. Copie `centrasa.db` para uma pasta escolhida por você e acrescente data e hora
   ao nome, por exemplo `centrasa-2026-07-13-1800.db`.
4. Mantenha apenas as cópias que considerar necessárias. A aplicação não ocupa
   espaço adicional por conta própria.

Para recuperar uma cópia:

1. Encerre completamente a aplicação.
2. Renomeie o banco atual para `centrasa-antes-restauracao.db`; essa é a cópia
   preventiva caso tenha selecionado o arquivo errado.
3. Copie o arquivo escolhido para a pasta `Data` e renomeie-o para `centrasa.db`.
4. Inicie a aplicação, entre e confira dashboard, um registro e seu histórico.
5. Só apague a cópia preventiva depois dessa conferência.

Essa reposição é integral. Ela devolve também o administrador e sua senha ao
estado da data copiada; não combina dados do banco atual com dados antigos. Não
copie o SQLite enquanto a aplicação estiver aberta, pois uma escrita em andamento
pode produzir uma cópia inconsistente.

## Migrations

A aplicação aplica migrations pendentes automaticamente. Para trabalhar no
esquema:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations list --project src/CentraSA.Infrastructure --startup-project src/CentraSA.Web
dotnet tool run dotnet-ef migrations add NomeDaMigration --project src/CentraSA.Infrastructure --startup-project src/CentraSA.Web --output-dir Persistence\Migrations
```

Antes de criar uma migration, compile e revise se a alteração pertence ao
modelo aprovado. Nunca edite o model snapshot manualmente.

## Alterar ou recuperar a senha

Um usuário autenticado pode usar **Alterar senha** no cabeçalho. Se perder a
senha, encerre a aplicação e execute em terminal interativo:

```powershell
dotnet run --project src/CentraSA.Web -- --reset-admin
```

A senha é lida sem ser exibida nem gravada no histórico do shell.

## Testar e verificar formatação

```powershell
.\V.cmd
.\V.cmd -Configuration Release
```

Para coletar cobertura com o coletor já configurado nos dois projetos de teste:

```powershell
.\V.cmd -Coverage
```

O roteiro de homologação para banco novo, banco existente, desktop, viewport
estreita, tela cheia, impressão, teclado e fallback sem JavaScript está no
[checklist do release candidate](release-checklist.md).

## Preparar publicação

Antes de publicar ou criar uma tag:

```powershell
.\scripts\audit-portfolio.cmd
git status --short
```

Após o commit final, repita com `-RequireClean`. O script rejeita bancos,
certificados, chaves e arquivos locais versionados, procura nomes não sanitizados
no material público e verifica caminhos sensíveis no histórico do Git.

As capturas podem ser regeneradas, após o build Release, com:

```powershell
node scripts/capture-portfolio.mjs
```

O processo usa banco, administrador, senha e perfil de navegador temporários.
Confira visualmente as imagens em [screenshots](screenshots/README.md) antes de
qualquer publicação.

## Seed de desenvolvimento

`appsettings.Development.json` habilita dados demonstrativos somente quando as
tabelas operacionais estão vazias. Production nunca recebe esse seed. O usuário
administrador não faz parte do seed em nenhum ambiente.

As limitações aceitas para este candidato a release estão registradas em
[Limitações conhecidas](limitations.md). A decisão de dispensar o Marco 9 está
na [ADR 0005](decisions/0005-manual-recovery-scope.md).

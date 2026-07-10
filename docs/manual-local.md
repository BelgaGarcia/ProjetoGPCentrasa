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

## Dados locais

Por padrão:

```text
%LOCALAPPDATA%\CentraSA\Data\centrasa.db
%LOCALAPPDATA%\CentraSA\Keys\
%LOCALAPPDATA%\CentraSA\Backups\
```

Defina outra raiz somente quando necessário:

```powershell
$env:Storage__DataDirectory = "C:\dados\CentraSA"
dotnet run --project src/CentraSA.Web
```

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
dotnet test CentraSA.sln --no-build
dotnet format CentraSA.sln --verify-no-changes
```

## Seed de desenvolvimento

`appsettings.Development.json` habilita dados demonstrativos somente quando as
tabelas operacionais estão vazias. Production nunca recebe esse seed. O usuário
administrador não faz parte do seed em nenhum ambiente.

Backup e restauração serão implementados no Marco 9. Até lá, para uma cópia
manual consistente, encerre a aplicação e copie o arquivo `centrasa.db`.

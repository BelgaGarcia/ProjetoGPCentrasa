# Central de Pendências e Entregas CentraSA

Aplicação web local para organizar pendências, SMUDs, chamados e reuniões
diárias em uma única interface. O projeto também serve como estudo e portfólio
de ASP.NET Core MVC.

> Estado atual: Marco 4 concluído. O módulo de pendências já possui cadastro,
> acompanhamento, histórico, arquivamento e apresentação; SMUDs entram no Marco 5.

## Tecnologias

- .NET 8 e ASP.NET Core MVC
- Razor Views
- Entity Framework Core 8 e SQLite
- ASP.NET Core Identity com autenticação local
- xUnit
- Bootstrap local e CSS próprio

## Interface

A interface possui dois contextos visuais:

- **Gestão:** sidebar, topbar, navegação responsiva e componentes operacionais.
- **Apresentação:** layout limpo, tela cheia e impressão/PDF pelo navegador.

Os estilos ficam separados em tokens, base, layout, componentes, páginas e
impressão. Ícones SVG e bibliotecas são servidos localmente, sem CDN.

## Pré-requisitos

- .NET SDK 8.0.414 ou patch compatível da mesma feature band
- Git

Confira o SDK selecionado:

```powershell
dotnet --version
```

## Restaurar, compilar e testar

```powershell
dotnet restore CentraSA.sln
dotnet build CentraSA.sln --no-restore
dotnet test CentraSA.sln --no-build
dotnet format CentraSA.sln --verify-no-changes
```

## Executar a aplicação base

```powershell
dotnet run --project src/CentraSA.Web
```

Use uma das URLs `localhost` exibidas no terminal. No primeiro acesso, a página
de login encaminha para a criação controlada do único administrador. Não existe
senha padrão.

Em Development, um banco vazio recebe dados demonstrativos sanitizados. As
migrations e os cadastros estruturais são aplicados automaticamente no início.

## Gerenciar pendências

Após entrar, abra **Pendências** no menu lateral. O módulo permite:

- captura rápida e formulário completo;
- busca, filtros por área, status e prazo, com paginação de 50 itens;
- conclusão, reabertura, ordenação e arquivamento sem exclusão física;
- vínculos opcionais com SMUD ou chamado;
- detalhe com histórico das alterações relevantes;
- apresentação em tela cheia inspirada no checklist do Gamma.

Filtros funcionam por URL mesmo sem JavaScript. Quando o navegador permite,
a lista é atualizada sem recarregar toda a página.

## Estrutura

```text
src/
  CentraSA.Domain          Regras e modelo de domínio
  CentraSA.Application     Casos de uso e portas
  CentraSA.Infrastructure  Persistência e integrações locais
  CentraSA.Web             ASP.NET Core MVC e Razor Views
tests/
  CentraSA.UnitTests
  CentraSA.IntegrationTests
docs/
  Decisões e documentação técnica
```

A direção das dependências está documentada em
[`docs/architecture.md`](docs/architecture.md).

## Dados locais e segurança

O banco fica em `%LOCALAPPDATA%\CentraSA\Data\centrasa.db`; chaves de proteção e
backups ficam sob a mesma raiz, fora do repositório. Bancos, backups, secrets,
credenciais e dados operacionais reais não devem ser versionados.

Para usar outra raiz local durante desenvolvimento:

```powershell
$env:Storage__DataDirectory = "C:\dados\CentraSA"
dotnet run --project src/CentraSA.Web
```

## Ferramentas e migrations

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations list --project src/CentraSA.Infrastructure --startup-project src/CentraSA.Web
```

As instruções completas estão no [manual local](docs/manual-local.md).

## Documentação

- [Arquitetura](docs/architecture.md)
- [Modelo de dados planejado](docs/data-model.md)
- [Manual local](docs/manual-local.md)
- [Roadmap](docs/roadmap.md)
- [Como contribuir](CONTRIBUTING.md)

## Licença

Distribuído sob a licença [MIT](LICENSE).

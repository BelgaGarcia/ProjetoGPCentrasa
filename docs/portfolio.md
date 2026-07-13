# Central CentraSA — estudo de caso

## Contexto

Aplicação web local criada para consolidar pendências, customizações, chamados e
reuniões diárias sem depender de serviços externos. O projeto demonstra desenho
de domínio, persistência relacional, segurança local e uma interface operacional
responsiva em ASP.NET Core MVC.

Todos os nomes pessoais, equipes, fornecedores, números e descrições presentes
no seed e nas imagens são fictícios. Nenhum banco operacional, credencial ou
dado corporativo integra o repositório.

## Solução

- monólito modular em Domain, Application, Infrastructure e Web;
- SQLite e migrations automáticas, com Identity no mesmo banco local;
- CRUD, concorrência otimista, arquivamento lógico e histórico transacional;
- quadros filtráveis de pendências, SMUDs e chamados;
- builder persistido de reunião, snapshots e modo de apresentação/impressão;
- dashboard derivado do banco, drill-down e timeline global;
- fallback sem JavaScript, navegação por teclado e layout responsivo;
- 53 testes automatizados após o fechamento do Marco 11.

## Decisões relevantes

- dados ficam no perfil local do usuário e não são enviados para terceiros;
- banco/status é a fonte da verdade para agrupamentos e contadores;
- conectores visuais usam grid e pseudo-elementos no fluxo;
- o backup automático foi dispensado; a recuperação suportada é uma cópia fria
  e integral do SQLite, documentada no manual;
- Production não recebe seed nem expõe o acesso temporário de screenshots.

## Evidências

- [Galeria sanitizada](screenshots/README.md)
- [Arquitetura](architecture.md)
- [Modelo de dados](data-model.md)
- [Checklist do release candidate](release-checklist.md)
- [Limitações conhecidas](limitations.md)

## Executar a demonstração

```powershell
.\V.cmd -Configuration Release
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Storage__DataDirectory = Join-Path $env:TEMP "CentraSA-Demo"
dotnet run --project src/CentraSA.Web -c Release --no-launch-profile --urls http://127.0.0.1:5180
```

Development cria apenas o seed fictício em banco vazio. O administrador continua
sendo criado manualmente no primeiro acesso e não possui senha padrão.

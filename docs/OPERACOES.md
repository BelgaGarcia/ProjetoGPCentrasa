# CentraSA — documentação unificada de operação e continuidade

> **Premissa obrigatória:** o CentraSA foi projetado para **um único
> administrador por vez**. Não é uma aplicação multiusuário nem deve ser usado
> com acessos concorrentes. SQLite, autenticação local e várias decisões de
> operação dependem intencionalmente dessa premissa.

Este arquivo é a fonte única de verdade para orientar manutenção,
desenvolvimento, deploy, backup e recuperação. Ele é versionado junto com o
código. Os documentos especializados continuam válidos nos limites indicados
em [Referências e estado dos documentos](#referências-e-estado-dos-documentos),
mas uma divergência operacional deve ser resolvida aqui e nos artefatos
executáveis no mesmo pull request.

## Identificação e estado desta revisão

| Campo | Valor |
|---|---|
| Sistema | Central de Pendências e Entregas CentraSA |
| Proprietário | CentraSA |
| Responsável nominal por aplicação e operação | **PENDENTE — designar formalmente uma pessoa e seu substituto** |
| Host de produção | `srvinfra` (`192.168.100.15`) |
| Endpoint interno | `http://192.168.100.15:5180` |
| Data da revisão | 22/07/2026 |
| Commit auditado | `fb7e0f2` (`main`, tag `v1.0.1`) |
| Prazo das pendências operacionais desta revisão | **PENDENTE — definir após designação do responsável** |

### Matriz de confiança

Os termos abaixo evitam transformar intenção em fato:

- **Verificado:** observado ou executado com sucesso nesta revisão.
- **Versionado:** está definido no repositório, mas não foi confirmado no host.
- **Pendente:** exige acesso ou validação adicional antes de ser tratado como
  garantia operacional.

| Item | Estado em 22/07/2026 | Evidência |
|---|---|---|
| Checkout e artefatos Docker | **Verificado** | `main` limpo em `fb7e0f2`; `Dockerfile`, `compose.yaml` e `ops/*.sh` revisados. |
| Build, testes e formatação Release | **Verificado** | `V.cmd -Configuration Release`: 0 warnings, 33 testes unitários e 40 de integração aprovados. |
| Migration mais recente | **Verificado no código** | `20260710120055_InitialCreate`, em `src/CentraSA.Infrastructure/Persistence/Migrations`. |
| Aplicação acessível pela rede interna | **Verificado** | `GET /conta/entrar` retornou HTTP 200 em `192.168.100.15:5180`. |
| Container, imagem e healthcheck em execução | **Pendente** | A autenticação SSH disponível nesta revisão foi recusada; não houve acesso ao Docker do host. |
| Stack `centrasa`, volume `centrasa_data` e variáveis | **Versionado; pendente no host** | Contrato de `compose.yaml`; conferir no Portainer/Docker. |
| Cron diário, snapshots existentes e retenção real | **Pendente** | Não foi possível ler `/etc/cron.d`, journal ou `/var/backups/centrasa` no host. |
| Restauração completa/restore drill | **Pendente de execução e aceite** | Existem scripts e roteiro versionados, mas não há registro verificável de uma restauração executada. |
| Validação por pessoa alheia ao deploy | **Pendente** | Registrar nome, data e resultado na seção de aceite. |

Não remova os estados **Pendente** apenas porque o comando parece correto.
Troque-os por **Verificado** somente no mesmo commit que registrar data, executor
e resultado, sem incluir senhas ou dados de produção.

## 1. Visão geral

O CentraSA é uma aplicação web interna para um administrador organizar
pendências, SMUDs, chamados, reuniões diárias, cadastros auxiliares e histórico
operacional em uma interface única. A aplicação usa .NET 8, ASP.NET Core MVC e
Razor Views, Entity Framework Core 8, SQLite e ASP.NET Core Identity com
autenticação local. Em produção ela roda em um único container Docker gerenciado
pelo Portainer, sem seed demonstrativo.

O banco funcional e a conta do administrador estão no mesmo SQLite. As chaves
de Data Protection, usadas pelos cookies, ficam no mesmo volume persistente. Um
backup operacional deve preservar toda a raiz `/data`, não apenas o arquivo do
banco.

## 2. Arquitetura e mapa do código

O sistema é um monólito modular em quatro camadas:

```text
HTTP / Razor
    |
    v
CentraSA.Web ---------------> CentraSA.Application
    |                                  |
    v                                  v
CentraSA.Infrastructure ----------> CentraSA.Domain
```

| Pasta/projeto | O que vive ali | Quando alterar |
|---|---|---|
| `src/CentraSA.Domain` | Entidades, enums, invariantes e regras puras. | Nova entidade ou regra de negócio sem dependência de infraestrutura. |
| `src/CentraSA.Application` | Casos de uso, serviços, contratos/DTOs e interfaces de repositório. | Novo fluxo funcional ou mudança na orquestração de uma funcionalidade. |
| `src/CentraSA.Infrastructure` | EF Core, SQLite, Identity, repositórios, configurações, migrations e seed. | Mudança de persistência, schema, autenticação ou implementação de uma porta da Application. |
| `src/CentraSA.Web` | Controllers, view models, Razor Views, arquivos estáticos, configuração e `Program.cs`. | Tela, rota HTTP, binding/validação de formulário ou composição da aplicação. |
| `tests/CentraSA.UnitTests` | Testes de regras puras e limites arquiteturais. | Toda mudança de regra ou estrutura de dependências. |
| `tests/CentraSA.IntegrationTests` | Testes de composição, EF/SQLite, autenticação e fluxos completos. | Mudança de caso de uso, persistência, Identity, inicialização ou rota. |
| `ops` | Scripts root-owned de backup, restauração, atualização e rollback. | Mudança operacional; exige revisão redobrada e teste isolado. |
| `docs/decisions` | ADRs com decisões arquiteturais aceitas. | Decisão estrutural nova ou reversão explícita de decisão anterior. |

Regras de dependência, composição e detalhes dos módulos estão em
[architecture.md](architecture.md); o schema está em
[data-model.md](data-model.md). Não replique nesses arquivos instruções de
produção que pertencem a este documento.

### Migrations

As migrations ficam em
`src/CentraSA.Infrastructure/Persistence/Migrations`. A mais recente nesta
revisão é `20260710120055_InitialCreate`; o snapshot do modelo é
`CentraSaDbContextModelSnapshot.cs`. Para listar no código:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations list `
  --project src/CentraSA.Infrastructure `
  --startup-project src/CentraSA.Web
```

A aplicação chama `MigrateAsync` na inicialização. Antes de criar uma migration,
use uma raiz de dados descartável, gere a migration com `--output-dir
Persistence\Migrations`, revise o SQL/model snapshot e teste tanto banco novo
quanto uma cópia sanitizada do schema anterior. Nunca edite o snapshot à mão.

## 3. Produção atual

### Contrato versionado

| Recurso | Valor definido no repositório |
|---|---|
| Host | `srvinfra` |
| Gerência | Portainer; stack/projeto Compose `centrasa` |
| Container | `centrasa` |
| Imagem | `centrasa:<semver>`; padrão do Compose nesta revisão: `1.0.0` |
| Release mais recente do repositório | `v1.0.1`; **a tag executada no host ainda precisa ser confirmada** |
| Endpoint | `http://192.168.100.15:5180` |
| Porta do container | `8080` |
| Volume | `centrasa_data`, montado em `/data` |
| Banco | `/data/Data/centrasa.db` |
| Chaves | `/data/Keys` |
| Política de restart | `unless-stopped` |
| Healthcheck | `GET http://127.0.0.1:8080/conta/entrar` a cada 30 s |

O endpoint HTTP foi verificado, mas os demais valores desta tabela são o
contrato de `compose.yaml`, não uma leitura do runtime. Execute a coleta abaixo
com acesso ao host e atualize a matriz de confiança.

### Variáveis do container

| Variável | Valor esperado | Função |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Desliga seed e recursos exclusivos de Development. |
| `ASPNETCORE_URLS` | `http://+:8080` | Faz o Kestrel escutar na porta interna do container. |
| `Storage__DataDirectory` | `/data` | Define a raiz persistente de banco e chaves. |
| `AllowedHosts` | `192.168.100.15;localhost;127.0.0.1` | Restringe os hosts HTTP aceitos. |
| `CENTRASA_IMAGE_TAG` | tag semver escolhida no Portainer | Seleciona a imagem local usada pelo Compose. |
| `CENTRASA_BIND_IP` | `192.168.100.15` após homologação | Limita a publicação à interface interna. |
| `CENTRASA_ALLOWED_HOSTS` | lista acima | Alimenta `AllowedHosts` no Compose. |

Nenhuma dessas variáveis deve conter senha. Credenciais não pertencem ao
Compose, a este documento ou ao Git.

### Operação diária

No Portainer, use **Containers > centrasa** para estado, logs e restart; use a
stack `centrasa` para stop/start ou redeploy. Equivalentes no host:

```bash
# Estado, imagem, healthcheck e porta
sudo docker ps --filter 'name=^/centrasa$'
sudo docker inspect centrasa --format \
  'image={{.Config.Image}} running={{.State.Running}} health={{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}'

# Logs
sudo docker logs --tail 200 centrasa
sudo docker logs --follow centrasa

# Reiniciar somente o container
sudo docker restart --time 30 centrasa

# Parar e iniciar
sudo docker stop --time 30 centrasa
sudo docker start centrasa

# Localizar o volume e seu mountpoint real no host
sudo docker volume inspect centrasa_data
sudo docker volume inspect centrasa_data --format '{{.Mountpoint}}'

# Conferir variáveis efetivas (revise antes para não exibir futuros secrets)
sudo docker inspect centrasa --format '{{range .Config.Env}}{{println .}}{{end}}'
```

Depois de restart/start, espere o estado `healthy`, confira os logs e abra a
tela de login. Não considere “container running” suficiente.

## 4. Desenvolvimento e release

### Fluxo GitHub

1. Mantenha `main` estável e deployável.
2. Crie uma branch pequena: `feature/nome`, `fix/nome`, `docs/nome` ou
   `chore/nome`.
3. Atualize código, testes, documentação e ADR no mesmo conjunto quando forem
   inseparáveis.
4. Execute `V.cmd -Configuration Release` antes do push.
5. Abra Pull Request, obtenha revisão e faça merge em `main`.
6. Crie uma tag Git imutável `vMAJOR.MINOR.PATCH` a partir do commit aprovado.
7. Construa no host uma imagem com a mesma versão, sem sobrescrever tags antigas.

Use SemVer simples:

- `PATCH`: correção compatível;
- `MINOR`: funcionalidade compatível;
- `MAJOR`: mudança incompatível de dados, operação ou comportamento público.

Qualquer IA usada no desenvolvimento deve ler este arquivo e as ADRs antes de
sugerir mudança estrutural. **SQLite, admin único, monólito modular e operação
local não são acidentes a “corrigir”; são decisões aceitas.** Alterá-las exige
necessidade demonstrada, ADR e plano de migração.

### Validação antes do merge/release

Na raiz do repositório, em Windows:

```powershell
.\V.cmd -Configuration Release
.\scripts\audit-portfolio.cmd
git status --short
```

Checklist mínimo:

- build sem warnings e todos os testes aprovados;
- nenhuma mudança de formatação pendente;
- migration nova testada em banco novo e em cópia sanitizada do schema anterior;
- documentação, ADRs e notas da release atualizadas;
- diff sem banco, backup, chave, credencial ou dado real;
- revisão aprovada e tag ainda inexistente no remoto.

### Deploy de uma nova versão

O método definido é **build da imagem no próprio `srvinfra`**. O Portainer usa a
imagem local e não faz o build nem puxa de registry (`pull_policy: never`). O
roteiro abaixo é o contrato versionado; a execução ponta a ponta ainda deve ser
registrada como teste operacional.

Na estação de desenvolvimento, depois do merge:

```powershell
git switch main
git pull --ff-only
.\V.cmd -Configuration Release
git tag -a v1.0.2 -m "CentraSA 1.0.2"
git push origin v1.0.2
```

Substitua `1.0.2` pela próxima versão real. No `srvinfra`:

```bash
cd /opt/centrasa/src
git fetch --tags --prune
git checkout --detach v1.0.2
git status --short
sudo docker build --pull --tag centrasa:1.0.2 .
sudo docker image inspect centrasa:1.0.2 --format '{{.Id}}'
sudo /usr/local/sbin/centrasa-update 1.0.2
```

`centrasa-update` exige a imagem local, executa um snapshot frio, registra a
imagem anterior em `/var/lib/centrasa/previous-image` e então pede o redeploy.
No Portainer:

1. edite a stack `centrasa`;
2. altere somente `CENTRASA_IMAGE_TAG` para `1.0.2`;
3. use **Update the stack / Redeploy**;
4. confirme imagem efetiva, healthcheck e ausência de restart loop;
5. valide login, dashboard, leitura, criação/edição de um registro controlado e
   persistência após um restart;
6. registre executor, horário, tag anterior/nova e snapshot pré-deploy.

Não apague a imagem anterior nem o snapshot pré-deploy antes do aceite.

### Rollback

Se a aplicação nova falhar:

```bash
sudo /usr/local/sbin/centrasa-rollback
```

O script cria outro snapshot e informa a tag anterior. Reponha essa tag em
`CENTRASA_IMAGE_TAG` no Portainer e redeploye. Se a versão nova aplicou migration
incompatível, reverter somente a imagem pode não bastar: após autorização na
janela de mudança, restaure também o snapshot criado **antes** do deploy conforme
a seção seguinte. Registre motivo, versão e resultado.

## 5. Backup e restauração

### O que está implementado

O backup versionado **não usa `sqlite3 .backup`**. O comando definido é:

```bash
sudo /usr/local/sbin/centrasa-backup
```

O script `ops/centrasa-backup.sh`:

1. obtém lock exclusivo;
2. detecta se o container estava em execução;
3. para o container por até 30 segundos;
4. copia todo `/data` com `docker cp`, incluindo banco e chaves;
5. exige `data/Data/centrasa.db` não vazio;
6. grava `SHA256SUMS`;
7. publica o diretório
   `/var/backups/centrasa/centrasa-AAAAMMDDTHHMMSSZ`;
8. reinicia o container quando necessário;
9. retém, por padrão, os sete snapshots `centrasa-*` mais recentes.

A intenção versionada para `/etc/cron.d/centrasa-backup` é:

```cron
0 2 * * * root /usr/local/sbin/centrasa-backup 2>&1 | /usr/bin/logger -t centrasa-backup
```

**Estado atual: pendente de comprovação no host.** Não assuma que o arquivo foi
instalado, que o daemon está ativo ou que existem sete cópias.

### Coleta obrigatória para validar o backup atual

Execute no `srvinfra` e registre o resultado sem copiar conteúdo do banco:

```bash
sudo test -x /usr/local/sbin/centrasa-backup
sudo test -x /usr/local/sbin/centrasa-restore
sudo cat /etc/cron.d/centrasa-backup
sudo systemctl is-active cron || sudo systemctl is-active crond
sudo journalctl -t centrasa-backup --since '8 days ago' --no-pager
sudo find /var/backups/centrasa -mindepth 1 -maxdepth 1 \
  -type d -name 'centrasa-*' -printf '%TY-%Tm-%Td %TH:%TM %f\n' | sort
sudo find /var/backups/centrasa -mindepth 2 -maxdepth 3 \
  -type f -printf '%M %u:%g %s %p\n'
```

Em seguida, execute manualmente um backup em janela combinada:

```bash
sudo /usr/local/sbin/centrasa-backup
sudo docker inspect centrasa --format \
  'running={{.State.Running}} health={{.State.Health.Status}}'
```

Critérios: novo snapshot com `Data/centrasa.db`, `Keys`, `SHA256SUMS`, permissões
restritas, container novamente `healthy` e no máximo sete diretórios
`centrasa-*`. Diretórios `pre-restore-*` são cópias preventivas e não entram
nessa retenção.

### Restore drill isolado — obrigatório antes de confiar no backup

Não teste restauração sobrescrevendo produção. Escolha um snapshot real e a
mesma imagem de produção, valide seus checksums e restaure em volume/container
temporários:

```bash
SNAPSHOT=centrasa-AAAAMMDDTHHMMSSZ
IMAGE=centrasa:1.0.1

cd "/var/backups/centrasa/${SNAPSHOT}"
sudo sha256sum --check SHA256SUMS

sudo docker volume create centrasa_restore_drill
sudo docker run --rm \
  --user 0:0 \
  --entrypoint /bin/sh \
  --mount type=volume,src=centrasa_restore_drill,dst=/data \
  --mount "type=bind,src=/var/backups/centrasa/${SNAPSHOT}/data,dst=/snapshot,readonly" \
  "${IMAGE}" \
  -c 'cp -a /snapshot/. /data/; chown -R "$APP_UID:$APP_UID" /data'

sudo docker run --detach \
  --name centrasa-restore-drill \
  --publish 127.0.0.1:5181:8080 \
  --env ASPNETCORE_ENVIRONMENT=Production \
  --env ASPNETCORE_URLS=http://+:8080 \
  --env Storage__DataDirectory=/data \
  --env 'AllowedHosts=localhost;127.0.0.1' \
  --mount type=volume,src=centrasa_restore_drill,dst=/data \
  "${IMAGE}"

sudo docker inspect centrasa-restore-drill --format \
  'running={{.State.Running}} health={{.State.Health.Status}}'
sudo docker logs --tail 100 centrasa-restore-drill
```

Abra um túnel SSH para `127.0.0.1:5181`, entre com a credencial correspondente
à data do snapshot e valide dashboard, um registro e seu histórico. A restauração
é integral: a senha do administrador também volta ao estado copiado.

Depois do aceite e somente confirmando os nomes temporários:

```bash
sudo docker rm --force centrasa-restore-drill
sudo docker volume rm centrasa_restore_drill
```

Registre nesta tabela, sem dados funcionais:

| Data UTC | Executor | Snapshot | Imagem | Checksums | Login/dados/histórico | Resultado |
|---|---|---|---|---|---|---|
| **PENDENTE** | — | — | — | — | — | Restauração ainda não comprovada nesta revisão. |

### Restauração de produção

Este procedimento é destrutivo e só deve ocorrer após aceite da área dona, em
janela de mudança e com o snapshot exato identificado:

```bash
sudo /usr/local/sbin/centrasa-restore centrasa-AAAAMMDDTHHMMSSZ
```

O script verifica checksums, para o container, cria
`/var/backups/centrasa/pre-restore-<timestamp>`, substitui o volume, corrige o
proprietário e exige healthcheck saudável. Em falha tenta repor a cópia
preventiva. Depois, valide login e dados antes de remover qualquer
`pre-restore-*`.

Snapshots no mesmo host não protegem contra perda física do `srvinfra`. Definir
uma cópia criptografada, controlada e testada fora do host é uma pendência de
continuidade.

## 6. Limitações e riscos aceitos

- somente um administrador por vez; sem multiacesso concorrente;
- sem cadastro público, roles ou recuperação de senha por e-mail;
- sem rastreabilidade individual entre várias pessoas: o histórico não substitui
  auditoria multiusuário e o ator pode ser opcional;
- SQLite e chaves são restaurados integralmente; não há merge ou restauração
  seletiva;
- sem anexos e sem backup/importação/exportação dentro da aplicação;
- HTTP sem TLS na rede interna é uma decisão **temporária**;
- a senha do administrador não está comprovadamente em processo formal de
  rotação e custódia: **pendência temporária**;
- backups locais no mesmo host não cobrem perda física do servidor;
- estado real do cron, retenção e restore drill permanece pendente nesta revisão;
- o projeto ainda usa .NET 8 e deve migrar para .NET 10 antes do fim do suporte
  da linha 8, conforme [dotnet10-upgrade.md](dotnet10-upgrade.md).

Detalhes funcionais adicionais estão em [limitations.md](limitations.md). Não
oculte uma limitação removendo-a: documente a mitigação, evidência e decisão que
realmente a encerrou.

## 7. Referências e estado dos documentos

| Documento | Uso atual | Ajuste/desatualização encontrada em 22/07/2026 |
|---|---|---|
| [architecture.md](architecture.md) | Detalhe arquitetural e regras dos módulos. | Estrutura permanece correta; referências a `%LOCALAPPDATA%` descrevem execução local, não produção em `/data`; parte do texto ainda usa futuro. |
| [data-model.md](data-model.md) | Schema e integridade. | Correto para a única migration `InitialCreate`; revisar quando surgir a segunda migration. |
| [limitations.md](limitations.md) | Limitações funcionais. | Correto no geral; este arquivo acrescenta HTTP sem TLS, rotação de senha e falta de comprovação operacional. |
| [manual-local.md](manual-local.md) | Desenvolvimento e recuperação local. | Continua correto para execução local. “Sem backup automático” significa dentro da aplicação; não contradiz o backup externo do host. |
| [dotnet10-upgrade.md](dotnet10-upgrade.md) | Plano de atualização de runtime. | Contagem de “53 testes” está desatualizada; a revisão atual executou 73 (33 + 40). |
| [deploy-srvinfra.md](deploy-srvinfra.md) | Runbook detalhado de implantação inicial. | Descreve o procedimento pretendido, mas não registra container/tag/cron/restore observados depois do deploy. Este documento controla o estado pós-deploy. |
| [release-checklist.md](release-checklist.md) | Histórico do release candidate. | Registro de 13/07/2026 tinha 20 integrações; a suíte atual possui 40. Preserve como histórico, não como contagem atual. |
| [decisions/0006-container-deployment.md](decisions/0006-container-deployment.md) | Decisão de container, HTTP interno e backup frio. | Decisão vigente; implementação no host ainda precisa das evidências marcadas como pendentes. |

## 8. Aceite e manutenção desta fonte de verdade

Antes de considerar a documentação operacional concluída:

- [x] arquivo único criado e versionável no próprio repositório;
- [x] código, Compose, Dockerfile, scripts, docs e migrations auditados;
- [x] build, testes e formatação Release executados nesta revisão;
- [x] endpoint interno respondeu HTTP 200;
- [ ] container, imagem, volume e variáveis coletados no host;
- [ ] cron, journal, snapshots e retenção real conferidos;
- [ ] backup manual executado e container novamente saudável;
- [ ] restore drill isolado concluído e registrado;
- [ ] senha e dados sensíveis revisados por responsável humano;
- [ ] uma pessoa que não participou do deploy seguiu as seções 3 a 5 sem ajuda;
- [ ] responsável nominal, substituto e prazo das pendências preenchidos;
- [ ] documento revisado por Pull Request e mergeado em `main`.

Em toda release, atualize a data, o commit auditado, a versão observada em
produção, a evidência de backup/restore e as pendências. Nunca registre senha,
hash, cookie, conteúdo do banco, chave de Data Protection ou dado operacional
real neste repositório.

# Deploy no `srvinfra`

Este runbook publica o CentraSA como um único container no Docker do host
`srvinfra`, administrado pelo Portainer. O endpoint interno é
`http://192.168.100.15:5180`; TLS e criação remota do primeiro administrador não
fazem parte desta entrega.

## Contrato operacional

- imagem local: `centrasa:<versão>`;
- container: `centrasa`;
- volume: `centrasa_data` montado em `/data`;
- banco: `/data/Data/centrasa.db`;
- chaves de Data Protection: `/data/Keys`;
- porta interna: `8080`;
- porta publicada: `192.168.100.15:5180`;
- backup: `/var/backups/centrasa`, sete snapshots frios;
- ambiente: ASP.NET Core `Production`, sem seed demonstrativo e sem HTTPS.

Os snapshots ficam no mesmo servidor da aplicação. Eles protegem contra erro
operacional, mas não contra perda física ou indisponibilidade total do
`srvinfra`.

## Pré-requisitos

Antes da janela de implantação, Infra deve confirmar:

1. acesso SSH ao `srvinfra` e permissão para executar Docker;
2. acesso administrativo ao endpoint correto no Portainer;
3. que `192.168.100.15` pertence ao host e a porta `5180` está livre;
4. saída HTTPS para `github.com` e `mcr.microsoft.com`;
5. espaço em disco para código, camadas de build e sete backups;
6. que a aplicação local está encerrada antes da exportação dos dados.

Se GitHub não estiver acessível, gere um pacote limpo com `git archive` e envie-o
por SCP. Se MCR não estiver acessível, Infra precisa carregar previamente as
imagens-base SDK e ASP.NET Core 8; sem uma dessas opções o build fica bloqueado.

## Validar e versionar

Na estação de desenvolvimento:

```powershell
.\V.cmd -Configuration Release
git status --short
```

A validação deve concluir build sem warnings, 33 testes unitários, 40 testes de
integração e verificação de formatação. Depois do merge, crie uma tag imutável:

```powershell
git tag -a v1.0.0 -m "CentraSA 1.0.0"
git push origin v1.0.0
```

Não reutilize uma tag publicada. Cada atualização recebe uma versão nova.

## Construir a imagem no host

No `srvinfra`, faça checkout da tag e construa a imagem local:

```bash
sudo install -d -o "$USER" -g "$USER" /opt/centrasa
git clone https://github.com/BelgaGarcia/ProjetoGPCentrasa.git /opt/centrasa/src
cd /opt/centrasa/src
git checkout --detach v1.0.0
sudo docker build --pull --tag centrasa:1.0.0 .
sudo docker image inspect centrasa:1.0.0 --format '{{.Id}}'
```

O Compose não contém `build:`. Essa separação evita depender do suporte do
Portainer a builds em stacks e mantém a tag efetivamente utilizada explícita.

## Criar a stack no Portainer

Crie a stack `centrasa` a partir do repositório Git, usando `compose.yaml` e a
referência da release. Se o Portainer não alcançar o GitHub, use a opção de upload
com o mesmo arquivo.

No primeiro deploy, configure:

```text
CENTRASA_IMAGE_TAG=1.0.0
CENTRASA_BIND_IP=127.0.0.1
CENTRASA_ALLOWED_HOSTS=192.168.100.15;localhost;127.0.0.1
```

Confirme no Portainer que a imagem não será puxada de registry, que o container
está saudável e que o volume criado se chama exatamente `centrasa_data`.

## Migrar banco e chaves

Na origem, encerre todas as instâncias da aplicação. Confirme que não existem
`centrasa.db-wal` nem `centrasa.db-shm` e transfira a raiz completa, sem abrir ou
editar os arquivos:

```powershell
Get-ChildItem "$env:LOCALAPPDATA\CentraSA\Data"
Get-ChildItem "$env:LOCALAPPDATA\CentraSA\Keys"
scp -r "$env:LOCALAPPDATA\CentraSA\Data" "$env:LOCALAPPDATA\CentraSA\Keys" operador@srvinfra:/srv/centrasa-import/
```

O diretório de staging no host deve ser criado por Infra com modo `0700`. Depois
da transferência, pare a aplicação e importe para o volume:

```bash
sudo docker stop --time 30 centrasa
sudo docker run --rm \
  --user 0:0 \
  --entrypoint /bin/sh \
  --mount type=volume,src=centrasa_data,dst=/data \
  --mount type=bind,src=/srv/centrasa-import,dst=/import,readonly \
  centrasa:1.0.0 \
  -c 'find /data -mindepth 1 -maxdepth 1 -exec rm -rf -- {} +; cp -a /import/Data /import/Keys /data/; chown -R "$APP_UID:$APP_UID" /data'
sudo docker start centrasa
sudo docker logs --tail 100 centrasa
```

Valide primeiro por túnel SSH:

```bash
ssh -L 5180:127.0.0.1:5180 operador@srvinfra
```

Abra `http://127.0.0.1:5180`, entre com a conta já existente e confira registros
e histórico. Não existe bootstrap remoto para um banco vazio.

Após o aceite, remova o staging de forma controlada e altere no Portainer apenas:

```text
CENTRASA_BIND_IP=192.168.100.15
```

Redeploye a stack e valide `http://192.168.100.15:5180` a partir de outra máquina
da rede interna.

## Instalar backup e restauração

Copie os scripts versionados para caminhos root-owned:

```bash
cd /opt/centrasa/src
sudo install -o root -g root -m 0750 ops/centrasa-backup.sh /usr/local/sbin/centrasa-backup
sudo install -o root -g root -m 0750 ops/centrasa-restore.sh /usr/local/sbin/centrasa-restore
sudo install -o root -g root -m 0750 ops/centrasa-update.sh /usr/local/sbin/centrasa-update
sudo install -o root -g root -m 0750 ops/centrasa-rollback.sh /usr/local/sbin/centrasa-rollback
sudo install -d -o root -g root -m 0700 /var/backups/centrasa /var/lib/centrasa
```

Teste o backup manualmente:

```bash
sudo /usr/local/sbin/centrasa-backup
sudo find /var/backups/centrasa -maxdepth 2 -type f -printf '%M %u:%g %p\n'
```

O script para o container, copia todo `/data`, valida o banco, gera
`SHA256SUMS`, reinicia a aplicação mesmo após falha e conserva os sete snapshots
mais recentes.

Agende para 02:00 em `/etc/cron.d/centrasa-backup`:

```cron
0 2 * * * root /usr/local/sbin/centrasa-backup 2>&1 | /usr/bin/logger -t centrasa-backup
```

Para restaurar produção, informe somente o nome de um snapshot dentro do
diretório controlado:

```bash
sudo /usr/local/sbin/centrasa-restore centrasa-20260722T020000Z
```

O script valida checksums, cria uma cópia preventiva, restaura com o container
parado, corrige propriedade e exige healthcheck saudável. Se alguma etapa falhar,
ele tenta repor automaticamente a cópia preventiva. Não remova `pre-restore-*`
antes do aceite funcional.

## Restore drill isolado

Teste uma restauração sem tocar em produção. Substitua `<snapshot>` e `<versão>`
por valores existentes:

```bash
sudo docker volume create centrasa_restore_drill
sudo docker run --rm \
  --user 0:0 \
  --entrypoint /bin/sh \
  --mount type=volume,src=centrasa_restore_drill,dst=/data \
  --mount type=bind,src=/var/backups/centrasa/<snapshot>/data,dst=/snapshot,readonly \
  centrasa:<versão> \
  -c 'cp -a /snapshot/. /data/; chown -R "$APP_UID:$APP_UID" /data'
sudo docker run --detach \
  --name centrasa-restore-drill \
  --publish 127.0.0.1:5181:8080 \
  --env ASPNETCORE_ENVIRONMENT=Production \
  --env ASPNETCORE_URLS=http://+:8080 \
  --env Storage__DataDirectory=/data \
  --env 'AllowedHosts=localhost;127.0.0.1' \
  --mount type=volume,src=centrasa_restore_drill,dst=/data \
  centrasa:<versão>
```

Use um segundo túnel para abrir `http://127.0.0.1:5181`, valide login e dados e,
somente depois, remova o container e o volume temporários.

## Atualizar e reverter

Construa a nova tag no host e prepare a atualização:

```bash
sudo docker build --pull --tag centrasa:1.0.1 .
sudo /usr/local/sbin/centrasa-update 1.0.1
```

O script verifica a imagem, cria o backup prévio e registra a imagem atual em
`/var/lib/centrasa/previous-image`. Depois, altere
`CENTRASA_IMAGE_TAG=1.0.1` no Portainer e redeploye. Confirme healthcheck, logs,
login, leitura, escrita e persistência após restart antes de apagar imagens
antigas.

Para preparar rollback:

```bash
sudo /usr/local/sbin/centrasa-rollback
```

O script cria outro snapshot e informa a tag que deve ser recolocada no
Portainer. Se a versão nova aplicou migration incompatível, reverta a imagem e
restaure também o snapshot anterior à atualização.

## Aceite final

- container executa como usuário não-root e permanece `healthy`;
- não há restart loop e os logs estão limitados a três arquivos de 10 MB;
- login e dados migrados funcionam pelo IP interno;
- criação/edição persiste após restart do container;
- backup manual e cron preservam `Data` e `Keys`;
- restore drill isolado foi aprovado;
- equipe sabe atualizar, reverter e localizar logs no Portainer.

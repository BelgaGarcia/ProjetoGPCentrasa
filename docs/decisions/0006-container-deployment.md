# ADR 0006 — Deploy interno em container

- **Status:** aceito
- **Data:** 22/07/2026

## Decisão

Executar o CentraSA em um único container .NET 8 no host `srvinfra`, com SQLite e
chaves de Data Protection em volume Docker persistente. O serviço será exposto
somente na rede interna, por HTTP, em `192.168.100.15:5180`, e continuará sendo
operado por um único administrador por vez.

A imagem será construída e versionada no próprio host. O Portainer será
responsável pela stack, mas não pelo build da imagem.

O host fará um snapshot frio diário de todo o volume, com sete cópias locais. O
container será parado durante a cópia para manter a consistência do SQLite.

## Relação com a ADR 0005

A ADR 0005 continua válida para a aplicação: não será criado módulo, endpoint,
importador, exportador ou retenção dentro do CentraSA. O backup desta decisão é
uma automação operacional externa que aplica a mesma cópia fria e integral já
aceita, agora sobre o volume Docker.

## Consequências

- banco e chaves precisam ser migrados juntos para `/data`;
- um banco vazio não pode criar o primeiro administrador remotamente; a entrega
  depende da conta existente no banco migrado;
- o serviço permanece inadequado para multiacesso concorrente;
- HTTP é aceito temporariamente apenas na rede interna;
- snapshots no mesmo host não protegem contra perda física do `srvinfra`;
- atualização e rollback exigem tag imutável, snapshot prévio e aceite funcional;
- a atualização para .NET 10 permanece necessária antes do fim do suporte do
  .NET 8.

# Plano de atualização para .NET 10

## Janela

O projeto permanece em .NET 8 para o primeiro release. Conforme a
[política oficial de suporte](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core),
.NET 8 encerra suporte em 10/11/2026. .NET 10 é LTS ativo e tem suporte previsto
até 14/11/2028. A atualização deve ser concluída e homologada até outubro de
2026, mantendo margem para correções.

## Procedimento planejado

1. Criar uma branch exclusiva e registrar o resultado atual de `V` em Release.
2. Instalar o SDK .NET 10 suportado e atualizar `global.json` para o patch
   aprovado.
3. Alterar os quatro projetos e os dois projetos de teste de `net8.0` para
   `net10.0`.
4. Atualizar ASP.NET Core, EF Core, ferramentas de migration e pacotes de teste
   para versões 10.x compatíveis.
5. Revisar as mudanças incompatíveis das versões intermediárias e o
   [guia oficial de migração para ASP.NET Core 10](https://learn.microsoft.com/aspnet/core/migration/90-to-100?view=aspnetcore-10.0).
6. Restaurar ferramentas, compilar com warnings como erros e executar os 53
   testes em Debug e Release.
7. Validar um banco novo e uma cópia de banco existente, conferindo migrations,
   Identity, histórico e ausência de duplicação no seed.
8. Executar login, CRUD dos três módulos, reunião, impressão, cópia/restauração
   manual e screenshots sanitizados.
9. Atualizar README, imagens, matriz de suporte e notas do release antes do merge.

## Critério de conclusão

`V` deve passar nas duas configurações, o schema SQLite deve permanecer
compatível, a restauração manual deve reproduzir o estado copiado e nenhum pacote
8.x incompatível pode permanecer referenciado.

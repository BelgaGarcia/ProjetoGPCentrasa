# Limitações conhecidas

Este candidato a release foi desenhado para execução local, por um administrador
e com SQLite. Não é uma aplicação multiusuário nem um serviço de rede.

- Não há backup, importação, exportação ou retenção automática. A recuperação
  suportada é a cópia manual com a aplicação encerrada.
- A cópia do SQLite é integral: não preserva seletivamente uma conta ou somente
  um módulo e restaura o administrador para o estado da própria cópia.
- Não há anexos; os registros guardam texto, relações, datas e histórico.
- Não há cadastro público, recuperação por e-mail, roles ou múltiplos perfis.
- Tela cheia, impressão e PDF dependem dos recursos do navegador.
- O fallback sem JavaScript mantém formulários e filtros; as atualizações apenas
  deixam de ser parciais.
- A migration atual é `InitialCreate`. Compatibilidade entre esquemas históricos
  deverá ganhar teste específico quando existir uma segunda migration real.
- Screenshots oficiais são gerados com seed fictício. A regeneração automatizada
  exige Node.js 22+ e Chrome ou Edge instalados.

O projeto permanece em .NET 8. A atualização para .NET 10 está planejada e deve
ser concluída antes do encerramento do suporte dessa linha.

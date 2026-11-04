# Inicialização por usuário e elevação

Referência para a [ADR 0033](../decisoes/0033-inicializacao-e-execucao-como-administrador.md), consulta em 2026-10-24. Nenhuma análise ou modificação de binário Microsoft foi necessária.

## Fontes oficiais

- [Run/RunOnce](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys): `Run` executa a cada logon, pode ser por usuário e limita o comando a 260 caracteres. O Windows pode adiar a execução.
- [ShellExecuteW, verbo runas](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shellexecutew): o Windows pede consentimento ou credenciais para executar elevado.
- [Administrador/broker](https://learn.microsoft.com/en-us/windows/win32/secauthz/administrator-broker-model): abertura comum pode despachar um componente elevado com confirmação do usuário.

## Observado e limites

- A consulta local via `StdRegProv`, em `HKEY_USERS\<SID>` explícito, retornou código 1 ao buscar valores `XGB_P` ausentes em `Run` e `StartupApproved\Run`. `EnumValues` permite conferir presença/tipo antes de solicitar o conteúdo.
- A aplicação usa o provedor de registro do sistema e `Win32_StartupCommand`, em vez de tomar somente a visão de registro do próprio processo como prova de visibilidade.
- A leitura dos bytes conhecidos de `StartupApproved` é compatibilidade, não API pública documentada. Bytes desconhecidos são preservados; somente o Windows muda sua aprovação.
- Registro/enumerabilidade não comprovam execução no próximo logon, abertura elevada, funcionamento do controle ou visibilidade real no Gerenciador de Tarefas. Essas verificações exigem a ação do usuário nesta máquina.

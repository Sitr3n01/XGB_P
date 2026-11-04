# 0033 — Inicialização com o Windows e execução como administrador

- **Status:** Aceita
- **Data:** 2026-10-24

## Contexto

O usuário pediu duas funções em Configurações: iniciar com o Windows, podendo desativar pelo Gerenciador de Tarefas, e sempre executar como administrador. Elas são opções da prévia manual; não concluem a integração do produto com o modo Xbox. A execução elevada depende da confirmação normal do Windows.

## Decisão

### Iniciar com o Windows

- Controle em Configurações → Inicialização. Desligado por padrão; nenhuma entrada é criada por iniciar, compilar ou atualizar a prévia.
- Ao ativar, registrar `XGB_P` para o usuário em `HKEY_USERS\<SID>\Software\Microsoft\Windows\CurrentVersion\Run`, comando `"<caminho absoluto do executável>" --startup`. O SID é o do usuário atual; não há HKLM, tarefa agendada, serviço ou SYSTEM.
- Escrever/ler via `StdRegProv` na visão de 64 bits e conferir a entrada com `Win32_StartupCommand` antes de declarar sucesso. Se a enumeração não confirmar uma entrada correspondente ao comando e SID, tentar desfazer apenas o registro próprio recém-criado.
- Nunca sobrescrever uma entrada `XGB_P` de outro caminho ou de tipo inesperado. Ao desligar, remover somente o registro próprio.
- Não escrever nem apagar os bytes de `StartupApproved\Run`. O Gerenciador de Tarefas/Aplicativos de inicialização é a autoridade do Windows. Uma entrada bloqueada continua bloqueada após atualizar ou abrir o programa. Clicar nela, nessa condição, abre `ms-settings:startupapps` para reativação pelo usuário.
- Interpretar estados conhecidos 2/6 como habilitados e 3/7 como desabilitados; formatos desconhecidos são preservados e apresentados como desconhecidos. Essa interpretação não é um contrato público da Microsoft; nunca é usada para fabricar bytes de aprovação.
- Com `--startup`, revalidar que a entrada própria está habilitada e iniciar somente na bandeja, sem abrir a barra. Inicialização duplicada sai silenciosamente. Atalho e controle continuam disponíveis.

### Sempre executar como administrador

- Preferência desligada por padrão e salva atomicamente em `%LOCALAPPDATA%\XGB_P\settings\launch.json`, com `.bak`; não altera camadas de compatibilidade do Windows.
- A mudança vale na próxima abertura. O executável mantém manifesto `asInvoker`, sem `uiAccess`, para funcionar na inicialização normal e permitir desligar a preferência.
- Antes de iniciar UI, atalhos ou HID, conferir a preferência e o token. Se necessário, solicitar `runas` pelo shell do Windows, preservando `--startup`. A cópia já elevada não solicita nova elevação; o marcador `--elevated` impede repetir a tentativa se o token não tiver sido elevado.
- A instância inicial libera seu mutex antes de despachar a cópia elevada e encerra. Se o usuário cancelar o UAC, não iniciar silenciosamente uma cópia comum. Outro erro é informado.
- Desligar a preferência não remove os privilégios da instância atual: vale para o próximo início. O usuário executa e confirma a elevação; o agente não ativa nem testa UAC real automaticamente.
- As duas opções podem ser combinadas, mas a confirmação de UAC continua necessária no logon. Não há mecanismo para dispensar a confirmação, nem tarefa/serviço paralelo que ignore uma desativação no Gerenciador de Tarefas.

## Consequências

`src/lib/windows/StartupRegistration.cs` isola o registro e a enumeração independente; `LaunchSettings.cs` contém a preferência, política de abertura e interface. O estado externo do Windows é relido enquanto Configurações está visível. Os testes usam substitutos para registro, UAC e telas externas; o diagnóstico real é somente leitura.

## Referências e evidências

- [Run/RunOnce — Microsoft](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys): registro por usuário, limite de 260 caracteres e execução no logon, que pode ser adiada pelo sistema.
- [ShellExecute/runas — Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shellexecutew): confirmação UAC ao executar como administrador.
- [Modelo de broker — Microsoft](https://learn.microsoft.com/en-us/windows/win32/secauthz/administrator-broker-model): separar abertura comum e despacho elevado.
- Observado nesta máquina: `GetStringValue`/`GetBinaryValue` de `StdRegProv` retornaram 1 para `XGB_P` ausente. A implementação enumera valores antes de ler, distinguindo ausência de falha ou tipo inesperado.
- A ativação real, a exibição da entrada registrada no Gerenciador de Tarefas, o próximo logon e aceitar/cancelar o UAC real ficam para o usuário. Não confundir os testes de política ou o render WPF com essas validações.

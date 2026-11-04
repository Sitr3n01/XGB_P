# Referência: janelas abertas e foreground

Implementação local em `src/lib/windows/DesktopWindows.cs`, consumida pela prévia em `src/app/OpenApps.cs`. C# 5, .NET Framework x64, sem SDK/NuGet adicional. Não analisa nem modifica binários; não há endereço ou ID REA associado.

## Contratos públicos

- [EnumWindows — Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumwindows): enumera janelas de topo de apps desktop; não é contrato de equivalência com a lista Alt+Tab do shell.
- [GetWindow — Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindow): `GW_OWNER` identifica owner; combinado com estilos e visibilidade para filtrar auxiliares.
- [DWMWINDOWATTRIBUTE — Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute): `DWMWA_CLOAKED` (14) permite descartar janelas compostas, mas ocultas pelo shell/app.
- [SendMessageTimeoutW — Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendmessagetimeoutw): timeout de 40 ms com `SMTO_ABORTIFHUNG` na leitura de ícone; executado fora da thread da UI. Cache remove entradas quando as janelas somem.
- [ShowWindowAsync — Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-showwindowasync): envia o pedido de restauração sem esperar a thread do outro app. Retorno não comprova conclusão.
- [SetForegroundWindow — Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow): sujeito a restrições de foco. A prévia verifica o foreground até aproximadamente 900 ms; confirmação aceita a janela ou um popup com o mesmo root owner.

**Revisão da alternância, 2026-10-25 (ADR 0046):** o limite atual do alternador é **1800 ms**. A barra permanece visível até observar a entrega e confere de novo depois de ocultar; retorno booleano do pedido não comprova a conclusão assíncrona. Sem entrega direta, tenta [AttachThreadInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-attachthreadinput) com desligamento em `finally`. Até três pedidos apenas com a barra em foreground e mesmo painel/ciclo; sem teclado simulado, reabertura automática ou disputa com outro app escolhido. Os logs anteriores comprovam falhas Firefox/Claude, mas não registravam o resultado nativo; a nova instrumentação passa a fazê-lo. Evidências e limites.

## Observado em 2026-10-23

- Coleta no Windows 10.0.26300: 8 janelas elegíveis, todas com ícone local, incluindo Firefox, Codex, Configurações, Spotify, Steam, Explorer, Claude e PowerShell. Houve janelas minimizadas na coleta; contagens/estados são retratos, não inventário permanente.
- Teste com duas instâncias de um executável temporário, usando o mesmo backend: janela normal/minimizada enumerada; tool/owner/oculta/sem título filtradas; próprio processo excluído; PID incorreto rejeitado; janela minimizada em outro processo restaurada e foreground confirmado; janela fechada rejeitada. Resultado final em `bin/window-switch-check.txt`.
- Uma tentativa automática de retornar o foco quando o processo de teste já estava em segundo plano não foi confirmada nesta máquina, coerente com as restrições documentadas. O fluxo do produto parte da escolha na prévia em primeiro plano e trata a ausência de confirmação; não força o foco do Windows.
- Renderização com janelas reais é só leitura. Os testes normais substituem enumeração/ativação para verificar estados, foco e erros sem alternar os apps do usuário.

## Limites

**Revisão 2026-10-25 — ADR 0045:** `DWM_CLOAKED_SHELL` (2) não significa janela destruída. Os logs de 14:57 mostram apps de trás nessa condição no FSE. A implementação atual aceita somente essa máscara, com FSE oficial confirmado e [desktop virtual atual confirmado](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ivirtualdesktopmanager-iswindowoncurrentvirtualdesktop). Não escreve `DWMWA_CLOAK` ou move janelas entre áreas. [AppUserModelID](https://learn.microsoft.com/en-us/windows/win32/properties/props-system-appusermodel-id), chave `{9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}`, PID da propriedade 5, permite relacionar conteúdo CoreWindow a um frame sem agrupar por título/processo. Na leitura real, frame e CoreWindow de Configurações têm a mesma identidade. `Exists` distingue identidade viva/destruída/incerta, sem depender da visibilidade. Evidências e limites da validação. APIs consultadas no Windows 10.0.26300, sem REA/análise de binários nesta revisão.

**Continuidade:** a coleta somente de leitura às 19:22 encontrou 7 janelas, todas com ícone; variação em relação às 8 anteriores é esperada. Renders, inventário, resultado e fonte do teste nativo foram preservados no projeto. O teste nativo não foi repetido na etapa documental; sua receita e o ramo histórico não alcançado estão descritos nesse arquivo.

Sem prova de equivalência para todos os apps empacotados, apps elevados, áreas de trabalho virtuais e jogos exclusivos/FSE. Uma janela que só existe na bandeja não é tratada como aberta. Apps podem trocar ícones/títulos e recriar janelas; HWND/PID/classe são revalidados, mas não constituem uma identidade permanente de aplicativo.

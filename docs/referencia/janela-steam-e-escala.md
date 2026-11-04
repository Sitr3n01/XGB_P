# Janela Steam e escala — observações de 2026-10-25

Referência factual da investigação do Big Picture pequeno no modo Xbox. [Decisão](../decisoes/0039-preservar-janela-steam-e-foco.md) · continuidade completa.

## Fontes e ambiente observado

- Windows **10.0.26300**; Steam client build registrado **1792618285**. São versões da análise, não garantia para sessões futuras.
- Monitor ativo **1920×1080, 240 Hz**, AMD Radeon RX 9070 XT.
- Steam `C:\Program Files (x86)\Steam`: `logs/webhelper.txt`, `config/config.vdf`, `userdata/888398940/config/localconfig.vdf`.
- Prévia: `bin/design-preview/preview.log`. FSE conferido pela API, sem usar a presença da Steam como substituto.
- Sem REA nesta investigação; não há endereço/ID REA. Evidências vieram do código-fonte, arquivos locais e APIs públicas. Nenhum binário externo alterado.

## Cronologia observada

Horários locais UTC−03:00:

| Horário | Evidência |
|---|---|
| 11:06:16 | Prévia registrou FSE ativo; leituras posteriores confirmaram |
| 11:06:17 | Steam criou Big Picture; `1.000000 * 1.423025 = 1.420000` |
| 11:06:27 | Steam mostrou menu/tocou entrada de menu; prévia registrou PS às 11:06:27.311 |
| 11:06:38–41 | Prévia registrou LS baixo/cima e ✕; Steam registrou sons de navegação/detalhe no trecho |
| 11:06:41.163 | Prévia enviou `steam://open/bigpicture` |
| 11:07:05 | Steam mostrou `SP Desktop_uid0`, criação 1918×1030; ocultou BPM anterior |
| 11:07:59 | Steam recriou `SP BPM_uid0`; `1.000000 * 1.030466 = 1.030000` |
| 11:08:21–22 | Sons de alternância de opção Steam; áudio não determina qual opção |
| 11:29:12 | Saída FSE autorizada: S_OK/False, posterior ao incidente |
| 11:40:39 / 11:41:30 | Nova exibição/ocultação Steam, fator 1,03; não acionadas pelo agente para reproduzir a falha |
| Leitura nativa posterior | BPM minimizado, HWND 1181486/PID 24212; posição normal 312,95 / 1296×839, monitor 1920×1080 |
| 12:09:52 | `config.vdf` recebeu gravação durante a sessão; escritor não capturado |
| 12:11:58 | Publicação da correção de integração, sem abrir/restaurar Steam ou alternar FSE |

No `webhelper.txt` consultado: fator 1,42 na linha **53875**, menu simultâneo na **53918**, interface desktop na **53951**, recriação BPM na **54100** e fator 1,03 na **54117**. Rotação/edição futura pode deslocar essas linhas.

## Geometria e configuração

`AfterCreated ... 1280x800` aparece antes e depois do incidente: é tamanho de criação, não prova isolada dos limites finais. A evidência nativa posterior foi `GetWindowPlacement`: posição normal **1296×839**, com `showCmd=2` por estar minimizada. `GetWindowRect` devolveu então `-32000,-32000 / 160×28`, que não é o retângulo restaurado.

A configuração ainda identificava o display como `External: 25G3ZM|||Fullscreen-1920x1080`. Na primeira leitura, `AutoScaleFactor` e `ScaleFactor` eram aproximadamente **1,0304656**. Esse nome persistido não comprova tela cheia efetiva, nem a igualdade dos fatores comprova zoom manual. Após a gravação de 12:09:52, `AutoScaleFactor` permanecia; `ScaleFactor` não aparecia mais. Não foi identificado o escritor.

`dpiJanela=96` não representa a escala interna Steam ou a configuração global Windows. `GetWindowRect` depende do contexto DPI do observador; a posição normal de `GetWindowPlacement` tem convenções próprias. O diagnóstico novo explicita a origem das coordenadas; `ocupaMonitor` é aproximação geométrica, não confirmação de fullscreen exclusivo.

## Código auditado e interpretação

- `SceneLayout.cs` dimensiona só janela/cena WPF. A notificação de mudança de display reposiciona a prévia; não foi encontrado setter global de resolução/DPI.
- Antes, Início sempre despachava `steam://open/bigpicture`. Depois, ativa janela existente pelo alternador, evitando o protocolo quando disponível.
- `DesktopWindows.Activate` restaura somente se minimizada e pede foreground; não escreve geometria. `FocusOnly`, usado no retorno, não restaura minimizada e valida identidade.
- PS HID é compartilhado. Steam e barra reagiram simultaneamente; bloqueio/exclusividade não foi implementado.

**Observado:** transição desktop/BPM, queda do fator interno e posição de restauração pequena. **Inferido:** perda/restauração de estado de janela Steam pode explicar o sintoma e recálculo. **Não demonstrado:** comando causador, participação exata de foco/PS/protocolo ou prevenção pelo build final no FSE. A saída pela nova API aconteceu muito depois do incidente.

## Fontes oficiais consultadas

Conferidas em 2026-10-25:

- [GetWindowPlacement](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowplacement): leitura de estado/posição normal e tamanho da estrutura.
- [GetWindowRect](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowrect): virtualização DPI e limites de bordas/bounds.
- [SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow): pedido de foco sujeito às restrições Windows.

Relatórios da entrega. Teste físico posterior pendente.

# 0091 — Barra de tarefas oculta automaticamente no modo Xbox

- **Status:** Aceita. No código desde a versão seguinte à 2.1.0 (ainda não lançada); teste físico pendente, inclusive com o Start11.
- **Data:** 2026-11-04

## Contexto

O usuário usa o Start11 com a barra de tarefas aprimorada. No modo Xbox, sem a ocultação automática ligada, a barra some, mas os apps do desktop continuam deixando o espaço dela vazio. A primeira print mostra o Firefox com uma faixa vazia embaixo. Ligar a ocultação automática resolve. O usuário perguntou se basta um gatilho que ligue e desligue essa opção. Basta, e foi o que se implementou.

**Observado (somente leitura, 04/11, no desktop):**

- Monitor principal 1920×1080, área de trabalho `0,0-1920,1030`.
- `Shell_TrayWnd` (Explorer, modificado pelo Start11) ocupa `0,1030-1920,1080`.
- `ABM_GETSTATE` = 0 (sem ocultação automática); `StuckRects3` byte 8 = `0x02`.
- O Start11 tem uma ocultação própria (`S11_InternalAutohideMode=0`). O item "Ocultar barra de tarefas automaticamente" mostrado pelo usuário é do menu do Start11.
- **Inferido:** o Big Picture ficou com 3840×2159 nas sessões em que o usuário tinha a ocultação ligada, e 3840×2160 nas outras. É o pixel que o Windows deixa para revelar a barra oculta.

## Decisão

1. **Gatilho:** ao confirmar o modo Xbox pela API oficial, a barra liga a ocultação automática pela API pública de appbar (`SHAppBarMessage`/`ABM_SETSTATE`, a mesma chave das Configurações do Windows). Ao confirmar o desktop, desliga. Não há escrita no registro nem injeção.
2. **Só desfaz o que fez.** Se a ocultação já estava ligada, a barra não a assume e a saída não a desliga. Se o usuário a desligar durante o modo Xbox, a saída respeita.
3. **Recuperação:** um marcador `%LOCALAPPDATA%\XGB_P\settings\barra-tarefas-modo-xbox.txt` é gravado antes de ligar. Na abertura do XGB_P no desktop, um marcador presente desfaz a mudança de uma queda. Leitura incerta preserva o marcador para a próxima confirmação.
4. **API incerta não mexe em nada.** Isso vale para o modo Xbox indefinido e para a barra de tarefas ilegível.
5. **Opção** em Configurações → MODO XBOX, "Ocultar a barra de tarefas", **ligada por padrão** porque o usuário pediu o comportamento. O arquivo guarda só a escolha de desligar (`KeepTaskbar` em `launch.json`). Desligar durante o modo Xbox devolve na hora.
6. O log registra o estado e a área de trabalho no início e antes e depois de cada mudança, para o teste físico.

## Consequências

- **Não confirmado:** a barra do Start11 respeitar a chave do Windows. Se o menu do Start11 controlar só a ocultação própria dele, o log mostrará a área de trabalho ainda em 1030 depois da mudança.
- Se o XGB_P for encerrado no modo Xbox, a barra continua oculta até a próxima abertura no desktop.
- A verificação (`tests/TaskbarAutoHideVerification.cs`, 19 casos) substitui barra, área, modo e preferência. A barra real não é alterada nos testes.

# 0087 — Teclado automático só com o modo Xbox estável

- **Status:** Aceita; publicada na 2.0.2 ([ADR 0088](0088-versao-2.0.2-e-entrega-em-exe-e-bin-app.md)). Teste físico pendente.
- **Data:** 2026-11-01
- **Revisa:** o item 4 da [ADR 0052](0052-teclado-virtual.md) ("Abrir sozinho, só no modo Xbox").

## Contexto

Em 01/11, o usuário relatou que o teclado virtual abria sozinho num campo de texto com o PC no modo desktop e pediu que ele só abrisse no modo Xbox.

**Código (observado).** A regra da ADR 0052 já exigia o FSE confirmado. `KeyboardAutoAllowed` chamava `ActivationAllowed()`, que lê a API na hora, mas ignorava o retorno e conferia só `activationMode == true`. Com a energia em andamento (`systemPowerBusy`), `ActivationAllowed()` sai antes da leitura e `activationMode` pode ficar antigo. A retomada depois da barra (`kbSuspended`) não conferia o modo; dependia de a mudança de modo já ter limpado a suspensão.

**Logs (observado).** Foram lidos todos os `xgb.log*` de `bin/app/logs`, `.exe/logs` e das pastas de backup e candidatas, entre 27/10 e 01/11. Há 12 aberturas automáticas e uma manual pela bandeja:
- Nenhuma abertura automática aconteceu com a API informando desktop. O detector só fica ligado com o FSE confirmado ou com o teclado aberto ou pendente.
- Em 15 das 17 entradas no modo Xbox registradas, a primeira leitura de foco chega 0,2–0,75 s depois de `ativo=sim` e ainda é o app do desktop que estava na frente (Firefox, Spotify, Claude, Explorador). A Steam assume 1,8–2,2 s depois de `ativo=sim`; numa entrada, com o AnyFSE no meio, levou 12 s.
- Em **4 das 12 aberturas automáticas** esse app tinha a barra de endereço do Firefox em foco, e o teclado abriu sobre o desktop 0,3–0,5 s depois de `ativo=sim`: 27/10 16:15:15 e 16:18:09 (`bin/app`), 27/10 19:53:45 e 29/10 10:20:38 (`.exe`).
- A abertura de 31/10 11:21:10 sobre o Flight Simulator, já no desktop, foi **manual pela bandeja** (`teclado: abrir pela bandeja`) e foi fechada 1,4 s depois. A ADR 0052 permite essa abertura no desktop.
- O XGB_P em execução na análise (PID 25060, `bin/app`, desde 01/11 14:22:44) começou no desktop e não ligou o detector nem abriu o teclado.

**Inferido.** O que o usuário viu como "abrir no desktop" corresponde à transição para o modo Xbox: a API já diz FSE, mas a tela ainda mostra o app do desktop com o campo em foco.

## Decisão

1. **O modo é lido na hora e falha fechado.** O automático exige `ActivationAllowed()` verdadeiro e `activationMode == true` na mesma leitura (`KbXboxModeConfirmed`). Desktop antes da notificação, API incerta (`null`) e energia em andamento não abrem.
2. **Transição de 3 s.** `ActivationAllowed()` guarda em `activationModeSince` o instante em que o modo foi visto mudar, ou o início. Nos 3 s depois de o FSE ficar ativo, o automático não abre.
3. **Campo herdado do desktop.** Um campo editável que recebe foco nessa janela de 3 s passa a contar como fechado pelo usuário (`kbDismissedKey`), como na ADR 0052. Ele só volta a abrir o teclado depois de o foco ir para outro elemento. Isso vale mesmo quando a Steam demora mais que 3 s a assumir. O log registra `teclado: campo ignorado | entrada no modo Xbox`.
4. **A retomada do automático depois da barra confere o modo.** Fora do modo Xbox, a suspensão é descartada (`teclado: nao retomado; fora do modo Xbox`).
5. **O manual não muda.** Acesso rápido e bandeja continuam abrindo o teclado também no desktop, como na ADR 0052, porque são pedidos explícitos do usuário.

## Consequências

- Entrar no modo Xbox com um campo em foco no desktop não abre mais o teclado sobre esse app. Para digitar nesse campo já no modo Xbox, basta mover o foco para outro elemento e voltar, ou abrir pelo Acesso rápido.
- Um campo que recebe foco nos primeiros 3 s do modo Xbox não abre o teclado sozinho.
- **Não resolvido:** a saída do modo Xbox pela interface do próprio Windows nunca foi observada nos logs; todas as saídas registradas foram pelo painel da barra, com a notificação chegando 0,5–2 s depois. Se o desktop aparecer antes de a API mudar, a leitura na hora só protege depois que a API mudar.
- **Achado fora do escopo:** em 29/10 18:59:09–18:59:14, ainda no modo Xbox, o teclado abriu e fechou quatro vezes em 5 s enquanto a janela da frente alternava entre o Explorador (campo Edit em foco), o Firefox e a Steam. Cada vez que uma janela volta à frente, o Windows devolve o foco ao campo dela. Esta ADR não muda esse comportamento.

`Activation.cs` ganhou `activationModeSince`. A detecção automática (`OnTextFocus`, `KeyboardAutoAllowed`) saiu de `VirtualKeyboard.cs` para `KeyboardAutoOpen.cs`, com `KbModeSettleMs`, `KbXboxModeConfirmed`, `KbModeSettling` e a marcação do campo herdado, para manter `VirtualKeyboard.cs` dentro do limite de 1090 linhas do gate. A conferência na retomada ficou em `KeyboardTick`. `KeyboardVerification.cs` cobre a entrada no modo, o mesmo campo depois da transição, um campo novo, o desktop antes da notificação, a API incerta, a energia em andamento e a retomada depois da barra.

## Evidências

Logs analisados, bateria e build.

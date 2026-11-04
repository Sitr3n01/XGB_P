# 0080 — Emulação Xbox do DSX: Guia pelo XInput e ícones dos botões

- **Status:** Aceita; implementada e publicada em `.exe` para teste (build 15554DEF). Teste físico com o DSX em emulação Xbox pendente.
- **Data:** 2026-10-29

## Contexto

O usuário quer saber se a barra respeita os comandos e os ícones do Xbox quando o DSX emula um controle Xbox. Hoje o DSX está em emulação DualSense: o log de 29/10 mostra `Type=DUALSENSE` pelo Nefarius VirtualPad 2.63.

A barra lê o DualSense **físico** pelo HID (Bluetooth, liberado no HidHide). Ela sabe que a Steam viu o PS porque o DualSense **virtual** do DSX também recebe o PS (ADRs 0042, 0043 e 0074). Esse sinal decide abrir no aperto, com o ciclo de transferência do Steam Input.

Na emulação Xbox não existe DualSense virtual. O Guia chega à Steam pelo Xbox 360 virtual (XInput), invisível ao leitor HID. Os ícones da interface eram fixos da PlayStation:
- marcas do teclado: □ △ ✕ ○ ≡, L1 a R3 e microfone;
- dica do slider: "✕ para mutar";
- textos "PS ou Win+G".

O teste de 23/10 já tinha mostrado que, em emulação Xbox, o shell manda `GPCMD_NEXUS_*` à identidade da Game Bar. A barra já rejeitava isso para não duplicar o HID (ADR 0004).

## Decisão

1. **Comandos.** A navegação continua pelo DualSense físico, sem mudança. As posições batem: ✕ = A confirma, ○ = B volta.
2. **Guia pelo XInput.** `XInputGuideMonitor` (`src/lib/dualsense/XInputGuide.cs`) lê `XInputGetStateEx` (ordinal 100 do `xinput1_4.dll`), só leitura.
   - O Guia vira um evento `PS` do dispositivo `XInput#n`. Esse dispositivo não é `-BT`, então conta como **PS visto pela Steam**: abre 40 ms depois do aperto, com o ciclo da Steam.
   - Se o controle sumir, isso conta como perda, nunca como soltura.
   - Slots conectados são lidos a cada 8 ms. Slots vazios, só a cada 2 s, porque consultá-los é lento.
   - Na emulação DualSense não há XInput, e o leitor fica só na varredura lenta.
3. **Ícones.** Nova opção em Configurações → CONTROLE: **Ícones dos botões**, que alterna entre Automático, PlayStation e Xbox e fica salva em `appearance.json`.
   - O Automático usa Xbox quando há controle XInput.
   - A conversão: ✕→A, ○→B, □→X, △→Y, L1/R1→LB/RB, L2/R2→LT/RT, L3/R3→LS/RS, PS→"Botão Xbox".
   - Options/Menu e o microfone mantêm o desenho. O touchpad não tem equivalente.
   - A troca vale para o teclado, a dica do slider, a seção Abertura e as dicas que citam o PS.

## Consequências e limites

- Um controle Xbox real também passa a abrir a barra pelo Guia e aciona o Automático. Isso é um efeito colateral aceitável e coerente.
- O aviso "PS chegou também pelo virtual" continua valendo para o XInput.
- Sem liberação no HidHide, a emulação Xbox deixa a barra sem nenhum controle, porque não há virtual DualSense de reserva. Os caminhos `.exe` e `bin/app` estão liberados. Uma candidata em `bin/candidatos` não está.
- O ordinal 100 não é documentado. Ele é o mesmo já usado em `tools/monitorar-botao-guia.ps1` e recebe uma estrutura com folga de 4 bytes.

## Evidências

Evidências:
- `icones-xinput-verificacao.txt`, com 20 verificações;
- `ativacao-verificacao.txt`, com os casos do Guia XInput junto do físico, a soltura e a desconexão;
- render `configuracoes-icones-xbox`.

Nenhuma troca de modo do DSX foi feita pelo agente.

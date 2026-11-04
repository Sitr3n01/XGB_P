# 0085 — ✕ no slider do mixer não silencia

- **Status:** Aceita; publicada na 2.0.0 ([ADR 0086](0086-versao-2.0.0-e-entrega-em-exe.md)). Teste físico pendente.
- **Data:** 2026-10-29
- **Revisa:** [ADR 0013](0013-som-compacto-e-mixer-integrado.md), que fazia Enter/✕ no slider alternar o mute.

## Contexto

A [ADR 0013](0013-som-compacto-e-mixer-integrado.md) definiu que Enter/✕ com o foco num slider do Som alterna o mute. A [ADR 0047](0047-midia-intencoes-fontes-e-retorno-dos-sliders.md) tirou esse gesto dos sliders do player e manteve o mixer como estava.

Em 29/10, o usuário relatou que, ao clicar no slider de volume de um app no painel Som, o app era silenciado, sendo que o botão de mute já fica ao lado. A análise encontrou a causa em `ActivateAudio()`: com o foco num slider do mixer, o ✕/Enter chamava `ToggleAudioMute`, e a dica do slider dizia "Enter / ✕ para mutar". O clique do mouse não passava por esse caminho; o gesto que silenciava era o ✕ do controle.

## Decisão

1. **✕/Enter no slider do mixer não executa comando.** Vale para som geral, microfone e apps: não muta, não muda o volume, não toca o som de seleção, e o foco continua no slider.
2. **O mute fica só no botão próprio**, à esquerda de cada slider. O volume continua em ←/→ (5 pontos por passo) e no mouse.
3. **A dica do slider mostra só o nome** do dispositivo ou app.
4. Os sliders do player ([ADR 0047](0047-midia-intencoes-fontes-e-retorno-dos-sliders.md)) e o do microfone no painel Gravação já não executavam comandos no ✕ e não mudam.

## Consequências

`ActivateAudio()` foi removido de `AudioPanel.cs`, e `Activate()` em `DesignPreview.cs` não trata mais os sliders do mixer. Quem usava o ✕ como atalho de mute agora precisa ir ao botão ao lado.

`AudioPanelVerification.cs` passou a exigir que o ✕ no slider do microfone e no de um app não enfileire mute nem volume e mantenha o foco no slider.

## Evidências

Candidata e gate.

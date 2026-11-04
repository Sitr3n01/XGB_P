# 0012 — Painel de Som funcional na prévia

- **Status:** Aceita
- **Data:** 2026-10-23

## Contexto

O usuário pediu um painel completo de som com volume master, volume do microfone, mute de saída/entrada, acesso às configurações do Windows, escolha de microfone e mixer por aplicativo. A prévia já possui navegação real para a Steam.

## Decisão

**Revisão:** layout e agrupamento por processo/dispositivo abaixo foram atualizados pela [ADR 0013](0013-som-compacto-e-mixer-integrado.md), que integra o mixer à tela principal e unifica linhas por aplicativo.

Criar um painel **Som** separado de **Jogo atual**, acessível pelo trilho, pelo atalho Áudio da Home e pelo painel do jogo. Usar Core Audio do Windows em C# 5, sem SDK adicional, NuGet, drivers ou serviços.

- `IAudioEndpointVolume`: nível e mute dos dispositivos padrão de saída e entrada.
- `IAudioSessionManager2` / `ISimpleAudioVolume`: sessões de aplicativos em todas as saídas ativas, agrupadas por processo e dispositivo. Cada grupo tem nível e mute.
- `IMMDeviceEnumerator`: dispositivos e padrões atuais.
- `IPolicyConfig.SetDefaultEndpoint`: escolha de saída/microfone. **Interface não documentada**, isolada e com fallback para as configurações oficiais. A seleção explícita aplica os três papéis (console, multimídia e comunicações), conforme indicado na tela. Em falha parcial, tentar restaurar os papéis anteriores e informar falhas de reversão.
- `ms-settings:sound`: abrir Som do Windows.

Criar e liberar COM em workers MTA, sem guardar interfaces COM na UI. Atualizar o estado por consulta, agrupar eventos rápidos de sliders e não disparar setters ao carregar/sincronizar o painel. Ler volumes não abre uma captura de microfone.

## Consequências

- O painel deixa de simular som: ajustes do usuário afetam o Windows e podem persistir após encerrar a prévia. Usar os mesmos controles ou Som do Windows para desfazer.
- O gesto microfone curto/PS do controle permanece intocado; o DualSense continua sendo lido via HID sem relatórios de saída.
- Apps aparecem quando possuem sessões Core Audio, inclusive inativas ainda existentes. O mesmo app em saídas diferentes tem controles separados. Uma seleção de dispositivo padrão não substitui a preferência explícita de um aplicativo.
- A aceitação da dependência privada para escolha de dispositivos resolve a decisão S7; compatibilidade e troca efetiva para outros dispositivos continuam sujeitos à validação de runtime.
- S1/S2 em jogos e a stack final continuam em aberto. A captura de áudio não é parte deste recurso.

## Evidências

- Pedido do usuário nesta conversa em 2026-10-23.
- [Design e uso](../design/som.md), [referência técnica](../referencia/audio-windows.md).
- `src/lib/audio/CoreAudio.cs`, `src/app/AudioPanel.cs`.
- Leitura real de 4 saídas, 3 entradas e 12 sessões na coleta inicial. Setters de volume/mute testados com os mesmos valores e leitura de confirmação; padrões reaplicados aos mesmos IDs, sem troca de dispositivo.

# 0030 — Trilho em descanso e sons da interface no estilo do Xbox

- **Status:** Aceita
- **Data:** 2026-10-24

**Refinamento atual:** a [ADR 0031](0031-coesao-dos-sons-da-interface.md) revisa o roteamento, a mistura entre canais, a duração da seleção e o liga/desliga. O comportamento descrito abaixo registra a implementação original.

## Contexto

O usuário pediu dois ajustes de UX:

- com o painel focado, a aba lateral (trilho) deve perder o foco de forma sutil, seguindo as animações, por exemplo diminuindo de tamanho;
- sons na interface, como no Xbox, usando o repositório [FireXDragon007/XBOX-ONE-audioloader-pack](https://github.com/FireXDragon007/XBOX-ONE-audioloader-pack).

**Observado no repositório** (commit `0871da8`):

- é um pacote do AudioLoader para o Big Picture: sons do Xbox One/Series com os nomes dos eventos da interface do Steam Deck;
- o README diz "ALL SOUNDS OWNED BY MICROSOFT-XBOX", e o repositório não tem licença;
- WAV PCM 48 kHz, estéreo, 16 bits;
- a maioria começa com 80–120 ms de silêncio;
- `navigation`, `slider_up` e `slider_down` são o mesmo arquivo;
- `deck_ui_toast.wav` não é um WAV válido.

## Decisão

### 1. Trilho em descanso

Com o foco dentro do painel, os ícones do trilho ficam a 90% do tamanho e a 55% de opacidade. Ao voltar o foco ao trilho, ou com o mouse sobre ele, voltam a 100%.

- A transição usa a duração normal das animações (167 ms) e o mesmo cancelamento.
- Com movimento reduzido, a mudança é imediata, sem animar.
- Cada ícone encolhe no próprio lugar: é uma escala própria depois da escala de foco, então o trilho não se desloca.
- Focos de passagem, como a janela durante uma reconstrução, não mudam o estado, o que evita piscar.

### 2. Sons da interface

| Gesto | Som (arquivo do pacote) |
|---|---|
| abrir a barra | `deck_ui_side_menu_fly_in` |
| fechar a barra (Win+Shift+G, Esc/◯ no trilho, clique fora, MUTE segurado) | `deck_ui_side_menu_fly_out` |
| mover o foco ou ajustar um controle deslizante | `deck_ui_navigation` |
| selecionar (clique, Enter, ✕ do controle) | `deck_ui_default_activation` |
| voltar sem fechar a barra | `deck_ui_hide_modal` |
| trocar de painel (L1/R1, Q/E) | `deck_ui_tab_transition_01` |
| X de fechar app aparece (segurar 2 s) | `deck_ui_show_modal` |
| iniciar um jogo (no lugar do som de selecionar) | `deck_ui_launch_game` |

- **Só gestos tocam som.** Foco programático, reconstruções e um gesto sem efeito (borda da lista, controle deslizante no limite) ficam mudos.
- **Arquivos só locais**, em `%LOCALAPPDATA%\XGB_P\sons`, trazidos por `scripts/baixar-sons.ps1`. O script fixa o commit e confere o SHA-256 de cada arquivo. Os sons pertencem à Microsoft e **não entram no repositório**, no mesmo cuidado da [ADR 0002](0002-identidade-do-pacote-da-game-bar.md). Sem os arquivos, a barra fica muda.
- **Reprodução** pelo `waveOut` do Windows (`src/lib/audio/UiSounds.cs`):
  - um canal por som, então sons diferentes se misturam e o mesmo som reinicia;
  - o silêncio do início é cortado na carga (até 114 ms);
  - volume de 70%;
  - sem NuGet e sem arquivos temporários.
- **Configurações → SONS → "Sons da interface"** liga e desliga, só para a sessão, como Reduzir movimento.

### Revisão (mesma data, pedido do usuário)

- **Volume dos sons:** um slider logo abaixo de "Sons da interface", no padrão dos volumes do painel Som:
  - vai de 0 a 100%, com 70% de início, e anda de 5 em 5 pela esquerda/direita;
  - vale só para a sessão;
  - o buffer de cada som é refeito na hora, sem reabrir os arquivos, e o som de navegação do próprio ajuste já sai no volume novo;
  - com os sons desligados ou sem os arquivos, o slider fica inativo e a navegação o pula.
- **Seção de mídia sem som de interface:** no player da Home e no do Som, nada toca ao selecionar, andar entre os controles, ajustar a posição ou o volume, nem ao levar o foco para dentro do player. Sair dele para outra seção toca normalmente.

## Consequências

- Os sons aparecem no mixer do Windows e no da própria barra como uma sessão de áudio "XGB_P", onde o volume deles pode ser ajustado.
- Uma troca do aparelho de áudio que derrube um canal reabre os sons na próxima abertura da barra.
- Em outra máquina, ou depois de apagar a pasta, é preciso rodar `scripts/baixar-sons.ps1` de novo.
- A escolha dos sons por gesto é de design e pode ser trocada no mapa `SoundFiles` (`src/app/Sounds.cs`).

## Evidências

- `--verify` (funcional):
  - `VerifyRailFocus`: trilho inteiro com foco nele, em descanso com foco no painel, foco de passagem sem efeito, ida e volta pelo controle, e render `trilho-em-descanso.png`;
  - `VerifySounds`: cada gesto com o seu som uma vez, reconstruções mudas, voltar no trilho só com o som de fechar, jogo no lugar de selecionar, desligar/religar, o slider de volume e a seção de mídia muda na Home e no Som. Sons e comandos de mídia registrados, nada tocado;
  - render `configuracoes-sons.png`.
- `--verify-sounds-live`: carrega os 8 arquivos locais e confere o volume refeito nas amostras: pico de 20091 a 100%, 10046 a 50% e 0 a 0%. Depois toca cada som com volume zero até o fim, sem som audível.
- **Não comprovado:** a impressão sonora real. O agente não tocou os sons de forma audível.

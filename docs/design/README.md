# Design v1 da Game Bar do XGB_P

**Revisão de UX de 2026-10-25:** continuidade consolidada, com listagem/foco de apps no FSE, animação de segurar, mídia, abertura opcional por Win+G no desktop e persistência de jogos. Inclui fontes, evidências, builds/backups e testes reais pendentes.

**Animações:** as cinco sprints Xbox mais expressivo estão implementadas na prévia; ver [catálogo, movimento reduzido e validação](animacoes.md), ADR 0022.

> **Status:** em andamento (fase 2). As decisões de UX já tomadas estão na [ADR 0007](../decisoes/0007-escopo-e-interacao-da-v1.md). Os pontos técnicos ainda abertos estão nos [spikes](#spikes-técnicos-antes-do-protótipo).

**Interface WPF atual:** execução independente, com **PS/Win+G somente no Xbox/FSE**, conforme a [ADR 0037](../decisoes/0037-ps-e-wing-somente-no-modo-xbox.md). Como validar e continuidade atual. Steam/Configurações, Som, player, alternância de janelas, Desempenho, jogos/recentes, Conexões, Luz noturna, DSX e Lojas têm backends reais. Energia permanece simulada; jogos e foco em uso real exigem validação. A ferramenta não conclui os spikes em jogos.

## Princípios

**Direção visual:** seguir a linguagem do guia Xbox, conforme as referências do usuário: fundos preto/cinza neutro, texto branco e verde como destaque padrão. Recursos e cores em [paleta.md](paleta.md), decisão na [ADR 0009](../decisoes/0009-linguagem-visual-xbox.md).

1. **Feita para a TV:** leitura a 3 m de distância, poucos itens por tela, foco sempre visível.
2. **O controle primeiro:** tudo funciona só com o DualSense. Nenhuma ação exige mouse ou teclado.
3. **Rápida de entrar e de sair:** abrir, fazer uma coisa e voltar ao jogo em poucos toques.
4. **Preservar a Steam no desktop:** PS/Win+G só têm efeito no XGB_P com FSE confirmado; HID continua compartilhado, sem bloquear a Steam (ADR 0037).
5. **Só no modo Xbox:** no desktop, o XGB_P não aparece (ADR 0005).

## Layout

```
┌──────────────────────────────────────────────────────────────────── TV 16:9 ──┐
│                                                        (rede) (DS)[▮▮▯]  21:47 │
│ ┌────┐ ┌──────────────────────┐                                                │
│ │[⌂] │ │ Início               │                                                │
│ │ ▤  │ │ ▸ Início             │                (jogo ao fundo)                 │
│ │ ── │ │   Voltar ao jogo     │                                                │
│ │ ♪  │ │   Apps abertos ▢ ▢ ▢ │                                                │
│ │ ◔  │ │   Recentes ×3        │                                                │
│ │    │ │   ♫ Player de mídia  │                                                │
│ │ ⚙  │ │  Acesso rápido ▢▢▢▢▢▢│                                                │
│ │ ⏻  │ │                      │                                                │
│ └────┘ └──────────────────────┘                                                │
└────────────────────────────────────────────────────────────────────────────────┘
```

- **Trilho à esquerda:** ícones dos painéis, com foco destacado. **Início** abre a [Home própria](home.md), com recentes e utilidades (ADR 0010). **Biblioteca** continua sendo uma ação direta para o Big Picture. Ordem: Início, Biblioteca, divisor, Som e Desempenho no topo; **Configurações e Energia presos ao pé do trilho, com Energia por último**. O painel **Jogo atual** saiu do trilho (ADR 0016): o jogo fica na Home, em "Voltar ao jogo".
- **Rolagem:** barra fina do Windows 11 em todo o app, sobreposta ao conteúdo: linha de 3 px que engrossa para 6 px sob o mouse ou no arraste (ADR 0016).
- **Painel:** cerca de 30% da largura da tela, à direita do trilho. O jogo continua visível ao fundo.
- **Sem textos decorativos:** o cabeçalho do painel é só o título. Nada de sobretítulo, frase de efeito ou nota fixa de rodapé. O rodapé do painel aparece apenas com o retorno de uma ação (erro, simulação, "Aplicando…") ou com o nome do ícone em foco na Home; vazio, ele some. Marcadores de dado simulado (`· EXEMPLO`, `· SIMULAÇÃO`) continuam enquanto os dados forem de exemplo.
- **Faixa de status no topo, como no guia do Xbox (ADR 0018):** à direita, numa só linha, os ícones de rede (Ethernet, ondas do Wi-Fi pelo sinal, alerta sem internet) e do DualSense com a bateria (preenchimento, raio ao carregar, alerta baixo) e, à direita deles, a hora grande (46 px, Segoe UI). O pé dos ícones fica na linha de base dos dígitos. Sem texto de estado; o detalhe fica na dica e no painel Controle e energia.
- **Conexões em Configurações (ADR 0027):** Wi-Fi e Bluetooth com ligar/desligar e páginas de redes salvas e dispositivos pareados. A página Wi-Fi mostra só as redes salvas em alcance. As ações abrem logo abaixo da linha, e esquecer, remover e desligar com risco pedem confirmação.
- **Tela e Controle em Configurações (ADR 0028):** Luz noturna (ligar/desligar) e um atalho para o DSX. Os botões de fechar apps e jogos têm contorno vermelho com o mouse ou o seletor.
- **Lojas (ADR 0029):** atalho no Acesso rápido da Home que abre um painel com as lojas e launchers instalados. O painel fica sob o Início no trilho.
- **Camada acabada (ADR 0023):** sem nome do projeto, selo de prévia, botão de encerrar ou faixa de dicas. A barra fica à esquerda, no centro vertical da tela, e tem a altura do conteúdo do painel (no mínimo a do trilho, no máximo a área segura), crescendo e encolhendo a partir do centro. A distância da borda esquerda é a mesma margem segura da hora. A cena segue a escala e a resolução da tela. Os botões de controle ficam documentados no guia.

## Estados

| Estado | O que aparece | Como entra / sai |
|---|---|---|
| **Oculta** | nada | estado padrão |
| **Interativa** | trilho + painel + faixas, com o foco no XGB_P | no FSE: PS/Win+G abre/fecha; ○ no trilho ou "Voltar ao jogo" fecham |
| *Persistente* (futuro) | widgets fixados (ex.: FPS) sem foco | fora da v1 |

**Entrada visual atual (ADRs 0010/0011):** a prévia inicia na **Home**, com foco em **Início do Big Picture** (rótulo visível "Início"), acima de **Voltar ao jogo** no cenário de exemplo. Ao reabrir a prévia, o painel selecionado é preservado. A Home do produto deverá adaptar a área de jogo atual quando não houver jogo rodando; esse estado ainda depende da integração real.

## Mapeamento do controle (estado Interativo)

**Estado atual (ADRs 0036/0037):** direcional/analógico esquerdo navegam, direito rola e gatilhos percorrem listas. **PS/Win+G só abre/fecha no modo Xbox/FSE; desktop e API incerta ficam inativos.** A abertura por microfone foi substituída. Ativação e evidências, mapeamento da navegação e guia. A interface continua separada do ativador/núcleo empacotado final.

| Botão | Ação |
|---|---|
| Direcional / analógico esquerdo | move o foco |
| ✕ | seleciona |
| ○ | volta (painel → trilho → fecha) |
| L1 / R1 | painel anterior / próximo |
| Microfone segurado | sem ação no XGB_P |
| Microfone curto | **não é tratado**: continua sendo mute (DSX ou sistema) |
| PS | abre/fecha no FSE; sem ação no XGB_P no desktop |

## Painéis da v1

| Painel | Conteúdo | Fonte técnica (a validar nos spikes) |
|---|---|---|
| **Início / Home** | jogo atual, apps abertos, três recentes, player de mídia e utilidades por ícone | catálogo e observador de jogos reais (ADR 0019); Steam, alternador de janelas (ADR 0014) e player via SMTC (ADRs 0015/0016) ativos na prévia. Ver [home.md](home.md) |
| **Biblioteca** (ação) | biblioteca no Big Picture | `steam://open/bigpicture/games` |
| **Som** | player de mídia no topo, volume master, microfone, mute de saída/entrada, seleção de dispositivos em lista flutuante, Som do Windows e mixer de apps | controles reais de Core Audio e `IPolicyConfig` (ADR 0012); player via SMTC (ADR 0016). Ver [som.md](som.md) |
| **Desempenho** | modo de energia do Windows; CPU e GPU (uso, temperatura, clock, consumo), VRAM, memória (uso, MT/s, timings), FPS e frametime | na prévia: HWiNFO (memória compartilhada), RTSS e reservas do Windows; modo pela API pública de `powrprof.h` (ADR 0017). Ver [desempenho-windows.md](../referencia/desempenho-windows.md). **FPS próprio sem injeção continua no S5** |
| **Controle e energia** | bateria e carregamento do DualSense; suspender, reiniciar, desligar, sair do modo Xbox | bateria: relatório de entrada HID (S6); energia: `SetSuspendState`, `shutdown`; sair do modo Xbox: `SetGamingFullScreenExperience(FALSE)` |
| **Configurações** | acessos ao Windows e à Steam; duração do toque segurado, painel inicial, sobre | `ms-settings:` e `steam://open/settings` ativos na prévia; preferências ainda simuladas |

A ação **Início do Big Picture**, acima do jogo atual, usa `steam://open/bigpicture`. Biblioteca usa `steam://open/bigpicture/games` no modo Xbox ou `steam://open/games` no desktop/API indisponível. **Downloads** usa `steam://open/downloads`. **As três navegações estão ativas na prévia** (ADR 0011 e sua ampliação). Áudio, Desempenho e Energia abrem os respectivos painéis; ○/Esc retorna ao atalho na Home. Os recentes usam catálogo/histórico reais desde a ADR 0019; ver [guia](../guias/jogos-recentes.md).

**Ficam fora da v1:** capturas, widgets persistentes e a parte social/Xbox.

## Comportamento por modo

| Entrada | Modo Xbox | Modo desktop |
|---|---|---|
| Microfone segurado | abre/fecha o XGB_P | ignorado |
| `ms-gamebar://hotkey/?ihkid=IHKID_GAME_GUIDE_OVERLAY` (Win+G) | abre/fecha o XGB_P | **ignorado** |
| `IHKID_CAPTURE_GAME_SCREENSHOT` / `IHKID_TOGGLE_GAME_RECORDING` | ignorado na v1 (sem capturas) | **ignorado** |
| `GPCMD_NEXUS_*` (Guia, só com emulação Xbox 360) | ignorado: o DSX fica em emulação DualSense | ignorado |
| `gamingFullScreen=shellReady` | inicia o núcleo residente | — |
| `gamepadConnect` | registrado no log | registrado no log |

## Spikes técnicos antes do protótipo

**Situação (2026-10-23):**
- **S1 e S2 estão pausados.** A primeira tentativa de construir a janela de teste foi interrompida por um filtro de segurança do agente, e a abordagem precisa ser repensada com o usuário.
- **S3 e S6 estão em andamento:** ver spikes-s3-s6.md.

| # | Pergunta | Como testar | Bloqueia |
|---|---|---|---|
| **S1** | Uma janela nossa aparece **por cima de jogos** no modo Xbox? Compare topmost comum com `uiAccess` | janela de teste sobre 2 ou 3 jogos reais (janela sem borda, tela cheia, flip model) | layout inteiro |
| **S2** | Com a barra aberta, o jogo continua recebendo o controle? | abrir a janela de teste com o jogo rodando e apertar botões | navegação |
| **S3** | Como o núcleo inicia e fica residente no modo Xbox (`shellReady`, tarefa de inicialização, primeira URI)? | sonda + entrada no modo Xbox com o AnyFSE instalado | residência |
| **S4** | Stack da UI (WinUI 3, WPF, outra), considerando foco do controle, transparência e tempo de abertura | protótipo descartável do trilho + painel | implementação |
| **S5** | FPS do jogo sem injeção (ETW de present / PresentMon) e se exige administrador | medir 1 jogo | painel Desempenho |
| **S6** | Bateria: offsets no USB/virtual (`0x01`) e no Bluetooth (`0x31`), e se o virtual do DSX repassa | `DualSenseReader` registrando o byte de status | painel Controle |
| **S7** | Dependência privada para seleção de áudio aceita pela ADR 0012; interface validada com os padrões atuais | faltam testes de troca para dispositivos diferentes e compatibilidade em jogos | robustez do painel Som |

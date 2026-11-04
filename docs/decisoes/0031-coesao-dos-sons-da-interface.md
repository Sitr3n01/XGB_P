# 0031 — Coesão dos sons da interface

- **Status:** Aceita
- **Data:** 2026-10-24

> Revisão de 27/10 pela [ADR 0061](0061-som-continuo-na-navegacao-do-trilho.md): a navegação direcional pelo trilho toca `mover` a cada mudança efetiva de foco, inclusive ao trocar painel. L1/R1, foco programático e reconstruções continuam silenciosos. As regras abaixo sobre silêncio ao trocar painel pelo foco são históricas.

## Contexto

O usuário ouviu uma combinação estranha ao abrir Lojas e autorizou corrigir os padrões identificados. No código anterior, Lojas acionava apenas a seleção genérica, de 881 ms; não havia um segundo pedido de abertura da barra nesse caminho. Cada efeito tinha seu canal independente e podia se misturar com os seguintes. Religar os sons pedia a seleção duas vezes. O teste antigo registrava pedidos, mas não verificava exclusividade entre canais reais.

## Decisão

**Revisão por instrução do usuário, na mesma data:** o efeito `deck_ui_tab_transition_01.wav` foi retirado do mapa ativo. Depois de identificar os botões que perderam também o clique, o usuário pediu **som de navegação nos 11 botões**: Início/Som/Desempenho/Configurações/Controle e energia do trilho; Lojas/Áudio/Desempenho/Energia do acesso rápido; Redes Wi-Fi/Dispositivos Bluetooth das Configurações. Esses cliques usam somente `mover` (`deck_ui_navigation.wav`, 50 ms), sem seleção adicional. Trocar painel pelo foco no trilho ou por L1/R1 continua sem som de transição. O WAV local de transição permanece preservado. A abertura da barra inteira mantém `deck_ui_side_menu_fly_in.wav`.

1. **Um efeito por ação:** os botões executam o callback antes de emitir o som. A ação pode substituir a seleção por transição, mostrar conteúdo, voltar ou lançar jogo. Não se toca primeiro a seleção para depois interrompê-la. Uma exceção não deixa efeito pendente.
2. **Um efeito ativo:** `UiSounds.Play` encerra os efeitos anteriores, sem fila. O gesto seguinte substitui o anterior, inclusive navegação rápida, voltar e fechar. A música e o áudio dos outros aplicativos não são afetados.
3. **Seleção curta:** o ataque do WAV original fica limitado a 160 ms, com redução gradual de amplitude nos últimos 20 ms. A derivação acontece apenas em memória. Não há edição, cópia para o repositório ou substituição dos WAVs locais.
4. **Significado consistente:**

| Ação | Efeito |
|---|---|
| Abrir/fechar a barra inteira | `abrir` / `fechar` |
| Trocar painel por L1/R1 ou foco no trilho | silencioso |
| Clicar/ativar um dos cinco painéis do trilho | somente `mover` |
| Clicar/ativar Lojas, Áudio, Desempenho ou Energia na Home, Redes Wi-Fi ou Dispositivos Bluetooth nas Configurações | somente `mover` |
| Abrir lista de áudio/energia, menu de conexão ou confirmação solicitada | `lista` |
| Recolher lista/menu ou cancelar pelo botão, Esc ou círculo | `voltar` |
| Iniciar jogo | somente `jogo`, após o despacho aceito |
| Acionar um botão comum | `selecionar`, curto |
| Mover foco/ajustar slider com mudança efetiva | `mover`; se o foco trocar o painel, silencioso |

5. Desligar os sons interrompe todos imediatamente; religar emite exatamente uma confirmação. **Persistência revisada pela [ADR 0032](0032-persistencia-dos-sons-da-interface.md):** volume e liga/desliga agora são salvos no perfil local.
6. Foco programático, atualizações e reconstruções continuam mudos. Pedidos semânticos de ações não tocam fora de um callback de botão. O player de mídia mantém o silêncio definido na ADR 0030.

O mapa da ADR 0030 permanece com sete efeitos ativos, excluindo a transição entre painéis. A referência de categorias é a [documentação de som da Microsoft](https://learn.microsoft.com/en-us/windows/apps/develop/ui/sound): acionar, mostrar/ocultar, navegar entre vistas, voltar e foco sutil. A aplicação dessas categorias e os 160 ms são escolhas do XGB_P, cuja impressão sonora ainda exige escuta do usuário.

## Consequências

O feedback de um gesto rápido pode interromper a cauda do anterior. Não há espera por uma sequência antiga. O roteamento fica em `Sounds.cs`, junto com a configuração, e a animação em `WireMotion` deixa de emitir cliques genéricos. As ações dos backends não mudam.

## Evidências

- Bateria completa em `bin/sons-coesao`: suítes funcional/temporal, dois testes de pausa e um de fechar nos processos de teste, nove diagnósticos de leitura, todos com saída zero.
- Regressões de Lojas, três caminhos de painel, subpáginas, listas, menu/confirmação/cancelamento, falha de callback, lançamento sem seleção anterior, liga/desliga e player mudo.
- Na primeira leitura a pasta local de sons estava ausente (causa desconhecida), apesar de a prévia anterior manter oito efeitos na memória. O script existente repôs os oito arquivos do mesmo commit, com todos os SHA-256 conferidos.
- O diagnóstico real foi repetido após a reposição: oito efeitos, seleção de 160 ms, 64 pares de reprodução com no máximo um canal ativo e parada total. Volume zero, sem som audível.
- Registro da entrega.

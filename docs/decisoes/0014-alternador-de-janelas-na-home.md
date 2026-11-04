# 0014 — Alternador de janelas na Home

- **Status:** Aceita. A identificação no rodapé ao focar foi revisada pela [0015](0015-player-de-midia-na-home.md): o nome passa ao lado do título da seção. O Acesso rápido passou a ter seis ícones com Lojas, pela [0029](0029-lojas-pelo-acesso-rapido.md).
- **Data:** 2026-10-23

## Contexto

O usuário pediu ícones dos programas/apps abertos na Home, como uma barra de tarefas para alternar de forma simples, e acessos rápidos sem texto abaixo dos ícones.

## Decisão

- Inserir uma faixa de **Aplicativos abertos** abaixo de Voltar ao jogo, com um ícone por janela elegível. Incluir minimizadas e distinguir várias janelas pelo título em tooltip, nome acessível e rodapé ao focar.
- Consultar janelas via `EnumWindows`, descartando própria prévia, ocultas/cloaked, ferramentas, sem título, shell e auxiliares com owner sem `WS_EX_APPWINDOW`. Não substituir a lista por processos em execução.
- Extrair ícones locais com timeout em `WM_GETICON`, fallback para classe/executável e símbolo genérico. Copiar/liberar HICON e manter apenas PNG gerenciado no snapshot.
- Atualizar ao abrir a barra e aproximadamente a cada 2 segundos na Home visível. Preservar posições durante navegação, foco e scroll; retornar de outros painéis por tags estáveis.
- Ao selecionar: validar HWND/PID/classe, solicitar restauração/foreground enquanto a prévia ainda tem foco e então ocultá-la. Confirmar depois; em indisponibilidade/erro/bloqueio, mostrar mensagem na prévia. Não usar injeção, hooks, `AttachThreadInput` ou simulação de Alt+Tab.
- Acesso rápido: uma linha com cinco ícones, sem rótulos visuais, mantendo tooltips/nomes acessíveis e indicação no rodapé ao focar.
- Refinamento solicitado na mesma data: setas vetoriais sobrepostas às bordas esfumadas, sem colunas reservadas; viewport na largura completa, ícones/espaçamentos alinhados, contador de janelas, foco fora da região esfumada e paginação sem ativar apps. Ver estados e renders em [home.md](../design/home.md).

## Consequências

A Home ganha alternância funcional no desktop, sem abrir/fechar processos. Não há nova configuração persistente, pacote ou serviço. A lista não promete equivalência exata ao Alt+Tab do shell; alguns apps/áreas de trabalho virtuais podem ter outra representação. Restrições normais de foreground do Windows continuam válidas.

Jogos/recentes continuam exemplos; essa integração não resolve detecção de jogo nem valida overlay/FSE em jogos.

## Evidências

- Pedido do usuário nesta conversa.
- [Design e validação](../design/home.md), [APIs e limites](../referencia/janelas-windows.md).
- Testes de UI com comandos substituídos e render de 8 janelas reais com ícones.
- Teste nativo com janelas temporárias: filtros, PID, restauração/foreground em outro processo e janela encerrada.

# 0093 — Rede e bateria ao lado do painel nas telas da Steam

- **Status:** Aceita. Visual aprovado em 04/11; ligado no código desde a versão seguinte à 2.1.0 (ainda não lançada). Teste físico pendente.
- **Data:** 2026-11-04

## Contexto

O Big Picture já mostra a hora no canto superior direito, e a faixa de status da barra fica no mesmo canto. O usuário pediu que, só nas telas da Steam, o relógio da barra fique oculto e que rede e bateria fiquem ao lado do painel, na altura do título (a área marcada em vermelho na segunda print), para não ficarem desconexos.

## Decisão

1. **Quando:** a janela da frente, na abertura, é o Big Picture (`DesktopWindows.IsSteamBigPicture`: título e processo da pasta da Steam), em qualquer tela dele. Leitura incerta mantém a faixa padrão.
2. **O quê:** o relógio fica oculto. Os ícones de rede e bateria vão para uma âncora sem tamanho no canto superior direito do painel, a 24 DIP da borda e centrados na linha do título. A âncora não muda a largura da barra e acompanha a animação de entrada.
3. Fora da Steam, inclusive com um jogo da Steam na frente, a faixa continua no canto com a hora.

## Consequências

- A verificação (`tests/SteamStatusVerification.cs`, 13 casos) gera as prévias em 1080p, 4K e no painel Som (`status-steam-*.png`, dados de teste), confere posição, alinhamento, área segura e o retorno ao padrão.
- Até a aprovação, o recurso foi entregue desligado (2.0.3 de 17:27/18:09). Ao ligar, a chave de aprovação saiu do código e a verificação passou a conferir também a abertura sobre outra janela, sem janela e com leitura incerta (16 casos).

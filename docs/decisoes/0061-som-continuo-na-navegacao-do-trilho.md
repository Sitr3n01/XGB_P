# 0061 — Som contínuo na navegação do trilho

- **Status:** Aceita
- **Data:** 2026-10-27

## Contexto

O usuário relatou som intermitente ao navegar pela barra lateral e pediu investigação antes de corrigir. `Navigate` exigia que o painel não mudasse para emitir `mover`. O foco no trilho troca o painel imediatamente, exceto na Biblioteca e no Início sob Lojas: isso tornava movimentos equivalentes audivelmente diferentes. A suíte antiga exigia silêncio na troca de painel, conforme a ADR 0031.

Preferência real ligada/30%, WAVs presentes e sete canais carregados no log do residente. Não houve falha sonora registrada; isso não substitui escuta física. Os quatro fontes envolvidos conferiam com o metadado do executável publicado `8356A71E…`.

O usuário autorizou a correção e pediu commit antes dela. Checkpoint local `45efed5`, com o trabalho anterior em fontes/documentação; sem push.

## Decisão

`Navigate` emite um único `mover` após uma mudança efetiva de foco ou valor, independentemente de o painel ter mudado. Continua usando o gesto direcional do usuário como origem; não se adiciona som a `GotKeyboardFocus` nem a `SetPanel`.

- Navegação direcional no trilho: um som curto por movimento, incluindo Biblioteca, troca de painel e volta circular.
- Confirmar/clicar no painel: mantém o efeito único existente.
- L1/R1: continuam silenciosos.
- Foco programático, reconstruções, gestos sem mudança e player de mídia: continuam silenciosos.
- Liga/desliga, volume, arquivos locais e exclusividade de reprodução: preservados.

## Consequências

A mudança é no roteamento da prévia WPF. Não restaura o WAV de transição nem altera Steam, HID, foco ou configuração de áudio. A primeira versão de lançamento e a integração final continuam pendentes.

## Evidências

`VerifyRailNavigationSounds` percorre dois ciclos completos em cada sentido (24 movimentos), confere destino/painel e um único som; verifica seis focos programáticos, gesto sem movimento, 12 mudanças por L1/R1 e duas passagens sob Lojas. A suíte existente mantém cliques, player silencioso, volume e liga/desliga.

Continuidade e resultados. Os testes de roteamento usam sons registrados; a escuta com DualSense/FSE/TV permanece pendente.

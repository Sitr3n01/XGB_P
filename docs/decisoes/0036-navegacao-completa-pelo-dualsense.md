# 0036 — Navegação completa da barra pelo DualSense

- **Status:** Aceita
- **Data:** 2026-10-25

## Contexto

> **Abertura revisada pela ADR 0037:** PS/Win+G apenas no Xbox/FSE; microfone não abre e desktop fica inativo. Navegação/analógicos e Biblioteca permanecem.

O direcional funcionava, mas os analógicos não eram extraídos dos relatórios HID. A prévia recebia o virtual do DSX normalmente; o bloqueio do físico pelo HidHide não causava a falta do analógico. O usuário autorizou implementar a navegação completa da barra, sem gestos de toque nem sensores de movimento.

## Decisão

- Ler os quatro eixos e os dois gatilhos nos relatórios USB/virtual, Bluetooth completo e Bluetooth simples. Preservar botões, gestos e bateria; validar limites antes de acessar o buffer.
- Analógico esquerdo move o foco e ajusta os controles existentes. Zona morta por eixo de 40%, saída a 28%, um eixo dominante por vez e margem de 25% para trocar de eixo numa diagonal. O direcional digital tem prioridade quando usado junto; soltar uma fonte preserva a outra.
- Repetir o foco após 420 ms, a cada 130 ms; analógico direito rola em passos de 64 DIP, após 250 ms e a cada 80 ms. Apenas mudanças de direção geram eventos no worker HID, evitando uma chamada ao dispatcher por relatório.
- Gatilhos ativam a 32/255 e soltam abaixo de 20/255. L2/R2 recuam/avançam cinco controles habilitados da lista atual por aperto, sem circular nos limites. Em listas flutuantes, continuam dentro delas.
- Manter ✕ (selecionar/segurar app por 2 s), ○ (voltar) e L1/R1 (trilho/painéis). **Refino pedido após o primeiro teste físico:** Biblioteca participa como parada de foco em ambos os sentidos do ciclo, inclusive L1/R1. Ela preserva o painel aberto e só abre a Steam ao confirmar com ✕; analógico no centro mantém o foco. Triângulo vai à Home; Options a Configurações; Quadrado/R3 ao trilho; L3 ao primeiro controle do painel. Clique do touchpad alterna trilho/painel ou fecha a lista flutuante primeiro. Atalhos cancelam escolhas pendentes sem executá-las.
- **Revisar a exceção da prévia da ADR 0008:** com o processo manual em execução, microfone segurado por 600 ms também abre a barra oculta. Fecha quando ela está em foco; se estiver visível sem foco, traz para frente. Win+Shift+G e bandeja permanecem. Isso não cria núcleo residente nem instala/integra o pacote e não muda a política do produto no desktop.
- Navegação comum continua exigindo janela visível e em primeiro plano. Perda de foco, ocultação e desconexão limpam repetição e segurar. Eventos anteriores à limpeza são descartados; atraso de mais de um segundo cancela o estado de entrada.
- PS, Create e microfone curto não executam ações na barra. Não simular teclado/mouse nem bloquear os inputs enviados a jogos/Steam. Não enviar relatórios de saída, ativar modos HID, vibração, LED ou gatilhos adaptativos. DSX/Steam continuam responsáveis pelas saídas.

## Consequências

`src/lib/dualsense/DualSenseHid.cs` publica eventos `LS_*` e `RS_*` somente nas transições. `ControllerInput.cs` concentra roteamento, repetição, atalhos e rolagem. `ControllerVerification.cs` cobre relatório → tracker → interface, relógio/foco substituídos, fontes simultâneas, limites e listas. A suíte faz parte de `--verify` e da bateria completa.

Mapeamento e evidências atuais em continuidade-controle-2026-10-25.md. Não há instalação ou nova preferência. A navegação por controle não prova isolamento dos inputs em jogos nem a integração no modo Xbox.

## Fontes e validação

Layout conferido no [driver oficial de DualSense do SDL](https://github.com/libsdl-org/SDL/blob/main/src/joystick/hidapi/SDL_hidapi_ps5.c), estruturas `PS5SimpleStatePacket_t` e `PS5StatePacketCommon_t`, consultado em 2026-10-25; implementação própria sobre o leitor existente. Sem análise de binários ou uso de REA nesta alteração.

Relatórios sintéticos e interface WPF substituem comandos externos na regressão. A continuidade separa esses testes das entradas físicas efetivamente observadas.

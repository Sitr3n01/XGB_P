# Requisitos do XGB_P · V.2.1.0

Barra de jogo para o modo Xbox do Windows 11, feita para o Steam Big Picture e o DualSense via DSX. Uso pessoal, uma configuração-alvo, sem promessa de compatibilidade universal.

## Controle

| Entrada | Ação |
|---|---|
| PS (modo Xbox) | Abre e fecha a barra |
| Direcional e analógico esquerdo | Navegam |
| Analógico direito | Rola |
| Confirmar (✕) | Ativa |
| Círculo | Volta; no trilho, fecha a barra |
| L1 / R1 | Percorrem o trilho de painéis |
| L2 / R2 | Paginam as seções aplicáveis |

A leitura HID é compartilhada e somente leitura: nenhum relatório de LED, vibração ou gatilhos é enviado. Cada movimento efetivo no trilho toca um som curto; foco programático e L1/R1 ficam em silêncio ([ADR 0061](decisoes/0061-som-continuo-na-navegacao-do-trilho.md)). Os ícones dos botões seguem o controle (PlayStation ou Xbox) ou a escolha do usuário ([ADR 0080](decisoes/0080-emulacao-xbox-guia-xinput-e-icones.md)).

## Recursos

- **Início:** todos os jogos abertos em *Voltar ao jogo*, o atual primeiro, com pausar e fechar; aplicativos abertos, com segurar para fechar; três recentes fechados; player de mídia; acesso rápido. Sem caixas de dica na Home ([ADR 0064](decisoes/0064-jogos-abertos-no-grupo-voltar-ao-jogo.md), [ADR 0034](decisoes/0034-descricoes-com-atraso-e-estilo-windows11.md)).
- **Biblioteca:** Big Picture em *Coleções → Todos* ([ADR 0054](decisoes/0054-biblioteca-do-big-picture-na-colecao-todos.md)).
- **Jogos:** abrir, retomar, pausar e fechar pela identidade do processo, com confirmação e recuperação persistida; troca de jogo que espera a saída e o fim da sessão na Steam.
- **Som e mídia:** dispositivos, volume e mute, mixer por app, microfone, fontes, posição e transporte. ✕ no slider não silencia; o mute fica no botão ao lado ([ADR 0085](decisoes/0085-slider-do-mixer-sem-mute.md)).
- **Gravação:** captura do Radeon ReLive, microfone do Windows e últimas capturas com miniaturas; foto aberta no Fotos maximizada no modo Xbox ([ADR 0081](decisoes/0081-aba-amd-captura-pelo-relive.md), [ADR 0084](decisoes/0084-miniaturas-e-fotos-no-modo-xbox.md)).
- **Desempenho:** sensores do HWiNFO, FPS do RTSS e modo de energia. Dado ausente aparece como indisponível ([ADR 0017](decisoes/0017-desempenho-funcional-e-modo-de-energia.md)).
- **AMD:** ajustes gráficos globais pelo ADLX, relidos depois de cada mudança ([ADR 0082](decisoes/0082-painel-amd-ajustes-pelo-adlx.md)).
- **Configurações:** Wi-Fi e Bluetooth, Luz noturna, DSX restaurado pela bandeja, bateria física do DualSense, ícones dos botões, inicialização com o Windows, administrador, sons, movimento reduzido, histórico de atividade e saída do modo Xbox.
- **Energia:** suspender, reiniciar e desligar com segunda confirmação.
- **Teclado virtual:** janela sem foco que digita no app da frente; abre sozinho só no modo Xbox estável, também pelo acesso rápido e pela bandeja ([ADR 0052](decisoes/0052-teclado-virtual.md), [ADR 0087](decisoes/0087-teclado-automatico-so-com-modo-xbox-estavel.md)).

Não há botão de preferência sem efeito nem ação simulada no fluxo normal. Dados de teste e efeitos substituídos existem só na build de verificação.

## Dependências

| Dependência | Obrigatória | Uso |
|---|---|---|
| Windows 11 x64 com modo Xbox | Sim | Ambiente-alvo; o app também roda no desktop |
| .NET Framework 4.8 | Sim (vem com o Windows) | Runtime |
| Steam com Big Picture | Sim | Tela inicial do modo Xbox, jogos e navegação |
| DSX e DualSense | Sim | Controle; o executável precisa estar liberado no HidHide |
| AMD Software: Adrenalin Edition | Não | Painéis Gravação e AMD |
| HWiNFO e RTSS | Não | Métricas do painel Desempenho |
| Ponte local de ativação | Não | Win+G; não distribuída ([ADR 0002](decisoes/0002-identidade-do-pacote-da-game-bar.md)) |

Sons da interface são opcionais: o app toca arquivos WAV locais em `%LOCALAPPDATA%\XGB_P\sons` quando existem e fica em silêncio quando não existem. Nenhum som é distribuído.

## Restrições

Sem modificar ou injetar código em binários da Microsoft ou de terceiros, sem alterar proteções do sistema e sem contornar anti-cheat. Falha de identidade ou permissão preserva os processos e explica o erro. O usuário confirma o UAC e faz instalações, certificados e configuração do HidHide.

## Critério de aprovação

Uma versão só é aprovada depois que cada caso físico aplicável tiver resultado registrado: modo Xbox, Steam e controle, jogos reais, áudio, teclado, bateria, TV e DPI, ações de energia e uma sessão longa na build final ([roteiro](guias/primeiros-testes-lancamento.md)). O gate automático não certifica FPS, compatibilidade universal nem energia real.

# 0083 — Ícones da Gravação, ordem do trilho e interruptores comuns

- **Status:** Aceita; implementada em candidata de desenvolvimento, não publicada. Teste físico pendente.
- **Data:** 2026-10-29

## Contexto

O usuário pediu melhores ícones no painel Gravação e um interruptor comum para todos os ajustes de liga/desliga. A referência visual fornecida mostra uma cápsula cinza com o indicador escuro à direita quando ativada, seguindo o estilo do Windows 10. Após a primeira prévia, pediu mais cuidado no ícone de repetição, blocos de captura somente com ícones e a troca de posição entre Desempenho e AMD. A ordem final dos recursos principais é Som → Gravação → Desempenho → AMD.

## Decisão

1. **Gravação:** substituir os glifos dos comandos por vetores próprios sobre uma grade de 24 DIP, com espessura consistente. Repetição usa apenas um arco circular com seta, sem o triângulo sobreposto no centro; foto usa câmera; gravar/parar usam círculo e quadrado vermelhos; GIF usa película; repetição no jogo usa tela com reprodução. A câmera de vídeo do trilho, os selos da galeria e o microfone também usam vetores. Os blocos de captura mostram **somente ícones**, de 32 DIP, centralizados numa altura mínima de 72 DIP. Nome e explicação ficam nas dicas e na acessibilidade. O cronômetro permanece na linha de estado, sem duplicação no bloco de parar.
2. **Trilho:** Som → Gravação → Desempenho → AMD. Início e Biblioteca continuam no topo; Configurações e Energia ficam na base. Direcional, L1/R1 e direção das animações usam a mesma ordem, mantendo os IDs existentes dos painéis.
3. **Interruptores:** `SettingToggle.cs` fornece uma única linha acionável por mouse, teclado e controle, com nome, estado e cápsula de 40 × 20 DIP. Ativado: cinza preenchido e indicador escuro à direita. Desativado: contorno claro e indicador à esquerda. O foco continua com o destaque do Windows utilizado pelo projeto.
4. **Aplicação global aos ajustes binários:** Gravação, dez recursos gráficos AMD, Wi-Fi, Bluetooth, Luz noturna, inicialização, administrador, Win+G no desktop, sons, teclado automático, sons das teclas e movimento reduzido.
5. **Estado e acessibilidade:** `IToggleProvider` expõe ligado, desligado ou indeterminado aos leitores de tela. O acionamento utiliza o callback existente; o visual não antecipa a confirmação. Durante aplicação mantém o estado observado com “Aplicando…”. Indisponibilidade e bloqueios do Windows mantêm suas mensagens específicas. A linha é o único ponto de foco.
6. **Fluxos existentes:** confirmações de Wi-Fi/Bluetooth, persistência, verificação de efeito pelo ReLive e releitura do ADLX continuam nos mesmos caminhos. Mute de áudio e transporte de mídia continuam como ações de reprodução; o painel Gravação recebe o microfone vetorial.
7. **Entrega:** candidata isolada, ainda 1.0.8, marcada em desenvolvimento. `.exe`, `bin/app` e a referência publicada são preservados. Nenhum comando real de captura, ajuste gráfico ou alteração de configuração é necessário para validar esta revisão visual.

## Consequências

Os ajustes usam “Ativado”/“Desativado” de maneira uniforme. Valores como duração da repetição, faixa do Chill e limite de FPS continuam aparecendo junto ao estado. As mensagens de operação mantêm o texto existente. Os ícones de captura deixam de depender do desenho dos glifos Segoe MDL2.

A compilação passa a referenciar `UIAutomationProvider.dll`, já integrante do .NET Framework instalado, para expor o padrão acessível de alternância. Não há dependência nova para instalar.

## Evidências

Revisão final: captura só com ícones e nova ordem. Primeira prévia e interruptores. Os renders usam dados substituídos; a bateria automática não comprova o teste físico de controle/FSE.

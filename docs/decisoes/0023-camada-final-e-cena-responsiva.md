# 0023 — Camada final sem marcas da prévia e cena responsiva

- **Status:** Aceita
- **Data:** 2026-10-24

## Contexto

O usuário pediu um refino da prévia, a partir de uma captura em 1920 × 1080 a 100%:

- tirar da tela o nome do projeto, o selo "PRÉVIA DO DESIGN" e o botão "Encerrar prévia";
- tirar a faixa de botões de referência (✕/Enter, ○/Esc, L1/R1, Win+Shift+G), para que a barra pareça uma camada acabada;
- fazer o programa se adaptar à escala e à resolução da tela, com espaçamento melhor em relação aos cantos.

A cena era fixa em 1920 × 1080 e ficava dentro de um `Viewbox` que cobria a área de trabalho. Em telas com outra proporção, aparecia uma faixa de cada lado. Na máquina do usuário, que tem área de trabalho de 1920 × 1030, a faixa era de cerca de 44 px por lado (**observado**). A escala do Windows não tinha efeito, e a barra ficava centralizada com 820 de altura, longe das bordas.

## Decisão

1. **Camada final:** a tela mostra só a faixa de status e a barra (trilho e painel). Saíram o título, o selo, a faixa de dicas e o botão de encerrar. Encerrar continua disponível no menu do ícone na bandeja e em Configurações. As dicas de controle ficam no guia.
2. **Janela:** cobre o monitor principal inteiro, como a Game Bar, inclusive sobre a barra de tarefas enquanto está aberta. A posição é recalculada a cada abertura e quando a resolução ou a escala mudam (`SystemEvents.DisplaySettingsChanged`).
3. **Cena responsiva** (`src/app/SceneLayout.cs`): a cena tem a proporção da janela, então o `Viewbox` só aplica a escala, sem faixas.
   - Uma unidade da cena é 1 DIP, ou seja, a interface segue a escala do Windows.
   - A cena só é reduzida quando a tela efetiva fica abaixo de **1024 × 880**: abaixo disso, a barra de 820 de referência e as margens não cabem.
   - A cena só é ampliada acima de **1440** de altura efetiva (por exemplo, 4K a 100% fica 1,5×), para manter a leitura a distância.
4. **Margem segura única:** 3% do lado menor da cena, entre 24 e 48 unidades (32 em 1080). A barra fica no canto superior esquerdo e a faixa de status no canto superior direito.
   - **Revisão do mesmo dia:** a barra com a altura total deixou um espaço livre grande na Home, e o usuário pediu para corrigir. Agora a barra tem a **altura do conteúdo do painel**, no mínimo a do trilho e no máximo a área segura, e a mudança de altura é animada na troca de painel (167 ms).
   - O Som cresce até o limite e, acima dele, rola só o mixer. A altura já não depende da resolução: a Home tem a mesma altura em todas as telas.
   - **Segunda revisão (02:55):** a barra fica à esquerda e no **centro vertical** da tela. Ela cresce e encolhe a partir do centro conforme o painel, inclusive durante a animação de altura. A hora continua no canto superior direito. Evidências.
5. **Alinhamento óptico:** a faixa de status sobe o espaço interno da fonte, para que o topo dos dígitos da hora fique na mesma linha do topo da barra.

Medidas internas sem mudança: trilho de 92, intervalo de 16, painel de 480, fontes e tempos de animação. A entrada lateral parte de `-(margem + largura da barra)`.

## Consequências

- Não sobra espaço vazio dentro dos painéis. O espaço que sobra fica abaixo da barra, sobre o jogo. Painéis curtos (Energia) ficam na altura do trilho, e Configurações e Energia continuam presos ao pé do trilho.
- A barra muda de altura ao trocar de painel. "Reduzir movimento" e animações desativadas no Windows tornam a troca imediata.
- Com a escala do Windows em 150% numa tela 1080p (área efetiva de 1280 × 720), a interface fica 1,25× em pixels físicos, e não 1,5×, para caber sem rolagem na Home.
- O processo continua com reconhecimento de DPI no nível do sistema. Se a escala mudar com a prévia aberta, o layout se ajusta, mas o Windows estica a imagem até a prévia ser reiniciada. Não há suporte por monitor; a camada usa só o monitor principal.
- Clicar fora da barra e da faixa de status oculta a camada. O vão entre trilho e painel não oculta.
- O texto "XGB_P · Design v1 / Protótipo visual local" e a ação "Encerrar prévia" continuam no painel Configurações, que é conteúdo e não moldura da camada.

## Evidências

- `--verify` verifica oito telas efetivas: 1080p a 100/125/150%, 1440p, 4K a 100%, ultrawide 3440 × 1440, 1280 × 800 e retrato 1080 × 1920. Para cada uma, confere:
  - a escala e a ausência de faixas;
  - a margem, com a barra e a faixa de status nos cantos e separadas;
  - a Home sem rolagem e sem sobra, com a mesma altura em todas as telas.

  Confere também o painel curto na altura do trilho e o Som cheio parando na margem, com rolagem só no mixer.
- `--verify-motion` confere que a cena acompanha a janela em 1080p e em 4K e que a altura anima na troca de painel e volta a seguir o conteúdo.
- Renders, tabela de escalas e identidade do build.
- **Não comprovado:** troca real de resolução ou de escala com o processo aberto, TV, multimonitor e input físico.

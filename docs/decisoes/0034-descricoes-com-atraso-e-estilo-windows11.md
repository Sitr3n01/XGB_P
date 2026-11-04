# 0034 — Descrições com atraso e estilo Windows 11

- **Status:** Aceita
- **Data:** 2026-10-24

## Contexto

O usuário mostrou a dica clara, pequena e retangular do WPF sobre um jogo recente e pediu fonte melhor, espera de três segundos e apresentação coerente com o Windows 11. O conteúdo suplementar e os nomes acessíveis dos controles devem ser preservados.

## Decisão

**Revisão posterior do usuário:** ocultar essa caixa em **toda a Home**, por redundância. O painel Início (`panel == 0`) não agenda nem apresenta dicas, seja por mouse ou foco; a regra inclui trilho e status enquanto a Home está aberta. Retornar à Home fecha imediatamente qualquer dica do painel anterior. Os textos ToolTip/nomes acessíveis permanecem nos controles. Nos outros painéis, a apresentação abaixo continua ativa.

- Substituir a apresentação nativa de `ToolTip` pela camada `DescriptionOverlay`, na própria cena WPF. Não alterar os textos `ToolTip` dos controles nem seus rótulos/nomes acessíveis.
- Mostrar depois de **3000 ms** no mesmo alvo, tanto com foco de teclado/controle quanto com mouse. Mudar de alvo ou agir reinicia o prazo; mover dentro do mesmo alvo não o reinicia.
- Fonte `Segoe UI Variable` com fallback `Segoe UI`, corpo de **16 DIP**, peso normal e entrelinha de **22 DIP**. Os 16 DIP são escolha do projeto para legibilidade, seguindo a escala da cena/Windows, não tamanho oficial extraído do sistema.
- Fundo opaco neutro `#2B2B2B`, texto `#F2F2F2`, borda `#454545`, sombra discreta, padding 12×8 e raio **4 DIP**. Recursos centralizados no XAML. O verde continua no foco do controle.
- Largura máxima de 360 DIP e quebra de linha; posicionar abaixo do alvo ou acima se não houver espaço, limitado às margens da cena. Não capturar cliques, foco ou comandos do controle.
- Fechar ao ocultar/fechar/desativar a barra, sair com o mouse, rolar ou perder o alvo. Não manter dicas de conteúdo rolado para fora da viewport.
- Reconstruir o mesmo alvo por backend conserva a espera quando tag, seção e texto são os mesmos. Adotar a nova instância visual; não guardar referência a um controle removido.
- Rótulos primários, o nome da utilidade no título e retornos de ação continuam imediatos. Somente a dica suplementar usa a espera. Não há som de entrada nem animação nova.

## Consequências

`Descriptions.cs` centraliza timer, entrada por foco/mouse, cancelamento, posicionamento e verificação. A aparência compartilha a escala e a árvore da prévia, permitindo renders fiéis. Não é um componente WinUI nativo, Mica ou Acrylic; é a aplicação dessas diretrizes ao WPF já usado.

## Referências e evidências

- [Tooltips — Microsoft](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/tooltips): informação suplementar, foco/hover e texto conciso; informação essencial deve estar na interface.
- [Geometria — Microsoft](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/geometry): ToolTip é a exceção dos overlays e usa raio de 4 px.
- [Tipografia — Microsoft](https://learn.microsoft.com/en-us/windows/apps/design/style/typography): referência da família visual do Windows; medidas e cores acima são escolhas explícitas do projeto.
- `VerifyDescriptions`: 2999 ms sem dica e 3000 ms com dica, mouse/foco, troca/repetição do alvo, ação, reconstrução da própria instância e de conteúdo alheio, rolagem, ocultação e limites da cena. Relógio substituído; renders da árvore WPF.
- A sensação na TV e os eventos físicos de mouse/controle continuam para validação do usuário. Teste determinístico não comprova a entrada física.

# 0053 — Design do teclado baseado no Windows

- **Status:** Aceita pelo usuário; implementação verificada, teste físico pendente
- **Data:** 2026-10-26
- **Complementa:** ADR 0052. Revisa a ADR 0009 somente no foco do teclado.

## Contexto

O usuário rejeitou a primeira proposta de refino, com teclas arredondadas e foco verde, e enviou uma captura do teclado do Windows como referência. Pediu uma proposta antes da implementação e aprovou a imagem apresentada: teclas retangulares, cinza uniforme, seleção com contorno duplo cinza, ícones discretos e indicações do DualSense.

## Decisão

- Aplicar o desenho aprovado na árvore WPF: painel escuro com contorno fino e cantos de 8 DIP; teclas planas com cantos de 2 DIP, letras brancas e números no alto à esquerda. Espaço sem texto ou sublinhado, Tab por extenso e setas laterais em chevron.
- Seleção com contorno externo cinza de 3 DIP, separado da borda escura da tecla. Modificadores ativos usam cinza mais claro e um pequeno traço branco; Caps tem também uma marca sob a seta. Não usar verde no teclado; a paleta da barra permanece como antes.
- Usar vetores WPF de 24 unidades, com espessura uniforme, inclusive para Windows, apagar, Enter, Shift, microfone e botões do DualSense. Não usar a imagem da proposta como textura da interface.
- Cabeçalho com engrenagem, marcador central e fechar. Engrenagem abre a seção existente do teclado nas Configurações, suspendendo o teclado pelo fluxo da barra; ao voltar ao mesmo app ele é retomado. O marcador alterna em cima/embaixo, como R3.
- Faixa de atalhos com ícones, sem botões em cápsulas ou rótulos permanentes; Letras/Completo mantém seu texto. Nomes acessíveis e tooltips identificam os comandos. Sem rodapé de legendas.
- O status normal fica oculto. Avisos, campo de senha e espera da Steam continuam visíveis no cabeçalho; o status interno mantém o destino, sem conteúdo digitado.
- Cabeçalho e atalhos são linhas navegáveis. Suas posições nos dados correspondem ao desenho, inclusive o espaço vazio entre os atalhos e Completo. As quatro linhas principais continuam com 13 unidades; o mapeamento dos botões, envio, integridade, abertura automática e política da Steam são os da ADR 0052.

## Consequências

`KeyboardVisuals.cs` concentra vetores e estados visuais; `KeyboardLayout.cs` descreve também o cabeçalho e os intervalos; `VirtualKeyboard.cs` monta a janela e integra a engrenagem com as Configurações. O script de compilação inclui o novo fonte. A interface continua sendo uma janela sem ativação, sem saída HID.

## Evidências

Continuidade e estado de publicação, proposta aprovada, renders e relatórios. A suíte do teclado tem 91 verificações com substitutos, incluindo abrir as Configurações e retomar. Renders adicionais cobrem Caps, Ctrl ativo, hover e áreas de trabalho menores. A aparência em TV e o uso físico no modo Xbox continuam pendentes.

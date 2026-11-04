# 0084 — Miniaturas corretas e apresentação de fotos no modo Xbox

- **Status:** Aceita; implementada em candidata de desenvolvimento, não publicada. Teste físico do Fotos no FSE pendente.
- **Data:** 2026-10-29

## Contexto

A [investigação](../referencia/capturas-miniaturas-fotos-audio-2026-10-29.md) confirmou a inversão vertical na conversão das miniaturas. O usuário também encontrou o Fotos numa janela menor sobre fundo preto no modo Xbox. Depois, mostrou os canais de gravação AMD já em **Estéreo** e autorizou aplicar as correções restantes.

## Decisão

1. **Miniaturas:** usar `GetDIBits` para obter pixels de 32 bits com orientação top-down explícita. Eliminar a leitura direta de `bmBits` e o `RotateFlip` baseado no sinal da altura do cabeçalho. Preservar alpha quando disponível e tornar opaco o byte reservado inteiramente zerado dos HBITMAPs opacos. HBITMAPs de outros formatos também podem ser convertidos. Recursos GDI são liberados em `finally`.
2. **Fotos:** somente ao abrir um arquivo de imagem pelo painel de capturas, com FSE confirmado pela API, solicitar abertura maximizada pelo shell. Vídeo, pasta e AMD Software seguem sua abertura habitual. Desktop e API incerta não recebem ajustes de janela.
3. **Observação limitada:** após a abertura, aguardar por no máximo quatro segundos a janela em primeiro plano. Antes de ajustar, exigir:
   - pacote/AUMID do Microsoft Fotos e executável dentro da pasta desse pacote em WindowsApps;
   - título exatamente igual ao nome do arquivo solicitado;
   - HWND, PID e criação de processo conhecidos e estáveis;
   - janela visível e não minimizada, com modo Xbox ainda confirmado.
4. **Apresentação:** uma solicitação de maximização; se o shell a ignorar, no máximo uma tentativa de ocupar o monitor da própria janela. A segunda tentativa usa `SetWindowPos` assíncrono, sem ativação, mudança de ordem, topmost ou alteração de estilo. Coordenadas são obtidas e aplicadas sob contexto DPI temporário por monitor, restaurado ao fim da chamada.
5. **Confirmação:** somente uma observação posterior que ocupe o monitor conclui a operação com sucesso. Falha/timeout deixa aviso no histórico. Não insistir indefinidamente nem anunciar sucesso com base no retorno da solicitação.
6. **Cancelamento:** reabrir a barra, sair do FSE, API incerta, nova operação, outra foto, outra janela/app, mudança de identidade ou encerramento do XGB_P interrompem o ajuste. Nenhum foco é disputado pelo novo fluxo. Não há observador global que redimensione outros programas.
7. **Áudio:** manter a configuração Estéreo já mostrada pelo usuário. Não alterar Adrenalin, dispositivos ou vídeos antigos; os arquivos 7.1 existentes continuam originais.
8. **Entrega:** candidata local separada, ainda 1.0.8; manter `.exe`, `bin/app`, referência e residente anterior. Sem commit/push ou reativação automática do FSE.

## Consequências e limites

`WindowFocus.cs` e suas regras de transferência de foco continuam sem ajuste de geometria. A exceção desta ADR está isolada em `CapturePhotoWindow.cs` e `CapturePhotoPresentation.cs`, vinculada a uma escolha de foto do usuário.

A política falha fechada se a identidade, o título ou o pacote do Fotos não forem reconhecidos. Outro visualizador padrão pode receber a solicitação inicial de abertura maximizada, mas não recebe o ajuste posterior específico do Microsoft Fotos.

A confirmação é geométrica, não uma certificação do renderizador ou modo exclusivo. O comportamento real do Fotos com o shell em FSE precisa de teste físico na candidata; o agente não reativa o modo para verificar.

## Evidências

Candidata, testes e miniaturas reais. Fixtures usam bitmaps GDI criados pelo teste e substitutos de modo/janelas/abertura. Foto e vídeo reais foram consultados somente por leitura para conferir a orientação.

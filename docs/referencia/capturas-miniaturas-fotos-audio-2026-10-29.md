# Investigação: miniaturas invertidas, Fotos no modo Xbox e vídeos sem som

**Implementação posterior autorizada:** miniaturas e apresentação de fotos corrigidas pela [ADR 0084](../decisoes/0084-miniaturas-e-fotos-no-modo-xbox.md); candidata e resultados. O restante deste documento preserva os resultados da investigação anterior à correção. O usuário mostrou a gravação AMD já em Estéreo.

Data: 29/10/2026. Escopo autorizado: investigar antes de corrigir. Candidata em uso: `bin/candidatos/XGB_P-1.0.8-20261029-162003`, SHA-256 `56C9A34984A78A10D5512B305624F7B5B728929A45091C69BC06E33C1B8603A1`.

Nenhum fonte do aplicativo, binário publicado, configuração AMD/Windows, modo Xbox ou arquivo original foi alterado. Foram criadas sondas locais de leitura e um controle de áudio de três segundos, separado dos originais, em `bin/investigacao-capturas-20261029`. Uma janela de foto foi aberta pelo Explorer para comparação. As tentativas de fechamento normal por teclado/clique não removeram essa janela da lista observada; não houve encerramento forçado. A foto de comparação permaneceu aberta no Fotos.

## 1. Miniatura invertida — causa confirmada na conversão da barra

O PNG `Neva_2026.10.29-16.29.png` está correto. A extração da miniatura pelo shell seguida de `Image.FromHbitmap` também produz a orientação correta. A implementação atual de `src/lib/amd/CaptureThumbnails.cs` interpreta os bits e aplica `RotateNoneFlipY` quando `dib.Header.Height > 0`.

Uma sonda compilada com esse mesmo fonte reproduziu exatamente a miniatura invertida. Na mesma captura:

- HBITMAP: 256 × 144, 32 bits, stride 1024; altura do cabeçalho 144.
- Diferença média RGB entre a barra e a conversão GDI: **78,979**.
- Diferença entre a barra e a conversão GDI invertida verticalmente: **0,000**.

Portanto, o sinal da altura lido por esse caminho não justifica o flip que o produto aplica à imagem do shell observada. Não é uma captura invertida produzida pela AMD. A correção deve normalizar a conversão do HBITMAP, sem girar os arquivos. Para uma implementação robusta, `GetDIBits` permite solicitar explicitamente uma saída top-down com altura negativa; ainda não implementado. [Contrato da API](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-getdibits).

Comparação e sonda.

## 2. Fotos em janela menor no modo Xbox — comportamento delimitado, gatilho exato pendente

O usuário confirmou **janela menor com fundo preto**. Os logs mostram Fotos aberto pela barra às 16:30:06 e 16:30:17, enquanto o modo Xbox estava ativo; o usuário saiu do modo às 16:30:48.

O Fotos instalado é `Microsoft.Windows.Photos` **2026.11090.22001.0**, com aplicativo `Photos.exe` e entrada `Windows.FullTrustApplication`. O fluxo da barra (`AmdActions.cs`) apenas usa `Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })`; não solicita maximização nem acompanha o estado da janela aberta.

No desktop, a mesma foto aberta diretamente pelo Explorer apareceu **maximizada**, com barras do Fotos e sem imagem invertida. Isso confirma que o aplicativo consegue se apresentar maximizado nesse contexto. **Não foi reproduzido pelo agente em FSE:** o modo já tinha sido encerrado pelo usuário e não foi reativado. Não há geometria histórica suficiente da janela do Fotos para determinar por que o shell deixou aquela janela menor.

A Microsoft descreve tela cheia **quando possível**, com aplicativos incompatíveis apresentados sobre fundo preto. O sintoma informado corresponde a essa apresentação de fallback; isso não identifica, por si só, a característica da janela que a desencadeou. [Comportamento de aplicativos no modo Xbox](https://support.microsoft.com/en-us/servicing/os/windows/docs/2025/10/windows-gaming-full-screen-experience).

Encaminhamento proposto: tratar a apresentação da janela específica aberta pela ação de foto, somente com modo Xbox confirmado, preservando o comportamento normal do desktop. O efeito precisa ser medido no FSE antes de afirmar que maximizar resolve. Não implementar uma disputa global de tamanho/foco ou redimensionamento de outros apps.

## 3. Vídeos sem som — incompatibilidade AAC 7.1 confirmada na pilha instalada

Foram lidos o replay novo do Neva (29/10) e um replay antigo do Microsoft Flight Simulator 2024 (22/10). Ambos têm vídeo **AV1** e áudio **AAC-LC, 48 kHz, oito canais/7.1**, com aproximadamente 225 segundos.

O áudio existe e não é silêncio. FFmpeg o decodifica; num trecho do Neva aos 90 segundos, os canais frontais apresentam RMS de aproximadamente **−20,55 dB** e picos próximos de **−9,45 dB**. Os outros seis canais estão silenciosos nesse trecho. Não é evidência de perda de áudio na gravação.

Uma sonda do **Media Foundation instalado**, com interfaces do SDK público 10.0.26100.0, abriu os dois arquivos e confirmou oito canais. Ao solicitar decodificação PCM, ambos retornaram **`0xC00D36B4`**, tipo de mídia inválido/inconsistente/não suportado. Um controle separado com três segundos do mesmo áudio convertido para **AAC estéreo** foi aceito e produziu amostra PCM (`ReadSample=0`, `sample=True`). Nenhum áudio foi reproduzido pelos testes.

Isso confirma a incompatibilidade da pilha nativa com o formato desses arquivos e sustenta a causa do silêncio relatado no Reprodutor de Mídia. A associação `.mp4` aponta para `Microsoft.ZuneMusic` **11.2607.16.0**. A documentação do decodificador AAC da Microsoft limita o suporte a **seis canais**, coerente com o resultado local. [AAC Decoder](https://learn.microsoft.com/en-us/windows/win32/medfound/aac-decoder).

A leitura do Core Audio mostrou a saída padrão Logitech G560 em 44%, sem mute. O player não tinha sessão ativa no momento da leitura; não foi atribuído a ele um estado de mute que não foi observado.

Encaminhamentos propostos, ainda não aplicados:

- Novas gravações: selecionar **Stereo** em **Audio Channels** das configurações de gravação AMD. A AMD documenta Automatic e Stereo; não é necessário trocar o codec AV1 do vídeo nem alterar a saída de jogos. [Configurações de gravação AMD](https://www.amd.com/en/resources/support-articles/faqs/DH3-023.html).
- Arquivos existentes: manter os originais e gerar cópias compatíveis convertendo apenas o áudio para estéreo, preservando o vídeo sem recodificação; alternativamente, usar um player que decodifique AAC 7.1.

## Limites e estado

As duas causas confirmadas são a inversão na conversão da miniatura e a rejeição do AAC 7.1 pelo Media Foundation instalado. O fallback visual do Fotos foi identificado pelo relato e documentação; seu gatilho exato no FSE permanece pendente de observação. Nada foi corrigido ou publicado nesta investigação.

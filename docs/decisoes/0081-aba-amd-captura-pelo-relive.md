# 0081 — Aba AMD: captura do Radeon ReLive pela barra

- **Status:** Aceita; implementada em candidata separada. Teste físico pendente.
- **Data:** 2026-10-29

## Contexto

O usuário usa o AMD Adrenalin com repetição instantânea, gravação e captura de tela. Pediu uma aba "AMD" na barra. Uma investigação somente leitura (evidências) encontrou no AMD Software 26.10.1:

- **O `RSServCmd.exe`**, que recebe comandos de ação (`save_ir`, `still_cap`, `capture_to_file_start/stop`, `toggle_ir`, `toggle_mic` e outros).
- **A entrega:** por `SendMessage`, à janela `DVREventWindowClass` do `AMDRSServ.exe`, que roda elevado.
- **O estado do ReLive:** no registro (`HKCU/HKLM\Software\AMD\DVR`).
- **As capturas:** em `Videos\Radeon ReLive\<jogo>`.

O usuário aprovou a fase 1 com três condições:
- as métricas da AMD ficam só na lógica, porque repetem o painel Desempenho;
- o design do painel é mostrado antes (aprovado com a prévia `amd-painel`/`amd-gravando`);
- commit antes de começar (`54912db`).

## Decisão

1. **Nova aba no trilho**, depois de Desempenho (painel 8, ícone de câmera), com:
   - linha de estado;
   - **Captura:** salvar repetição, foto e gravar/parar. GIF e repetição no jogo aparecem só se ativados no Adrenalin;
   - **Gravação:** repetição instantânea, área de trabalho, microfone e câmera na gravação;
   - **Microfone do Windows:** mudo e volume;
   - **Últimas capturas:** três, com miniatura do shell, duração do MP4 e jogo;
   - pasta das capturas e AMD Software.
2. **Comandos** só pelo `RSServCmd.exe` instalado, com lista fechada de ações em `src/lib/amd/RadeonRelive.cs`. Nunca `set_*`, transmissão, `exit` ou `crashdump`. Sem atalhos simulados nem injeção.
3. **A barra sai da imagem antes de capturar.** Foto, repetição, GIF e início/fim de gravação fecham a barra com a animação, esperam 250 ms e só então enviam o comando.
4. **Confirmação pelo efeito:**
   - foto, repetição e GIF: arquivo novo na pasta;
   - gravação: vídeo sem `_replay_` crescendo e depois parado;
   - interruptores: valor do registro.

   Sem efeito no prazo, o aviso vai para o histórico. Se a barra não estiver elevada, a mensagem pede "Sempre executar como administrador", porque o Windows bloqueia a mensagem ao `AMDRSServ.exe` elevado.
5. **Gravação iniciada fora da barra** (atalho do Adrenalin) é detectada pelo arquivo crescendo. O cronômetro conta a partir do início observado.
6. **Métricas da AMD:** `toggle_in_content_rs_perf_from_rsx` e `toggle_rs_perf_rec` existem em `SendAmdMetrics`, sem botão.
7. **Microfone do Windows:** `CoreAudio.ReadMicrophone/SetMicrophone` no dispositivo de captura padrão, sempre pelo id lido.
8. **Pasta `steamappid…`:** quando o ReLive não reconhece o jogo, a barra procura o nome no catálogo da Steam. Sem correspondência, mostra "Jogo da Steam".

## Consequências e limites

- **Interface não documentada:** pode mudar com o driver. Usar o `RSServCmd.exe` da própria instalação mantém o mapeamento da versão. Uma recusa ("Input not supported") vira erro claro.
- **Estado da gravação deduzido:** se o ReLive gravar num arquivo temporário e só mover no fim, o início não é confirmado. O aviso vai para o histórico e o botão Parar continua disponível.
- **Ainda não confirmado na prática:** não sei se `toggle_ir`, `toggle_desktop`, `toggle_mic` e `toggle_camera` atualizam o registro. Se não atualizarem, a barra avisa "não confirmou" mesmo com o efeito aplicado. O teste físico vai dizer.
- **Nenhum comando real** foi enviado pelo agente. A leitura real (registro, pasta, MP4, miniaturas e microfone) foi conferida por um diagnóstico somente leitura.
- **Fase 2 (ADLX 1.5):** Fluid Motion Frames, Super Resolution, Anti-Lag, Chill e FPS ficam fora desta etapa.

## Evidências

Investigação e prévia aprovada; `amd-verificacao.txt` (44 verificações); renders `amd-painel` e `amd-gravando`.

# 0092 — Volume do app em foco reduzido enquanto a barra está aberta

- **Status:** Aceita. No código desde a versão seguinte à 2.1.0 (ainda não lançada); teste físico pendente.
- **Data:** 2026-11-04

## Contexto

O usuário pediu que, ao abrir a barra com um jogo aberto, o som do jogo ou do programa em foco abaixe um pouco. O mixer do Windows controla o volume de cada sessão de app (`ISimpleAudioVolume`), e o XGB_P já usa essa API no painel Som. O Windows guarda o volume de cada app entre execuções. Por isso, uma redução esquecida (queda da barra, ou app que fecha antes da restauração) faria o app abrir baixo na vez seguinte.

O ducking de comunicação do Windows não serve: ele reage a chamadas de voz e baixa todos os outros apps, não o app em foco.

## Decisão

1. **Ao abrir a barra** (abertura nova): o app da frente antes de abrir. Com janela de jogo, entram todos os processos do jogo (lançador e filhos, ADR 0089) e as sessões do mesmo executável. Ficam de fora o próprio XGB_P e os sons do sistema.
2. **Redução para 50%** do nível de cada sessão, numa rampa de 5 passos de 30 ms. Um app já mudo no mixer não é tocado. Os outros apps e o volume geral não mudam.
3. **Ao fechar** (início do `Hide`, antes do adiamento da Steam), o nível original volta pela mesma rampa. Uma reabertura nunca reduz sobre um nível já reduzido.
4. **Escolha do usuário prevalece:** se o nível de uma sessão mudou com a barra aberta (pelo mixer), ela fica como o usuário deixou.
5. **Diário** `%LOCALAPPDATA%\XGB_P\settings\volume-reduzido.json`, gravado antes da rampa.
   - Uma sessão que sai antes da restauração fica no diário.
   - Quando o mesmo app reaparece (identificador persistente da sessão) com o nível que a barra deixou, ele volta ao original. A conferência roda a cada 5 s enquanto houver pendência, na abertura do XGB_P e antes de cada redução. Qualquer outro nível é escolha do usuário.
   - Entradas com mais de 7 dias são descartadas.
6. **Ordem:** o trabalho de áudio roda fora da thread da interface, com trava e geração. Fechar interrompe uma rampa de redução; a restauração sempre termina.
7. **Opção** "Abaixar o som do app ao abrir" em Configurações (seção de inicialização e abertura), **ligada por padrão** porque o usuário pediu o comportamento (`KeepAppVolume` em `launch.json` guarda só a escolha de desligar).

## Consequências

- Com a barra aberta, o mixer do painel Som mostra o nível reduzido do app em foco.
- Um app que toca som por outro processo, sem relação de PID ou de executável (alguns navegadores e serviços de áudio), pode não ser reduzido.
- A verificação (`tests/AppVolumeVerification.cs`, 18 casos) substitui mixer, janela, jogo e diário. Nenhum volume real é alterado nos testes.

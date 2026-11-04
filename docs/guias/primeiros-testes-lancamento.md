# Matriz física de aprovação

O gate automático libera os testes manuais; esta matriz aprova a versão. Ela cobre o que as verificações não conseguem provar: modo Xbox real, Steam e DSX, jogos de verdade, hardware e energia.

## Preparar

1. Gere uma candidata com `scripts\preparar-candidato.ps1` e confira o `candidate.json`.
2. Encerre pela bandeja qualquer XGB_P aberto. Abra o `XGB_P.exe` da candidata **pelo Explorer**, nunca a partir de um app de assistente de IA (ele virtualiza os dados do app).
3. Libere o caminho da candidata no HidHide ([instalação e uso](instalacao-e-uso.md)) e entre no modo Xbox pelo fluxo normal.
4. Não ative o modo Xbox, mude a cor do Windows nem altere Steam, DSX ou HidHide por automação.

## Registrar cada caso

Para cada caso, registre: hash do executável, versões do Windows, da Steam e do DSX, USB ou Bluetooth, elevação dos processos, resolução e DPI, jogo e launcher, horário e o resultado observado. Use **APROVADO**, **FALHOU** ou **NÃO TESTADO**. Teste não feito nunca conta como aprovado.

| Caso | Ação e resultado exigido |
|---|---|
| Ativação | No desktop, PS não abre a barra. No modo Xbox confirmado, PS abre e fecha. API indisponível mantém a barra fechada. |
| Controle | Dois analógicos, direcional, L1/R1, L2/R2, confirmar, voltar, toque e segurar. Nenhuma navegação simultânea na Steam ou no jogo. |
| Foco e camada | Abrir sobre o Big Picture e sobre ao menos três jogos; alternar apps; fechar a barra; respeitar uma janela escolhida pelo usuário durante uma operação. Testar janela sem borda e os modos de tela do jogo. |
| Steam | Início reutiliza o Big Picture; Biblioteca mostra *Coleções → Todos*. O layout do controle volta ao fechar a barra e o teclado. |
| Jogos | Abrir, retomar, pausar, voltar e fechar normalmente. Trocar de jogo com outro aberto (*Fechar jogo e iniciar*) e conferir que o próximo só abre depois do fim da sessão na Steam. Fechar o ativo e o de fundo. Forçar só num jogo sem progresso importante. |
| Recuperação | Pausar um jogo de teste e encerrar a barra normalmente: o jogo retoma. Reabrir e conferir o diário em `%LOCALAPPDATA%\XGB_P\games\paused-by-xgb.json`. |
| Persistência | Usar um jogo, fechar e reiniciar: o recente permanece. Conferir também uma sessão sem foco. |
| Som e mídia | Volume e mute, troca de dispositivo e de fonte, posição e transporte. ✕ no slider do mixer não silencia. |
| Gravação e AMD | Uma captura real de cada tipo; foto aberta no Fotos, maximizada no modo Xbox; ajustes AMD num jogo sem perfil próprio no Adrenalin. |
| Teclado | Campo normal e de senha. Entrar no modo Xbox com um campo já em foco no desktop: não abre. Campo novo já no modo Xbox: abre. Steam fica fora do automático; nenhum texto digitado aparece no log. |
| DSX | Atalho do DSX com o DSX na bandeja restaura a janela sem abrir outra instância; emulação Xbox reconhece o Guia. |
| Bateria | Controle físico por USB e por Bluetooth; virtual separado; carregar, descarregar e desconectar. |
| TV e acessibilidade | Resoluções e DPI usados no sofá; foco legível, contraste, movimento reduzido. |
| Energia | Suspender e retomar, reiniciar, desligar, com o trabalho salvo. Cancelar em cada confirmação não envia nada ao Windows. |
| Recursos | Sessão residente de pelo menos duas horas, alternando painéis e jogos. Medir CPU, memória, handles, threads e tempo de abertura; conferir crescimento depois do aquecimento. |
| Compatibilidade | Diferenças de elevação, launchers e jogos com proteção. Falha de acesso preserva os processos e mostra erro, sem contornar anti-cheat. |

Use o `logs\xgb.log` como diagnóstico. Para o controle e o modo Xbox, deixe `bin\DualSenseReader.exe` e `bin\ModoXboxMonitor.exe` rodando durante o teste (compilados por `scripts\compilar-ferramentas.ps1`).

## Aprovar

A versão é aprovada quando todos os casos aplicáveis têm resultado registrado, nenhuma falha alta está aberta e os limites da versão foram aceitos. Só então a release sai sem o sufixo de pre-release.

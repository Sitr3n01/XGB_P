# 0042 — PS pelo controle físico e abertura ao soltar

- **Status:** Aceita; a parte sem atraso depende de duas configurações feitas pelo usuário (DSX e HidHide)
- **Data:** 2026-10-25
- **Revisão posterior:** [ADR 0074](0074-transferencia-rapida-do-steam-input-apos-soltar-ps.md) oferece uma candidata com transporte Steam mais rápido e espera pela última soltura. Não neutraliza globalmente a saída do DSX; a análise de latência abaixo pertence ao transporte anterior.
- **Complementa:** ADRs 0037, 0039 e 0041. Revisa a "abertura no aperto" da ADR 0037.

## Contexto

No teste do build com o ciclo da ADR 0041 (13:35–13:37), o usuário relatou que "melhorou bastante" e pediu a menor transição possível. Medições (**observado**):

- **A Steam percebe a barra na hora.** Ela registra `OnFocusWindowChanged ... Desktop` no mesmo segundo de cada abertura (13:36:36, 13:36:44, 13:36:55, 13:37:07).
- **O PS solto devolve o controle ao Big Picture.** Uns 120 ms depois de abrir (durações registradas entre 78 e 193 ms), a Steam volta a 769. O ciclo forçado corrige isso, confirmado entre 1099 e 1239 ms depois. É nesse intervalo que o Big Picture ainda reage.
- **O piso de 1,1 s é do processo da Steam.** Medido no desktop às 13:40, com doze disparos e liberação no fim: o `steam.exe` aberto pelo protocolo é criado em 1 a 6 ms, mas só entrega o endereço e sai em cerca de 1,03 s. A Steam executa o comando de 50 a 100 ms depois. Chamar o `steam.exe` direto em vez do protocolo não muda o resultado (1039–1153 ms contra 1078–1150 ms). A parte da barra fica abaixo de 20 ms. Na pasta da Steam há um `wsock32.dll` de terceiros (Millennium), carregado também nesse processo.
- **Repique do PS.** Às 13:35:54 houve um aperto de 4 ms, 2 ms depois de soltar, registrado também no controle físico pelo leitor. Ele fechou a barra logo depois de abrir.
- **O HidHide libera o leitor (`bin\DualSenseReader.exe`), mas não a barra.** Por isso o leitor vê o controle físico por Bluetooth, e a barra só o virtual do DSX (`--app-list`, lido sem alterar).

Sem o PS, a Steam não volta ao Big Picture, e a troca natural dela já é imediata. Escolhas do usuário: a barra lê o PS do **controle físico**, e a Steam deixa de recebê-lo (PS silenciado no DSX). A barra **abre ao soltar um toque curto, sem atalho**, como o botão Xbox.

## Decisão

1. **Ciclo do PS.** Começa no primeiro aperto, em qualquer dispositivo, e termina na última soltura. O monitor deixa de juntar o PS dos dois dispositivos (`DeduplicateGuide = false`); os demais botões continuam juntos.
2. **Só o físico (Bluetooth) recebeu o PS.** A Steam não o viu, então a barra abre ou fecha **ao soltar**. Não abre se outro botão (exceto analógicos) foi apertado durante o toque, porque isso é atalho do DSX com o PS como modificador. Também não abre num toque de 600 ms ou mais.
3. **O virtual do DSX também recebeu (ou foi o único).** A Steam vê o PS e reage ao soltar. A barra abre já **no aperto**, como antes, e só nesse caso pede o ciclo da ADR 0041. Quando o físico e o virtual recebem o mesmo PS, o log avisa uma vez por sessão: `aviso: o PS chegou tambem pelo controle virtual...`.
4. **Repique.** Um aperto que começa menos de 30 ms depois da soltura anterior é ignorado com a sua soltura. O log registra `entrada: PS repique ignorado`.
5. **Ciclo da Steam sob demanda.** Aberturas por Win+G, bandeja ou PS só do físico não forçam layout nenhum, nem atrasam o fechamento.
6. **Configuração pelo usuário** (regra 4 do `AGENTS.md`):
   - HidHide: liberar o `XGB_P.exe` no caminho fixo escolhido pelo usuário; ver [instalação e uso](../guias/instalacao-e-uso.md). O DSX está configurado para controlar o HidHide (`SaveFile_LetDSXControlHidHide`). Se a liberação sumir, fazê-la pela página do HidHide no DSX.
   - DSX (Mapping Lab do perfil em uso): silenciar o PS **sozinho** no controle virtual, com a mesma opção que os atalhos PS + botão já usam. Os atalhos continuam funcionando. Se quiser o menu da Steam, criar um atalho do DSX que gere o botão PS virtual.

## Consequências e limites

- **Com as duas configurações:** a Steam não recebe o PS, não volta ao Big Picture e troca sozinha no mesmo segundo. A abertura acontece ao soltar, cerca de 120 ms depois do aperto. Não há ciclo nem atraso no fechamento.
- **Só com o HidHide:** o virtual também recebe o PS. A barra abre no aperto, com o ciclo e a janela de cerca de 1,1 s, e o log avisa.
- **Só com o DSX:** a barra não vê o PS, porque o virtual fica sem ele e o físico continua escondido. Ela abre só por Win+G ou pela bandeja.
- **Sem nenhuma das duas:** o comportamento é igual ao do build anterior, com o filtro de repique.
- **A Steam perde o botão Guia (menu e atalhos do Guia)**, a menos que um atalho do DSX o gere.
- Um controle físico ligado por **USB** não é distinguível do virtual do DSX pelo caminho do dispositivo. A barra o trata como visto pela Steam: abre no aperto e usa o ciclo, mais seguro e mais lento.
- A barra lê o físico em modo compartilhado, só leitura, como o leitor de diagnóstico. Não envia saída HID.

## Evidências

- `bin/design-preview/preview.log`, 13:35:39–13:37:13, e os logs da Steam (`controller_ui.txt`, `console_log.txt`) do mesmo intervalo.
- `%USERPROFILE%\GameBarProbe\logs\dualsense.log`: repique às 13:35:54.050 no `DualSense#0-BT` e no `DualSense#1-USB`.
- Medição do protocolo às 13:40 (script temporário fora do repositório) e criação/saída do processo às 13:41.
- Suíte: 63 verificações de ativação (antes 46) e 24 do ciclo da Steam.

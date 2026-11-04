# 0027 — Conexões nas Configurações: Wi-Fi, Bluetooth, redes e dispositivos conhecidos

- **Status:** Aceita
- **Data:** 2026-10-24

## Contexto

O usuário pediu, na aba Configurações:

- opções de ligar e desligar Wi-Fi e Bluetooth;
- uma lista para conectar e gerenciar as redes e os dispositivos já conhecidos.

Ele pediu também que todos os testes automáticos fossem executados e validados (sem ele presente), com extremo cuidado contra regressões e um commit antes da mudança.

**Observado nesta máquina, só com leitura:**

- **Wi-Fi:** adaptador Realtek 8852CE, desconectado, rádio ligado, 36 redes salvas, 2 em alcance.
- **Bluetooth:** adaptador Realtek. Pareados: DualSense (controle), um celular (entradas clássica e LE) e um fone (áudio, com filtro de conexão encontrado).
- **Privacidade:** "Rádios" e "Localização" permitidos.

## Decisão

1. **Seção "Conexões"** no topo de Configurações. O conteúdo anterior continua igual e na mesma ordem.
   - Linhas **Wi-Fi** e **Bluetooth** mostram o estado à direita (verde quando ligado) e alternam ao selecionar.
   - Linhas **Redes Wi-Fi** e **Dispositivos Bluetooth** abrem as páginas, com o resumo de salvas, em alcance, conhecidos e conectados. Desde a revisão, o resumo do Wi-Fi conta só as redes em alcance ("2 em alcance", "Nenhuma em alcance" ou "Alcance indisponível").
2. **Rádios** (`src/lib/network/Radios.cs`): `Windows.Devices.Radios`, o mesmo liga/desliga das Configurações rápidas.
   - A permissão de "Rádios" é consultada antes de mudar.
   - Rádio ausente ou desativado no sistema deixa a linha desabilitada.
3. **Desligar com risco pede confirmação**, com o foco em Cancelar e a confirmação logo abaixo da linha:
   - Bluetooth com um DualSense conectado por Bluetooth, detectado pela leitura HID (`DualSense#N-BT`) ou pela lista. A barra perderia o controle; a mensagem orienta a religar pelo teclado, pelo mouse ou pelo cabo.
   - Wi-Fi com a internet pelo Wi-Fi.
4. **Página Wi-Fi** (`src/lib/network/Wifi.cs`, Native Wifi API):
   - Mostra as redes salvas: a conectada primeiro, depois as em alcance pelo sinal (1 a 4) e as demais em ordem alfabética.
   - Selecionar uma rede abre as ações logo abaixo dela, como no Windows 11: **Conectar** pelo perfil salvo (em alcance, rádio ligado), **Desconectar** (a conectada) e **Esquecer rede**, com confirmação, porque apaga a senha salva.
   - Perfis de política de grupo não podem ser esquecidos.
   - Sem a lista de alcance (por exemplo, com a localização negada no 24H2+), aparece uma nota, e os perfis continuam listados.
   - Redes novas, que pedem senha, ficam em "Abrir Wi-Fi no Windows". A prévia não digita nem lê senhas.
   - **Revisão do mesmo dia:** com 36 redes salvas e só 2 em alcance, a lista longa confundia, e o usuário pediu para mostrar **só as redes salvas em alcance**. Agora:
     - a página ("REDES SALVAS EM ALCANCE · N") lista apenas as redes em alcance ou conectada, com "Conectada" ou "Sinal N/4";
     - as fora de alcance ficam ocultas e continuam gerenciáveis no Wi-Fi do Windows ("Gerenciar redes conhecidas"), inclusive para esquecer;
     - sem nenhuma em alcance, aparece "Nenhuma rede salva em alcance agora.";
     - com o rádio desligado, só o aviso de Wi-Fi desligado;
     - sem a lista de alcance, só a nota, porque nenhuma rede pode ser dada como em alcance (substitui o item acima);
     - se a rede com as ações abertas sai do alcance, ela some, as ações e a confirmação fecham e o foco vai para a primeira rede.
     
     A biblioteca `Wifi.Read` continua lendo todos os perfis; o filtro fica na página. Evidências.
5. **Página Bluetooth** (`src/lib/network/Bluetooth.cs`):
   - Lista os dispositivos pareados pela API `Windows.Devices.Enumeration`. Clássico e LE do mesmo aparelho são unidos pelo `ContainerId`.
   - O tipo vem da classe do dispositivo (áudio, celular, controle, entrada, computador).
   - **Conectar/Desconectar** só para áudio Bluetooth, pelas propriedades oneshot de `KSPROPSETID_BtAudio` (ksmedia.h) no filtro de áudio do aparelho.
   - **Remover dispositivo** desfaz o pareamento, com confirmação. **O DualSense não pode ser removido por aqui**, porque é o controle da barra (ADR 0004). Outros tipos mostram o estado e "Remover".
   - Parear um aparelho novo fica em "Abrir Bluetooth no Windows".
6. **Navegação:**
   - Esc/○ fecha a confirmação, depois as ações e depois volta da página para Configurações, com o foco na entrada.
   - L1/R1 sai das páginas, e Configurações sempre reabre na página principal.
   - A troca de página anima a altura e o conteúdo, como a troca de painel, e limpa o retorno anterior.
7. **Leitura** em worker e só com Configurações aberto e visível:
   - rádios e redes a cada 3 s;
   - dispositivos a cada 6 s;
   - varredura de Wi-Fi só ao entrar na página.
   
   A tela só é reconstruída quando o estado muda, preservando foco e rolagem.

## Consequências

- Ligar/desligar rádios, conectar, desconectar, esquecer e remover têm efeito real no Windows, sempre por escolha do usuário e com retorno de erro.
- Esquecer uma rede apaga a senha salva. Remover um aparelho exige parear de novo. As duas ações pedem confirmação.
- Conectar áudio Bluetooth depende do driver da Microsoft aceitar o oneshot. Celular, controle e entrada se conectam sozinhos quando ligados, e para eles não há botão de conectar.
- A leitura periódica com Configurações aberto custa uma consulta em worker a cada 3 a 6 s. Com a barra oculta, nada é lido.

## Evidências

- `--verify` (com substitutos) cobre:
  - a ordem da página principal e o conteúdo antigo preservado;
  - ligar/desligar, as duas confirmações de risco, Esc, rádio ausente e erro;
  - na página Wi-Fi: conectar, desconectar, esquecer com confirmação, política, nota, erro, foco na atualização e Wi-Fi do Windows. Desde a revisão, também a rede fora de alcance oculta, a rede que sai do alcance com as ações abertas, nenhuma em alcance, o rádio desligado e o resumo da página principal;
  - na página Bluetooth: áudio conecta/desconecta, celular remove com confirmação, DualSense sem remover, erro e rádio desligado;
  - páginas, Esc, L1/R1 e listas vazias.
- `--verify-connections-live` (só leitura): rádios, permissão, redes salvas e dispositivos pareados reais, sem varredura e sem ações. Só as contagens vão para o relatório; as telas com nomes reais ficam em `bin`.
- Antes da integração, uma sonda isolada leu os três backends no Windows real sem erro de interop, inclusive a obtenção do `IKsControl` do fone, sem envio.
- Renders e relatórios. Referência técnica em [conexoes-windows.md](../referencia/conexoes-windows.md).
- **Não comprovado** (o agente não altera rádios, redes nem dispositivos reais):
  - ligar/desligar de verdade;
  - conectar e esquecer redes;
  - o oneshot de áudio enviado a um fone;
  - remover um aparelho;
  - o comportamento com o DualSense conectado por Bluetooth.

# Decisões (ADRs)

Cada decisão importante fica num arquivo `NNNN-titulo.md`. Uma decisão aceita não é apagada: quando for substituída, marque `Substituída por NNNN`.

| # | Decisão | Status |
|---|---|---|
| [0001](0001-nao-modificar-binarios-microsoft.md) | Não modificar binários da Microsoft | Aceita |
| [0002](0002-identidade-do-pacote-da-game-bar.md) | Ponte local de ativação por Win+G (registro público resumido) | Aceita; detalhes no arquivo privado |
| [0003](0003-msix-assinado-v99.md) | Instalação da ponte local (registro público resumido) | Aceita; detalhes no arquivo privado |
| [0004](0004-dualsense-nativo-via-virtual-dsx.md) | Entrada do controle: DualSense nativo, lido do virtual do DSX, só leitura | Aceita |
| [0005](0005-dois-modos-xbox-e-desktop.md) | Ativo só no modo Xbox; no desktop quem responde é a Steam | Aceita |
| [0006](0006-big-picture-como-app-inicial.md) | Big Picture como app inicial (AnyFSE); rotas para a Steam | Aceita; Início revisado pela 0010 |
| [0007](0007-escopo-e-interacao-da-v1.md) | v1: barra à esquerda, três painéis, microfone segurado, desktop ignorado | Aceita; Jogo atual removido pela 0016 |
| [0008](0008-previa-manual-de-design-no-desktop.md) | Prévia manual do design no desktop com Win+Shift+G | Aceita |
| [0009](0009-linguagem-visual-xbox.md) | Linguagem visual Xbox: superfícies neutras, texto branco e destaque verde | Aceita |
| [0010](0010-home-com-recentes-e-utilidades.md) | Início como Home própria com recentes e utilidades | Aceita |
| [0011](0011-navegacao-steam-ativa-na-previa.md) | Início do Big Picture acima do jogo ativo e Biblioteca real na prévia | Aceita |
| [0012](0012-painel-de-som-funcional.md) | Som funcional: master, microfone, dispositivos e mixer de aplicativos | Aceita |
| [0013](0013-som-compacto-e-mixer-integrado.md) | Mutes compactos, dispositivos inline e mixer integrado por aplicativo | Aceita; listas flutuantes pela 0016; ✕ no slider pela 0085 |
| [0014](0014-alternador-de-janelas-na-home.md) | Apps abertos para alternar janelas e acessos rápidos só com ícones | Aceita; nome em foco revisado pela 0015; seis atalhos pela 0029 |
| [0015](0015-player-de-midia-na-home.md) | Player de mídia na Home pela API de mídia do Windows (SMTC) | Aceita; posição revisada pela 0016 |
| [0016](0016-player-no-som-e-listas-flutuantes.md) | Player também no Som, sem Jogo atual, rolagem fina e listas flutuantes | Aceita |
| [0017](0017-desempenho-funcional-e-modo-de-energia.md) | Desempenho funcional com HWiNFO, RTSS e modo de energia do Windows | Aceita |
| [0018](0018-faixa-de-status-estilo-xbox.md) | Faixa de status no estilo do guia do Xbox: rede, DualSense com bateria e hora grande | Aceita |
| [0019](0019-jogos-recentes-locais-e-sessoes.md) | Catálogo Steam, atalhos externos, recentes locais e sessões observadas | Aceita; implementada na prévia |
| [0020](0020-voltar-ao-ultimo-jogo-permanente.md) | Voltar ao jogo permanece com o último jogo executado, sem duplicação nos recentes | Aceita; repetição revista pela 0025 |
| [0021](0021-imagens-locais-automaticas-dos-jogos.md) | Extração automática de imagens locais dos jogos, cache e símbolo de reserva | Aceita |
| [0022](0022-animacoes-da-previa.md) | Animações em cinco sprints, cancelamento e movimento reduzido | Aceita |
| [0023](0023-camada-final-e-cena-responsiva.md) | Camada sem marcas da prévia nem dicas; cena que segue a escala e a resolução da tela | Aceita; barra na altura do conteúdo, no centro vertical |
| [0024](0024-pausar-e-fechar-jogos-abertos.md) | Pausar (técnica do Nyrna) e fechar jogos abertos; tracking pelo processo, sem depender da janela | Aceita; pausa pela maioria dos threads (0026) |
| [0025](0025-tres-recentes-com-o-destaque.md) | Recentes com os três jogos mais recentes, incluindo o do destaque | Substituída pela 0060 |
| [0026](0026-segurar-para-fechar-apps.md) | Segurar 2 s o ícone de um app aberto mostra o X para fechar a janela | Aceita |
| [0027](0027-conexoes-wifi-e-bluetooth.md) | Conexões nas Configurações: Wi-Fi e Bluetooth, redes e dispositivos conhecidos | Aceita; Wi-Fi só com as redes em alcance |
| [0028](0028-luz-noturna-dsx-e-fechar-vermelho.md) | Luz noturna e atalho do DSX nas Configurações; contorno vermelho em fechar | Aceita |
| [0029](0029-lojas-pelo-acesso-rapido.md) | Lojas pelo Acesso rápido da Home: Xbox, Steam, Epic e outros launchers instalados | Aceita |
| [0030](0030-trilho-em-descanso-e-sons-da-interface.md) | Trilho em descanso com o foco no painel; sons da interface do Xbox, só locais | Aceita |
| [0031](0031-coesao-dos-sons-da-interface.md) | Um efeito por ação, reprodução exclusiva e categorias sonoras consistentes | Aceita; refina a 0030 |
| [0032](0032-persistencia-dos-sons-da-interface.md) | Salvar e restaurar volume e liga/desliga dos sons no perfil local | Aceita; revisa a persistência das 0030/0031 |
| [0033](0033-inicializacao-e-execucao-como-administrador.md) | Inicialização por usuário controlável pelo Windows e preferência de administrador com UAC | Aceita; amplia a prévia da 0008 |
| [0034](0034-descricoes-com-atraso-e-estilo-windows11.md) | Dicas suplementares com fonte legível, tema escuro e espera de três segundos | Aceita |
| [0035](0035-feedback-vinculado-as-operacoes.md) | Rodapé por operação/contexto, confirmação observada, expiração e timeout sem roubar foco | Aceita |
| [0036](0036-navegacao-completa-pelo-dualsense.md) | Navegação completa pelo DualSense: analógicos, gatilhos, atalhos e microfone segurado na prévia | Aceita; amplia a exceção manual da 0008 |
| [0037](0037-ps-e-wing-somente-no-modo-xbox.md) | PS e Win+G com modo Xbox/FSE confirmado | Aceita; exceção opcional Win+G no desktop pela 0048 |
| [0038](0038-sair-do-modo-xbox-pelas-configuracoes.md) | Saída real do modo Xbox em Configurações e Energia, só por pedido do usuário | Aceita |
| [0039](0039-preservar-janela-steam-e-foco.md) | Reutilizar Big Picture, preservar foco e observar geometria; PS separado da navegação | Aceita |
| [0040](0040-foco-da-barra-e-camada-uiaccess.md) | Primeiro plano da barra por `AttachThreadInput`, camuflagem registrada e teste da camada uiAccess | Aceita; barra elevada, uiAccess de reserva |
| [0041](0041-layout-de-desktop-da-steam-ao-abrir.md) | Ao abrir a barra, fixar e liberar o layout de desktop da Steam (`steam://forceinputappid`) | Aceita; confirmação/transporte revisados na candidata 0074 |
| [0042](0042-ps-pelo-controle-fisico-e-abertura-ao-soltar.md) | PS pelo controle físico, abertura ao soltar sem atalho, filtro de repique | Aceita; depende de DSX e HidHide pelo usuário |
| [0043](0043-entrada-lenta-enquanto-a-steam-solta.md) | Entrada lenta e painel apagado, sem navegar, até a Steam soltar o controle | Substituída na candidata pela 0074; publicados preservados |
| [0044](0044-foco-coeso-com-apps-do-windows.md) | Foco com apps do Windows: Alt neutro só quando a fila é recusada, atalhos do DSX, retorno ao app certo | Aceita; teste no modo Xbox pendente |
| [0045](0045-apps-abertos-camuflados-no-modo-xbox.md) | Apps camuflados pelo shell no FSE/desktop atual, identidade hospedada e confirmação real de fechamento | Aceita; teste no modo Xbox pendente |
| [0046](0046-entrega-de-foco-antes-de-ocultar-a-barra.md) | Entregar foco antes de ocultar, tentativa com filas, confirmação posterior e cancelamento sem disputa de foco | Aceita; novo teste no modo Xbox pendente |
| [0047](0047-midia-intencoes-fontes-e-retorno-dos-sliders.md) | Sliders sem mute/play implícitos, voltar dentro do player, intenções em voo, fontes/seek isolados e transporte FIFO | Aceita; teste físico no FSE pendente |
| [0048](0048-wing-opcional-no-desktop.md) | Opção persistida permite somente Win+G no desktop confirmado; desligada por padrão, PS continua restrito ao FSE | Aceita; teste físico do atalho pendente |
| [0049](0049-persistencia-de-sessoes-sem-foco.md) | Persistir uso pela runtime exata estável sem depender de foco, mantendo filtros e histórico após fechar/reiniciar | Aceita; teste real do Genshin pendente |
| [0050](0050-ocultar-barra-ao-fechar-jogo.md) | Ocultar a barra após pedido aceito de fechar jogo, confirmando saída em segundo plano e sem reabrir por timeout | Aceita; teste físico pendente |
| [0051](0051-bateria-fisica-do-dualsense.md) | Bateria física com validade, formatos/CRC, painel atualizado e ícone de carregamento refinado | Aceita; teste físico pendente |
| [0052](0052-teclado-virtual.md) | Teclado virtual sem foco pelo DualSense: Acesso rápido, abertura sozinha só no modo Xbox, camadas com a barra e Steam Input | Aceita; teste físico pendente |
| [0053](0053-design-do-teclado-baseado-no-windows.md) | Design aprovado do teclado: teclas retangulares, foco cinza, vetores e atalhos compactos | Aceita; teste físico pendente |
| [0054](0054-biblioteca-do-big-picture-na-colecao-todos.md) | Biblioteca no Big Picture pela coleção nativa Todos, destino real e regressões dos dois acessos | Aceita; teste físico da barra no FSE pendente |
| [0055](0055-destaque-do-windows-com-contraste.md) | Cor de destaque do Windows com tons por função, contraste, atualização central e reserva | Aceita; troca real de cor e teste físico pendentes |
| [0056](0056-estabilizacao-e-candidata-local.md) | Pausa transacional/recuperação, identidade nativa, limites de recursos, gate verificável e candidata local | Aceita; lançamento e meta 9,5 pendentes |
| [0057](0057-apresentacao-com-custo-limitado.md) | Altura por desenho/recorte, cópia vetorial limitada e agenda por necessidade, com comparação reproduzível | Aceita na candidata rc.2; validação física pendente |
| [0058](0058-navegacao-explicita-home-e-biblioteca.md) | Home explícita, espera Steam Input, descoberta da janela e entrega confirmada de foco à Biblioteca | Aceita; publicada, teste físico FSE pendente |
| [0059](0059-despacho-local-da-navegacao-steam.md) | Home/Biblioteca pelo canal local do cliente conferido, sem processo auxiliar; guarda de reentrância e fallback | Aceita; publicada, teste físico do ganho pendente |
| [0060](0060-recentes-sem-o-jogo-do-destaque.md) | Três outros recentes, excluindo o jogo de Voltar ao jogo antes de limitar, inclusive durante tracking com foco | Aceita; implementação e evidências na continuidade |
| [0061](0061-som-continuo-na-navegacao-do-trilho.md) | Um som curto por movimento direcional no trilho, inclusive ao trocar painel | Aceita; revisa o silêncio da ADR 0031 |
| [0062](0062-preparacao-da-versao-1.0.1.md) | Produto V.1.0.1, energia real, acessibilidade persistida e candidata antes da aprovação | Aceita para implementação; aprovação de lançamento pendente |
| [0063](0063-transicao-e-retorno-entre-jogos.md) | Transição estável com Steam e retorno ao jogo sobrevivente após saída confirmada | Aceita; teste físico pendente |
| [0064](0064-jogos-abertos-no-grupo-voltar-ao-jogo.md) | Todos os abertos em Voltar ao jogo e confirmação de fechar somente com texto | Aceita; teste físico pendente |
| [0065](0065-versao-1.0.2-e-entrega-local.md) | Versão 1.0.2 e publicação local em .exe, backups e caminhos compatíveis | Aceita; atualização local autorizada |
| [0066](0066-ps-protegido-em-jogos-geridos-pela-steam.md) | PS protegido sobre atalhos da Steam e no fechamento com jogo na frente; 769 após o foco termina com aviso | Aceita; publicada na 1.0.3; diagnóstico revisado pela 0068 |
| [0067](0067-versao-1.0.3-e-entrega-em-exe.md) | Versão 1.0.3 publicada só em .exe; bin/app e caminho legado seguem na 1.0.2 até novo pedido | Aceita; validação física pendente |
| [0068](0068-abertura-pelo-big-picture-e-foco-apos-soltar.md) | Segundo jogo aberto pelo Big Picture para o overlay do jogo aberto não ficar visível; foco ao jogo só depois de soltar o botão | Aceita; publicada na 1.0.4, teste físico pendente |
| [0069](0069-versao-1.0.4-e-entrega-em-exe.md) | Versão 1.0.4 publicada só em .exe com a ADR 0068; bin/app e caminho legado seguem na 1.0.2 | Aceita; validação física pendente |
| [0070](0070-fechar-antes-de-iniciar-e-retorno-ao-inicio.md) | Home com Fechar jogo e iniciar/Cancelar sem ícones; abertura após saída confirmada e retorno explícito ao Início sem jogos | Aceita; publicada na 1.0.5, teste físico pendente |
| [0071](0071-versao-1.0.5-e-entrega-em-exe.md) | Versão 1.0.5 publicada só em `.exe`, backup/logs preservados e comandos HidHide entregues | Aceita; validação física pendente |
| [0072](0072-aguardar-encerramento-da-sessao-steam.md) | Aguardar fim da sessão Steam além da saída nativa antes de iniciar outro jogo, inclusive após fechamento manual | Aceita; publicada na 1.0.6, teste físico pendente |
| [0073](0073-versao-1.0.6-e-entrega-em-exe.md) | Versão 1.0.6 publicada somente em `.exe`, backup/logs preservados e comandos do driver entregues | Aceita; validação física pendente |
| [0074](0074-transferencia-rapida-do-steam-input-apos-soltar-ps.md) | Transferência Steam após soltar PS, entrada normal e navegação dependente de confirmação nova | Incorporada na 1.0.7 pela 0075; teste físico pendente |
| [0075](0075-versao-1.0.7-e-backup-da-1.0.6.md) | Publicação 1.0.7 em `.exe`, backup integral da 1.0.6 e commit da base reproduzível | Publicada para teste; validação física pendente |

| [0076](0076-versao-1.0.8-confirmacao-steam-e-abertura.md) | Confirmar Guia → desktop, coordenar abertura e publicar 1.0.8 nos dois caminhos; backup 1.0.7 e limpeza | Aceita; validação física pendente |
| [0077](0077-fechamento-ps-e-final-da-sprint-1.0.8.md) | Fechamento com última soltura do PS, saída animada e build final da sprint 1.0.8 por hash | Publicada; teste físico pendente |
| [0078](0078-historico-de-acoes-e-inicio-so-critico.md) | Histórico de ações e problemas nas Configurações; Início mostra só erro, aviso que pede ação e espera longa | Publicada em `.exe` para teste; teste físico pendente |
| [0079](0079-dsx-pela-bandeja-sem-segunda-instancia.md) | DSX restaurado da bandeja; Steam só com o DSX fechado, sem segunda instância | Publicada em `.exe` para teste; teste físico pendente |
| [0080](0080-emulacao-xbox-guia-xinput-e-icones.md) | Emulação Xbox do DSX: Guia pelo XInput como PS visto pela Steam e ícones PlayStation/Xbox | Publicada em `.exe` para teste; teste físico pendente |
| [0081](0081-aba-amd-captura-pelo-relive.md) | Aba AMD: captura do Radeon ReLive pelo `RSServCmd.exe`, microfone do Windows e últimas capturas; métricas só na lógica | Candidata em desenvolvimento; teste físico pendente |
| [0082](0082-painel-amd-ajustes-pelo-adlx.md) | Painel AMD com logotipo: ajustes gráficos globais pelo ADLX (FSR 4, geração de quadros, AFMF, RSR, Anti-Lag, Boost, Chill, limite de FPS, nitidez, Enhanced Sync); Gravação em painel separado | Candidata em desenvolvimento; teste físico pendente |
| [0083](0083-icones-gravacao-ordem-do-trilho-e-interruptores.md) | Captura só com ícones vetoriais, ordem Som → Gravação → Desempenho → AMD e interruptores comuns no estilo Windows 10 | Candidata em desenvolvimento; teste físico pendente |
| [0084](0084-miniaturas-e-fotos-no-modo-xbox.md) | Miniaturas com orientação GDI explícita e apresentação limitada da foto aberta no Microsoft Fotos em FSE | Candidata em desenvolvimento; teste físico pendente |
| [0085](0085-slider-do-mixer-sem-mute.md) | ✕/Enter no slider do mixer não silencia; mute só no botão ao lado | Publicada na 2.0.0; teste físico pendente |
| [0086](0086-versao-2.0.0-e-entrega-em-exe.md) | Versão 2.0.0 em `.exe` e `bin/app`, com backup das pastas 1.0.8; perfis por jogo da AMD suspensos | Publicada; teste físico pendente |
| [0087](0087-teclado-automatico-so-com-modo-xbox-estavel.md) | Teclado automático só com o modo Xbox lido na hora e estável; campo herdado do desktop não abre | Publicada na 2.0.2; teste físico pendente |
| [0088](0088-versao-2.0.2-e-entrega-em-exe-e-bin-app.md) | Versão 2.0.2 em `.exe` e `bin/app`, inicialização na 2.0.2, backup das pastas 2.0.0 | Publicada; teste físico pendente |
| [0089](0089-jogo-filho-do-lancador-de-atalho.md) | Atalho externo sem mapeamento adota o jogo aberto como filho do alvo (unlocker); janela do filho só sem janela do alvo | Aceita; ainda não lançada |
| [0090](0090-jogo-em-encerramento-nao-e-outro-jogo.md) | Processo do jogo em encerramento (sem caminho/criação) não conta como outro jogo: o retorno ao Início não é cancelado | Aceita; ainda não lançada |
| [0091](0091-barra-de-tarefas-oculta-no-modo-xbox.md) | Ocultação automática da barra de tarefas ligada no modo Xbox e devolvida no desktop, com marcador de recuperação | Aceita; ainda não lançada |
| [0092](0092-volume-do-app-em-foco-ao-abrir.md) | Som do app em foco pela metade enquanto a barra está aberta, com diário para restaurar depois de queda | Aceita; ainda não lançada |
| [0093](0093-status-ao-lado-do-painel-nas-telas-steam.md) | Sobre o Big Picture, sem relógio e com rede/bateria ao lado do título do painel | Aceita; ainda não lançada |
| [0094](0094-bateria-de-fones-bluetooth.md) | Bateria de fones Bluetooth (Hands-Free) lida da propriedade do Windows, na faixa de status e nas Conexões | Aceita; ainda não lançada |

| [0095](0095-versao-2.0.3-e-entrega-em-exe-e-bin-app.md) | Versão e distribuição pública | Aceita; candidata pública 3.0.0 |
| [0096](0096-pesquisa-de-programas-pelo-acesso-rapido.md) | Pesquisa de programas pelo Acesso rápido | Aceita; candidata pública 3.0.0 |
| [0097](0097-acesso-rapido-no-topo-da-home.md) | Acesso rápido no topo da Home | Aceita; candidata pública 3.0.0 |
| [0098](0098-gravacao-e-amd-no-acesso-rapido-compacto.md) | Gravação e AMD no Acesso rápido compacto | Aceita; candidata pública 3.0.0 |
| [0099](0099-som-e-gravacao-em-retangulos-na-home.md) | Som e Gravação em retângulos na Home | Aceita; candidata pública 3.0.0 |
| [0100](0100-mais-opcoes-globais-da-amd-no-painel.md) | Mais opções globais da AMD no painel | Aceita; candidata pública 3.0.0 |
| [0101](0101-teclado-sobre-jogos-compor-e-enviar.md) | Teclado sobre jogos: compor e enviar | Aceita; candidata pública 3.0.0 |
| [0102](0102-despertar-pelo-ps-integrado.md) | Despertar pelo PS integrado | Aceita; candidata pública 3.0.0 |
| [0103](0103-previa-do-titulo-deslizante.md) | Título original com seta | Aceita; candidata pública 3.0.0 |
| [0104](0104-versao-3.0.0-com-e-sem-trilho.md) | Versão 3.0.0 com e sem trilho | Aceita; candidata pública 3.0.0 |
| [0105](0105-voltar-ao-jogo-acima-do-acesso-rapido.md) | Voltar ao jogo acima do Acesso rápido | Aceita; candidata pública 3.0.0 |
| [0106](0106-bateria-de-fones-pelo-g-hub.md) | Bateria de fones pelo G HUB | Aceita; candidata pública 3.0.0 |
| [0107](0107-microfone-mudo-sincronizado-com-o-dsx.md) | Microfone mudo sincronizado com o DSX | Aceita; candidata pública 3.0.0 |
| [0108](0108-svg-do-usuario-no-indicador-do-dualsense.md) | SVG no indicador de bateria do DualSense | Aceita; candidata pública 3.0.0 |
| [0109](0109-feedback-amd-so-para-avisos-relevantes.md) | Feedback AMD discreto nas operações rotineiras | Aceita; candidata pública 3.0.0 |
| [0110](0110-integracao-da-tv-samsung.md) | Integração da TV Samsung | Aceita; candidata pública 3.0.0 |
| [0111](0111-tv-integrada-comandos-automacao-e-retomada.md) | TV integrada: comandos, automação e retomada | Aceita; candidata pública 3.0.0 |
| [0112](0112-trilho-lateral-como-opcao-e-ordem-das-configuracoes.md) | Trilho lateral como opção e ordem das Configurações | Aceita; candidata pública 3.0.0 |
| [0113](0113-reforco-sutil-do-icone-dualsense.md) | Reforço sutil do ícone DualSense | Aceita; candidata pública 3.0.0 |
| [0114](0114-bootps-reversivel-e-identificador-de-retomada.md) | BootPS reversível e identificador de retomada | Aceita; candidata pública 3.0.0 |

## Modelo

```markdown
# NNNN — Título

- **Status:** Proposta | Aceita | Substituída por NNNN
- **Data:** AAAA-MM-DD

## Contexto
## Decisão
## Consequências
## Evidências
```

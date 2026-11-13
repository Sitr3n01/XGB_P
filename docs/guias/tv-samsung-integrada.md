# TV Samsung integrada — candidata 3.0.0

Recurso opcional para TVs Tizen compatíveis na rede local. **Automação começa desligada.** A validação física da TV permanece pendente; testes com transporte substituído não demonstram compatibilidade com qualquer modelo.

## Preparação local

O porte preserva a configuração do SamsungTvOrchestrator: na primeira abertura normal, tenta importar `%LOCALAPPDATA%\SamsungTvOrchestrator\config.json` e a credencial da mesma TV, sem escrever nessa pasta. A configuração própria passa a ficar em `%LOCALAPPDATA%\XGB_P\tv`. Se nenhuma configuração existir, o painel informa a ausência; não há assistente universal de descoberta/instalação nesta versão. Não execute dois controladores da mesma TV; o mutex compartilhado também protege essa exclusão.

O schema importado tem `Tv.Model`, `Tv.Mac`, `Tv.Duid`, `Tv.LastKnownIp` e, opcionalmente, `Tv.MonitorDevicePathFragment`. Use dados do seu equipamento, somente em arquivos locais. Identificadores de fixtures neste repositório são fictícios e não devem ser usados como configuração. A credencial protegida pertence ao usuário Windows; não a compartilhe nem a copie ao Git. Uma credencial antiga exige nova autorização pelo painel.

## Operação

Configurações → Tela → TV Samsung reúne **Tela confirmada**, **Conexão com a TV**, automação e suas regras. Energia mantém **Ligar TV** e **Desligar TV**. Confirme na tela se está ligada ou apagada: conexão TCP, HTTP ou HDMI não comprova energia. Autorizar pode pedir confirmação física na TV; certificados são fixados por conexão, sem callback TLS global.

`KEY_POWER` é alternância. Um comando enviado ou possivelmente enviado nunca é repetido automaticamente; aguarde o resultado e confirme a tela. O despertar por rede pode depender do estado de standby. Subopções de topologia, HDR, suspensão e retomada aparecem com a automação ligada. Confirmação implícita e som são opcionais e começam desligados. **Restaurar intenção** pode religar a TV em qualquer retomada, não apenas por PS.

O PS pós-retomada vem do leitor HID existente; não há outro leitor, relatório de saída, tarefa, serviço ou bandeja. O primeiro PS pode ser consumido como retomada e exigir outro toque para abrir a barra. O aplicativo só roda depois que o Windows acordou.

## Validar e reverter

Teste manualmente comandos com a automação desligada antes de habilitar regras, observando tela e histórico. Depois teste suspensão, retomada, PS, teclado, topologia e HDR em sessões que possam ser interrompidas. Desligar a automação impede suas reações; fechar o XGB_P encerra o host. Desativação de tarefas/Run/HidHide de instalações antigas é uma ação separada do usuário.

Configuração, MAC, DUID, IP, credenciais e logs reais não acompanham o repositório nem o pacote. As suítes da CI usam relógio, transporte e tela substituídos e um par TLS efêmero restrito a loopback.

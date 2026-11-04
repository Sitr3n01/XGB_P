# 0048 — Win+G opcional no desktop

- **Status:** Aceita
- **Data:** 2026-10-25
- **Revisa:** a restrição de Win+G ao FSE da ADR 0037; PS permanece restrito ao modo Xbox.

## Contexto

O usuário pediu uma opção para abrir o sistema no desktop apenas com Win+G. O processo já começa na bandeja e a sonda instalada entrega eventos desse atalho.

## Decisão

Adicionar **Permitir Win+G no desktop**, desligada por padrão, em Configurações → INICIALIZAÇÃO E ABERTURA e no menu da bandeja. Persistir `DesktopWinG` como campo opcional de `launch.json`, preservando a preferência de administrador e a compatibilidade com arquivos antigos.

No FSE confirmado, manter PS/Win+G. Com API confirmando desktop e opção ligada, aceitar somente novos eventos Win+G para abrir/fechar. API incerta, opção desligada, PS HID, microfone, bandeja e chamadas diretas de abertura não abrem no desktop. Depois da abertura autorizada, a navegação pelo controle funciona normalmente. Ativar a opção não abre a interface.

Reutilizar `ProbeActivations.cs`, sem registrar outro atalho nem capturar teclado. Reiniciar a leitura no fim do log e invalidar callbacks antigos ao mudar preferência/modo. Mudanças entre FSE e desktop ocultam a barra; a próxima abertura exige novo comando válido. Separar a permissão de navegação da permissão de ativação pelo PS em todos os caminhos do gesto HID.

## Consequências

A prévia precisa estar em execução na bandeja. Um remapeador que gere um evento Win+G válido é indistinguível do atalho recebido pelo Windows; a restrição não identifica a origem física do teclado. O PS lido diretamente pelo HID continua bloqueado no desktop.

A preferência permanece desligada na publicação; cabe ao usuário ligá-la. Não há mudança de pacote, certificado, HidHide, Steam/DSX ou estado FSE.

## Evidências

Continuidade e publicação. São 25 verificações novas com substitutos, bateria completa aprovada e smoke somente leitura do publicado. O atalho físico no desktop ainda precisa de teste pelo usuário.

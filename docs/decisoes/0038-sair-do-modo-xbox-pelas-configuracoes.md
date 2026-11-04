# 0038 — Sair do modo Xbox pelas Configurações

- **Status:** Aceita
- **Data:** 2026-10-25

## Contexto

O usuário ficou com o FSE ativo sem uma saída acessível e pediu explicitamente desativá-lo agora e adicionar a função em Configurações. A ação de Energia "Sair do modo Xbox" ainda era simulada. A tela de Configurações também mantinha textos obsoletos de abertura por Win+Shift+G e microfone.

## Decisão

- Usar a API do Windows `SetGamingFullScreenExperience(FALSE)`, somente por pedido explícito do usuário ou pelo botão. Nunca ativar o FSE automaticamente nem criar configuração persistente, tarefa, serviço ou alteração de pacote.
- Implementar `GamingExperience.Exit()`: leitura do estado, no-op se já estiver no desktop, verificação `CanSetGamingFullScreenExperience` e tratamento do HRESULT. O wrapper expõe apenas saída.
- Adicionar **Configurações → MODO XBOX → Sair do modo Xbox**, detalhe "Voltar ao desktop". Reutilizar a mesma ação no painel Controle e energia; as outras três ações de energia permanecem simuladas.
- Desabilitar se API/permissão estiverem indisponíveis, já estiver no desktop ou houver pedido pendente. Não repetir o comando por clique duplo.
- Executar fora da thread da interface. O retorno da API é despacho aceito: aguardar observar FSE `false` para confirmar. Consultar a cada 200 ms por até seis segundos após o retorno; erro/timeout/API incerta não anunciam sucesso.
- A política da ADR 0037 oculta a barra ao sair; PS/Win+G deixam de abri-la no desktop. Reabilitar o botão numa futura abertura no FSE sem depender de reconstrução do painel.
- Corrigir o texto de ABERTURA para **PS ou Win + G**, condicionado ao FSE, removendo o exemplo antigo de microfone.

## Evidências e limites

**Saída real autorizada, 11:29:12:** antes `FSE=True`, `CanSet=True`; `SetGamingFullScreenExperience(FALSE)` retornou `S_OK` (`0x00000000`), leitura posterior `FSE=False`. O agente não reativou o modo para testar o botão.

Assinatura conferida em `C:\Program Files (x86)\Windows Kits\10\Include\10.0.26100.0\um\gamingexperience.h`: retorno `HRESULT`, argumento `BOOL`, chamada `WINAPI`. Não foi necessário REA nem modificar binários.

Regressões isoladas cobrem botão nos dois painéis, textos, permissões, duplicidade, despacho sem observação, saída confirmada, reentrada, erro, timeout e leitura incerta. Comando/API/relogio substituídos; render WPF inspecionado. O clique físico da nova versão permanece para o usuário, em uma futura entrada no FSE.

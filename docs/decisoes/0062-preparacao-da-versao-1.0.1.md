# 0062 — Preparação da versão 1.0.1

- **Status:** Aceita para implementação; lançamento aguarda aprovação
- **Data:** 2026-10-27
- **Revisão:** 2026-10-27, 15:35. A revisão de código corrigiu dois bugs de energia (itens 5 e 9); candidata final `XGB_P-1.0.1-20261027-153501`.

## Contexto

O usuário considerou o produto pronto para o fechamento e pediu limpeza de lançamento: remover menções de produto em estágio de design, placeholders e lógicas incompletas, inclusive desligar o Windows, e informar o que falta antes da aprovação. A primeira versão definida é **V.1.0.1**.

O levantamento encontrou energia simulada no fluxo normal, um botão Painel inicial sem efeito, movimento reduzido só em memória e marca/textos de protótipo. Fixtures dos testes, casos de indisponibilidade real e registros históricos não são placeholders do fluxo normal.

## Decisão

1. O host WPF residente existente é a aplicação da V.1.0.1. A ponte local de protocolos da identidade XboxGamingOverlay permanece como dependência explícita, fora do ZIP; não é uma distribuição pública/autossuficiente do pacote Microsoft.
2. Produto com nome **XGB_P**, executável **XGB_P.exe**, versões de assembly/arquivo **1.0.1.0** e informativa **1.0.1**. Título, bandeja, erros e Sobre não apresentam rótulos de design/protótipo. Nomes técnicos legados e mutex são mantidos onde necessários à compatibilidade com a instância anterior.
3. Retirar o botão de preferência sem efeito. Salvar movimento reduzido em `settings/appearance.json`, com tamanho/schema validados, backup, erro visível e retry; respeitar as animações desativadas no Windows.
4. Suspender, reiniciar e desligar são reais após confirmação. Foco inicial em Cancelar; voltar/cancelar/trocar painel invalidam confirmação ainda não consumida. Confirmação é consumida uma vez; reentrância, clique repetido e entrega incerta não repetem efeitos.
5. Antes do envio, exigir teclado e ciclo Steam Input liberados, concluir operações, retomar os jogos pausados pelo app e conferir recuperação/persistência. Falhas bloqueiam a operação. Escritores que podem atualizar feedback WPF ficam na thread da interface; recuperação/histórico/comando ficam no worker. Só bloqueia a gravação que pode e deve acontecer: histórico ilegível, preservado sem escrita por design, não conta como pendência. Histórico gravável com gravação pendente continua bloqueando.
6. Suspensão usa `Application.SetSuspendState(PowerState.Suspend, false, false)` do WinForms. Reinício/desligamento usam caminho absoluto de `shutdown.exe` do System32, `/r` ou `/s`, `/t 0`, `/d p:0:0`, sem `/f`. Não se altera configuração de energia ou eventos de wake. Pedido aceito não comprova estado físico final.
7. A bateria substitui todos os efeitos de energia. Nenhuma suspensão/reinício/desligamento real é executada pelo agente. Testes físicos são pendências explícitas antes da aprovação.
8. Gerar candidata/ZIP separados, sem substituir/reiniciar a instância usada no dia a dia. Preservar dados e principal, sem novo commit/push solicitado nesta etapa. Documentação de uso corrente descreve a versão; históricos/evidências mantêm o contexto original.
9. Falha ou entrega incerta reabre a mesma barra com o erro, inclusive no desktop. Ali, `Show()` só reabre pelo Win+G, e a barra que pediu a energia já estava aberta. A política de ativação continua valendo: API incerta ou desktop sem a opção não reabrem.

## Consequências e limites

Renomear o executável muda o caminho liberado no HidHide e o alvo de inicialização: a migração precisa ser conferida pelo usuário. A preferência administrativa continua usando UAC normal; não há autoelevação extra, tarefa, serviço ou instalador novo.

A ponte instalada é um componente local. Certificado/instalação/HidHide continuam operações do usuário conforme as instruções do projeto. Gate automático não aprova lançamento, não mede gameplay/TV e não transfere resultados de um build anterior para o final.

Com histórico ilegível, a energia segue sem gravar o histórico da sessão. É o mesmo comportamento que o app já tinha no resto do tempo, e o aviso continua no status dos jogos.

## Evidências e referências

- Relatório e pendências, guia de uso, continuidade.
- [Microsoft: shutdown](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/shutdown): timeout positivo implica fechamento forçado; timeout zero e ausência de `/f` preservam o tratamento normal dos aplicativos.
- [Microsoft: Application.SetSuspendState](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.application.setsuspendstate?view=netframework-4.8.1) e [SetSuspendState](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-setsuspendstate): contrato de suspensão, resultado/recusa e eventos de wake.

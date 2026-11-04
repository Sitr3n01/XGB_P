# 0065 — Versão 1.0.2 e entrega local

- **Status:** Aceita; atualização local autorizada, validação física final pendente
- **Data:** 2026-10-27
- **Revisa:** versão/entrega da ADR 0062; incorpora as ADRs 0063 e 0064.

## Contexto

Após aprovar as proporções corrigidas da Home, o usuário pediu documentar o trabalho e atualizar a pasta `.exe` e todo o projeto para **v1.0.2**, informando que havia encerrado o processo. A conferência inicial encontrou zero instâncias e `.exe` vazia.

## Decisão

1. Produto **1.0.2**, assembly/arquivo/manifests **1.0.2.0**, Sobre e testes de versão coerentes. A versão da ponte local é independente e não será alterada/reinstalada.
2. Consolidar transição/retorno entre jogos, agrupamento da Home, confirmação sem imagens e cartões uniformes. Esta etapa não modifica novamente essas regras.
3. Gerar candidata isolada e exigir o gate completo antes de publicar. Popular `.exe` com a lista fechada da entrega, guia e metadados; atualizar `bin/app/XGB_P.exe` e o caminho legado `bin/XgbDesignPreview.exe` com o mesmo binário validado e metadado correspondente, preservando backups.
4. Não iniciar residente pelo agente. O usuário abre pelo Explorer para evitar a virtualização do AppData dos apps empacotados. Não migrar configurações, inicialização, HidHide ou dados automaticamente.
5. Documentação corrente aponta para 1.0.2. Guias/ADRs/relatórios/evidências anteriores conservam seus nomes, hashes e contexto; não reescrever a história como se fosse teste da nova versão. `.exe` é saída de build ignorada pelo Git.
6. Publicação local autorizada não certifica FSE/DualSense/jogos/TV, energia física ou lançamento público. Sem commit/push, instalação de pacote, certificados ou distribuição de sons Microsoft.

## Validação e reversão

Conferir versão do executável, recibos do gate por hash, fontes, manifesto/ZIP, igualdade dos três caminhos publicados e preservação dos dados na visão acessível ao agente. Renders de fixtures são apenas evidência de interface.

Antes de reverter, encerrar a instância e conferir recuperação/Steam Input; restaurar juntos executável e metadado dos backups registrados. Não apagar catálogo/histórico/diário de pausas.

Relatório 1.0.2 · guia · [mudanças](../../CHANGELOG.md).

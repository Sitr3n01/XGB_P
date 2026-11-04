# 0056 — Estabilização e candidata local de testes

- **Status:** Aceita; implementação e gate locais. Aprovação de lançamento depende dos testes físicos e da integração final.
- **Data:** 2026-10-26
- **Preserva:** identidade Microsoft estritamente local, HID somente leitura, políticas de foco/FSE, teclado, Biblioteca e destaque aprovados.

## Contexto

Após a análise rigorosa, o usuário autorizou corrigir o código e preparar os primeiros testes de lançamento, com meta de notas 9,5 ou superiores. A prévia reúne operações reais, mas a fase de design e o núcleo/ativador empacotado continuam separados. Uma meta de pontuação não substitui evidência de produto.

## Decisão

1. Identificar processos por PID e criação desde a observação. Conferir essa criação no mesmo handle que recebe suspender, retomar ou encerrar. Recusar identidade incerta e impedir ações sobre descendentes quando a raiz é rejeitada.
2. Serializar comandos de jogos num coordenador. Registrar a intenção de pausa antes do efeito nativo, atualizar pelos resultados individuais e compensar falhas parciais. Encerramento tratado aguarda a transação e tenta recuperar sem depender da Dispatcher. Repetir uma pausa pendente é recusado.
3. Persistir o diário em %LOCALAPPDATA%/XGB_P/games/paused-by-xgb.json. Retomar somente identidades correspondentes. Acesso negado e criação ilegível conservam pendências; processo encerrado ou PID reciclado libera apenas a identidade antiga. JSON inválido é preservado e bloqueia novas pausas. Não há serviço de recuperação enquanto a barra está fechada.
4. Separar histórico, índice de processos, cache de descoberta, protocolo de ações e entrada do programa. Fazer retry de histórico com backoff. Usar índice com ambiguidade preservada, polling adaptativo, cache limitado e buffer nativo pequeno com fallback para caminhos longos.
5. Limitar fila/validade dos comandos de mídia, cache de ícones e log. O log escreve em worker, rotaciona e expõe perda/falha; não promete armazenamento ilimitado. Falha de entrega/agendamento não prende a fila de mídia.
6. Não esperar a instalação dos hooks de foco na UI. Sinalizar o encerramento das threads e liberar o evento quando ambas saírem. Limitar a uma consulta UIA em voo: um fornecedor externo preso pode manter uma thread de fundo e impedir novas consultas automáticas até reiniciar o processo, sem multiplicar threads a cada entrada no FSE.
7. Compilar o candidato isolado com C# 5/.NET Framework, avisos como erros e inventário de fontes. Cada verificação produz recibo novo com modo, GUID, hash do executável, intervalo UTC e resultado. Instância duplicada retorna erro. Gate exige suítes, testes nativos próprios, diagnósticos somente leitura e fontes inalteradas.
8. Preparar ZIP local com lista fechada de arquivos e hashes. Conferir preservação do principal/dados. Excluir pacote da identidade Microsoft, certificados, WAVs e dados privados. Não instalar, iniciar residente, publicar, fazer commit ou push nesse preparo.

## Consequências

- As verificações de concorrência e falhas exercitam o mesmo serviço que a prévia usa; os testes nativos operam apenas processos e janelas criados para eles.
- O gate é reproduzível localmente, mas não é uma auditoria de cobertura, uma prova de FPS ou uma certificação de nota 9,5. Tetos legados explícitos impedem crescimento, sem declarar resolvida a concentração de responsabilidades na UI.
- O observador real é medido isoladamente. Aquecimento e coleta de lixo antecedem a comparação de handles; o resultado curto não substitui duas horas de residência com jogos.
- A prévia pode ser testada localmente. FSE, DualSense/DSX/Steam, jogos, privilégios, proteção, TV/DPI, troca real de destaque e recuperação física seguem no roteiro humano.

## Evidências

Relatório de revisão, continuidade, [roteiro de testes](../guias/primeiros-testes-lancamento.md), evidências locais e [política do gate](../../quality/README.md).

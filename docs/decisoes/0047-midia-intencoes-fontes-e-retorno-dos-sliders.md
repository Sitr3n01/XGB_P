# 0047 — Mídia: intenções, fontes e retorno dos sliders

- **Status:** Aceita; implementada e verificada, teste físico no FSE pendente
- **Data:** 2026-10-25
- **Revisa:** interação dos sliders do player da ADR 0015; complementa ADRs 0016 e 0035.

## Contexto

O usuário pediu uma auditoria antes de relatar seus sintomas. A revisão reproduziu em memória volume solicitado em 25% voltando visualmente a 70%, dois cliques de mute pedindo mutar duas vezes, troca de fonte herdando valor e substituindo comando anterior, e falso positivo de executáveis com mesmo nome. Também identificou seek sem fonte capturada, identidade por posição na enumeração, reconstrução durante edição, tarefas de transporte sem ordem e capa corrigida com mesmo tamanho ignorada.

Depois, o usuário confirmou que ajustar o slider mutava a mídia e misturava comandos, e que ○ saía diretamente para o trilho. Autorizou as correções. O código anterior executava mute em ✕/Enter no volume e play/pause na posição, embora esses gestos também fossem usados para entrar no slider.

## Decisão

1. **Sliders do player ajustam valores.** ✕/Enter confirma o foco/edição sem mute ou reprodução. Mute e play/pause ficam nos botões próprios. ○/Esc no volume retorna ao alto-falante; na posição, ao botão de reproduzir/pausar, sem acioná-los. Outro voltar segue a navegação normal. Vale na Home e no Som. Volume/microfone/mixer fora do player mantêm a interação anterior. Repetição automática de Enter/espaço não repete comandos.
2. **Volume/mute preservam a intenção em voo.** `audioPending` não é toda a operação: `audioInFlight` acompanha o lote até leitura válida posterior, falha ou limite de seis segundos. A UI combina o snapshot com a intenção mais nova por dispositivo/sessão/campo. Volume não altera mute; mute não altera nível. Player e mixer refletem a mesma intenção nativa.
3. **Fonte e alvo não se misturam.** A chave do player inclui a sessão de mídia. Coalescer comandos não enviados apenas quando campo/dispositivo/alvos se sobrepõem, preservando os demais alvos. IDs de sessão são capturados ao enfileirar. Erros conservam o contexto e não aparecem como erro da outra fonte.
4. **Seek vinculado ao gesto.** Capturar sessão e faixa ao ajustar; cancelar na troca explícita ou descartar se a fonte/faixa mudou antes do envio. Nunca consultar a nova fonte para retargetar os segundos antigos.
5. **Identidade SMTC por objeto COM retido.** `MediaSessionKeys` gera chaves locais opacas por identidade `IUnknown`, retém o objeto durante sua presença e remove ausentes. Reordenar não renomeia; fechar/recriar não reaproveita a chave antiga. `Find` reenumera e valida a mesma identidade, sem fallback ordinal. Chaves não são persistidas entre processos. Uma identidade nova é sessão nova, sem tentar adivinhar equivalência.
6. **Correspondência de áudio conservadora.** Caminhos conhecidos não caem no fallback por nome de exe. Quando o SMTC só informa o nome e o mixer apresenta caminhos distintos correspondentes, volume/mute ficam indisponíveis, em vez de controlar instalações diferentes.
7. **Transporte em FIFO e leitura com revisão.** Uma drenagem serial preserva a ordem dos gestos, continua após erro e descarta comandos ainda não iniciados ao encerrar. Leituras anteriores a uma ação não sobrescrevem o estado posterior. Não iniciar leitura periódica com comandos em curso. Erros antigos ficam no log sem substituir o contexto de outra fonte/ação.
8. **Edição preservada.** Adiar snapshots/reconstruções durante edição/captura; aplicar o último snapshot ainda válido ao sair. Se o controle ficar indisponível, devolver foco a uma ação habilitada no player.
9. **Capa por conteúdo.** Comparar bytes em worker e reutilizar a referência apenas quando iguais. A UI reconhece nova referência mesmo com o mesmo comprimento.

## Consequências e limites

Os comandos reais continuam somente por escolha do usuário. Sem setters nativos nos testes de mídia; sem instalação, configuração de Steam/DSX/HidHide, saída HID ou entrada no FSE. Comando nativo já em execução pode terminar ao encerrar; os ainda na fila são descartados. Restrições/capacidades dos aplicativos continuam válidas.

40 verificações com dependências controladas e bateria completa aprovadas; leitura real de Spotify/Firefox manteve chaves em duas consultas consecutivas. Isso não comprova todos os provedores nem o controle físico no FSE. Evidências, publicação e continuidade.

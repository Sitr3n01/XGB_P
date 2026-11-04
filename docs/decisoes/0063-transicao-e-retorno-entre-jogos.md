# 0063 — Transição estável e retorno entre jogos

- **Status:** Aceita; implementada, teste físico pendente
- **Data:** 2026-10-27
- **Complementa:** ADRs 0024, 0041, 0046, 0050 e 0058.

## Contexto

O usuário relatou uma camada invisível ao voltar do Genshin ao Neva e uma tela cinza ao fechar um deles com ambos abertos. Pediu diagnóstico antes de editar e depois autorizou a implementação das regras propostas.

O log do XGB_P às 16:22:24 confirma a janela do Neva. O `controller_ui.txt` da Steam registra, na sequência, contexto do Neva (`2420660`) seguido de ClientUI (`769`). A divergência está observada; a superfície visual da camada não foi identificada. Às 16:22:35, o fechamento do Neva devolveu o foco ao próprio jogo; a frente passou para `GamingFallbackWindow` do Explorer. O Genshin continuava aberto. `CheckGameClose` não tinha entrega ao sobrevivente.

## Decisão

1. Cartões e ícones de apps que pertencem a jogos usam a mesma transição. Não relançar, pausar ou fechar os demais jogos ao alternar. Preservar o alternador genérico para apps comuns.
2. Aguardar o ciclo completo do Steam Input, inclusive a liberação após a navegação ficar disponível. Pedir a janela exata do jogo, sem promover o último popup auxiliar. Restauração de minimizada continua usando a API existente.
3. Ocultar após observar o primeiro foco; continuar observando até pelo menos 750 ms de estabilidade. Consultar somente os novos registros de contexto da Steam, em worker, com uma leitura em voo e limite de tamanho. Separar game/window/URL forcing, ignorar Guia momentâneo, normalizar atalhos signed e descartar linhas incompletas. Log ausente implica contexto Steam não confirmado, não sucesso de input/overlay.
4. Se o Windows ainda mostra o destino, mas a Steam registra outro contexto, permitir uma recuperação: janela própria de ferramenta, transparente/sem hit test, recebe foco sem Alt e entrega novamente ao destino. Não matar/ocultar janelas da Steam, alterar perfis, fixar appid de jogo ou enviar teclas ao jogo. Até três pedidos de foco e prazo total de oito segundos. Falha encerra o fluxo e remove a ponte transparente; não declarar estabilidade com contexto divergente.
5. Fechar continua sendo pedido normal, com confirmações do próprio jogo preservadas. A ausência no inventário só confirma saída quando os processos observados terminam ou mudam de identidade, com criação/saída lidas no mesmo handle. Acesso negado/identidade desconhecida não prova término. Timeout prepara a oferta de forçar, sem fazê-lo automaticamente.
6. Somente no modo Xbox confirmado, fechar o jogo ativo arma retorno após a saída confirmada. Escolher o sobrevivente com foco mais recente, depois início mais recente e chave ordinal. Revalidar janela/processo; retomar automaticamente somente uma pausa registrada pelo XGB_P. Pausa externa ou janela indisponível exige intervenção. Jogo de fundo não arma retorno.
7. Se o destino sair durante a entrega, escolher outro sobrevivente dentro do mesmo prazo. Processos conhecidos ainda vivos/ilegíveis, mesmo omitidos do catálogo, impedem o falso estado de nenhum jogo aberto. Sem jogos restantes, pedir explicitamente o Início do Big Picture, reutilizando sua janela; não iniciar a Steam fechada automaticamente.
8. Fechamento pelo próprio jogo também participa, quando sua sessão foi observada em foreground e o término nativo é confirmado. Não há hook: usa o polling existente; uma sessão que inicia e termina entre leituras pode não ter sido observada.
9. Outra janela escolhida, reabertura/fechamento manual da barra, navegação, nova seleção, mudança/incerteza de modo, energia ou encerramento cancelam a intenção. Somente ausência de foreground, o destino ainda escolhido ou a classe `GamingFallbackWindow` pertencente ao Explorer do Windows permitem a ponte. Um app desconhecido não é tratado como fallback. Não disputar foco com um aplicativo já na frente.
10. Candidata separada; preservar binários e residente atuais, configurações e dados. Sem publicação, instalação, FSE/jogo novo, energia real, commit ou push nesta implementação.

## Consequências e limites

Há confirmação por período de estabilidade, sem atraso fixo anterior a toda entrega. A observação do contexto é indício interno da Steam, não contrato público nem prova de imagem renderizada. Mudanças de formato/inexistência de log reduzem a confirmação a janela estável; o log do XGB_P distingue essa condição. A recuperação limitada precisa ser validada fisicamente com Neva/Genshin, nos dois sentidos, e não garante corrigir todo overlay interno.

O retorno automático é conservador: se outro app já estiver em foreground, ele prevalece. Uma Steam que passa à frente por conta própria também pode cancelar o retorno; não é possível atribuir toda troca ao usuário somente pelo foreground.

As regras antigas da ADR 0050 sobre ocultação/confirmação permanecem; esta decisão acrescenta a etapa posterior de entrega. A ADR 0046 continua regendo apps comuns. Preferências, Steam/DSX/HidHide e diário não são migrados ou regravados por esta correção.

## Evidências

Continuidade e candidata e evidências. Fixtures substituem jogos, foco, Steam e relógio; consultas nativas novas exercitam somente o próprio processo. O teste físico permanece pendente.

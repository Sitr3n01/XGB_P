# 0082 — Painel AMD: ajustes gráficos globais pelo ADLX

- **Status:** Aceita; implementada em candidata em desenvolvimento, não publicada. Teste físico pendente.
- **Data:** 2026-10-29

## Contexto

A fase 1 (ADR 0081) trouxe a captura do ReLive. Na fase 2, o usuário pediu que os ajustes gráficos da AMD ficassem num **painel separado, com o logotipo da AMD** no trilho: gravação num painel, ajustes em outro. Ele aprovou a prévia (`previa-ajustes`, `previa-ajustes-valores`, `previa-gravacao`) e pediu para manter tudo em desenvolvimento, sem interferir na `.exe` publicada.

A investigação (evidências) achou o ADLX 1.5 no driver e tirou as posições das vtables dos cabeçalhos públicos do SDK (GPUOpen, MIT). Uma sonda somente leitura confirmou tudo na RX 9070 XT: FSR 4 e Anti-Lag ligados, os demais recursos suportados e desligados.

## Decisão

1. **Trilho:** o painel 8 passa a se chamar **Gravação** (ícone de câmera). O painel 9 é o **AMD**, com o logotipo vetorial (seta da AMD) que herda a cor do botão e acende com o foco.
2. **Grupos:**
   - **Upscaling e quadros:** FSR 4 (upgrade), Geração de quadros FSR (upgrade), Fluid Motion Frames, Radeon Super Resolution;
   - **Latência e desempenho:** Anti-Lag, Boost, Chill, Limite de FPS;
   - **Imagem:** Nitidez da imagem, Enhanced Sync.

   Os valores guardados no driver (faixa do Chill, limite de FPS, nitidez, resolução mínima do Boost, nitidez do RSR) aparecem só com o recurso ligado.
3. **Backend `src/lib/amd/AdlxGraphics.cs`:**
   - carrega `amdadlx64.dll`, chama `ADLXInitialize` com a versão do próprio DLL e escolhe a GPU discreta com telas;
   - as interfaces estendidas (`…Services1/3`) vêm por `QueryInterface`, e uma interface ausente vira "Indisponível" sem quebrar o painel;
   - as chamadas são serializadas, os objetos liberados a cada operação, e `ADLXTerminate` roda ao sair;
   - se a inicialização falhar, nova tentativa só depois de 30 s.
4. **Mudança:** só por escolha do usuário, um ajuste por vez, em worker. Depois do `SetEnabled`, a barra relê tudo:
   - confirmado: sucesso;
   - não aplicado: aviso (perfil do Adrenalin ou dependência);
   - recusado: erro;
   - outro recurso mudou junto: a mensagem diz qual ("O driver também desligou Nitidez da imagem").
5. **Releitura a cada 3 s** com o painel aberto, porque o Adrenalin e os atalhos podem mudar o estado.
6. **Métricas e FPS da AMD:** `ReadMetrics`/`ReadFps` existem só na lógica, sem tela. O FPS só existe com jogo em tela cheia exclusiva.
7. **Fora de escopo:**
   - ajuste de GPU (overclock, ventoinha, energia), SmartShift e SAM;
   - mudar os valores numéricos (faixas, nitidez), que fica para uma etapa seguinte;
   - FreeSync, VSR e outros ajustes de tela.

## Consequências e limites

- **Configurações globais:** os perfis por jogo do Adrenalin têm prioridade. FSR 4 e geração de quadros funcionam só em jogos compatíveis.
- **Escrita no driver real:** foi verificada só com driver substituído. O agente não ligou nem desligou nada na GPU do usuário. A leitura real foi conferida por sonda e pelo próprio backend, só leitura.
- **Versão do driver:** a ordem das funções vem do SDK público e vale para o ADLX 1.5. Uma versão futura sem uma interface estendida aparece como "Indisponível".
- **Sem elevação:** o ADLX funcionou sem administrador.

## Evidências

Investigação, sonda e prévias; `amd-ajustes-verificacao.txt`; renders `amd-ajustes` e `amd-ajustes-valores`.

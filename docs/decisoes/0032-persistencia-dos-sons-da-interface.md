# 0032 — Persistência dos sons da interface

- **Status:** Aceita
- **Data:** 2026-10-24

## Contexto

O usuário pediu que o volume e o liga/desliga dos sons da interface sobrevivam ao encerramento e à próxima abertura da prévia. As demais preferências não fazem parte deste pedido.

## Decisão

- Salvar automaticamente `Version`, `Enabled` e `Volume` (porcentagem inteira de 0 a 100) em `%LOCALAPPDATA%\XGB_P\settings\ui-sounds.json`.
- Restaurar antes de abrir os canais e tocar a abertura da barra. Sons desligados permanecem desligados; 0% permanece zero.
- Sem arquivo válido nem backup válido, usar 70% e sons ligados. Validar campos obrigatórios, versão, faixa e tamanho máximo de 4 KB.
- Ao ajustar o volume, agrupar mudanças com um timer de 350 ms após a última alteração. O efeito no som é imediato. O liga/desliga grava imediatamente; encerrar normalmente também tenta gravar qualquer ajuste pendente.
- Reusar `GameFiles.Write` para gravar em `.tmp`, descarregar para disco e mover/substituir atomicamente, guardando a versão anterior em `.bak`. Não sobrescrever arquivos inválidos apenas por iniciar a prévia; tentar ler o backup em caso de corrupção.
- Se a gravação falhar, preservar o ajuste da execução, registrar a falha e avisar na interface. Outra alteração ou o encerramento tenta novamente.
- Preferências separadas dos WAVs: apagar a pasta `sons` não apaga o volume salvo. Apagar/renomear `ui-sounds.json` e seu `.bak`, com a prévia fechada, restaura os padrões na próxima execução.
- Os modos de verificação não leem nem gravam as preferências reais. O teste de persistência usa arquivos isolados em `bin` e limpa somente sua pasta própria vazia.

## Consequências

`SoundPreferences.cs` centraliza o contrato JSON, leitura, fila de gravação, recuperação e teste. `Sounds.cs` solicita a gravação nos controles e faz o flush no encerramento. Nenhuma mudança no volume do Windows, no mixer de outros apps, nos WAVs, no pacote ou nos demais ajustes. Reduzir movimento continua só na sessão.

Uma queda abrupta do processo/PC pode perder os últimos 350 ms de ajuste do slider. O timer evita escrita síncrona em cada passo do arraste; o fechamento normal salva o valor final.

## Evidências

`VerifySoundPreferences` cobre ausência de arquivo, gravação pelo slider, gravação imediata pelo botão, releitura de ambos e reflexo na UI, limites 0/100, backup de arquivo corrompido, campos/versão/faixa inválidos, falha com arquivo bloqueado, preservação do valor anterior, nova tentativa e flush final. Os testes usam a mesma leitura e gravação do runtime, sem alterar preferências do usuário. Ver a continuidade e o estado da máquina para o build publicado.

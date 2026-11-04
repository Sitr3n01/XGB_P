# 0049 — Persistência de sessões sem depender do foco

- **Status:** Aceita
- **Data:** 2026-10-25
- **Revisa:** a exigência exclusiva de foco para persistir sessões da ADR 0019.

## Contexto

O usuário informou que o Genshin às vezes desaparece dos recentes depois de fechado e autorizou corrigir a persistência. O cadastro/runtime estavam válidos; a consulta atual retornou Genshin entre os três recentes. Não havia registro local dele, apenas a data importada da Steam (22/10 às 21:11:29). A causa exata do desaparecimento não foi capturada; não confundir essa lacuna do histórico com prova de falha de disponibilidade.

## Decisão

Confirmar uso após duas leituras consecutivas da **mesma instância da runtime exata cadastrada**, com caminho legível e PID + criação conhecidos. Não exigir janela nem foco para essa confirmação. Não basta um processo com mesmo nome ou qualquer executável da pasta: esses casos conservam a confirmação por duas leituras com foco. Runtimes ambíguas entre jogos, interpretadores e executáveis auxiliares não confirmam pelo novo caminho em segundo plano.

Ausência entre leituras ou intervalo maior que 8 s reinicia a contagem; PID reaproveitado não soma leituras de instâncias diferentes. A margem existente de 10 s para reinício interno da partida permanece. Confirmar a sessão uma vez, conservar o registro após fechar/reiniciar a prévia e renovar quando outro foco de jogo for confirmado, sem escrever a cada tick. Não atribuir foco a uma confirmação em segundo plano.

Manter formato, escrita atômica/backup e proteção de histórico ilegível. Não preencher datas passadas, mudar o mapeamento do Genshin, scripts, Steam ou critério de disponibilidade dos recentes.

## Consequências

Jogos conhecidos podem gerar histórico enquanto a prévia está rodando, mesmo em segundo plano. Se o Windows negar caminho/criação, permanece a exigência de foco. A correção vale para sessões observadas a partir do novo build; não recupera execuções anteriores sem evidência.

## Evidências

`GamePersistenceVerification.cs`: 26 verificações com processos sintéticos e JSON isolado em `bin`, incluindo fechamento/reinício, ausência, salto de tempo, PID reutilizado, nomes/auxiliares/ambiguidade e histórico corrompido. Continuidade. O teste real do Genshin permanece pendente.

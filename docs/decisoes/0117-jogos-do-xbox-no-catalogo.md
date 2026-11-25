# 0117 — Jogos do Xbox e do Game Pass no catálogo

- **Status:** Aceita; candidata pública 3.1.0. Teste físico pendente.
- **Data:** 2026-11-25
- Cobre o provedor Xbox que a [ADR 0019](0019-jogos-recentes-locais-e-sessoes.md) deixou de fora.

## Contexto

Os jogos instalados pelo app Xbox (inclusive pelo Game Pass) não apareciam na barra: o catálogo só lia a Steam, os atalhos da Steam e os cadastros locais.

## Decisão

- **Leitura:** pacotes do usuário (`HKCU\…\AppModel\Repository\Packages`) que têm `MicrosoftGame.config`, com o identificador conferido no `AppxManifest.xml`. Só leitura, sem API de instalação. XML sem DTD e com limite de 1 MB; pacote ilegível não derruba os demais.
- **Filtros:** executável de PC dentro da pasta do pacote. Builds de console, de desenvolvimento e conteúdo sem programa ficam fora.
- **Processo:** aceita o caminho do pacote, o dos Serviços de Jogos e o caminho final da pasta. Como reserva, a identidade do pacote com o nome do executável; a identidade sozinha não basta, porque processos filhos a herdam. `gamelaunchhelper.exe` não conta como jogo.
- **Chave:** `xbox:<família>!<id>`. Não entra nas regras da Steam. Se já existe um registro (atalho da Steam, cadastro local) para o mesmo executável, ele prevalece.
- **Abertura:** `shell:AppsFolder\<família>!<id>`. Com a barra elevada, pelo Explorer ([ADR 0096](0096-pesquisa-de-programas-pelo-acesso-rapido.md)), para o jogo não nascer como administrador; sem o Explorer, direto.
- **Home:** o jogo aparece quando está aberto ou depois de observado pela barra, como os demais. O Windows não informa um último uso, então não há data importada. "Biblioteca" continua abrindo a da Steam.

## Consequências

- Leitura real, só leitura, na máquina do autor: três jogos instalados, os três disponíveis.
- 24 verificações (`tests/XboxGamesVerification.cs`), com pacotes de teste numa pasta temporária.
- Não validado: abrir um jogo, reconhecer o processo real, pausar, fechar e o modo Xbox.
- Fora do escopo: jogos UWP antigos, sem `MicrosoftGame.config`.

## Evidências

Leitura real do catálogo e suíte da 3.1.0.

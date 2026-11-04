# 0021 — Imagens locais automáticas dos jogos

- **Status:** Aceita
- **Data:** 2026-10-23

## Contexto

O usuário aprovou usar os ícones existentes dos jogos e pediu uma lógica permanente de extração. A Home já identifica jogos Steam, externos e cadastrados localmente, mas mostrava o mesmo símbolo genérico. A proporção do cartão Voltar ao jogo deve permanecer como ajustada na referência do usuário.

## Decisão

Adicionar `GameImages` ao backend independente de loja. Cada jogo conhecido, ao precisar aparecer na Home, passa pelo mesmo resolvedor, em worker:

1. `IconLocation` explícito no cadastro ou informado pelo atalho externo Steam.
2. Ícone de atalho `.lnk`/`.url` do desktop do usuário ou público, associado ao executável exato, à URI Steam ou a `steam.exe -applaunch <appid>`. Não associar por nome de arquivo/título.
3. Recursos do executável de lançamento próprio do jogo, depois suas runtimes conhecidas. Ignorar interpretadores/launchers genéricos, como WScript, CMD e Steam, nesse fallback.
4. Símbolo de jogo U+E7FC quando não houver imagem válida. Falha de arte nunca desabilita o jogo ou impede a leitura do catálogo.

PNG/JPEG/BMP/ICO locais e recursos EXE/DLL são aceitos. Índices de recursos, inclusive negativos, são respeitados. Recursos são lidos por `SHDefExtractIconW`, solicitando 256 px, sem iniciar o executável. PNGs são normalizados em 64×64 com transparência e proporção preservadas; a Home mostra 32×32 dentro do espaço existente de 48×44. Nenhum tamanho de cartão, gesto ou regra de recentes foi alterado.

Cache em `%LOCALAPPDATA%\XGB_P\games\images\`, com nome derivado de caminho, tamanho, data de alteração, índice e versão do extrator. Não é hash do conteúdo completo dos executáveis. Mudanças nesses metadados invalidam a imagem; cache PNG corrompido é refeito. Falha ao gravar cache conserva a imagem em memória. Entradas antigas não são apagadas automaticamente.

Revisão da decisão de símbolo uniforme na ADR 0019: o símbolo permanece como reserva. O layout e a permanência do último jogo da ADR 0020 continuam iguais.

## Consequências

- Funciona para qualquer origem representada no catálogo, sem cadastrar uma imagem obrigatória por jogo. A descoberta de jogos fora dos provedores existentes continua exigindo cadastro, como antes.
- Não cria capas ausentes nem garante que um executável forneça uma arte específica; sempre há o símbolo de reserva. Nenhuma busca/download de arte, mudança em atalhos ou escrita nos jogos/Steam.
- Atalhos são relidos junto do catálogo; arquivos de imagem mudados são detectados pelo fingerprint. A extração não ocorre na thread da UI.
- O diagnóstico live grava seu cache apenas em `bin/design-preview/games-images-live-cache`; a execução normal usa a pasta local acima. Não há mudança no formato do histórico.

## Evidências

- [Contratos e fontes](../referencia/imagens-jogos.md).
- `-Verificar` passou, incluindo prioridade, recurso nativo, imagem ausente/inválida, cache persistente/corrompido/inválido, escrita indisponível, interpretadores, leitura de `.lnk`/`.url` com fixtures e proporção do cartão.
- Extração real bem-sucedida dos executáveis de **Wuthering Waves**, **Genshin Impact** e **CONTROL Resonant**, sem iniciar os jogos. Render `bin/design-preview/home-jogos-reais.png` inspecionado.
- Resultados preservados em evidências das imagens, com hashes e sem builds.

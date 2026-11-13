# 0110 — Integração da TV Samsung

- **Status:** Aceita; portada para a candidata pública 3.0.0.
- **Data:** 2026-11-08

## Contexto

Integrar uma TV Tizen pela rede local, preservando a autoridade do estado de energia.

## Decisão

Portar o núcleo para src/lib/tv em C# 5. Identidade vem de configuração local, nunca de um alvo fixo no código. Importar configuração do SamsungTvOrchestrator sem escrever na origem. Usar DPAPI, persistência atômica, mutex compartilhado e TLS com certificado fixado por conexão. Exemplos usam identificadores fictícios; a automação começa desligada.

Ver [guia da TV](../guias/tv-samsung-integrada.md).

## Validação e limites

A build pública mantém produto e testes separados. Suítes com substitutos não comprovam controle, TV, Steam, áudio ou energia físicos. A publicação no GitHub e a matriz física permanecem pendentes. Ver [comparação e validação do porte](../atualizacao-publica-2026-11-08.md).

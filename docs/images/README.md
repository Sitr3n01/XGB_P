# Imagens do README

Todas as telas são **renders da build de testes** (`XGB_P.Tests.exe`) da candidata 3.0.0, gerados pela suíte `--verify-motion` com dados de teste. Não são capturas de uma sessão real: jogos, apps, dispositivos, a TV e os números de desempenho são valores das fixtures em `tests/`. A interface está em português.

## Origem

Os renders saíram da validação local do porte de 08/11/2026 (build de testes **06E846E0…**, fontes **9BA120EB…**; ver o [relatório do porte](../atualizacao-publica-2026-11-08.md#validação)). Nenhum arquivo de `src/` ou `tests/` mudou depois dessa validação. A suíte grava cada render 1920 × 1080, com fundo transparente e a camada escura da cena (preto com alfa 72) sobre a tela inteira.

Para montar as imagens, a camada escura foi retirada (alfa até 80 vira transparente), a barra foi recortada pela área visível e colada sobre o fundo `#181A1B`, em escala 1:1. Nenhum conteúdo foi editado.

| Imagem | Render de origem |
|---|---|
| `paineis.png` | `home-jogos-abertos`, `programas-busca`, `amd-ajustes`, lado a lado |
| `configuracoes.png` | `configuracoes-3.0.0` |
| `tv-samsung.png` | `tv-integrada-configuracoes` |
| `gravacao.png` | `amd-painel` |
| `som.png` | `painel-6` |
| `desempenho.png` | `painel-3` |
| `energia.png` | `suspender-despertar` |
| `sem-trilho.png` | `titulo-deslizante-0` |
| `teclado.png` | `teclado-completo` |
| `teclado-jogo.png` | `teclado-modo-jogo` |

## Capa

`social-preview.png` (1280 × 640) é a imagem de prévia do repositório no GitHub, no mesmo formato das capas dos outros projetos do autor. À direita, o recorte de `home-jogos-abertos` em 86%. Os três números vêm do README: a latência da [ADR 0059](../decisoes/0059-despacho-local-da-navegacao-steam.md), o total de ADRs e a invariante de só leitura do DualSense. A imagem foi montada em HTML e capturada pelo Edge sem interface, com as fontes Segoe UI Variable e Cascadia Mono do Windows.

A prévia é configurada à mão em **Settings → General → Social preview** do repositório; o GitHub não a lê deste arquivo.

## Regenerar

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\compilar-xgb.ps1 -Verificar
```

Os renders ficam em `bin\app\design-preview\*.png`. Se o layout mudar, refaça as composições a partir dos mesmos nomes e atualize esta tabela.

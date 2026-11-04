# Biblioteca da Steam no Big Picture — diagnóstico de 2026-10-26

## Complemento de 27/10 — Home explícita

O mesmo `sp.js` foi reconferido por SHA-256 em 27/10. A tabela no offset UTF-16 12390800 registra `open/bigpicture`; o handler `function fe` no offset 12395094 chama `SteamClient.UI.SetUIMode(a.$l2)` e não pede Home. Já `open/library` é registrado junto de `open/games`/`nav/games`; `OnOpenLibrary` divide os componentes e `NavigateLibrary`, offset 12111360, trata `library` com `EnsureLargeMode(), Oe.Home()`. Portanto, `steam://open/library` é o pedido de Home na janela Big Picture existente, apesar do nome da URL.

Teste real no desktop, aproximadamente 12:56: a coleção Todos foi aberta, depois `steam://open/library` retornou à tela Jogos recentes da Home. A implementação e os limites estão na [ADR 0058](../decisoes/0058-navegacao-explicita-home-e-biblioteca.md) e na continuidade. Não é teste físico do FSE. REA reteve análise parcial por timeout (`ev_a5033439d92192e48d6c341782ce081bccaf66cedd53dc6c0cb711abc70e9b8b`); os trechos foram conferidos diretamente, sem executar o JavaScript.

## Resultado

O botão Biblioteca do XGB_P usava uma rota que, no cliente instalado, termina na Home do Big Picture. O diagnóstico inicial foi feito por leitura do fonte, do log e do JavaScript da Steam. Após autorização do usuário, a causa foi reproduzida na Steam real e a rota foi corrigida; ver continuidade e ADR 0054.

## Estado anterior observado no XGB_P

Os trechos/linhas desta seção descrevem o código anterior à correção, não o handler atual. Para a implementação vigente e os marcos de publicação, ver a consolidação.

- No código anterior, `src/app/SteamActions.cs:21–23`: `SteamLibraryUri(true)` retornava `steam://open/bigpicture/games`; desktop/API indisponível usavam `steam://open/games`.
- O trilho (`DesignPreview.cs`, `SelectRail`) e o Acesso rápido (`HomePreview.cs`, `UseHomeUtility`) chamam `OpenSteamLibrary`, que despacha essa URL e oculta a barra.
- `bin/design-preview/preview.log:37895–37896` registra, em 26/10/2026 às 15:38:32–33, `Biblioteca: modo Xbox=True` seguido do envio de `steam://open/bigpicture/games`. O registro comprova o despacho; não comprova a página renderizada.
- No código anterior, `SteamActions.cs:109–110` aceitava exatamente essa mesma URL na verificação com substituto. O teste não observava a tela da Steam e, portanto, não detectava o destino incorreto.

## Observado no cliente Steam instalado

- Instalação confirmada pelo registro `HKCU\Software\Valve\Steam`: `C:\Program Files (x86)\Steam`.
- `steam.exe`: FileVersion `11.15.70.27`, ProductVersion `01.00.00.02`.
- Artefato lido: `C:\Program Files (x86)\Steam\steamui\sp.js`, 16.297.800 bytes, SHA-256 `692083495994D2B2CF686EFFD89C11FA987AEFA45787CBFA212627C37EDA9C7A`.

As posições abaixo são offsets UTF-16 a partir de zero e colunas a partir de zero na linha 221 do JavaScript original:

| Posição | Trecho relevante |
|---|---|
| offset 12390655, coluna 450058 | A tabela registra `["open/bigpicture/games",[a.ogI,a.$l2],ee]` |
| offset 12394835, coluna 454238 | `function ee({url:zn,instance:qe}){return h.n6.OnOpenLibrary(zn,qe)}` |
| offset 12110844, coluna 170247 | `OnOpenLibrary` divide a URL por `/`, retira os dois últimos componentes e chama `NavigateLibrary(ee,fe,Oe)` |
| offset 12111360, coluna 170763 | `NavigateLibrary` decide pelo penúltimo componente; `bigpicture` não é um dos casos tratados |
| offset 12113324, coluna 172727 | O ramo de reserva é `default:Oe.Home();break` |

Para a URL enviada pelo XGB_P, os componentes finais são `bigpicture` e `games`. A leitura do fluxo demonstra que ela cai na navegação `Home()`. A rota é registrada, mas seu destino não corresponde ao que o botão do XGB_P promete.

Trocar apenas para `steam://open/games` ou `steam://open/library` também não estabelece a correção no Big Picture: os casos `games`/`library` do mesmo método chamam `Home()`. A interface gamepad possui uma navegação própria `LibraryTab`, com o destino interno `/library/tab/<id>` e uma aba `AllGames`; esse caminho interno não prova que exista um protocolo externo equivalente.

## REA e limites

Skill aplicada: `%USERPROFILE%\.agents\skills\reverse-engineer-anything\SKILL.md`.

- Análise estática selecionada somente para `sp.js`: `ev_c8efb9f4e4398f214ab484ba30bb3feabd106bbe75fdcdb0a0a8453a395ad116`.
- Vista do artefato e identidade SHA-256: `ev_576d947df4e1ee5580b9c6408840cbf27322c268b5d3e660216f3705a331c061`.
- A análise reteve os fatos estáticos do arquivo, mas teve limite de memória na expansão das relações e timeout de 45 segundos. Não usar esse grafo parcial para alegar cobertura completa do cliente. Os trechos acima foram conferidos diretamente nos bytes originais, sem executar o JavaScript.
- No diagnóstico inicial, a ligação com o sintoma era inferida do fluxo estático e do despacho. O teste real posterior abaixo confirmou a diferença entre os destinos.

## Correção validada no cliente real

O cliente declara `AllGames = "all-games"` na enumeração de coleções (offset UTF-16 11907915 do mesmo `sp.js`). O handler registrado `open/library` aceita `steam://open/library/collection/all-games`: os dois componentes finais são `collection` e `all-games`, levando a `Navigator.Collection("all-games")` em vez de `Home()`.

Em 26/10/2026, aproximadamente 18:59–19:01, America/Sao_Paulo, o protocolo foi lançado na Steam instalada e a janela real mostrou **Biblioteca → Coleções → Todos**, com a grade completa de jogos. A rota antiga foi reproduzida na Home. As capturas registram os dois resultados. O protocolo candidato `steam://nav/library/tab/AllGames` não mudou a página e não foi adotado.

A implementação usa a coleção nativa, sem depender da última aba nem enviar teclas. O destino exato é a coleção Todos dentro da aba Coleções; não confundir com selecionar a aba Todos os jogos. O teste real foi feito com Big Picture sobre o desktop; FSE não foi ativado. Bateria completa e publicação passaram; teste físico da barra/DualSense no FSE continua pendente.

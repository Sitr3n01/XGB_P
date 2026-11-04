# 0002 — Ponte local de ativação (registro público resumido)

- **Status:** Aceita; detalhes mantidos fora do repositório público
- **Data:** 2026-10-23

## Contexto

O shell do Windows entrega o Win+G e os comandos do botão Guia apenas ao pacote do sistema da Xbox Game Bar. Não existe configuração suportada que aponte esses comandos para outro aplicativo, e a plataforma oficial de overlays é um recurso de acesso limitado.

## Decisão

O desenvolvedor usa, **somente no próprio PC**, uma ponte local que recebe esses comandos do shell e grava cada ativação num log local. O XGB_P apenas lê esse log para reagir ao Win+G.

A ponte **não faz parte deste repositório**: manifesto, scripts de instalação, certificados e a análise que levou a ela ficam num arquivo privado. Nada nela é distribuído, e nenhum binário da Microsoft é modificado ou recebe injeção de código ([ADR 0001](0001-nao-modificar-binarios-microsoft.md)).

## Consequências

- Sem a ponte, o XGB_P funciona normalmente pelo **botão PS do DualSense no modo Xbox**, lido diretamente do HID. O Win+G fica indisponível.
- O código que lê o log da ponte é opcional e não faz nada quando o log não existe.
- Quem quiser reproduzir a ativação por Win+G precisa de uma solução própria; este projeto não oferece nem documenta uma.

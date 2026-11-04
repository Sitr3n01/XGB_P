# 0029 — Lojas pelo Acesso rápido da Home

- **Status:** Aceita
- **Data:** 2026-10-24

## Contexto

O usuário pediu uma opção "Lojas" no Acesso rápido da Home, levando a outro painel com o app Xbox, a Steam, o Epic Games Launcher e outros launchers instalados.

**Observado nesta máquina, só com leitura:**

- Instalados: Xbox, Microsoft Store, Steam, Epic Games, EA app, Ubisoft Connect e Riot Client.
- O Ubisoft Connect estava aberto na bandeja.

Detalhes em [lojas-windows.md](../referencia/lojas-windows.md).

## Decisão

1. **Atalho "Lojas"** (ícone de sacola) no Acesso rápido, logo depois da Biblioteca. A linha passa a ter seis atalhos com a mesma largura: Biblioteca, Lojas, Downloads, Áudio, Desempenho e Energia.
2. **Painel "Lojas"** (painel 7), aberto pela Home como Áudio e Desempenho:
   - Esc/○ volta para a Home, com o foco no atalho Lojas;
   - o painel fica sob o Início: no trilho, o Início fica destacado e em foco, e ir ao trilho não sai de Lojas;
   - L1/R1 sai como se estivesse na Home.
3. **Lista** "LOJAS INSTALADAS · N", com o ícone real da loja (o mesmo do Menu Iniciar), o nome e o que acontece ao abrir:
   - ordem: Xbox, Steam, Epic, depois EA, Ubisoft, GOG, Battle.net, Riot, Amazon, Rockstar, itch, Heroic e Microsoft Store;
   - só aparecem as instaladas. Sem nenhuma, aparece "Nenhuma loja de jogos encontrada neste PC.".
4. **Abrir:** pelo shell, como o atalho do Menu Iniciar, e a barra se esconde.
   - A Steam abre direto na **loja**: a do Big Picture no modo Xbox e a página da loja no desktop.
   - A Epic abre na loja (`com.epicgames.launcher://store`).
   - Os pacotes (Xbox, Microsoft Store) abrem pelo AUMID. Os outros launchers, pelo executável.
   - Um erro do shell vira aviso, e a barra continua aberta.
5. **Leitura** numa thread STA a cada abertura do painel. A lista anterior aparece na hora, e a leitura nova só redesenha se algo mudou. Uma leitura que falha mantém a lista anterior.

## Consequências

- Só lojas conhecidas aparecem. Uma loja nova exige uma entrada no catálogo.
- Com um launcher já aberto, quem decide mostrar a janela é o próprio launcher, por ter instância única.
- A primeira abertura do painel mostra "Procurando lojas…" até os ícones chegarem. Depois, a lista fica em memória.

## Evidências

- `--verify` (com substitutos), em `lojas-verificacao.txt`, cobre:
  - o atalho na Home: posição, ícone, nome no título da seção e largura igual;
  - o painel: título, Início destacado, ordem, ícone e símbolo de reserva;
  - abrir esconde a barra, reabrir continua em Lojas, trilho, Esc, erro do shell, Steam no modo Xbox, R1, leitura que falha, sem lojas e lista vazia.
- `--verify-stores-live` (só leitura): 7 lojas encontradas, todas com ícone. A tela real fica só em `bin`.
- Renders e relatórios.
- **Não comprovado:** abrir cada loja de verdade e a rota da loja na Epic.

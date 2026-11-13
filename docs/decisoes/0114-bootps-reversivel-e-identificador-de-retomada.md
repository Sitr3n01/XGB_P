# 0114 — BootPS reversível e identificador de retomada

- **Status:** Aceita; portada para a candidata pública 3.0.0.
- **Data:** 2026-11-08

## Contexto

O diagnóstico deve separar ausência atual, erro parcial e causa desconhecida de uma retomada.

## Decisão

Selecionar apenas instâncias HID Bluetooth Sony DualSense/Edge, validar a tabela exata de 24 bytes e salvar o estado anterior antes da mudança. -Desfazer restaura somente o backup da máquina e usuário, sem inventar histórico. Cada retomada ganha CycleId e evento Power-Troubleshooter/1 correlacionado por XML; PS posterior é observação, com PhysicalCauseConfirmed=false. Os testes usam registro, WMI, evento e relógio substituídos.

Ver [despertar pelo PS](../guias/despertar-pelo-ps.md).

## Validação e limites

A build pública mantém produto e testes separados. Suítes com substitutos não comprovam controle, TV, Steam, áudio ou energia físicos. A publicação no GitHub e a matriz física permanecem pendentes. Ver [comparação e validação do porte](../atualizacao-publica-2026-11-08.md).

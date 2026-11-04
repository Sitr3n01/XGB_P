# 0008 — Prévia manual do design no desktop

- **Status:** Aceita
- **Data:** 2026-10-23

> **Evolução do escopo:** a descrição de ações simuladas abaixo é a decisão inicial. As ADRs 0011–0014 ativaram Steam/Configurações, Som e alternância de janelas na prévia; exemplos de jogo/recentes/métricas/energia permanecem. A exceção manual de desktop e as restrições desta decisão continuam válidas. Estado corrente no documento de continuidade.

## Contexto

> **Exceção de abertura no desktop encerrada pela ADR 0037 (2026-10-25):** a ferramenta independente começa na bandeja e só abre por PS/Win+G com FSE confirmado. Inspeções/verificações explícitas usam fixtures.

**Ampliação em 2026-10-25:** a [ADR 0036](0036-navegacao-completa-pelo-dualsense.md) adiciona analógicos/gatilhos e abertura pelo microfone segurado enquanto a prévia manual está rodando. A política do produto e a separação do pacote continuam válidas.

O usuário pediu validar o design no desktop com Win+Shift+G e autorizou preparar e iniciar tudo para essa avaliação. A ADR 0005 e a ADR 0007 definem o comportamento normal do produto como ativo somente no modo Xbox.

## Decisão

Criar uma ferramenta de prévia separada da sonda, executada manualmente sem instalação. Win+Shift+G mostra e oculta a barra no desktop enquanto esse processo estiver aberto. O registro é feito com `RegisterHotKey` e liberado ao encerrar. Disponibilizar também controles visíveis e um menu na bandeja.

A prévia usa WPF do .NET Framework para evitar novas dependências; a stack final permanece aberta em S4. A biblioteca HID existente fornece navegação pelo direcional, botões e bateria, sem relatórios de saída. Dados de produto e ações de sistema são simulados e identificados.

## Consequências

- A exceção de desktop é restrita à ferramenta de validação; não muda a política normal de Win+G, protocolos ou gestos no produto.
- Nenhuma alteração no pacote instalado, certificados, HidHide ou inicialização do Windows.
- A prévia permite revisar aparência e navegação no desktop. Não conclui os testes S1/S2 em jogos nem S3–S7.
- Win+Shift+G depende de estar disponível. Se o Windows recusar o registro, a ferramenta informa o erro. Combinações com Win são reservadas ao sistema e podem conflitar após atualizações.

## Evidências

- Solicitação e autorização do usuário nesta conversa.
- Registro temporário de Win+Shift+G aceito e liberado neste PC em 2026-10-23.
- Guia de validação.
- [RegisterHotKey — Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey).

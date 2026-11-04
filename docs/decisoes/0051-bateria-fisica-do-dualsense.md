# 0051 — Bateria física do DualSense, validade e carregamento

- **Status:** Aceita
- **Data:** 2026-10-26
- **Complementa:** ADRs 0004 e 0036

## Contexto

A análise solicitada pelo usuário encontrou conflito entre o físico Bluetooth (55%) e o virtual do DSX (100%, completa) nos logs de 26/10 às 13:10:41. A faixa usava a última fonte que mudasse. O painel Controle e energia não atualizava seu texto depois de aberto; carga completa também desenhava o raio de carregamento. Relatórios simples Bluetooth preenchidos até 78 bytes eram interpretados como USB e podiam inventar bateria. Não havia validade por chegada de relatórios nem conferência de CRC Bluetooth. O usuário autorizou corrigir esses pontos e refinar o ícone.

## Decisão

1. Separar a origem da bateria da navegação. A navegação continua podendo vir do DSX; bateria exibida exige fonte física identificada pela ancestralidade PnP, consultada somente para leitura. ViGEm/virtual é identificado; origem incerta não recebe confiança por padrão. O rótulo USB sozinho não prova origem física.
2. Priorizar físico USB sobre físico Bluetooth. Conservar a fonte selecionada entre fontes de mesma prioridade; na perda/expiração, escolher outra física elegível. Virtual ou origem incerta sem física mostram bateria indisponível, sem inventar percentual. Com vários controles físicos não há seletor novo nesta revisão.
3. USB completo exige 64 bytes; Bluetooth completo exige 78 bytes e CRC-32 de entrada com semente 0xA1. Relatório simples 0x01 de 10 ou 78 bytes conserva navegação e não tem bateria. Níveis reservados acima de 10 não viram 100%.
4. Publicar mudanças de bateria e renovar a leitura a cada segundo de relatórios válidos, mesmo com status igual. Após mais de 5 s sem renovar, descartar a amostra na próxima atualização de status (timer de 1 s). Perda explícita usa evento próprio, sem depender do texto de log. Relatório simples conectado é representado como sem bateria disponível.
5. Atualizar diretamente o texto do painel Controle e energia, sem reconstruir o painel ou alterar o foco. Faixa e painel usam a mesma seleção.
6. Carregando: preenchimento verde e selo lateral compacto com raio, sem cobrir o nível e sem animação contínua. Completa: preenchimento normal cheio, sem raio. Descarregando, baixa e erro conservam seus significados.

## Consequências

Se o HidHide impedir a leitura do físico, a navegação pelo virtual pode continuar funcionando com bateria indisponível. Não modificar o HidHide para contornar isso automaticamente. A estimativa por faixas de 10% é mantida. Origem PnP é classificação operacional, não uma fronteira de segurança contra drivers maliciosos.

O monitor renova `BatteryChanged` também com status igual; consumidores devem usar o horário do relatório, e não tratar falta de mudança percentual como desconexão. Não há feature/output report, escrita HID, hook ou alteração de Steam/DSX. O leitor de diagnóstico é compilado isoladamente, sem recompilar ou instalar a sonda.

## Evidências

31 verificações novas com relógio e relatórios substituídos; CRC fixo conferido independentemente com `zlib.crc32`; bateria completa aprovada em `bin/bateria-dualsense`; renders WPF inspecionados. Diagnóstico real e inventário não encontraram DualSense presente nessa coleta, portanto carga física, reconexão e classificação do hardware do usuário ainda dependem do teste manual. Continuidade, [referência técnica](../referencia/dualsense-hid.md).

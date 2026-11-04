# 0004 — Entrada do controle: DualSense nativo, lido do virtual do DSX, só leitura

- **Status:** Aceita
- **Data:** 2026-10-23

## Contexto

O usuário sempre usa o **DSX em emulação DualSense**, para ter todos os recursos do controle na Steam e nos jogos.
- Nesse modo **não existe controle XInput**, então o shell nunca repassa o botão PS. O PS só abria a Game Bar com emulação Xbox.
- O DSX esconde o DualSense físico com o HidHide, e a Steam enxerga o **virtual** do DSX.

## Decisão

O XGB_P lê o controle **direto pelo HID**:
- **Fonte principal:** o DualSense virtual do DSX, que não fica escondido e entrega PS e microfone no mesmo milissegundo do físico.
- **Fonte alternativa:** o físico, quando liberado no HidHide.
- Sempre **só leitura**: nada de relatórios de saída (LED, vibração, gatilhos).
- O caminho do shell (`GPCMD_NEXUS_*`) fica como **complemento**, para controles XInput.

## Consequências

- Não precisa mexer no HidHide no modo padrão.
- O PS continua sendo da Steam. O gesto que abre o XGB_P precisa ser outro (a definir).
- É preciso lidar com o DSX trocando de modo: o virtual aparece e some, e o leitor varre os dispositivos a cada 2 s.
- Quando físico e virtual forem lidos juntos, os eventos chegam em dobro e precisam ser deduplicados.

## Evidências

[testes-de-controle.md](../guias/testes-de-controle.md), [dualsense-hid.md](../referencia/dualsense-hid.md).

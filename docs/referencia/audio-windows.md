# Referência: áudio do Windows na prévia

## Fontes e implementação

Assinaturas de `IMMDeviceEnumerator`, `IAudioEndpointVolume`, `IAudioSessionManager2`, `IAudioSessionControl2` e `ISimpleAudioVolume` conferidas nos headers locais do SDK **10.0.26100.0** (`mmdeviceapi.h`, `endpointvolume.h`, `audiopolicy.h`, `audioclient.h`). Implementação em `src/lib/audio/CoreAudio.cs`.

Fontes primárias:

- [IAudioEndpointVolume — Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nn-endpointvolume-iaudioendpointvolume): volume/mute de endpoint.
- [SetMasterVolumeLevelScalar — Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nf-endpointvolume-iaudioendpointvolume-setmastervolumelevelscalar): nível normalizado de 0 a 1.
- [IAudioSessionManager2 — Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/audiopolicy/nn-audiopolicy-iaudiosessionmanager2): enumeração/gerenciamento de sessões por dispositivo e COM inicializado na thread.
- [Grouping Parameters — Microsoft](https://learn.microsoft.com/en-us/windows/win32/coreaudio/grouping-parameters): GUID não vazio permite controlar várias sessões juntas; `GUID_NULL` indica ausência de agrupamento. O Sndvol clássico controla uma saída por vez e um GUID não une controles de dispositivos diferentes.
- [Audio Sessions — Microsoft](https://learn.microsoft.com/en-us/windows/win32/coreaudio/audio-sessions): nomes/ícones das sessões de renderização usados pelos controles de volume.
- [Configurações do Windows — Microsoft](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings): `ms-settings:sound`.
- [IPolicyConfig no OpenVR-AdvancedSettings — código do projeto](https://github.com/matzman666/OpenVR-AdvancedSettings/blob/master/src/tabcontrollers/audiomanager/IPolicyConfig.h): referência de ordem da vtable e GUIDs da interface **não documentada**. Nenhuma dependência binária desse projeto foi instalada.

`IPolicyConfig`: IID `F8679F50-850A-41CF-9C72-430F290290C8`, classe `870AF99C-171D-4F9E-AF0D-E63DF40C2BC9`; `SetDefaultEndpoint` após dez métodos específicos da interface. Não é uma API pública garantida para todas as versões do Windows.

## Observado neste PC em 2026-10-23

- Quatro saídas ativas: DualSense virtual, Logitech G560, Razer USB Sound Card e G435.
- Três entradas ativas: microfones G435, Razer e DualSense virtual.
- Saída padrão console/multimídia: **Logitech G560**, 50%, sem mute. Comunicação: DualSense.
- Entrada padrão console/multimídia: **Razer USB Sound Card**, aproximadamente 70,6%, **mutada**. Comunicação: DualSense.
- Doze sessões na coleta inicial, com sons do sistema, Steam, Firefox, Spotify, AMD e Wallpaper Engine em diferentes saídas. A presença de sessão não prova reprodução audível naquele momento.
- Setters de volume/mute e leitura de confirmação funcionaram para os endpoints padrão e sessões enumeradas ao reaplicar valores idênticos.
- `IPolicyConfig` aceitou reaplicar os seis defaults existentes. A leitura posterior preservou os mesmos IDs; não foi feito teste de troca para outro dispositivo.
- Na revisão do mixer integrado, o mesmo Firefox apresentou GUIDs distintos entre G560 e Razer, mas o mesmo caminho de executável. Steam e Radeon também tinham sessões em duas saídas. Por isso, a apresentação compacta usa identidade do programa além do GUID, unificando 12 sessões em 6 linhas. Isso é uma escolha de UI do projeto (ADR 0013), não uma dedução de que todo mixer nativo faça o mesmo agrupamento.
- Nomes e ícones reais foram obtidos de sessões/arquivos locais. `QueryFullProcessImageName` com acesso limitado permitiu identificar Steam e Radeon, sem elevar o processo. Leitura de `Process.HasExited` exigia acesso adicional nessa máquina; ela foi retirada desse caminho.

Arquivos locais de evidência, ignorados pelo Git: `bin/audio-inventory.txt`, `bin/audio-controls-check.txt` e `bin/design-preview/audio-leitura-real.txt`, além dos renders descritos em [som.md](../design/som.md). Esses arquivos são retratos da coleta, não estado permanente da máquina.

### Coleta de continuidade às 19:22

Para documentar a sessão, foi repetida somente a leitura/renderização, sem setters: 4 saídas, 3 entradas e **13 sessões em 6 linhas**. Saída console/multimídia agora Razer em 86% sem mute; entrada Razer em aproximadamente 70,6% sem mute; padrões de comunicação continuam DualSense. Isso difere da preparação inicial acima e não deve ser restaurado automaticamente para os valores antigos. A coleta não atribui a origem de toda mudança externa de padrão.

Os resultados históricos e novos foram preservados em docs/evidencias/2026-10-23-design, com datas/hashes e distinção entre leitura e testes de setters. Nenhum teste de setters foi repetido para produzir a documentação.

## Limites

Leitura de volumes não grava nem captura áudio do microfone. O volume exibido é o nível escalar de endpoint/sessão, não medição de loudness, ganho em dB ou teste auditivo. Fluxos exclusivos, drivers e apps com roteamento próprio podem ter comportamento diferente do mixer compartilhado. A compatibilidade em jogos não foi testada.

Esta implementação usa contratos de API e leitura de estado, sem análise de binários; portanto não há endereço de função ou ID REA associado a estes testes.

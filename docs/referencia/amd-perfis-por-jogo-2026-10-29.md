# AMD: ajustes globais e perfis por jogo

> **Suspenso a pedido do usuário em 29/10, depois desta investigação.** Nada foi implementado. Retomar só com novo pedido; os próximos passos abaixo continuam válidos como ponto de partida.

Investigação de 29/10/2026, **somente leitura**. O usuário perguntou se, com um jogo aberto, o painel AMD pode escolher entre alterar o ajuste global ou o do jogo, como o Adrenalin faz. Nenhum ajuste, perfil ou arquivo da AMD foi alterado. Evidências brutas.

Ambiente: Adrenalin `CNVersion` 26.20.15.07, `amdadlx64.dll` 1.5.0.124, `atiadlxx.dll` 7.26.20.1616. `RadeonSoftware.exe` estava aberto durante as leituras.

## ADLX não tem perfis por jogo (observado)

A `amdadlx64.dll` instalada contém 185 nomes de interface `IADLX*`. Os ajustes 3D (`IADLX3DAntiLag`, `IADLX3DChill`, `IADLX3DBoost`, `IADLX3DImageSharpening`, `IADLX3DEnhancedSync`, `IADLX3DFrameRateTargetControl`, `IADLX3DRadeonSuperResolution`, `IADLX3DAMDFluidMotionFrames`, `IADLX3DFidelityFXSuperResolution`, `IADLX3DFidelityFXFrameGenUpgrade` e os demais) são obtidos pelo `IADLX3DSettingsServices*` por GPU, sem parâmetro de aplicativo. Nenhuma interface de perfil por aplicativo aparece.

`IADLXApplication`/`IADLXApplicationList` existem, mas, pelo SDK público, servem para listar os processos que usam a GPU (`IADLXGPU2::GetApplications`). Isso ajuda a identificar o jogo, mas não altera ajustes.

## Onde os perfis por jogo ficam (observado)

| Arquivo | Conteúdo |
|---|---|
| `%LOCALAPPDATA%\ATI\ACE\APL\User.blb` | Perfis de aplicativo do driver, banco do usuário do ADL. 20.552 bytes, gravado pela última vez em 04/10/2026 17:21:06, SHA-256 `78DFD8E6…`. |
| `C:\Windows\System32\atiapfxx.blb` | Perfis de aplicativo do sistema, que vêm com o driver. |
| `%LOCALAPPDATA%\AMD\CN\gmdb.blb` | Lista de jogos do Adrenalin em JSON: caminho, ícone, tempo de jogo e marcadores como `has_upscaling_profile`/`has_framegen_profile`. Não guarda os valores dos ajustes. |

A `atiadlxx.dll` exporta 47 funções `ADL_/ADL2_ApplicationProfiles_*`, documentadas no SDK público do ADL: busca de perfil por aplicativo, criação, atribuição, remoção, recarga e interceptação.

## Leitura pela API do ADL (observado)

A sonda chamou só `ADL2_Main_Control_Create/Destroy`, `ADL2_ApplicationProfiles_HitListsX2_Get` e `ADL2_ApplicationProfiles_ProfileOfAnApplicationX2_Search`/`_InMemorySearch`.

- **Área:** só `3D_User` devolveu o perfil 3D. `null` devolveu -3, e `''`, `3D`, `D3D`, `Main3D`, `OGL`, `PX` e `OCL` devolveram -1. `PXDynamic` devolveu um registro só para o League (troca de GPU).
- `InMemorySearch` devolveu -1 em todos os casos, e `HitListsX2_Get` devolveu -8 (não suportado) para os bancos do sistema e do usuário.
- **Jogos com perfil 3D:** `GenshinImpact.exe` (40 propriedades), `League of Legends.exe` (40), `KRWebView.exe` do Wuthering Waves (43) e `forzahorizon6.exe` (59). O `User.blb` também cita `steamappid:3513350` e o pacote `Microsoft.ForteBaseGame`, que não foram consultados.
- **Jogos sem perfil 3D:** `FlightSimulator2024.exe`, `Neva.exe` e `notepad.exe` (controle). Eles aparecem no `gmdb.blb`, mas não têm perfil no driver.
- **Formato dos valores:** quase todos são texto UTF-16 do tipo `0x0300::1;;0x1300::0;;`, com tipo `0`. O significado de `0x0300`/`0x1300` não foi decodificado. Chill, Anti-Lag e nitidez só têm a entrada `0x0300`.

### Valores que diferem entre os jogos

| Propriedade | Genshin | League | Wuthering Waves | Forza Horizon 6 | Recurso provável (inferido) |
|---|---|---|---|---|---|
| `Dlg_PFEnable` | 1 | 1 | 0 | 1 | Anti-Lag |
| `TurboSync` | 1 | 1 | 1 | 0 | Enhanced Sync |
| `FsrOverride` | 0 | 1 | 1 | 1 | Upgrade para FSR 4 |
| `TFQ` | 0 | 1 | 1 | 1 | Qualidade da filtragem de textura |
| `Bst_PFEnable` / `Bst_MaxScale` | — | — | 0 / 84 | vazio | Boost e resolução mínima |
| `Chil_Level` | 2 | 2147483650 | 2 | 2 | Chill (nível) |

Iguais nos quatro: `Chil_PFEnable` 0 e `Chil_MinFRate`/`Chil_MaxFRate` 75/140 (Chill), `Ris_PFEnable` 0 e `Ris_SHDegree` 0.5 (nitidez), `MlfiOverride` 0 (geração de quadros por ML), `FsrOtaIndex` 1, `VSyncControl` 1 (espera pela atualização vertical), `ScalingOverride` 0, `ShaderCache` 1, `AntiAlias` 1, `Tessellation` 64. A tabela completa está na saída da sonda.

O perfil do Forza tem 11 propriedades com valor vazio (tamanho 0): `Bst_PFEnable`, `Bst_MaxScale`, `Bst_AdaPFEnable`, `Vsr_PFEnable`, `Dlg_LimitFPS`, `Dlg_Level`, `RTBst_PFState`, `Chil_LdSDThsh`, `CFM_EXE`, `MVPU` e `FfxDllPath`. Pode ser a forma de dizer "usa o global", mas isso não foi confirmado.

**A ligação entre cada nome e o recurso do Adrenalin é inferida** pelo nome e ainda precisa ser conferida na tela de cada jogo no Adrenalin. Não foram encontradas propriedades com nome claro de AFMF ou de limite de FPS (FRTC) nesses perfis.

## Consequências para o painel AMD

- Os ajustes globais do painel ([ADR 0082](../decisoes/0082-painel-amd-ajustes-pelo-adlx.md)) não valem num jogo com perfil próprio. Hoje isso afeta Genshin, League, Wuthering Waves e Forza Horizon 6.
- **Ler** o perfil do jogo aberto é viável pela API documentada do ADL, com `3D_User`.
- **Gravar** também passa por essa API (`Profile_Create`, `ProfileApplicationX2_Assign`), mas não foi testado. Pontos em aberto:
  - se o Adrenalin aberto mostra a mudança ou a sobrescreve;
  - se o jogo aplica na hora ou só na próxima abertura;
  - se os marcadores do `gmdb.blb` precisam acompanhar;
  - como representar "usar o global" para cada item;
  - criar perfil para jogo sem perfil (MSFS, Neva) é mais invasivo do que editar um que já existe.

## Próximos passos propostos

1. **Conferência pelo usuário (sem escrita):** abrir o Genshin no Adrenalin e comparar com a tabela. O esperado, pela inferência, é Anti-Lag ligado, Enhanced Sync ligado, upgrade FSR 4 desligado, Chill e nitidez desligados, e filtragem de textura diferente do League.
2. **Um teste de escrita, só com autorização:** mudar uma propriedade num perfil que já existe, com backup do `User.blb`, e conferir o Adrenalin e o jogo; depois reverter.
3. **Interface:** com jogo aberto, escolha "Global | nome do jogo" no painel AMD, mostrando por item se vale o global ou o do jogo. Mesmo sem escrita, a leitura já permite avisar que o jogo tem perfil próprio e que o ajuste global não vale ali.

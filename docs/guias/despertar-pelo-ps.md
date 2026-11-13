# Despertar pelo PS — diagnóstico e reversão

Configurações → Controle → **Acordar pelo PS** lê a política Bluetooth, a presença atual de instâncias DualSense/Edge e as permissões de despertar. Uma ausência atual não significa que o controle nunca conectou. Erros parciais são apresentados como leitura incompleta.

## Scripts do usuário

Os scripts estão na cópia dos fontes; não acompanham o ZIP portátil. O aplicativo não altera a política. O diagnóstico é somente leitura:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verificar-despertar-ps.ps1
```

Para examinar a ativação, em PowerShell como administrador:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\ativar-despertar-ps.ps1 -WhatIf
```

Somente se quiser aplicar, execute o mesmo script sem `-WhatIf`. Ele grava a política Bluetooth de 24 bytes e arma apenas instâncias HID Bluetooth Sony DualSense/Edge; USB e outros fabricantes não são alvos. Se a tabela mudar, o script pede reinício antes de continuar. O backup anterior à primeira mudança fica em `%LOCALAPPDATA%\XGB_P\wake-policy\bootps-backup-v1.json`; a reexecução não o substitui.

Para examinar a reversão:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\ativar-despertar-ps.ps1 -Desfazer -WhatIf
```

Execute sem `-WhatIf` para restaurar somente o conjunto do backup, na mesma máquina e usuário. Mantenha os controles originais presentes; se uma instância faltar, o backup é conservado para repetir. Sem backup ativo, o script não inventa o estado anterior. `politica-despertar.ps1` é uma ação separada: exige `-Permitidos` explícito e desarma os demais dispositivos. Examine o arquivo e seu `-WhatIf` antes de escolher alvos; nenhuma lista particular de teclado ou controle vem como padrão.

## Registros de retomada

Cada ciclo tem `CycleId` e um JSON em `%LOCALAPPDATA%\XGB_P\wake-trace`. O registro preserva horário de suspensão/retomada, evento `Power-Troubleshooter/1` por XML, `RecordId`, tentativas de leitura e PS observado. A interface mostra o ID curto; o JSON e o log têm o completo.

`Unknown` com PS observado é correlação. **PhysicalCauseConfirmed=false**: nenhum programa suspenso pode decidir qual sinal físico acordou o PC. Compare ciclos PS, teclado/botão e Unknown em testes reais. Os arquivos podem conter identificadores do equipamento e XML do Windows; não os publique sem revisão.

As suítes isoladas não mudam registro, WMI, permissões, tarefas ou energia do PC. Não há som distribuído nem rotina para disputar continuamente com outros softwares as permissões de despertar.

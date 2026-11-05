# XGB_P · instalação e uso

Barra de jogo para o modo Xbox do Windows 11, com Steam Big Picture e DualSense via DSX. Aplicativo portátil: não tem instalador, serviço nem atualização automática.

## Requisitos

- Windows 11 x64 com o modo Xbox. O app também abre no desktop.
- Steam, com o Big Picture como app inicial do modo Xbox.
- DSX com o DualSense, no perfil que você já usa.
- [HidHide](https://github.com/nefarius/HidHide) com o `XGB_P.exe` liberado (abaixo).
- Opcionais: AMD Software: Adrenalin Edition, para os painéis Gravação e AMD; HWiNFO (memória compartilhada ligada) e RTSS, para o painel Desempenho. Sem eles, os painéis mostram o recurso como indisponível.

## Instalar

1. Baixe o `XGB_P-<versão>-win-x64.zip` e o `SHA256SUMS.txt` da mesma [release](https://github.com/Sitr3n01/XGB_P/releases).
2. Confira o hash e compare com o `SHA256SUMS.txt`:

   ```powershell
   Get-FileHash .\XGB_P-2.1.0-win-x64.zip -Algorithm SHA256
   ```

3. Extraia numa pasta fixa, por exemplo `%LOCALAPPDATA%\Programs\XGB_P`. Não deixe o executável em Downloads: o caminho é usado pelo HidHide e pela inicialização com o Windows.
4. O executável não tem assinatura Authenticode. Na primeira abertura, o SmartScreen pode pedir **Mais informações → Executar assim mesmo**.

### Liberar o controle no HidHide

O DSX esconde o DualSense físico dos outros apps. O XGB_P lê o controle físico, então o executável precisa estar na lista de apps permitidos. **Quem executa é você**, num PowerShell **como administrador**, trocando o caminho pelo da sua pasta:

```powershell
& 'C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe' --app-reg "$env:LOCALAPPDATA\Programs\XGB_P\XGB_P.exe"
```

Conferir a lista:

```powershell
& 'C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe' --app-list
```

Trocar o arquivo no mesmo caminho mantém a liberação. Para remover, use `--app-unreg` com o mesmo caminho.

## Abrir, usar e encerrar

Abra o `XGB_P.exe` pelo Explorer. O app começa na bandeja, com uma instância por sessão.

- **No modo Xbox:** PS abre e fecha a barra. O ícone da bandeja também.
- **No desktop:** o PS continua com a Steam. A barra abre pela bandeja.
- **Navegação:** direcional ou analógico esquerdo navegam, analógico direito rola, ✕ confirma, círculo volta, L1/R1 trocam de painel, L2/R2 paginam.
- **Encerrar:** **Encerrar XGB_P** na bandeja ou nas Configurações. Jogos pausados pelo XGB_P são retomados ao encerrar; depois de uma queda, reabra o app para retomá-los.

**Administrador.** Steam e DSX costumam rodar elevados, e o Windows não deixa um app comum trazer o foco de volta sobre eles. Ligue **Sempre executar como administrador** em Configurações se a barra abrir sem foco; a elevação sempre passa pelo UAC.

**Barra de tarefas no modo Xbox.** Algumas barras de tarefas (a do Start11, por exemplo) somem no modo Xbox, mas continuam reservando o espaço delas na tela. Com **Ocultar a barra de tarefas** ligado (Configurações → MODO XBOX, ligado por padrão), o XGB_P liga a ocultação automática da barra de tarefas do Windows ao entrar no modo Xbox e devolve ao sair. Se você já usava a ocultação automática, ela fica como está.

**Som do app ao abrir a barra.** Com **Abaixar o som do app ao abrir** ligado (padrão), o jogo ou app em foco toca na metade do volume enquanto a barra está aberta e volta ao fechar. Uma mudança feita no mixer com a barra aberta vale como escolha sua.

**Baterias.** A faixa de status mostra a bateria do DualSense (o físico, não o virtual do DSX) e a de fones Bluetooth que informam o nível ao Windows, só enquanto estão conectados. Sobre as telas do Big Picture, que já mostram a hora, a barra esconde a dela e leva rede e baterias para o lado do título do painel.

**Win+G** abre a Game Bar original do Windows. Ele só abre o XGB_P com uma ponte local que não é distribuída.

**Abra pelo Explorer, por um atalho ou pela inicialização do Windows,** e não a partir de um app empacotado (como apps de assistentes de IA). Esses apps virtualizam o `%LOCALAPPDATA%` dos processos que iniciam: o XGB_P aberto por eles grava numa cópia privada dos dados e é encerrado quando o app é atualizado.

## Energia

**Suspender**, **Reiniciar** e **Desligar** fazem pedidos reais ao Windows, depois de uma segunda confirmação com o foco em **Cancelar**. Salve seu trabalho antes.

## Dados e inicialização

| Local | Conteúdo |
|---|---|
| `%LOCALAPPDATA%\XGB_P\settings` | Preferências e, só quando há o que restaurar, `barra-tarefas-modo-xbox.txt` e `volume-reduzido.json` |
| `%LOCALAPPDATA%\XGB_P\games` | Catálogo, histórico e diário de pausas |
| `%LOCALAPPDATA%\XGB_P\history` | Histórico de ações (7 dias, até 300) |
| `%LOCALAPPDATA%\XGB_P\sons` | Sons da interface, opcionais (abaixo) |
| `logs\xgb.log`, ao lado do executável (em `%LOCALAPPDATA%\XGB_P\logs` se o app estiver em Program Files) | Log de uso, com rotação |

Trocar o executável não apaga esses dados. **Iniciar com o Windows**, em Configurações, cria a entrada `XGB_P` em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, que respeita o Gerenciador de Tarefas. Desligar a opção remove a entrada.

**Sons da interface.** Nenhum som é distribuído. Para ter sons, coloque arquivos WAV seus em `%LOCALAPPDATA%\XGB_P\sons`, com estes nomes: `deck_ui_side_menu_fly_in.wav` (abrir), `deck_ui_side_menu_fly_out.wav` (fechar), `deck_ui_navigation.wav` (mover), `deck_ui_default_activation.wav` (selecionar), `deck_ui_hide_modal.wav` (voltar), `deck_ui_show_modal.wav` (lista) e `deck_ui_launch_game.wav` (iniciar jogo). Sem eles, a barra fica em silêncio.

## Atualizar e voltar de versão

1. Encerre o XGB_P pela bandeja.
2. Guarde o `XGB_P.exe` atual e a pasta `logs`, se quiser.
3. Substitua o `XGB_P.exe` pelo da nova versão, no mesmo caminho. A liberação no HidHide e a inicialização continuam valendo.

Para voltar, repita com o executável anterior. Os dados em `%LOCALAPPDATA%\XGB_P` são compartilhados entre as versões.

## Desinstalar

1. Desligue **Iniciar com o Windows** nas Configurações e encerre o app fora do modo Xbox, para ele devolver a barra de tarefas e o volume dos apps. Se a barra de tarefas ficar oculta, desligue a ocultação em Configurações → Personalização → Barra de tarefas.
2. Remova o executável do HidHide com `--app-unreg`.
3. Apague a pasta do app e, se quiser, `%LOCALAPPDATA%\XGB_P`.

## Problemas

Ao relatar um problema, inclua as linhas relevantes do `logs\xgb.log` sem caminhos pessoais nem títulos de janela, e diga a versão do Windows, do DSX e se o modo Xbox estava ligado: [abrir um relato](https://github.com/Sitr3n01/XGB_P/issues/new/choose).

// Sons da interface no estilo do Xbox: abrir e fechar a barra, mover o foco, selecionar, voltar,
// mostrar o X de fechar e iniciar um jogo. Arquivos WAV so locais (%LOCALAPPDATA%\XGB_P\sons), fornecidos
// pelo usuario: nenhum som e distribuido nem entra no repositorio. Sem os arquivos, a barra segue muda.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using XGBP.Audio;


namespace XGBP.Design
{
    sealed partial class Preview
    {
        static readonly string SoundFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XGB_P", "sons");
        // Nomes do pacote do AudioLoader (eventos do Steam Deck com os sons do Xbox One/Series).
        static readonly Dictionary<string, string> SoundFiles = new Dictionary<string, string> {
            { "abrir", "deck_ui_side_menu_fly_in.wav" }, { "fechar", "deck_ui_side_menu_fly_out.wav" },
            { "mover", "deck_ui_navigation.wav" }, { "selecionar", "deck_ui_default_activation.wav" },
            { "voltar", "deck_ui_hide_modal.wav" },
            { "lista", "deck_ui_show_modal.wav" }, { "jogo", "deck_ui_launch_game.wav" } };
        // Volume dos sons (0 a 1) e liga/desliga persistidos nas preferencias locais.
        double soundVolume = .7;
        UiSounds sounds;
        bool soundsOn = true, soundSync;
        Button soundSetting;
        Slider soundVolumeSlider;
        TextBlock soundVolumeText;
        // Substituiveis: a verificacao registra os sons em vez de tocar.
        Action<string> soundPlay;
        Action soundStopAll;
        int soundActionDepth;
        string soundActionCue;
        readonly List<string> soundLog = new List<string>();

        void StartSounds()
        {
            StartSoundPreferences();
            if (verify) { soundPlay = delegate(string cue) { soundLog.Add(cue); }; soundStopAll = delegate { soundLog.Add("parar-todos"); }; return; }
            LoadSounds();
            soundPlay = delegate(string cue) { if (sounds != null) sounds.Play(cue); };
            soundStopAll = delegate { if (sounds != null) sounds.StopAll(); };
        }
        void LoadSounds()
        {
            if (sounds != null) { sounds.Dispose(); sounds = null; }
            try { sounds = new UiSounds(SoundFolder, SoundFiles, soundVolume); WriteLog("sons: " + (sounds.Count == 0 ? "arquivos ausentes em " + SoundFolder : sounds.Describe())); }
            catch (Exception ex) { WriteLog("sons indisponiveis: " + ex.Message); }
        }
        // Aparelho de audio trocado ou removido: os canais sao reabertos na proxima abertura da barra.
        void CheckSounds() { if (sounds != null && sounds.Broken) { WriteLog("sons: canal falhou; reabrindo"); LoadSounds(); } }
        void Sound(string cue)
        {
            if (!soundsOn || disposed) return;
            try { soundPlay(cue); } catch (Exception ex) { WriteLog("sons: " + cue + " falhou | " + ex.Message); }
        }
        // O callback escolhe o resultado antes de tocar. Sem selecionar seguido de outro efeito.
        // Pedidos fora de uma acao (reconstrucoes e backends) nao produzem som.
        void ActionSound(string cue) { if (soundActionDepth > 0) soundActionCue = cue; }
        void InvokeSoundAction(Button button, Action action)
        {
            if (!button.IsEnabled || motionClosing || systemPowerBusy) return;
            bool outer = soundActionDepth == 0;
            string tag = button.Tag as string ?? "";
            if (outer) soundActionCue = SoundlessMedia(button) ? "" : tag.EndsWith("-cancel", StringComparison.Ordinal) ? "voltar" : "selecionar";
            soundActionDepth++;
            bool completed = false;
            try { action(); completed = true; }
            finally
            {
                soundActionDepth--;
                if (outer)
                {
                    string cue = soundActionCue; soundActionCue = null;
                    if (completed && !string.IsNullOrEmpty(cue)) Sound(cue);
                }
            }
        }
        void StopAllSounds() { try { if (soundStopAll != null) soundStopAll(); } catch (Exception) { } }
        // A secao de midia (player da Home e do Som) fica sem som de interface: nada toca ao agir num controle dela
        // nem ao levar o foco para dentro dela. Sair dela para outra secao toca normalmente.
        bool SoundlessMedia(Control c) { return MediaControl(c); }
        void DisposeSounds() { FlushSoundPreferences(); if (sounds != null) { sounds.Dispose(); sounds = null; } }

        // Liga/desliga e volume sao salvos no perfil local; Reduzir movimento continua so na sessao.
        void AddSoundSetting()
        {
            soundSetting = ToggleSetting(body, "Sons da interface", soundsOn, "ui-sounds", delegate { soundsOn = !soundsOn; if (!soundsOn) StopAllSounds(); UpdateSoundSetting(); QueueSoundPreferences(true); });
            var block = new StackPanel { Margin = new System.Windows.Thickness(0, 2, 0, 8) }; body.Children.Add(block);
            var label = new DockPanel { Margin = new System.Windows.Thickness(0, 0, 0, 3) }; block.Children.Add(label);
            soundVolumeText = Text("", 14, "Muted"); soundVolumeText.MinWidth = 44; soundVolumeText.TextAlignment = System.Windows.TextAlignment.Right;
            DockPanel.SetDock(soundVolumeText, Dock.Right); label.Children.Add(soundVolumeText);
            var name = Text("Volume dos sons", 15, "TextPrimary"); name.TextWrapping = System.Windows.TextWrapping.NoWrap; label.Children.Add(name);
            soundVolumeSlider = new Slider { Minimum = 0, Maximum = 100, TickFrequency = 1, IsSnapToTickEnabled = true, Height = 34,
                Style = (System.Windows.Style)window.FindResource("VolumeSlider"), Tag = "ui-sounds-volume", Value = Math.Round(soundVolume * 100) };
            System.Windows.Automation.AutomationProperties.SetName(soundVolumeSlider, "Volume dos sons da interface");
            soundVolumeSlider.ToolTip = "Volume dos sons da interface · esquerda/direita";
            soundVolumeSlider.GotKeyboardFocus += delegate { block.BringIntoView(); };
            soundVolumeSlider.ValueChanged += delegate { if (!soundSync) SetSoundVolume(soundVolumeSlider.Value / 100); };
            block.Children.Add(soundVolumeSlider); actions.Add(soundVolumeSlider);
            UpdateSoundSetting();
        }
        // O buffer de cada som e refeito na hora; o som de navegacao do proprio ajuste ja sai no volume novo.
        void SetSoundVolume(double volume)
        {
            soundVolume = Math.Max(0, Math.Min(1, volume));
            if (sounds != null) sounds.SetVolume(soundVolume);
            UpdateSoundSetting();
            QueueSoundPreferences(false);
        }
        void UpdateSoundSetting()
        {
            if (soundSetting == null) return;
            bool files = verify || sounds != null && sounds.Count > 0;
            UpdateToggleSetting(soundSetting, "Sons da interface", soundsOn, soundsOn && !files ? "Sem os arquivos" : null);
            soundSetting.ToolTip = files ? "Sons ao navegar, menos no player de mídia. Volume e liga/desliga salvos neste PC." : "Coloque os arquivos WAV dos sons em " + SoundFolder + ".";
            if (soundVolumeSlider == null) return;
            // Sem sons (desligados ou sem arquivos), o volume fica inativo e a navegacao o pula, como os controles sem dispositivo.
            bool active = soundsOn && files;
            if (!active && soundVolumeSlider.IsKeyboardFocused) soundSetting.Focus();
            soundVolumeSlider.IsEnabled = active; soundVolumeSlider.Opacity = active ? 1 : .4;
            soundSync = true;
            try { soundVolumeSlider.Value = Math.Round(soundVolume * 100); } finally { soundSync = false; }
            soundVolumeText.Text = Math.Round(soundVolume * 100) + "%";
        }

        static void SoundAssert(bool condition, string message) { if (!condition) throw new Exception("Sons: " + message); }
        string SoundsSince(int from) { return string.Join(",", soundLog.GetRange(from, soundLog.Count - from).ToArray()); }
#if XGB_VERIFY
        // Cada gesto toca o seu som, uma vez; reconstrucoes e foco programatico ficam mudos. Nada e tocado de verdade.
        void VerifySounds()
        {
            var launch = gameLaunch; var send = mediaSend;
            try
            {
                gameLaunch = delegate { };
                returnToHome = false; Hide(); SetPanel(0); soundLog.Clear();
                Show(); SoundAssert(SoundsSince(0) == "abrir", "abrir a barra");
                int at = soundLog.Count; Show(); SoundAssert(SoundsSince(at) == "", "barra ja aberta nao repete");
                FocusHome("steam-home"); at = soundLog.Count; Navigate("BAIXO");
                SoundAssert(SoundsSince(at) == "mover", "mover o foco");
                at = soundLog.Count; FocusHome("steam-home"); RenderGames(true); RebuildOpenApps();
                SoundAssert(SoundsSince(at) == "", "foco programatico e reconstrucao sem som");
                VerifyRailNavigationSounds();
                SetPanel(6); window.UpdateLayout(); audioBindings[0].Slider.Focus(); at = soundLog.Count; Navigate("DIREITA");
                SoundAssert(SoundsSince(at) == "mover", "ajustar um controle deslizante");
                audioBindings[0].Slider.Value = 100; at = soundLog.Count; Navigate("DIREITA");
                SoundAssert(SoundsSince(at) == "", "controle no limite fica mudo");
                audioPending.Clear();
                at = soundLog.Count; ChangePanel(1); SoundAssert(SoundsSince(at) == "", "trocar de painel fica mudo");
                SetPanel(0); FocusHome("utility-performance"); at = soundLog.Count; Activate();
                SoundAssert(panel == 3 && SoundsSince(at) == "mover", "clique no acesso rapido com navegacao curta");
                at = soundLog.Count; Back(); SoundAssert(panel == 0 && SoundsSince(at) == "voltar", "voltar");
                at = soundLog.Count; Back(); SoundAssert(SoundsSince(at) == "voltar", "voltar ao trilho");
                at = soundLog.Count; Back(); SoundAssert(!window.IsVisible && SoundsSince(at) == "fechar", "fechar pelo trilho toca so fechar");
                Show(); openAppsRow[0].Focus(); BeginHold("pad"); at = soundLog.Count; HoldElapsed();
                SoundAssert(SoundsSince(at) == "lista", "X de fechar app"); ReleaseHold(); AppCloseBack();
                gamesView = ExampleGames(); gamesView.Activities.Clear(); gamesView.Current = null; RenderGames(true);
                at = soundLog.Count; FocusHome("recent-fixture:one"); Activate();
                SoundAssert(SoundsSince(at) == "jogo" && !window.IsVisible, "iniciar jogo emite somente jogo");
                launchingGame = null; Show();
                SetPanel(5); window.UpdateLayout(); FocusHome("ui-sounds"); at = soundLog.Count; Activate();
                SoundAssert(!soundsOn && ActionLabel("ui-sounds") == "Sons da interface · Desativado" && SoundsSince(at) == "parar-todos", "desligar interrompe os sons sem clique adicional");
                at = soundLog.Count; Navigate("BAIXO"); Navigate("CIMA"); Dismiss(); Show();
                SoundAssert(SoundsSince(at) == "", "sons desligados ficam mudos");
                FocusHome("ui-sounds"); at = soundLog.Count; Activate(); SoundAssert(soundsOn && ActionLabel("ui-sounds") == "Sons da interface · Ativado" && SoundsSince(at) == "selecionar", "religar emite exatamente uma selecao");
                // Volume dos sons: o slider abaixo do liga/desliga ajusta na hora e soa no volume novo; fica inativo com os sons desligados.
                window.UpdateLayout(); FocusHome("ui-sounds-volume"); at = soundLog.Count; Navigate("ESQUERDA");
                SoundAssert(Keyboard.FocusedElement == soundVolumeSlider && Math.Abs(soundVolume - .65) < .001 && soundVolumeText.Text == "65%" && SoundsSince(at) == "mover", "baixar o volume dos sons");
                window.UpdateLayout(); SaveRender("configuracoes-sons");
                FocusHome("ui-sounds"); Navigate("BAIXO"); SoundAssert(Keyboard.FocusedElement == soundVolumeSlider, "volume logo abaixo de Sons da interface");
                FocusHome("ui-sounds"); Activate(); SoundAssert(!soundVolumeSlider.IsEnabled, "volume inativo com os sons desligados");
                Navigate("BAIXO"); SoundAssert(Keyboard.FocusedElement != soundVolumeSlider, "navegacao pula o volume inativo");
                FocusHome("ui-sounds"); Activate(); SoundAssert(soundVolumeSlider.IsEnabled && soundVolumeSlider.Value == 65, "religar mantem o volume escolhido");
                // Secao de midia sem som de interface: entrar no player, selecionar, andar na linha e ajustar ficam mudos; sair dele toca.
                mediaSend = delegate { }; mediaSessions = ExampleMedia(); SetPanel(0); window.UpdateLayout();
                gameRecentRows[2][0].Focus(); at = soundLog.Count; Navigate("BAIXO");
                SoundAssert(MediaControl(Keyboard.FocusedElement as Control) && SoundsSince(at) == "", "entrar no player fica mudo");
                mediaPlay.Focus(); at = soundLog.Count; Activate(); Navigate("DIREITA");
                SoundAssert(Keyboard.FocusedElement == mediaNext && SoundsSince(at) == "", "selecionar e andar no player ficam mudos");
                mediaVolume.Focus(); double before = mediaVolume.Value; at = soundLog.Count; Navigate("DIREITA");
                SoundAssert(mediaVolume.Value > before && SoundsSince(at) == "", "volume do player fica mudo");
                mediaPlay.Focus(); at = soundLog.Count; Navigate("CIMA");
                SoundAssert(!MediaControl(Keyboard.FocusedElement as Control) && SoundsSince(at) == "mover", "sair do player para os recentes toca");
                SetPanel(6); window.UpdateLayout(); mediaPlay.Focus(); at = soundLog.Count; Activate(); Navigate("DIREITA");
                SoundAssert(SoundsSince(at) == "", "player no Som tambem fica mudo");
                audioPending.Clear();
                VerifySoundRoutes();
                VerifySoundPreferences();
                File.WriteAllText(Path.Combine(Output, "sons-verificacao.txt"),
                    "OK: um efeito por acao, 11 botoes de painel/subpagina com apenas mover ao clicar; navegacao no trilho toca mover mesmo ao trocar painel, foco programatico e L1-R1 mudos; listas de audio/energia, " +
                    "menus e confirmacoes de rede, cancelar pelo botao e Esc, erros sem efeito pendente; abrir/fechar, foco programatico e reconstrucoes mudos; jogo sem selecao previa; " +
                    "desligar interrompe todos, religar confirma exatamente uma vez; " +
                    "volume dos sons pelo slider (som no volume novo, inativo e pulado com os sons desligados, mantido ao religar); secao de midia muda na Home e no Som " +
                    "(entrar, selecionar, andar e volume do player), sair dela toca. Sons e comandos de midia registrados, nada tocado.\r\n", Encoding.UTF8);
            }
            finally
            {
                gameLaunch = launch; mediaSend = send; launchingGame = null; soundsOn = true; soundVolume = .7; mediaSessions = ExampleMedia(); audioPending.Clear();
                gamesView = ExampleGames(); returnToHome = false; SetPanel(0); soundLog.Clear();
            }
        }
#endif
#if XGB_VERIFY
        // Ciclos completos: a Biblioteca preserva o painel, os demais destinos o trocam.
        void VerifyRailNavigationSounds()
        {
            int[] order = { 0, 1, 6, AmdPanel, 3, AmdTuningPanel, 5, 4 };
            foreach (string direction in new[] { "BAIXO", "CIMA" })
            {
                returnToHome = false; SetPanel(0); window.UpdateLayout(); rail[0].Focus();
                int position = 0;
                for (int step = 0; step < order.Length * 2; step++)
                {
                    int previousPanel = panel, from = soundLog.Count;
                    position = (position + (direction == "BAIXO" ? 1 : order.Length - 1)) % order.Length;
                    int target = order[position];
                    Navigate(direction);
                    SoundAssert(Keyboard.FocusedElement == rail[target] && panel == (target == 1 ? previousPanel : target) &&
                        SoundsSince(from) == "mover", "trilho " + direction + " passo " + step + " toca uma vez, destino " + target);
                }
            }
            foreach (int target in order)
            {
                int from = soundLog.Count; rail[target].Focus();
                SoundAssert(SoundsSince(from) == "", "foco programatico no trilho continua mudo: " + target);
            }
            rail[0].Focus(); int at = soundLog.Count; Navigate("ESQUERDA");
            SoundAssert(SoundsSince(at) == "", "gesto sem movimento no trilho fica mudo");
            foreach (int direction in new[] { 1, -1 })
                for (int step = 0; step < order.Length; step++)
                {
                    at = soundLog.Count; ChangePanel(direction);
                    SoundAssert(SoundsSince(at) == "", "L1-R1 continua mudo: " + direction + " passo " + step);
                }
            SetPanel(StoresPanel); rail[0].Focus(); at = soundLog.Count; Navigate("BAIXO");
            SoundAssert(panel == StoresPanel && Keyboard.FocusedElement == rail[1] && SoundsSince(at) == "mover", "Lojas para Biblioteca toca sem trocar painel");
            at = soundLog.Count; Navigate("CIMA");
            SoundAssert(panel == StoresPanel && Keyboard.FocusedElement == rail[0] && SoundsSince(at) == "mover", "Biblioteca para Inicio sob Lojas toca uma vez");
        }
#endif
#if XGB_VERIFY
        // Caminhos reais da UI, comandos de sistema substituidos pela fixture de verificacao.
        void VerifySoundRoutes()
        {
            // Os 11 botoes pedidos: mesmo efeito curto por clique, sem selecionar/abrir/transicao adicionais.
            foreach (int index in new[] { 0, 6, 3, 5, 4 })
            {
                returnToHome = false; SetPanel(0); window.UpdateLayout(); int from = soundLog.Count;
                rail[index].RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
                SoundAssert(panel == index && SoundsSince(from) == "mover", "clique no painel do trilho " + index);
            }
            string[] utilityTags = { "utility-stores", "utility-audio", "utility-recording", "utility-performance", "utility-amd", "utility-power" };
            int[] utilityPanels = { StoresPanel, 6, AmdPanel, 3, AmdTuningPanel, 4 };
            for (int i = 0; i < utilityTags.Length; i++)
            {
                returnToHome = false; SetPanel(0); window.UpdateLayout(); FocusHome(utilityTags[i]); int from = soundLog.Count;
                Activate(); SoundAssert(panel == utilityPanels[i] && SoundsSince(from) == "mover", "clique no acesso rapido " + utilityTags[i]);
            }
            returnToHome = false; SetPanel(0); window.UpdateLayout();
            FocusHome("utility-stores"); int at = soundLog.Count; Activate();
            SoundAssert(panel == StoresPanel && SoundsSince(at) == "mover", "Lojas: somente navegacao curta ao clicar");
            at = soundLog.Count; Back(); SoundAssert(SoundsSince(at) == "voltar" && panel == 0, "voltar de Lojas");
            at = soundLog.Count; rail[6].RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
            SoundAssert(panel == 6 && SoundsSince(at) == "mover", "clique no trilho usa navegacao curta");
            FocusHome("output"); at = soundLog.Count; Activate();
            SoundAssert(audioExpanded == 0 && SoundsSince(at) == "lista", "abrir lista de saida sem selecionar");
            at = soundLog.Count; Activate(); SoundAssert(audioExpanded < 0 && SoundsSince(at) == "voltar", "recolher lista pelo seletor");
            Activate(); at = soundLog.Count; Back(); SoundAssert(SoundsSince(at) == "voltar", "Esc recolhe lista com mesmo som");
            SetPanel(3); window.UpdateLayout(); FocusHome("power-mode"); at = soundLog.Count; Activate();
            SoundAssert(perfExpanded && SoundsSince(at) == "lista", "abrir modos de energia");
            at = soundLog.Count; Activate(); SoundAssert(!perfExpanded && SoundsSince(at) == "voltar", "recolher modos de energia");
            ExampleConnections(); SetPanel(5); window.UpdateLayout(); FocusHome("page-wifi"); at = soundLog.Count; Activate();
            SoundAssert(settingsPage == "wifi" && SoundsSince(at) == "mover", "Wi-Fi com navegacao curta ao clicar");
            FocusHome("net-Casa"); at = soundLog.Count; Activate(); SoundAssert(SoundsSince(at) == "lista", "menu de rede");
            FocusHome("net-forget"); at = soundLog.Count; Activate(); SoundAssert(connectionConfirm == "forget" && SoundsSince(at) == "lista", "confirmacao de esquecer rede");
            at = soundLog.Count; Activate(); SoundAssert(connectionConfirm == null && SoundsSince(at) == "voltar", "cancelar pelo botao usa voltar");
            Activate(); at = soundLog.Count; Back(); SoundAssert(connectionConfirm == null && SoundsSince(at) == "voltar", "cancelar pelo Esc usa voltar");
            Back(); Back(); FocusHome("page-bluetooth"); at = soundLog.Count; Activate();
            SoundAssert(settingsPage == "bluetooth" && SoundsSince(at) == "mover", "Bluetooth com navegacao curta ao clicar");
            SoundAssert(!SoundFiles.ContainsValue("deck_ui_tab_transition_01.wav"), "efeito de transicao removido do mapa ativo");
            at = soundLog.Count; RebuildSettings(null); SetPanel(0); RenderGames(true);
            SoundAssert(SoundsSince(at) == "", "reconstrucoes continuam mudas");
            var button = new Button(); at = soundLog.Count;
            try { InvokeSoundAction(button, delegate { ActionSound("lista"); throw new InvalidOperationException("teste"); }); }
            catch (InvalidOperationException) { }
            SoundAssert(SoundsSince(at) == "" && soundActionDepth == 0 && soundActionCue == null, "falha nao toca nem deixa efeito pendente");
            InvokeSoundAction(button, delegate { }); SoundAssert(SoundsSince(at) == "selecionar", "acao seguinte a falha emite uma unica selecao");
            gamesView = ExampleGames(); RenderGames(true);
            FocusHome("recent-fixture:one"); gameLaunch = delegate { SoundAssert(SoundsSince(at) == "", "nenhuma selecao antes de despachar o jogo"); };
            gamesView.Activities.Clear(); gamesView.Current = null; RenderGames(true); FocusHome("recent-fixture:one"); at = soundLog.Count; Activate();
            SoundAssert(SoundsSince(at) == "jogo", "despacho de jogo sem ataque da selecao");
            launchingGame = null; Show();
        }
#endif
#if XGB_VERIFY
        // Diagnostico real: carrega os arquivos locais e toca cada um com volume zero (sem som audivel) ate o fim.
        public void VerifySoundsLive()
        {
            var lines = new List<string>();
            using (var silent = new UiSounds(SoundFolder, SoundFiles, 0))
            {
                lines.Add("pasta: " + (Directory.Exists(SoundFolder) ? "presente" : "ausente") + " | carregados: " + silent.Count + " de " + SoundFiles.Count);
                lines.Add(silent.Describe());
                SoundAssert(silent.Errors.Count == 0, "arquivo de som invalido: " + string.Join(" | ", silent.Errors.ToArray()));
                // Volume refeito sem tocar: cheio tem amplitude, zero fica em silencio absoluto.
                if (silent.Has("mover"))
                {
                    silent.SetVolume(1); int loud = silent.Peak("mover"); silent.SetVolume(.5); int half = silent.Peak("mover"); silent.SetVolume(0);
                    SoundAssert(loud > 1000 && Math.Abs(half - loud / 2.0) <= 1 && silent.Peak("mover") == 0 && silent.Volume == 0, "volume aplicado nas amostras");
                    lines.Add("volume: pico " + loud + " a 100%, " + half + " a 50%, 0 a 0%");
                }
                foreach (var cue in SoundFiles.Keys)
                {
                    if (!silent.Has(cue)) continue;
                    silent.Play(cue); var start = DateTime.UtcNow;
                    while (!silent.Done(cue) && DateTime.UtcNow - start < TimeSpan.FromSeconds(3)) Thread.Sleep(10);
                    SoundAssert(silent.Done(cue) && !silent.Broken, "o canal de " + cue + " nao terminou de tocar");
                    lines.Add(cue + ": tocado em silencio ate o fim");
                }
                if (silent.Has("selecionar")) SoundAssert(silent.Duration("selecionar") <= .160001, "selecao limitada a 160 ms");
                int pairs = 0;
                foreach (var first in SoundFiles.Keys) foreach (var next in SoundFiles.Keys)
                {
                    if (!silent.Has(first) || !silent.Has(next)) continue;
                    silent.Play(first); silent.Play(next);
                    SoundAssert(silent.PlayingCount <= 1 && (first == next || silent.Done(first)), "sobreposicao entre " + first + " e " + next);
                    foreach (var other in SoundFiles.Keys) if (other != next) SoundAssert(silent.Done(other), "efeito anterior ainda ativo: " + other);
                    pairs++;
                }
                silent.StopAll(); SoundAssert(silent.PlayingCount == 0, "parar todos deixa nenhum canal ativo");
                lines.Add("Exclusividade: " + pairs + " pares, no maximo um efeito ativo, anteriores encerrados; parar todos confirmado.");
            }
            lines.Add("Volume zero: nada audivel. Arquivos so locais, fora do repositorio.");
            File.WriteAllLines(Path.Combine(Output, "sons-reais.txt"), lines, Encoding.UTF8);
            WriteLog("sons: diagnostico real concluido em silencio");
        }
#endif
    }
}

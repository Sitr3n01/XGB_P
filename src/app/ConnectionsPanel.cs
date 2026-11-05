// Configuracoes: secao Conexoes (Wi-Fi e Bluetooth) e as paginas de redes e dispositivos conhecidos.
// Leitura em worker so com Configuracoes aberto; ligar/desligar, conectar, esquecer e remover so por escolha do usuario.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using XGBP.Network;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        readonly DispatcherTimer connectionsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        string settingsPage = "main", connectionMenu, connectionConfirm, radioPending, connectionsSignature;
        RadioInfo wifiRadio, btRadio;
        WifiState wifiState;
        List<BluetoothThing> btDevices;
        bool connectionsBusy, connectionsScan;
        DateTime connectionsNextRead = DateTime.MinValue, devicesNextRead = DateTime.MinValue;
        // Backends substituiveis: a verificacao nunca liga/desliga radios nem mexe em redes e dispositivos reais.
        Func<string, RadioInfo> radioRead = Radios.Read;
        Action<string, bool> radioSet = Radios.Set;
        Func<bool, WifiState> wifiRead = Wifi.Read;
        Action<string> wifiConnect = Wifi.Connect, wifiForget = Wifi.Forget;
        Action wifiDisconnect = Wifi.Disconnect;
        Func<List<BluetoothThing>> btRead = Bluetooth.Read;
        Action<BluetoothThing, bool> btAudio = Bluetooth.Audio;
        Action<BluetoothThing> btUnpair = Bluetooth.Unpair;

        void StartConnections()
        {
#if XGB_VERIFY
            if (verify) { ExampleConnections(); ExampleSettingsExtras(); NoConnectionCommands(); return; }
#endif
            connectionsTimer.Tick += delegate { ConnectionsTick(); }; connectionsTimer.Start();
        }
        void NoConnectionCommands()
        {
            radioSet = delegate { }; wifiConnect = wifiForget = delegate { }; wifiDisconnect = delegate { };
            btAudio = delegate { }; btUnpair = delegate { }; nightSet = delegate { };
        }
#if XGB_VERIFY
        void ExampleConnections()
        {
            wifiRadio = new RadioInfo { Present = true, On = true, Name = "Wi-Fi" };
            btRadio = new RadioInfo { Present = true, On = true, Name = "Bluetooth" };
            wifiState = new WifiState { Adapter = true, AdapterName = "Adaptador de teste", Networks = new List<WifiNetwork> {
                new WifiNetwork { Profile = "Casa", Connected = true, InRange = true, Signal = 90, Secured = true },
                new WifiNetwork { Profile = "Trabalho", InRange = true, Signal = 60, Secured = true },
                new WifiNetwork { Profile = "Café", InRange = true, Signal = 40, Secured = false },
                new WifiNetwork { Profile = "Empresa", InRange = true, Signal = 20, GroupPolicy = true, Secured = true },
                new WifiNetwork { Profile = "Antiga", Secured = true } } };
            btDevices = new List<BluetoothThing> {
                new BluetoothThing { Name = "DualSense Wireless Controller", Kind = "Controle", Controller = true, Connected = true, CanUnpair = true, Key = "pad", Ids = { "id-pad" } },
                new BluetoothThing { Name = "Fone de teste", Kind = "Áudio", AudioControl = true, CanUnpair = true, Key = "fone", Ids = { "id-fone" } },
                new BluetoothThing { Name = "Celular de teste", Kind = "Celular", CanUnpair = true, Key = "celular", Ids = { "id-cel-1", "id-cel-2" } } };
        }
#endif

        // Leitura com Configuracoes aberto: radios, redes e Luz noturna a cada 3 s, dispositivos a cada 6 s, varredura so ao entrar em Wi-Fi.
        void ConnectionsTick()
        {
            if (verify || disposed || connectionsBusy || panel != 5 || !window.IsVisible || DateTime.UtcNow < connectionsNextRead) return;
            connectionsBusy = true; connectionsNextRead = DateTime.UtcNow.AddSeconds(3);
            bool scan = connectionsScan; connectionsScan = false;
            bool devices = settingsPage != "wifi" && DateTime.UtcNow >= devicesNextRead;
            if (devices) devicesNextRead = DateTime.UtcNow.AddSeconds(6);
            var readRadio = radioRead; var readWifi = wifiRead; var readBt = btRead; var readNight = nightRead;
            var readStartup = startupRead;
            Task.Factory.StartNew(delegate {
                RadioInfo w = null, b = null; WifiState networks = null; List<BluetoothThing> things = null; string error = null;
                bool? night = null; bool nightOk = false;
                XGBP.Desktop.StartupState startup = null; string startupError = null;
                try { w = readRadio("wifi"); b = readRadio("bluetooth"); } catch (Exception ex) { error = "rádios: " + ex.Message; }
                try { networks = readWifi(scan); } catch (Exception ex) { error = "Wi-Fi: " + ex.Message; }
                if (devices) try { things = readBt(); } catch (Exception ex) { error = "Bluetooth: " + ex.Message; }
                try { night = readNight(); nightOk = true; } catch (Exception ex) { error = "Luz noturna: " + ex.Message; }
                if (readStartup != null) try { startup = readStartup(); } catch (Exception ex) { startupError = ex.Message; }
                Dispatch(delegate {
                    connectionsBusy = false;
                    if (w != null) wifiRadio = w; if (b != null) btRadio = b; if (networks != null) UpdateWifi(networks); if (things != null) btDevices = things;
                    if (nightOk && !nightPending) { nightLight = night; nightKnown = true; }
                    if (startup != null) startupState = startup;
                    startupReadError = startupError;
                    if (error != null) WriteLog("conexoes: leitura parcial | " + error);
                    RenderSettings();
                });
            });
        }
        string SettingsSignature()
        {
            var text = new System.Text.StringBuilder(settingsPage + "|" + connectionMenu + "|" + connectionConfirm + "|" + radioPending + "|" + nightKnown + nightLight + nightPending);
            text.Append("|" + startupState.Registered + startupState.Owned + startupState.Enabled + startupReadError + launchPreferences.AlwaysAdmin);
            text.Append("|i" + buttonIcons + xboxPadPresent);
            text.Append("|h" + (settingsPage == "history" ? historyVersion + "/" + historyProblemsOnly : UnseenProblems() + "/" + actionHistory.Count));
            foreach (var r in new[] { wifiRadio, btRadio }) text.Append("|" + (r == null ? "?" : r.Describe()));
            if (wifiState != null) { text.Append("|" + wifiState.Adapter + wifiState.Note); foreach (var n in wifiState.Networks) text.Append("|" + n.Profile + n.Connected + n.InRange + n.Bars); }
            if (btDevices != null) foreach (var d in btDevices) text.Append("|" + d.Key + d.Name + d.Connected + d.AudioControl);
            return text.ToString();
        }
        // So as redes salvas em alcance aparecem; as demais ficam no Wi-Fi do Windows. Com o radio desligado, nenhuma.
        List<WifiNetwork> WifiVisible()
        {
            if (wifiState == null || (wifiRadio != null && !wifiRadio.On)) return new List<WifiNetwork>();
            return wifiState.Networks.FindAll(n => n.InRange || n.Connected);
        }
        // Rede que saiu do alcance some da lista: fecha as acoes e a confirmacao abertas nela.
        void UpdateWifi(WifiState state)
        {
            wifiState = state;
            if (connectionMenu != null && connectionMenu.StartsWith("net-", StringComparison.Ordinal) && !WifiVisible().Exists(n => "net-" + n.Profile == connectionMenu))
            { connectionMenu = null; connectionConfirm = null; }
        }
        void RenderSettings()
        {
            if (panel != 5 || SettingsSignature() == connectionsSignature) return;
            // Uma leitura nova nao reconstroi o painel no meio do arraste do volume dos sons.
            if (soundVolumeSlider != null && soundVolumeSlider.IsMouseCaptureWithin) return;
            var focused = Keyboard.FocusedElement as Control;
            RebuildSettings(focused != null && actions.Contains(focused) ? focused.Tag as string : null);
        }

        // Pagina principal: Conexoes primeiro, depois o conteudo de sempre das Configuracoes.
        void BuildSettings()
        {
            connectionsSignature = SettingsSignature();
            if (settingsPage == "wifi") { BuildWifiPage(); return; }
            if (settingsPage == "bluetooth") { BuildBluetoothPage(); return; }
            if (settingsPage == "history") { BuildHistoryPage(); return; }
            ((TextBlock)window.FindName("PanelTitle")).Text = "Configurações";
            Caption("CONEXÕES");
            RadioRow("wifi", "Wi-Fi", wifiRadio);
            int inRange = WifiVisible().Count;
            string networks = wifiState == null ? "Lendo…" : !wifiState.Adapter ? "Sem adaptador" : wifiState.Note != null ? "Alcance indisponível" :
                inRange > 0 ? inRange + " em alcance" : "Nenhuma em alcance";
            ConnectionButton(body, "Redes Wi-Fi", networks + "  ›", "page-wifi", delegate { OpenSettingsPage("wifi"); });
            RadioRow("bluetooth", "Bluetooth", btRadio);
            int connected = btDevices == null ? 0 : btDevices.FindAll(d => d.Connected).Count;
            string devices = btDevices == null ? "Lendo…" : btDevices.Count + (btDevices.Count == 1 ? " conhecido" : " conhecidos") + (connected > 0 ? " · " + connected + " conectado" + (connected == 1 ? "" : "s") : "");
            ConnectionButton(body, "Dispositivos Bluetooth", devices + "  ›", "page-bluetooth", delegate { OpenSettingsPage("bluetooth"); });
            Caption("TELA");
            NightLightRow();
            Caption("CONTROLE");
            var dsx = Action("DSX · ajustes do controle", OpenDsx); dsx.Tag = "dsx"; dsx.ToolTip = "Abre o DSX para ajustar o DualSense";
            AddButtonIconsSetting();
            AddKeyboardSettings();
            Caption("SISTEMA E STEAM");
            Action("Configurações do Windows", OpenWindowsSettings).Tag = "windows-settings";
            Action("Configurações da Steam", OpenSteamSettings).Tag = "steam-settings";
            AddHistorySetting();
            Caption("MODO XBOX");
            AddExitGamingMode();
            AddTaskbarSetting();
            Caption("ABERTURA");
            body.Children.Add(Text(GuideName + " ou Win + G", 27, "AccentText"));
            body.Children.Add(Text("No modo Xbox, " + GuideName + " ou Win+G. No desktop, somente Win+G com a opção abaixo ligada.", 15, "Muted"));
            AddLaunchSettings();
            AddAppVolumeSetting();
            AddSoundSetting();
            AddMotionSetting();
            Caption("SOBRE");
            body.Children.Add(Text("XGB_P · V.2.1.0", 19, "Muted"));
            Action("Encerrar XGB_P", delegate { app.Shutdown(); });
        }
        void RadioRow(string kind, string label, RadioInfo info)
        {
            string detail = info == null ? "Lendo…" : radioPending == kind ? "Aplicando…" : !info.Present ? "Indisponível" : info.Disabled ? "Desativado no sistema" : info.On ? "Ligado" : "Desligado";
            var button = ToggleSetting(body, label, info == null || !info.Present ? (bool?)null : info.On, "radio-" + kind, delegate { ToggleRadio(kind); },
                info == null || radioPending == kind || !info.Present || info.Disabled ? detail : null);
            button.IsEnabled = info != null && info.Present && !info.Disabled;
            if (connectionMenu != "radio-" + kind) return;
            var box = MenuBox();
            var question = Text(RadioRisk(kind), 15, "TextPrimary"); question.Margin = new Thickness(2, 0, 0, 6); box.Children.Add(question);
            ConnectionButton(box, "Desligar " + label, "", "radio-confirm", delegate { ApplyRadio(kind, false); });
            ConnectionButton(box, "Cancelar", "", "radio-cancel", delegate { connectionMenu = null; RebuildSettings("radio-" + kind); });
        }
        // Desligar com risco pede confirmacao: o DualSense por Bluetooth sai, ou a internet pelo Wi-Fi cai.
        string RadioRisk(string kind)
        {
            if (kind == "bluetooth")
            {
                bool pad = btDevices != null && btDevices.Exists(d => d.Controller && d.Connected);
                foreach (var device in batteries.Keys) pad |= device.EndsWith("-BT", StringComparison.Ordinal);
                return pad ? "O DualSense conectado por Bluetooth vai desconectar. Para ligar de novo, use o teclado, o mouse ou o controle pelo cabo." : null;
            }
            return networkState != null && networkState.Kind == "wifi" ? "A internet está no Wi-Fi e a conexão vai cair." : null;
        }
        void ToggleRadio(string kind)
        {
            var info = kind == "wifi" ? wifiRadio : btRadio;
            if (info == null || !info.Present || info.Disabled || radioPending != null) return;
            if (info.On && RadioRisk(kind) != null) { connectionMenu = "radio-" + kind; connectionConfirm = null; RebuildSettings("radio-cancel"); ActionSound("lista"); return; }
            ApplyRadio(kind, !info.On);
        }
        void ApplyRadio(string kind, bool on)
        {
            string label = kind == "wifi" ? "Wi-Fi" : "Bluetooth";
            radioPending = kind; connectionMenu = null;
            var notice = BeginFeedback((on ? "Ligando o " : "Desligando o ") + label + "…"); RebuildSettings("radio-" + kind);
            WriteLog("conexoes: radio " + kind + " -> " + (on ? "ligado" : "desligado"));
            var set = radioSet;
            ConnectionWork(delegate { set(kind, on); }, delegate(Exception error) {
                radioPending = null;
                if (error != null) { FinishFeedback(notice, error.Message, FeedbackKind.Error); WriteLog("conexoes: radio falhou | " + error.Message); }
                else
                {
                    var info = kind == "wifi" ? wifiRadio : btRadio; if (info != null) info.On = on;
                    FinishFeedback(notice, label + (on ? " ligado." : " desligado."), FeedbackKind.Success);
                }
                connectionsNextRead = DateTime.MinValue; devicesNextRead = DateTime.MinValue; RebuildSettings("radio-" + kind); ConnectionsTick();
            });
        }

        void OpenSettingsPage(string page)
        {
            settingsPage = page; connectionMenu = null; connectionConfirm = null;
            if (page == "wifi") connectionsScan = true; else devicesNextRead = DateTime.MinValue;
            connectionsNextRead = DateTime.MinValue;
            RebuildSettings(null, 1); ConnectionsTick();
            ActionSound("mover"); // Clique na subpagina: navegacao curta, sem efeito de abertura.
        }
        void BuildWifiPage()
        {
            ((TextBlock)window.FindName("PanelTitle")).Text = "Wi-Fi";
            var state = wifiState; var visible = WifiVisible();
            Caption("REDES SALVAS EM ALCANCE" + (state != null && state.Adapter ? " · " + visible.Count : ""));
            if (state == null) ConnectionNote("Lendo redes…");
            else if (!state.Adapter) ConnectionNote("Nenhum adaptador Wi-Fi neste PC.");
            else if (wifiRadio != null && !wifiRadio.On) ConnectionNote("Wi-Fi desligado. Ligue em Configurações para conectar.");
            else
            {
                if (state.Note != null) ConnectionNote(state.Note);
                else if (visible.Count == 0) ConnectionNote(state.Networks.Count == 0 ? "Nenhuma rede salva neste PC." : "Nenhuma rede salva em alcance agora.");
                foreach (var entry in visible)
                {
                    var n = entry; string tag = "net-" + n.Profile;
                    var row = ConnectionButton(body, n.Profile, n.Connected ? "Conectada" : "Sinal " + n.Bars + "/4", tag, delegate { ToggleMenu(tag); });
                    if (n.Connected) ((TextBlock)((Grid)row.Content).Children[1]).Foreground = Brush("AccentText");
                    if (connectionMenu == tag) NetworkMenu(n);
                }
            }
            ConnectionButton(body, "Abrir Wi-Fi no Windows", "↗", "wifi-windows", delegate { OpenWindowsPage("ms-settings:network-wifi", "Wi-Fi"); });
        }
        void NetworkMenu(WifiNetwork n)
        {
            var box = MenuBox();
            if (connectionConfirm == "forget")
            {
                ConnectionQuestion(box, "Esquecer " + n.Profile + "? A senha salva neste PC será apagada.");
                ConnectionButton(box, "Esquecer rede", "", "net-forget-confirm", delegate { WifiAction("forget", n); });
                ConnectionButton(box, "Cancelar", "", "net-cancel", delegate { connectionConfirm = null; RebuildSettings("net-forget"); });
                return;
            }
            if (n.Connected) ConnectionButton(box, "Desconectar", "", "net-disconnect", delegate { WifiAction("disconnect", n); });
            else ConnectionButton(box, "Conectar", "", "net-connect", delegate { WifiAction("connect", n); });
            if (!n.GroupPolicy) ConnectionButton(box, "Esquecer rede", "", "net-forget", delegate { connectionConfirm = "forget"; RebuildSettings("net-cancel"); ActionSound("lista"); });
            else ConnectionQuestion(box, "Rede definida pela política do sistema; não pode ser esquecida aqui.");
        }
        void WifiAction(string verb, WifiNetwork n)
        {
            connectionMenu = null; connectionConfirm = null; string tag = "net-" + n.Profile;
            var notice = BeginFeedback(verb == "connect" ? "Conectando a " + n.Profile + "…" : verb == "disconnect" ? "Desconectando de " + n.Profile + "…" : "Esquecendo " + n.Profile + "…");
            RebuildSettings(tag); WriteLog("conexoes: wifi " + verb + " | " + n.Profile);
            var connect = wifiConnect; var disconnect = wifiDisconnect; var forget = wifiForget;
            ConnectionWork(delegate {
                if (verb == "connect") connect(n.Profile); else if (verb == "disconnect") disconnect(); else forget(n.Profile);
            }, delegate(Exception error) {
                if (error != null) { FinishFeedback(notice, error.Message, FeedbackKind.Error); WriteLog("conexoes: wifi falhou | " + error.Message); }
                else
                {
                    FinishFeedback(notice, verb == "connect" ? "Conexão com " + n.Profile + " solicitada." : verb == "disconnect" ? "Wi-Fi desconectado de " + n.Profile + "." : n.Profile + " esquecida.", FeedbackKind.Success);
                    if (verb == "forget" && wifiState != null) wifiState.Networks.Remove(n);
                    if (verb == "disconnect") n.Connected = false;
                }
                connectionsNextRead = DateTime.MinValue; RebuildSettings(tag); ConnectionsTick();
            });
        }

        void BuildBluetoothPage()
        {
            ((TextBlock)window.FindName("PanelTitle")).Text = "Bluetooth";
            Caption("DISPOSITIVOS CONHECIDOS" + (btDevices != null ? " · " + btDevices.Count : ""));
            bool on = btRadio == null || btRadio.On;
            if (btRadio != null && !btRadio.Present) ConnectionNote("Nenhum adaptador Bluetooth neste PC.");
            else if (!on) ConnectionNote("Bluetooth desligado. Ligue em Configurações para conectar.");
            if (btDevices == null) ConnectionNote("Lendo dispositivos…");
            else if (btDevices.Count == 0) ConnectionNote("Nenhum dispositivo pareado.");
            else foreach (var entry in btDevices)
            {
                var d = entry; string tag = "bt-" + d.Key;
                var row = ConnectionButton(body, d.Name, BluetoothDetail(d), tag, delegate { ToggleMenu(tag); });
                if (d.Connected) ((TextBlock)((Grid)row.Content).Children[1]).Foreground = Brush("AccentText");
                if (connectionMenu == tag) DeviceMenu(d, on);
            }
            ConnectionButton(body, "Abrir Bluetooth no Windows", "↗", "bt-windows", delegate { OpenWindowsPage("ms-settings:bluetooth", "Bluetooth"); });
        }
        void DeviceMenu(BluetoothThing d, bool radioOn)
        {
            var box = MenuBox();
            if (connectionConfirm == "remove")
            {
                ConnectionQuestion(box, "Remover " + d.Name + "? Será preciso parear de novo para usar.");
                ConnectionButton(box, "Remover dispositivo", "", "bt-remove-confirm", delegate { DeviceAction("remove", d); });
                ConnectionButton(box, "Cancelar", "", "bt-cancel", delegate { connectionConfirm = null; RebuildSettings("bt-remove"); });
                return;
            }
            if (d.AudioControl && radioOn)
                ConnectionButton(box, d.Connected ? "Desconectar" : "Conectar", "", d.Connected ? "bt-disconnect" : "bt-connect", delegate { DeviceAction(d.Connected ? "disconnect" : "connect", d); });
            // O DualSense e o controle da barra (ADR 0004): sem remover por aqui.
            if (d.Controller) ConnectionQuestion(box, "Controle usado pela barra. Para removê-lo, use o Bluetooth do Windows.");
            else if (d.CanUnpair) ConnectionButton(box, "Remover dispositivo", "", "bt-remove", delegate { connectionConfirm = "remove"; RebuildSettings("bt-cancel"); ActionSound("lista"); });
            if (!d.AudioControl && !d.Controller) ConnectionQuestion(box, d.Connected ? "Conectado. A conexão deste tipo de dispositivo é gerenciada por ele." : "Ligue o dispositivo para ele se conectar.");
        }
        void DeviceAction(string verb, BluetoothThing d)
        {
            connectionMenu = null; connectionConfirm = null; string tag = "bt-" + d.Key;
            var notice = BeginFeedback(verb == "connect" ? "Conectando " + d.Name + "…" : verb == "disconnect" ? "Desconectando " + d.Name + "…" : "Removendo " + d.Name + "…");
            RebuildSettings(tag); WriteLog("conexoes: bluetooth " + verb + " | " + d.Name);
            var audio = btAudio; var unpair = btUnpair;
            ConnectionWork(delegate { if (verb == "remove") unpair(d); else audio(d, verb == "connect"); }, delegate(Exception error) {
                if (error != null) { FinishFeedback(notice, error.Message, FeedbackKind.Error); WriteLog("conexoes: bluetooth falhou | " + error.Message); }
                else
                {
                    FinishFeedback(notice, verb == "connect" ? "Conexão com " + d.Name + " solicitada." : verb == "disconnect" ? d.Name + " desconectado." : d.Name + " removido.", FeedbackKind.Success);
                    if (verb == "remove" && btDevices != null) btDevices.Remove(d);
                }
                devicesNextRead = DateTime.MinValue; connectionsNextRead = DateTime.MinValue; RebuildSettings(tag); ConnectionsTick();
            });
        }

        void ToggleMenu(string tag)
        {
            bool open = connectionMenu != tag;
            connectionMenu = open ? tag : null; connectionConfirm = null;
            RebuildSettings(tag);
            // Ao abrir, o foco entra na primeira acao da linha; sem acoes, fica na propria linha.
            if (open) { int at = actions.FindIndex(c => (string)c.Tag == tag); if (at >= 0 && at + 1 < actions.Count && IsMenuTag((string)actions[at + 1].Tag)) actions[at + 1].Focus(); }
            ActionSound(open ? "lista" : "voltar");
        }
        static bool IsMenuTag(string tag)
        {
            foreach (var t in new[] { "net-connect", "net-disconnect", "net-forget", "net-forget-confirm", "net-cancel", "bt-connect", "bt-disconnect", "bt-remove", "bt-remove-confirm", "bt-cancel", "radio-confirm", "radio-cancel" })
                if (tag == t) return true;
            return false;
        }
        // Esc/circulo: fecha a confirmacao, depois as acoes da linha, depois volta das paginas para Configuracoes.
        bool ConnectionsBack()
        {
            if (panel != 5) return false;
            if (connectionConfirm != null)
            {
                string back = connectionConfirm == "forget" ? "net-forget" : connectionConfirm == "remove" ? "bt-remove" : "history-clear";
                connectionConfirm = null; RebuildSettings(back); return true;
            }
            if (connectionMenu != null) { string tag = connectionMenu; connectionMenu = null; RebuildSettings(tag); return true; }
            if (settingsPage != "main") { string entry = "page-" + settingsPage; settingsPage = "main"; RebuildSettings(entry, -1); return true; }
            return false;
        }
        void OpenWindowsPage(string uri, string label)
        {
            try { settingsLaunch(uri); WriteLog("conexoes: " + label + " do Windows | " + uri); Hide(); }
            catch (Exception ex) { Feedback("Não foi possível abrir " + label + " do Windows.", FeedbackKind.Error); WriteLog("conexoes: falha ao abrir " + uri + " | " + ex.Message); }
        }

        // Reconstroi so o conteudo do painel, preservando rolagem e foco; nas paginas, anima como a troca de painel.
        void RebuildSettings(string focusTag, int direction = 0)
        {
            if (panel != 5) return;
            double height = direction != 0 ? ReleaseSidebarHeight() : 0;
            var scroll = (ScrollViewer)window.FindName("BodyScroll"); double offset = direction != 0 ? 0 : scroll.VerticalOffset;
            CancelElementMotion(body); body.Children.Clear(); actions.Clear(); flyout = null; flyoutSelector = null; panelFill = null;
            // Trocar de pagina limpa o retorno da acao anterior, como a troca de painel.
            if (direction != 0) ClearFeedback(true);
            BuildSettings();
            window.UpdateLayout(); scroll.ScrollToVerticalOffset(offset);
            var target = focusTag == null ? null : actions.Find(c => (string)c.Tag == focusTag && c.IsEnabled);
            if (target == null && direction != 0) target = actions.Find(c => c.IsEnabled);
            if (target == null && focusTag != null) target = actions.Find(c => ((string)c.Tag ?? "").StartsWith(settingsPage == "wifi" ? "net-" : settingsPage == "bluetooth" ? "bt-" : "radio-", StringComparison.Ordinal) && c.IsEnabled)
                ?? actions.Find(c => c.IsEnabled);
            if (target != null) target.Focus();
            if (direction != 0) { MotionHeight(height); MotionReveal(MotionPart("PanelContent"), MotionNormal, direction * 24, 0); }
        }
        void ConnectionWork(Action work, Action<Exception> done)
        {
            if (verify) { Exception x = null; try { work(); } catch (Exception ex) { x = ex; } done(x); return; }
            Task.Factory.StartNew(delegate {
                Exception x = null; try { work(); } catch (Exception ex) { x = ex; }
                Dispatch(delegate { done(x); });
            });
        }
        StackPanel MenuBox() { var box = new StackPanel { Margin = new Thickness(16, 0, 0, 6) }; body.Children.Add(box); return box; }
        void ConnectionNote(string text) { var t = Text(text, 14, "Muted"); t.Margin = new Thickness(2, 0, 0, 8); body.Children.Add(t); }
        void ConnectionQuestion(Panel box, string text) { var t = Text(text, 15, "TextPrimary"); t.Margin = new Thickness(2, 2, 0, 6); box.Children.Add(t); }
        // Linha de duas colunas no padrao dos botoes compactos: nome a esquerda, estado a direita.
        Button ConnectionButton(Panel parent, string name, string detail, string tag, Action callback, Button existing = null)
        {
            var button = existing ?? new Button();
            button.Tag = tag; button.Style = (Style)window.FindResource("Action"); button.FontSize = 16;
            button.Padding = new Thickness(10, 7, 10, 7); button.Margin = new Thickness(0, 0, 0, 5);
            button.MinHeight = 40; button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            var content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition()); content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = Text(name, 16, "TextPrimary"); left.TextWrapping = TextWrapping.NoWrap; left.TextTrimming = TextTrimming.CharacterEllipsis;
            var right = Text(detail, 14, "Muted"); right.TextWrapping = TextWrapping.NoWrap; right.Margin = new Thickness(12, 0, 0, 0); right.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(right, 1); content.Children.Add(left); content.Children.Add(right);
            button.Content = content;
            string label = detail.Length > 0 ? name + " · " + detail.Replace("  ›", "").Replace("↗", "abre no Windows") : name;
            button.ToolTip = label; System.Windows.Automation.AutomationProperties.SetName(button, label);
            WireMotion(button);
            button.Click += delegate { InvokeSoundAction(button, callback); };
            button.GotKeyboardFocus += delegate { button.BringIntoView(); };
            parent.Children.Add(button); actions.Add(button);
            return button;
        }
    }
}

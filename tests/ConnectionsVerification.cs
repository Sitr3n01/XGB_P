// Verificacao das Conexoes com substitutos: nenhum radio, rede ou dispositivo real e alterado.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;
using XGBP.Desktop;
using XGBP.Display;
using XGBP.Network;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        static void ConnectionAssert(bool condition, string message) { if (!condition) throw new Exception("Conexoes: " + message); }
        string FocusedTag() { var c = Keyboard.FocusedElement as Control; return c == null ? null : c.Tag as string; }
        string FeedbackText() { return ((TextBlock)window.FindName("Feedback")).Text; }
        string ActionLabel(string tag) { return System.Windows.Automation.AutomationProperties.GetName(actions.Find(c => (string)c.Tag == tag)); }
        // Redes listadas na pagina Wi-Fi, na ordem da tela (sem as acoes abertas abaixo de uma rede).
        string NetworkRows()
        {
            var rows = new List<string>();
            foreach (var c in actions) { string tag = c.Tag as string ?? ""; if (tag.StartsWith("net-", StringComparison.Ordinal) && !IsMenuTag(tag)) rows.Add(tag.Substring(4)); }
            return string.Join("|", rows);
        }
        bool BodyHas(string text)
        {
            foreach (var child in body.Children) { var t = child as TextBlock; if (t != null && t.Text == text) return true; }
            return false;
        }

        void VerifyConnections()
        {
            var calls = new List<string>();
            var set = radioSet; var connect = wifiConnect; var forget = wifiForget; var disconnect = wifiDisconnect; var audio = btAudio; var unpair = btUnpair;
            var launch = settingsLaunch; var network = networkState; string opened = null;
            try
            {
                ExampleConnections(); batteries.Clear();
                radioSet = delegate(string kind, bool on) { calls.Add("radio:" + kind + ":" + on); };
                wifiConnect = delegate(string p) { calls.Add("conectar:" + p); }; wifiForget = delegate(string p) { calls.Add("esquecer:" + p); };
                wifiDisconnect = delegate { calls.Add("desconectar"); };
                btAudio = delegate(BluetoothThing d, bool on) { calls.Add("audio:" + d.Key + ":" + on); }; btUnpair = delegate(BluetoothThing d) { calls.Add("remover:" + d.Key); };
                settingsLaunch = delegate(string uri) { opened = uri; };
                networkState = new NetworkState { Kind = "ethernet", Internet = true };
                SetPanel(5); Show(); window.UpdateLayout();
                // Pagina principal: Conexoes no topo e o conteudo de antes preservado.
                string[] expected = { "radio-wifi", "page-wifi", "radio-bluetooth", "page-bluetooth", "night-light", "dsx", "button-icons",
                    "keyboard-auto", "keyboard-position", "keyboard-sounds", "windows-settings", "steam-settings", "page-history" };
                for (int i = 0; i < expected.Length; i++) ConnectionAssert((string)actions[i].Tag == expected[i], "ordem da pagina principal: " + actions[i].Tag);
                ConnectionAssert(motionSetting != null && actions.Contains(motionSetting) && actions.Exists(c => c is ContentControl && ((ContentControl)c).Content as string == "Encerrar XGB_P"), "conteudo das Configuracoes");
                ConnectionAssert(ActionLabel("page-wifi") == "Redes Wi-Fi · 4 em alcance", "resumo conta so as redes em alcance: " + ActionLabel("page-wifi"));
                SaveRender("configuracoes-conexoes");
                FocusFirstAction(); ConnectionAssert(FocusedTag() == "radio-wifi", "foco inicial na chave do Wi-Fi");
                // Desligar sem risco e imediato; com internet pelo Wi-Fi, pede confirmacao que comeca em cancelar.
                Activate(); ConnectionAssert(calls.Count == 1 && calls[0] == "radio:wifi:False" && !wifiRadio.On && FeedbackText() == "Wi-Fi desligado.", "desligar Wi-Fi sem risco");
                Activate(); ConnectionAssert(calls[1] == "radio:wifi:True" && wifiRadio.On, "ligar Wi-Fi");
                networkState = new NetworkState { Kind = "wifi", Internet = true, Bars = 4 };
                actions.Find(c => (string)c.Tag == "radio-wifi").Focus(); Activate();
                ConnectionAssert(calls.Count == 2 && connectionMenu == "radio-wifi" && FocusedTag() == "radio-cancel", "confirmar Wi-Fi com internet pelo Wi-Fi");
                Back(); ConnectionAssert(connectionMenu == null && FocusedTag() == "radio-wifi" && calls.Count == 2, "Esc cancela o desligar");
                Activate(); Navigate("CIMA"); ConnectionAssert(FocusedTag() == "radio-confirm", "navegar ate confirmar");
                Activate(); ConnectionAssert(calls[2] == "radio:wifi:False" && connectionMenu == null, "confirmar desligar Wi-Fi");
                wifiRadio.On = true; networkState = new NetworkState { Kind = "ethernet", Internet = true }; RebuildSettings("radio-bluetooth");
                // Bluetooth com o DualSense conectado (pela leitura HID ou pela lista) pede confirmacao.
                batteries["DualSense#0-BT"] = new XGBP.Input.Battery { Raw = 0x05, Percent = 55 };
                Activate(); ConnectionAssert(connectionMenu == "radio-bluetooth" && FocusedTag() == "radio-cancel" && calls.Count == 3, "confirmar Bluetooth com DualSense");
                window.UpdateLayout(); SaveRender("configuracoes-bluetooth-confirmar");
                FocusHome("radio-confirm"); Activate(); ConnectionAssert(calls[3] == "radio:bluetooth:False" && !btRadio.On, "desligar Bluetooth confirmado");
                batteries.Clear(); btDevices[0].Connected = false; RebuildSettings("radio-bluetooth");
                Activate(); ConnectionAssert(calls[4] == "radio:bluetooth:True" && btRadio.On, "ligar Bluetooth sem confirmacao");
                btDevices[0].Connected = true; RebuildSettings("radio-bluetooth"); Activate();
                ConnectionAssert(connectionMenu == "radio-bluetooth", "DualSense pela lista tambem pede confirmacao"); Back();
                // Radio ausente ou desativado: linha desabilitada; erro do Windows informa e nao muda o estado.
                btRadio = new RadioInfo(); RebuildSettings(null); ConnectionAssert(!actions.Find(c => (string)c.Tag == "radio-bluetooth").IsEnabled, "radio ausente desabilitado");
                btRadio = new RadioInfo { Present = true, On = true }; radioSet = delegate { throw new InvalidOperationException("O Windows recusou desligar o Bluetooth."); };
                btDevices[0].Connected = false; RebuildSettings("radio-bluetooth"); Activate();
                ConnectionAssert(btRadio.On && FeedbackText() == "O Windows recusou desligar o Bluetooth." && radioPending == null, "erro do radio");
                radioSet = delegate(string kind, bool on) { calls.Add("radio:" + kind + ":" + on); };

                // Pagina Wi-Fi: so as salvas em alcance, conectada primeiro; conectar, desconectar, esquecer com confirmacao e politica sem esquecer.
                FocusHome("page-wifi"); Activate();
                ConnectionAssert(settingsPage == "wifi" && ((TextBlock)window.FindName("PanelTitle")).Text == "Wi-Fi" && FocusedTag() == "net-Casa", "abrir redes Wi-Fi");
                ConnectionAssert(NetworkRows() == "Casa|Trabalho|Café|Empresa" && BodyHas("REDES SALVAS EM ALCANCE · 4"), "rede fora de alcance oculta: " + NetworkRows());
                window.UpdateLayout(); SaveRender("configuracoes-wifi");
                Activate(); ConnectionAssert(connectionMenu == "net-Casa" && FocusedTag() == "net-disconnect", "acoes da rede conectada");
                Activate(); ConnectionAssert(calls[calls.Count - 1] == "desconectar" && FeedbackText() == "Wi-Fi desconectado de Casa." && connectionMenu == null && FocusedTag() == "net-Casa", "desconectar");
                FocusHome("net-Trabalho"); Activate(); ConnectionAssert(FocusedTag() == "net-connect", "acoes da rede em alcance");
                Activate(); ConnectionAssert(calls[calls.Count - 1] == "conectar:Trabalho" && FeedbackText() == "Conexão com Trabalho solicitada.", "conectar pelo perfil");
                FocusHome("net-Café"); Activate(); ConnectionAssert(FocusedTag() == "net-connect" && actions.Exists(c => (string)c.Tag == "net-forget"), "acoes da rede aberta em alcance");
                FocusHome("net-forget"); Activate(); ConnectionAssert(connectionConfirm == "forget" && FocusedTag() == "net-cancel", "esquecer pede confirmacao em cancelar");
                window.UpdateLayout(); SaveRender("configuracoes-wifi-esquecer");
                Back(); ConnectionAssert(connectionConfirm == null && connectionMenu == "net-Café" && FocusedTag() == "net-forget", "Esc volta da confirmacao");
                Activate(); FocusHome("net-forget-confirm"); Activate();
                ConnectionAssert(calls[calls.Count - 1] == "esquecer:Café" && !wifiState.Networks.Exists(n => n.Profile == "Café") && FeedbackText() == "Café esquecida.", "esquecer rede");
                FocusHome("net-Empresa"); Activate(); ConnectionAssert(!actions.Exists(c => (string)c.Tag == "net-forget"), "politica do sistema nao esquece");
                Back(); ConnectionAssert(connectionMenu == null && FocusedTag() == "net-Empresa", "Esc fecha as acoes");
                wifiConnect = delegate { throw new InvalidOperationException("O Windows negou conectar a Trabalho."); };
                FocusHome("net-Trabalho"); Activate(); Activate(); ConnectionAssert(FeedbackText() == "O Windows negou conectar a Trabalho.", "erro ao conectar");
                var trabalho = wifiState.Networks.Find(n => n.Profile == "Trabalho");
                trabalho.Signal = 30; RenderSettings();
                ConnectionAssert(FocusedTag() == "net-Trabalho" && ActionLabel("net-Trabalho") == "Trabalho · Sinal 2/4", "atualizacao preserva o foco");
                // Rede que sai do alcance com as acoes abertas: some da lista, as acoes fecham e o foco vai para a primeira rede.
                Activate(); ConnectionAssert(connectionMenu == "net-Trabalho", "acoes abertas antes de sair do alcance");
                trabalho.InRange = false; trabalho.Signal = -1; UpdateWifi(wifiState); RenderSettings();
                ConnectionAssert(connectionMenu == null && NetworkRows() == "Casa|Empresa" && FocusedTag() == "net-Casa", "rede que sai do alcance some: " + NetworkRows());
                // Sem a lista de alcance (localizacao negada) nenhuma rede e dada como em alcance: so a nota.
                foreach (var n in wifiState.Networks) { n.InRange = false; n.Connected = false; n.Signal = -1; }
                wifiState.Note = "Alcance das redes indisponível: o Windows exige a localização para apps da área de trabalho."; RenderSettings();
                ConnectionAssert(NetworkRows() == "" && BodyHas(wifiState.Note) && FocusedTag() == "wifi-windows", "nota de alcance");
                wifiState.Note = null; RenderSettings();
                ConnectionAssert(NetworkRows() == "" && BodyHas("Nenhuma rede salva em alcance agora."), "nenhuma rede em alcance");
                foreach (var n in wifiState.Networks) { n.InRange = true; n.Signal = 50; }
                wifiRadio.On = false; RenderSettings();
                ConnectionAssert(NetworkRows() == "" && BodyHas("Wi-Fi desligado. Ligue em Configurações para conectar."), "Wi-Fi desligado sem redes");
                wifiRadio.On = true; RenderSettings(); ConnectionAssert(NetworkRows() == "Casa|Trabalho|Empresa|Antiga", "redes de volta ao alcance");
                FocusHome("wifi-windows"); Activate(); ConnectionAssert(opened == "ms-settings:network-wifi" && !window.IsVisible, "abrir Wi-Fi do Windows");
                Show(); Back(); ConnectionAssert(settingsPage == "main" && FocusedTag() == "page-wifi", "Esc volta da pagina para Configuracoes");
                // Resumo da pagina principal: em alcance, alcance indisponivel e nenhuma em alcance.
                ConnectionAssert(ActionLabel("page-wifi") == "Redes Wi-Fi · 4 em alcance", "resumo com redes em alcance");
                wifiState.Note = "Alcance indisponível (código 1)."; foreach (var n in wifiState.Networks) n.InRange = false;
                RenderSettings(); ConnectionAssert(ActionLabel("page-wifi") == "Redes Wi-Fi · Alcance indisponível", "resumo sem a lista de alcance");
                wifiState.Note = null; RenderSettings(); ConnectionAssert(ActionLabel("page-wifi") == "Redes Wi-Fi · Nenhuma em alcance", "resumo sem redes em alcance");

                // Pagina Bluetooth: audio conecta/desconecta, celular so remove, DualSense sem remover.
                FocusHome("page-bluetooth"); Activate();
                ConnectionAssert(settingsPage == "bluetooth" && ((TextBlock)window.FindName("PanelTitle")).Text == "Bluetooth", "abrir dispositivos Bluetooth");
                window.UpdateLayout(); SaveRender("configuracoes-bluetooth");
                FocusHome("bt-fone"); Activate(); ConnectionAssert(FocusedTag() == "bt-connect", "acoes do fone");
                Activate(); ConnectionAssert(calls[calls.Count - 1] == "audio:fone:True" && FeedbackText() == "Conexão com Fone de teste solicitada.", "conectar audio");
                btDevices.Find(d => d.Key == "fone").Connected = true; RebuildSettings("bt-fone");
                Activate(); ConnectionAssert(FocusedTag() == "bt-disconnect", "fone conectado oferece desconectar");
                Activate(); ConnectionAssert(calls[calls.Count - 1] == "audio:fone:False", "desconectar audio");
                FocusHome("bt-pad"); Activate();
                ConnectionAssert(!actions.Exists(c => (string)c.Tag == "bt-remove") && FocusedTag() == "bt-pad", "DualSense sem remover");
                Back(); FocusHome("bt-celular"); Activate();
                ConnectionAssert(FocusedTag() == "bt-remove" && !actions.Exists(c => (string)c.Tag == "bt-connect"), "celular so remove");
                Activate(); ConnectionAssert(connectionConfirm == "remove" && FocusedTag() == "bt-cancel", "remover pede confirmacao");
                FocusHome("bt-remove-confirm"); Activate();
                ConnectionAssert(calls[calls.Count - 1] == "remover:celular" && !btDevices.Exists(d => d.Key == "celular") && FeedbackText() == "Celular de teste removido.", "remover dispositivo");
                btUnpair = delegate { throw new InvalidOperationException("O Windows negou remover Fone de teste."); };
                FocusHome("bt-fone"); Activate(); FocusHome("bt-remove"); Activate(); FocusHome("bt-remove-confirm"); Activate();
                ConnectionAssert(FeedbackText() == "O Windows negou remover Fone de teste." && btDevices.Exists(d => d.Key == "fone"), "erro ao remover");
                btRadio.On = false; RebuildSettings("bt-fone"); Activate();
                ConnectionAssert(!actions.Exists(c => (string)c.Tag == "bt-connect" || (string)c.Tag == "bt-disconnect"), "Bluetooth desligado sem conectar"); Back();
                btRadio.On = true;
                // L1/R1 sai da pagina; voltar a Configuracoes comeca na pagina principal.
                ChangePanel(1); ChangePanel(-1); ConnectionAssert(panel == 5 && settingsPage == "main", "trocar de painel reinicia a pagina");
                wifiState = new WifiState(); btDevices = new List<BluetoothThing>(); RebuildSettings(null);
                FocusHome("page-wifi"); Activate(); ConnectionAssert(actions.Exists(c => (string)c.Tag == "wifi-windows"), "sem adaptador Wi-Fi");
                Back(); FocusHome("page-bluetooth"); Activate(); ConnectionAssert(actions.Exists(c => (string)c.Tag == "bt-windows"), "sem dispositivos pareados");
                Back();
                File.WriteAllText(Path.Combine(Output, "conexoes-verificacao.txt"),
                    "OK: Conexoes no topo das Configuracoes com o conteudo anterior preservado; Wi-Fi e Bluetooth ligar/desligar, confirmacao com internet pelo Wi-Fi e com DualSense por Bluetooth (HID ou lista), " +
                    "Esc/cancelar, radio ausente e erro; redes salvas so em alcance (fora de alcance oculta, conectada primeiro, conectar, desconectar, esquecer com confirmacao, politica sem esquecer, erro, foco na atualizacao, " +
                    "rede que sai do alcance com as acoes abertas, nota de alcance, nenhuma em alcance, radio desligado, Wi-Fi do Windows, resumo da pagina principal); " +
                    "dispositivos (audio conecta/desconecta, celular remove com confirmacao, DualSense sem remover, erro, radio desligado); paginas, Esc, L1/R1 e listas vazias. Comandos substituidos; nada real alterado.\r\n", Encoding.UTF8);
            }
            finally
            {
                radioSet = set; wifiConnect = connect; wifiForget = forget; wifiDisconnect = disconnect; btAudio = audio; btUnpair = unpair;
                settingsLaunch = launch; networkState = network; batteries.Clear(); ExampleConnections();
                settingsPage = "main"; connectionMenu = null; connectionConfirm = null; radioPending = null;
            }
        }

        // Diagnostico real so de leitura: radios, redes salvas, dispositivos pareados, Luz noturna e DSX; telas so em bin.
        public void VerifyConnectionsLive()
        {
            var lines = new List<string>();
            wifiRadio = Radios.Read("wifi"); btRadio = Radios.Read("bluetooth");
            lines.Add("radio Wi-Fi: " + wifiRadio.Describe() + " | radio Bluetooth: " + btRadio.Describe() + " | acesso: " + Radios.Access());
            wifiState = Wifi.Read(false);
            lines.Add("Wi-Fi: adaptador " + wifiState.Adapter + " | redes salvas " + wifiState.Networks.Count + " | em alcance " + wifiState.Networks.FindAll(n => n.InRange).Count +
                " | conectadas " + wifiState.Networks.FindAll(n => n.Connected).Count + (wifiState.Note == null ? "" : " | " + wifiState.Note));
            btDevices = Bluetooth.Read();
            lines.Add("Bluetooth: pareados " + btDevices.Count + " | conectados " + btDevices.FindAll(d => d.Connected).Count + " | com audio controlavel " + btDevices.FindAll(d => d.AudioControl).Count +
                " | controles " + btDevices.FindAll(d => d.Controller).Count);
            foreach (var d in btDevices) lines.Add("  tipo " + d.Kind + " | conectado " + d.Connected + " | audio " + d.AudioControl + " | entradas " + d.Ids.Count +
                " | bateria guardada " + (d.Battery >= 0 ? d.Battery + "%" : "sem leitura"));
            // Luz noturna: so leitura; a ida e volta do blob acontece em memoria e precisa reproduzir o original (fora o carimbo).
            byte[] blob = NightLight.ReadData(); nightLight = NightLight.Parse(blob); nightKnown = true; string roundTrip = "sem teste";
            if (nightLight != null)
            {
                byte[] flipped = NightLight.Change(blob, !nightLight.Value, 0), back = NightLight.Change(flipped, nightLight.Value, 0);
                bool same = back.Length == blob.Length && NightLight.Parse(flipped) == !nightLight.Value && NightLight.Stamp(back) == NightLight.Stamp(blob) + 2;
                for (int i = 0; same && i < blob.Length; i++) if ((i < 10 || i > 14) && back[i] != blob[i]) same = false;
                if (!same) throw new Exception("Luz noturna: a ida e volta em memoria nao reproduziu o blob.");
                roundTrip = "ok (" + flipped.Length + " bytes no estado oposto)";
            }
            lines.Add("Luz noturna: " + (nightLight == null ? "indisponivel" : nightLight.Value ? "ligada" : "desligada") + " | blob " + (blob == null ? 0 : blob.Length) + " bytes | ida e volta em memoria " + roundTrip);
            int dsxProcesses = 0; foreach (var p in Process.GetProcessesByName("DSX")) using (p) dsxProcesses++;
            DesktopWindow dsxWindow; using (var self = Process.GetCurrentProcess()) dsxWindow = FindDsx(DesktopWindows.Read((uint)self.Id));
            lines.Add("DSX: processos " + dsxProcesses + " | janela visivel " + (dsxWindow != null) + " | o botao faria: " + (dsxWindow != null ? "trazer a janela" : "abrir " + DsxUri));
            lines.Add("Nenhum radio, rede, dispositivo ou Luz noturna alterado; DSX nao aberto; sem varredura. Nomes de redes e aparelhos ficam so nas telas em bin.");
            window.Width = 1920; window.Height = 1080; window.Left = -30000; window.Top = -30000; window.Show();
            SetPanel(5); window.UpdateLayout(); SaveRender("conexoes-reais");
            settingsPage = "wifi"; RebuildSettings(null); SaveRender("conexoes-reais-wifi");
            settingsPage = "bluetooth"; RebuildSettings(null); SaveRender("conexoes-reais-bluetooth");
            settingsPage = "main"; window.Hide();
            File.WriteAllLines(Path.Combine(Output, "conexoes-reais.txt"), lines.ToArray(), Encoding.UTF8);
            WriteLog("conexoes: leitura real concluida, sem alteracoes");
        }
    }
}

// Configuracoes: Luz noturna (filtro amarelo do Windows) e atalho para o DSX, onde o usuario ajusta o controle.
// Leitura junto com as Conexoes; ligar/desligar e abrir o DSX so por escolha do usuario.
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using XGBP.Desktop;
using XGBP.Display;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        // DSX na Steam (steamapps\appmanifest_1812620.acf). O launcher dele autentica pela Steam antes de abrir o programa.
        const string DsxUri = "steam://rungameid/1812620";
        bool? nightLight;
        bool nightKnown, nightPending;
        // Substituiveis: a verificacao nunca altera a Luz noturna real.
        Func<bool?> nightRead = NightLight.Read;
        Action<bool> nightSet = NightLight.Set;

#if XGB_VERIFY
        void ExampleSettingsExtras() { nightLight = false; nightKnown = true; nightPending = false; }
#endif

        void NightLightRow()
        {
            string detail = !nightKnown ? "Lendo…" : nightPending ? "Aplicando…" : nightLight == null ? "Indisponível" : nightLight.Value ? "Ligada" : "Desligada";
            var button = ToggleSetting(body, "Luz noturna", nightKnown ? nightLight : null, "night-light", ToggleNightLight,
                !nightKnown || nightPending || nightLight == null ? detail : null);
            button.IsEnabled = nightKnown && nightLight != null;
        }
        void ToggleNightLight()
        {
            if (!nightKnown || nightLight == null || nightPending) return;
            bool on = !nightLight.Value; nightPending = true;
            var notice = BeginFeedback(on ? "Ligando a Luz noturna…" : "Desligando a Luz noturna…"); RebuildSettings("night-light");
            WriteLog("configuracoes: luz noturna -> " + (on ? "ligada" : "desligada"));
            var set = nightSet;
            ConnectionWork(delegate { set(on); }, delegate(Exception error) {
                nightPending = false;
                if (error != null) { FinishFeedback(notice, error.Message, FeedbackKind.Error); WriteLog("configuracoes: luz noturna falhou | " + error.Message); }
                else { nightLight = on; FinishFeedback(notice, on ? "Luz noturna ligada." : "Luz noturna desligada.", FeedbackKind.Success); }
                connectionsNextRead = DateTime.MinValue; RebuildSettings("night-light"); ConnectionsTick();
            });
        }

        // DSX residente fica na bandeja com a janela oculta (ADR 0079). Abrir pela Steam com ele rodando cria outra
        // instancia, que o DSX 3.2 trata com aviso de instancia e pode encerrar a residente. A Steam so abre o DSX fechado.
        Func<DesktopWindow> dsxTrayWindow = delegate { return DesktopWindows.FindTrayWindow("DSX", IsDsxMain); };
        Func<bool?> dsxRunning = delegate { return DesktopWindows.ProcessRunning("DSX", IsDsx); };
        Action<DesktopWindow> dsxReveal = DesktopWindows.Reveal;
        Func<DesktopWindow, bool> dsxShown = DesktopWindows.IsShown;
        Action<int> dsxWait = System.Threading.Thread.Sleep;
        Func<bool> dsxElevated = delegate { return LaunchSettings.Elevated; };
        void OpenDsx()
        {
            var read = readOpenApps; var tray = dsxTrayWindow; var running = dsxRunning; var reveal = dsxReveal; var shown = dsxShown; var wait = dsxWait;
            DesktopWindow found = null, hidden = null; bool? alive = null; string refused = null;
            ConnectionWork(delegate {
                try { found = FindDsx(read()); } catch (Exception ex) { WriteLog("dsx: leitura das janelas falhou | " + ex.Message); }
                if (found != null) return;
                hidden = tray();
                if (hidden == null) { alive = running(); return; }
                try { reveal(hidden); for (int i = 0; i < 30 && !shown(hidden); i++) wait(50); }
                catch (Exception ex) { refused = ex.Message; }
                try { found = FindDsx(read()); } catch (Exception) { }
            }, delegate(Exception error) {
                if (found != null) { WriteLog("dsx: janela aberta | " + found.Key + (hidden != null ? " | restaurada da bandeja" : "")); OpenApp(found); return; }
                if (hidden != null) { DsxNotShown(refused); return; }
                if (error != null || alive == null)
                {
                    Feedback("Não foi possível verificar se o DSX está aberto. Abra-o pelo ícone da bandeja.", FeedbackKind.Error);
                    WriteLog("dsx: verificacao falhou; Steam nao chamada | " + (error != null ? error.Message : "processo DSX sem caminho legivel")); return;
                }
                if (alive == true) { Feedback("O DSX está abrindo. Tente de novo em instantes.", FeedbackKind.Attention); WriteLog("dsx: processo sem janela; Steam nao chamada"); return; }
                try { steamLaunch(DsxUri); WriteLog("dsx: fechado; aberto pela Steam | " + DsxUri); Hide(); }
                catch (Exception ex) { Feedback("Não foi possível abrir o DSX. Confira se ele está instalado na Steam.", FeedbackKind.Error); WriteLog("dsx: falha ao abrir | " + ex.Message); }
            });
        }
        void DsxNotShown(string reason)
        {
            bool elevated = dsxElevated();
            WriteLog("dsx: janela da bandeja nao apareceu | elevado=" + elevated + " | " + (reason ?? "sem confirmacao em 1,5 s"));
            Feedback(elevated ? "O DSX não mostrou a janela. Abra-o pelo ícone da bandeja." :
                "O DSX roda como administrador. Para abri-lo pela barra, ligue “Sempre executar como administrador” em Configurações.", FeedbackKind.Attention);
        }
        static DesktopWindow FindDsx(List<DesktopWindow> windows)
        {
            return windows == null ? null : windows.Find(w => w.ExecutablePath != null && IsDsx(w.ExecutablePath));
        }
        // O lancador fica na raiz da pasta da Steam e sai depois de abrir o programa, que fica numa subpasta (Main_v3_Beta).
        static bool IsDsx(string path)
        {
            return path.EndsWith(@"\DSX.exe", StringComparison.OrdinalIgnoreCase) && path.IndexOf(@"\steamapps\common\DSX\", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        static bool IsDsxMain(string path)
        {
            if (!IsDsx(path)) return false;
            int at = path.IndexOf(@"\steamapps\common\DSX\", StringComparison.OrdinalIgnoreCase) + @"\steamapps\common\DSX\".Length;
            return path.IndexOf('\\', at) > 0;
        }

        // Cor atual do contorno de um botao (Frame do template).
        Color? Outline(Control c)
        {
            var frame = c.Template == null ? null : c.Template.FindName("Frame", c) as Border;
            var brush = frame == null ? null : frame.BorderBrush as SolidColorBrush;
            return brush == null ? (Color?)null : brush.Color;
        }
        // O mouse nao pode ser simulado aqui: confere no estilo que passar o mouse pinta o contorno de vermelho.
        bool CloseHoverIsRed()
        {
            var style = (Style)window.FindResource("CloseAction");
            foreach (var s in style.Setters)
            {
                var setter = s as Setter; var template = setter == null ? null : setter.Value as ControlTemplate;
                if (setter == null || setter.Property != Control.TemplateProperty || template == null) continue;
                foreach (var t in template.Triggers)
                {
                    var trigger = t as Trigger;
                    if (trigger == null || trigger.Property != UIElement.IsMouseOverProperty || !true.Equals(trigger.Value)) continue;
                    foreach (var item in trigger.Setters)
                    {
                        var paint = item as Setter; var brush = paint == null ? null : paint.Value as SolidColorBrush;
                        if (paint != null && paint.TargetName == "Frame" && paint.Property == Border.BorderBrushProperty && brush != null && brush.Color == Brush("CloseRed").Color) return true;
                    }
                }
            }
            return false;
        }
        static void ExtrasAssert(bool condition, string message) { if (!condition) throw new Exception("Configuracoes: " + message); }

#if XGB_VERIFY
        void VerifySettingsExtras()
        {
            var set = nightSet; var read = readOpenApps; var activate = switchApp; var available = appAvailable; var foreground = appForeground; var launch = steamLaunch;
            var trayWindow = dsxTrayWindow; var dsxAlive = dsxRunning; var reveal = dsxReveal; var shown = dsxShown; var wait = dsxWait; var elevatedNow = dsxElevated;
            var calls = new List<string>(); string activated = null;
            var browser = new DesktopWindow { Handle = new IntPtr(201), ProcessId = 70, Title = "Navegador", AppName = "Navegador", ExecutablePath = @"C:\Apps\navegador.exe" };
            var dsx = new DesktopWindow { Handle = new IntPtr(202), ProcessId = 71, Title = "DSX", AppName = "DSX", ExecutablePath = @"D:\SteamLibrary\steamapps\common\DSX\Main_v3_Beta\DSX.exe" };
            try
            {
                // Blob da Luz noturna ligada, observado neste PC: desligar remove 10 00 e ajusta o tamanho; religar volta ao original.
                byte[] on = { 0x43, 0x42, 0x01, 0x00, 0x0A, 0x02, 0x01, 0x00, 0x2A, 0x06, 0xC8, 0x9B, 0xEF, 0xD6, 0x06, 0x2A, 0x2B, 0x0E, 0x15, 0x43, 0x42, 0x01,
                    0x00, 0x10, 0x00, 0xD0, 0x0A, 0x02, 0xC6, 0x14, 0x82, 0xBC, 0xFB, 0xE4, 0xB3, 0xE6, 0xD8, 0xEE, 0x01, 0x00, 0x00, 0x00, 0x00 };
                ExtrasAssert(NightLight.Parse(on) == true && NightLight.Stamp(on) == 1792789960, "blob ligado e carimbo");
                byte[] off = NightLight.Change(on, false, 1792800000);
                ExtrasAssert(off.Length == 41 && off[18] == 0x13 && off[23] == 0xD0 && NightLight.Parse(off) == false && NightLight.Stamp(off) == 1792800000, "desligar no blob");
                byte[] again = NightLight.Change(off, true, 0);
                bool same = again.Length == on.Length && NightLight.Stamp(again) == 1792800001;
                for (int i = 0; same && i < on.Length; i++) if ((i < 10 || i > 14) && again[i] != on[i]) same = false;
                ExtrasAssert(same && NightLight.Change(on, true, 0) == on, "religar volta ao original, com carimbo mais novo; mesmo estado nao muda");
                byte[] broken = (byte[])on.Clone(); broken[18] = 0x16;
                ExtrasAssert(NightLight.Parse(broken) == null && NightLight.Parse(new byte[10]) == null && NightLight.Parse(null) == null, "formato desconhecido recusado");
                broken = (byte[])on.Clone(); broken[24] = 0x01;
                bool refused = false; try { NightLight.Change(broken, false, 0); } catch (InvalidOperationException) { refused = true; }
                ExtrasAssert(refused, "nao escreve sobre formato desconhecido");

                ExampleConnections(); ExampleSettingsExtras(); settingsPage = "main";
                nightSet = delegate(bool value) { calls.Add("luz:" + value); };
                SetPanel(5); Show(); window.UpdateLayout();
                ExtrasAssert(BodyHas("TELA") && BodyHas("CONTROLE") && ActionLabel("night-light") == "Luz noturna · Desativado", "secoes Tela e Controle com a Luz noturna desligada");
                // Luz noturna: alterna pelo Windows, mantem o foco e o interruptor confirmado; erro nao muda o estado.
                FocusHome("night-light"); Activate();
                ExtrasAssert(calls.Count == 1 && calls[0] == "luz:True" && nightLight == true && !nightPending && FeedbackText() == "Luz noturna ligada." &&
                    FocusedTag() == "night-light" && ActionLabel("night-light") == "Luz noturna · Ativado", "ligar a Luz noturna");
                var row = (Button)actions.Find(c => (string)c.Tag == "night-light");
                ExtrasAssert(((SettingToggle)row).State == true, "Luz noturna ativada no interruptor comum");
                window.UpdateLayout(); SaveRender("configuracoes-tela-controle");
                Activate(); ExtrasAssert(calls[1] == "luz:False" && nightLight == false && FeedbackText() == "Luz noturna desligada.", "desligar a Luz noturna");
                nightSet = delegate { throw new InvalidOperationException("Formato da Luz noturna desconhecido."); };
                Activate(); ExtrasAssert(nightLight == false && !nightPending && FeedbackText() == "Formato da Luz noturna desconhecido." && FocusedTag() == "night-light", "erro da Luz noturna");
                nightSet = delegate(bool value) { calls.Add("luz:" + value); };
                nightLight = null; RebuildSettings("night-light");
                ExtrasAssert(!actions.Find(c => (string)c.Tag == "night-light").IsEnabled && ActionLabel("night-light") == "Luz noturna · Indisponível", "Luz noturna indisponivel desabilitada");
                ToggleNightLight(); ExtrasAssert(calls.Count == 2, "indisponivel nao envia nada");
                nightKnown = false; RebuildSettings(null); ExtrasAssert(ActionLabel("night-light") == "Luz noturna · Lendo…", "Luz noturna antes da leitura");
                ExampleSettingsExtras(); RebuildSettings(null);

                // DSX: com janela vem para frente; na bandeja, a janela e restaurada; rodando sem janela ou sem leitura,
                // a Steam nunca e chamada; fechado abre pela Steam; erro da Steam avisa e a barra fica (ADR 0079).
                switchApp = delegate(DesktopWindow w) { activated = w.Key; }; appAvailable = delegate { return true; }; appForeground = delegate { return true; };
                readOpenApps = delegate { return new List<DesktopWindow> { browser, dsx }; }; lastSteamUri = null;
                bool shownNow = false; DesktopWindow revealed = null; bool? running = false; bool elevated = true;
                dsxTrayWindow = delegate { return revealed == null ? null : dsx; }; dsxRunning = delegate { return running; };
                dsxReveal = delegate(DesktopWindow w) { calls.Add("restaurar:" + w.Key); }; dsxShown = delegate { return shownNow; }; dsxWait = delegate { };
                dsxElevated = delegate { return elevated; };
                FocusHome("dsx"); Activate();
                ExtrasAssert(activated == "71:202" && lastSteamUri == null && !window.IsVisible && switchingApp == null && !calls.Contains("restaurar:71:202"), "DSX aberto vem para frente");
                Show(); activated = null; revealed = dsx;
                readOpenApps = delegate { return shownNow ? new List<DesktopWindow> { browser, dsx } : new List<DesktopWindow> { browser }; };
                dsxReveal = delegate(DesktopWindow w) { calls.Add("restaurar:" + w.Key); shownNow = true; };
                FocusHome("dsx"); Activate();
                ExtrasAssert(calls.Contains("restaurar:71:202") && activated == "71:202" && lastSteamUri == null && !window.IsVisible, "DSX na bandeja e restaurado e vem para frente, sem Steam");
                Show(); calls.Clear(); activated = null; shownNow = false; elevated = false;
                dsxReveal = delegate(DesktopWindow w) { calls.Add("restaurar:" + w.Key); };
                FocusHome("dsx"); Activate();
                ExtrasAssert(calls.Count == 1 && activated == null && lastSteamUri == null && window.IsVisible && FeedbackText().Contains("Sempre executar como administrador"), "bandeja sem elevacao avisa e nao chama a Steam");
                elevated = true; dsxReveal = delegate { throw new InvalidOperationException("negado"); };
                FocusHome("dsx"); Activate();
                ExtrasAssert(lastSteamUri == null && window.IsVisible && FeedbackText() == "O DSX não mostrou a janela. Abra-o pelo ícone da bandeja.", "restauracao recusada avisa e nao chama a Steam");
                revealed = null; running = true; readOpenApps = delegate { return new List<DesktopWindow> { browser }; };
                FocusHome("dsx"); Activate();
                ExtrasAssert(lastSteamUri == null && window.IsVisible && FeedbackText() == "O DSX está abrindo. Tente de novo em instantes.", "processo sem janela nao chama a Steam");
                running = null; FocusHome("dsx"); Activate();
                ExtrasAssert(lastSteamUri == null && window.IsVisible && FeedbackText().StartsWith("Não foi possível verificar se o DSX"), "processo sem caminho legivel nao chama a Steam");
                running = false; FocusHome("dsx"); Activate(); ExtrasAssert(activated == null && lastSteamUri == DsxUri && !window.IsVisible, "DSX fechado abre pela Steam");
                Show(); lastSteamUri = null; readOpenApps = delegate { throw new InvalidOperationException("janelas indisponiveis"); };
                FocusHome("dsx"); Activate(); ExtrasAssert(lastSteamUri == DsxUri && !window.IsVisible, "leitura das janelas falha, processo fechado e abre pela Steam");
                Show(); lastSteamUri = null; dsxRunning = delegate { throw new InvalidOperationException("processos indisponiveis"); };
                FocusHome("dsx"); Activate(); ExtrasAssert(lastSteamUri == null && window.IsVisible, "nenhuma leitura possivel nao chama a Steam");
                dsxRunning = delegate { return running; };
                Show(); readOpenApps = delegate { return new List<DesktopWindow>(); }; steamLaunch = delegate { throw new InvalidOperationException("falha de despacho simulada"); };
                FocusHome("dsx"); Activate();
                ExtrasAssert(window.IsVisible && FeedbackText() == "Não foi possível abrir o DSX. Confira se ele está instalado na Steam.", "erro ao abrir o DSX");
                ExtrasAssert(FindDsx(new List<DesktopWindow> { new DesktopWindow { ExecutablePath = @"C:\Outro\DSX.exe" } }) == null, "outro DSX.exe fora da Steam nao conta");
                ExtrasAssert(IsDsxMain(@"C:\Program Files (x86)\Steam\steamapps\common\DSX\Main_v3_Beta\DSX.exe") && !IsDsxMain(@"C:\Program Files (x86)\Steam\steamapps\common\DSX\DSX.exe") &&
                    IsDsx(@"C:\Program Files (x86)\Steam\steamapps\common\DSX\DSX.exe"), "janela da bandeja so do programa; lancador conta como DSX abrindo");

                // Fechar apps e jogos: contorno vermelho com o mouse (estilo) e com o foco (testado na Home).
                ExtrasAssert(CloseHoverIsRed(), "mouse sobre fechar pinta o contorno de vermelho");
                System.IO.File.WriteAllText(System.IO.Path.Combine(Output, "configuracoes-extras-verificacao.txt"),
                    "OK: blob da Luz noturna (ligado observado, desligar/religar em memoria, carimbo mais novo, mesmo estado sem mudanca, formato desconhecido recusado); " +
                    "secoes Tela e Controle nas Configuracoes; Luz noturna liga/desliga pelo backend substituido, mantem o foco, fica verde ligada, erro sem mudar o estado, indisponivel desabilitada e sem envio, leitura pendente; " +
                    "DSX com janela vem para frente, na bandeja e restaurado sem Steam, sem elevacao/restauracao recusada avisa, processo sem janela ou sem leitura nunca chama a Steam, " +
                    "fechado abre pela Steam (" + DsxUri + "), erro da Steam avisa, DSX.exe fora da Steam ignorado e lancador separado do programa; " +
                    "contorno vermelho nos botoes de fechar com o mouse (estilo) e com o foco (jogos e apps, na verificacao da Home). Nada real alterado.\r\n", System.Text.Encoding.UTF8);
            }
            finally
            {
                nightSet = set; readOpenApps = read; switchApp = activate; appAvailable = available; appForeground = foreground; steamLaunch = launch;
                dsxTrayWindow = trayWindow; dsxRunning = dsxAlive; dsxReveal = reveal; dsxShown = shown; dsxWait = wait; dsxElevated = elevatedNow;
                switchingApp = null; appSwitchTimer.Stop(); ExampleConnections(); ExampleSettingsExtras(); settingsPage = "main";
            }
        }
#endif
    }
}

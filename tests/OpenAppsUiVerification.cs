// Fixtures e verificacoes deste painel, separadas do codigo de operacao.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using XGBP.Desktop;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        static List<DesktopWindow> ExampleOpenApps()
        {
            return new List<DesktopWindow> {
                new DesktopWindow { Handle = new IntPtr(101), ProcessId = 10, Title = "Navegador · exemplo", AppName = "Navegador" },
                new DesktopWindow { Handle = new IntPtr(102), ProcessId = 20, Title = "Steam · exemplo", AppName = "Steam", Minimized = true },
                new DesktopWindow { Handle = new IntPtr(103), ProcessId = 30, Title = "Arquivos · exemplo", AppName = "Arquivos" }
            };
        }
        // Segurar 2 s: X ao lado do icone, foco nele, fechar pela janela, recolher por Esc ou foco, toque curto abre o app.
        void VerifyOpenAppClose()
        {
            var closer = appClose; var closed = new List<string>(); string activated = null; var activate = switchApp;
            var existence = appExists;
            var foreground = appForeground;
            try
            {
                appClose = delegate(DesktopWindow w) { closed.Add(w.Key); }; switchApp = delegate(DesktopWindow w) { activated = w.Key; };
                appExists = delegate(DesktopWindow w) { return openApps.Exists(a => a.Key == w.Key); };
                appForeground = delegate { return true; };
                Show(); openApps = ExampleOpenApps(); RebuildOpenApps(); window.UpdateLayout();
                FocusHome("recent-fixture:two");
                if (BeginHold("key")) throw new Exception("Falha: segurar so vale para icones de apps.");
                openAppsRow[1].Focus();
                if (!BeginHold("pad")) throw new Exception("Falha: iniciar o segurar no icone.");
                ReleaseHold();
                if (activated != "20:102" || window.IsVisible || openAppsRow.Count != 3) throw new Exception("Falha: toque curto abre o app sem X.");
                switchingApp = null; appSwitchTimer.Stop();
                Show(); openAppsRow[1].Focus(); BeginHold("pad"); HoldElapsed(); window.UpdateLayout();
                if (openAppsRow.Count != 4 || (string)openAppsRow[2].Tag != "close-window-20:102" || Keyboard.FocusedElement != openAppsRow[2] ||
                    (string)openAppsRow[2].ToolTip != "Fechar Steam" || Math.Abs(openAppsRow[2].ActualWidth - 52) > .5 || !actions.Contains(openAppsRow[2]))
                    throw new Exception("Falha: X ao lado do icone segurado, com foco.");
                if (Outline(openAppsRow[2]) != Brush("CloseRed").Color) throw new Exception("Falha: X de fechar em foco com contorno vermelho.");
                double xLeft = openAppsRow[2].TranslatePoint(new Point(0, 0), openAppsButtons).X;
                if (Math.Abs(xLeft - 2 * OpenAppSlot) > .5) throw new Exception("Falha: X no mesmo passo da faixa.");
                SaveRender("faixa-fechar-app");
                activated = null; ReleaseHold();
                if (activated != null || openAppsRow.Count != 4) throw new Exception("Falha: soltar depois do X nao abre o app.");
                RebuildOpenApps(); if (openAppsRow.Count != 4) throw new Exception("Falha: X sobrevive a leitura da faixa.");
                Navigate("ESQUERDA"); if (Keyboard.FocusedElement != openAppsRow[1] || openAppsRow.Count != 4) throw new Exception("Falha: voltar do X ao icone.");
                Navigate("DIREITA"); if (Keyboard.FocusedElement != openAppsRow[2]) throw new Exception("Falha: ir do icone ao X.");
                Activate();
                if (closed.Count != 1 || closed[0] != "20:102" || openAppsRow.Count != 3 || (string)((Control)Keyboard.FocusedElement).Tag != "window-20:102" || !window.IsVisible ||
                    LatestHistoryText() != "Fechando Steam…") throw new Exception("Falha: fechar pelo X.");
                openApps.RemoveAt(1); CheckAppClose();
                if (LatestHistoryText() != "Janela de Steam fechada." || closingApp != null) throw new Exception("Falha: confirmar fechamento pela leitura.");
                openApps = ExampleOpenApps(); RebuildOpenApps(); openAppsRow[0].Focus(); BeginHold("key"); HoldElapsed();
                closingApp = null; Back();
                if (openAppsRow.Count != 3 || Keyboard.FocusedElement != openAppsRow[0] || panel != 0 || !window.IsVisible) throw new Exception("Falha: Esc recolhe o X.");
                openAppsRow[0].Focus(); BeginHold("key"); HoldElapsed(); FocusHome("steam-home");
                if (openAppsRow.Count != 3) throw new Exception("Falha: foco fora recolhe o X.");
                openAppsRow[0].Focus(); BeginHold("mouse", openAppsRow[0]); HoldElapsed();
                if (holdTarget != null || openAppsRow.Count != 4) throw new Exception("Falha: segurar com o mouse.");
                appClose = delegate { throw new InvalidOperationException("O Windows negou fechar Navegador. Ele pode estar como administrador."); };
                FocusHome("close-window-10:101"); Activate();
                if (!((TextBlock)window.FindName("Feedback")).Text.StartsWith("O Windows negou") || openAppsRow.Count != 3) throw new Exception("Falha: erro ao fechar.");
                appClose = delegate(DesktopWindow w) { closed.Add(w.Key); }; openAppsRow[2].Focus(); BeginHold("pad"); HoldElapsed();
                closingApp = new DesktopWindow { Handle = new IntPtr(103), ProcessId = 30, AppName = "Arquivos" }; appCloseDeadline = DateTime.UtcNow.AddSeconds(-1); CheckAppClose();
                if (!((TextBlock)window.FindName("Feedback")).Text.Contains("pedindo confirmação")) throw new Exception("Falha: janela que continua aberta.");
            }
            finally { appForeground = foreground; appExists = existence; appClose = closer; switchApp = activate; CancelHold(); appCloseTag = null; closingApp = null; openApps = ExampleOpenApps(); RebuildOpenApps(); }
        }
        // Fechamento real so numa janela criada aqui, fora da tela: nenhum app do usuario e tocado.
        public void VerifyAppCloseLive()
        {
            var form = new System.Windows.Forms.Form { Text = "XGB_P · teste de fechar", StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                Location = new System.Drawing.Point(-3000, -3000), Size = new System.Drawing.Size(220, 120) };
            bool closed = false; form.FormClosed += delegate { closed = true; };
            try
            {
                // Com o processo iniciado oculto, o primeiro ShowWindow herda SW_HIDE: mostrar de novo, sem ativar.
                form.Show(); ShowWindow(form.Handle, 4); PumpMotion(250);
                var item = DesktopWindows.Read(0).Find(w => w.Handle == form.Handle);
                if (item == null)
                {
                    uint own = (uint)Process.GetCurrentProcess().Id;
                    throw new Exception("Falha: janela de teste fora da enumeracao. Visivel=" + form.Visible + " handle=" + form.Handle +
                        " | janelas do processo: " + string.Join("; ", DesktopWindows.Read(0).FindAll(w => w.ProcessId == own).ConvertAll(w => w.Handle + " " + w.Title).ToArray()));
                }
                DesktopWindows.Close(item); PumpMotion(400);
                if (!closed || DesktopWindows.Belongs(item)) throw new Exception("Falha: WM_CLOSE nao fechou a janela de teste.");
                bool refused = false; try { DesktopWindows.Close(item); } catch (InvalidOperationException) { refused = true; }
                if (!refused) throw new Exception("Falha: fechar de novo deve recusar janela inexistente.");
                File.WriteAllText(Path.Combine(Output, "apps-fechar-real.txt"), "OK: janela de teste encontrada pela enumeracao, fechada por WM_CLOSE e recusada na segunda tentativa. Nenhum app do usuario tocado.\r\n", System.Text.Encoding.UTF8);
                WriteLog("janelas: fechar real verificado so na janela de teste");
            }
            finally { if (!form.IsDisposed) form.Dispose(); }
        }
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int command);
        void VerifyOpenApps()
        {
            var original = openApps; var available = appAvailable; var activate = switchApp; var foreground = appForeground;
            string activated = null;
            try
            {
                appAvailable = delegate { return true; }; switchApp = delegate(DesktopWindow w) {
                    if (!window.IsVisible) throw new Exception("Solicitar alternancia antes de ocultar a previa.");
                    activated = w.Key;
                }; appForeground = delegate { return true; };
                SetPanel(0); Show(); window.UpdateLayout();
                if (openAppsScroll.ViewportWidth < 400 || openAppsPreviousEdge.Visibility == Visibility.Visible || openAppsNextEdge.Visibility == Visibility.Visible)
                    throw new Exception("Falha: faixa curta deve ocupar a largura toda, sem setas.");
                openAppsRow[1].Focus(); Activate();
                if (activated != openApps[1].Key || window.IsVisible) throw new Exception("Falha: alternar janela minimizada pela Home.");
                Show(); appAvailable = delegate { return false; }; openAppsRow[0].Focus(); Activate();
                if (!window.IsVisible || !LatestHistoryText().Contains("fechada")) throw new Exception("Falha: janela encerrada.");
                appAvailable = delegate { return true; }; switchApp = delegate { throw new InvalidOperationException("falha simulada"); };
                openAppsRow[0].Focus(); Activate();
                if (!window.IsVisible) throw new Exception("Falha: preservar previa em erro de alternancia.");
                switchApp = delegate { }; appForeground = delegate { return false; };
                openAppsRow[0].Focus(); Activate(); switchDeadline = DateTime.UtcNow.AddSeconds(-1); CheckAppSwitch();
                if (!window.IsVisible || !((TextBlock)window.FindName("Feedback")).Text.Contains("não confirmou")) throw new Exception("Falha: confirmar foreground.");
                VerifyOpenAppClose();
                openApps = new List<DesktopWindow>(original);
                for (int i = 0; i < 20; i++) openApps.Add(new DesktopWindow { Handle = new IntPtr(200 + i), ProcessId = 99, Title = "App " + i, AppName = "App" });
                RebuildOpenApps(); window.UpdateLayout(); FocusHome("steam-home");
                ScrollOpenApps(0); window.UpdateLayout();
                if (openAppsPreviousEdge.Visibility == Visibility.Visible || openAppsNextEdge.Visibility != Visibility.Visible) throw new Exception("Falha: seta no inicio da faixa.");
                SaveRender("faixa-exemplo-inicio");
                ScrollOpenApps(openAppsScroll.ScrollableWidth / 2); window.UpdateLayout();
                if (openAppsPreviousEdge.Visibility != Visibility.Visible || openAppsNextEdge.Visibility != Visibility.Visible) throw new Exception("Falha: setas no meio da faixa.");
                SaveRender("faixa-exemplo-meio");
                ScrollOpenApps(openAppsScroll.ScrollableWidth); window.UpdateLayout();
                if (openAppsPreviousEdge.Visibility != Visibility.Visible || openAppsNextEdge.Visibility == Visibility.Visible) throw new Exception("Falha: seta no fim da faixa.");
                SaveRender("faixa-exemplo-fim");
                openAppsRow[15].Focus(); window.UpdateLayout();
                if (openAppsScroll.HorizontalOffset <= 0) throw new Exception("Falha: rolagem da faixa de janelas.");
                double focusedX = openAppsRow[15].TranslatePoint(new Point(0, 0), openAppsScroll).X;
                double leftInset = openAppsPreviousEdge.Visibility == Visibility.Visible ? OpenAppEdge : 0;
                double rightInset = openAppsNextEdge.Visibility == Visibility.Visible ? OpenAppEdge : 0;
                if (focusedX < leftInset - 1 || focusedX + openAppsRow[15].ActualWidth > openAppsScroll.ViewportWidth - rightInset + 1)
                    throw new Exception("Falha: app em foco encoberto pela seta/esfumacado.");
                SaveRender("faixa-exemplo-foco");
                openAppsRow[0].Focus(); window.UpdateLayout();
                ((Button)openAppsNextEdge.Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
                int pagedFocus = openAppsRow.IndexOf(Keyboard.FocusedElement as Control);
                if (pagedFocus <= 0 || !window.IsVisible) throw new Exception("Falha: paginar sem ativar app, mantendo foco visivel.");
                string tag = (string)((Control)Keyboard.FocusedElement).Tag;
                openApps.RemoveAt(0); RebuildOpenApps(); window.UpdateLayout();
                if ((string)((Control)Keyboard.FocusedElement).Tag != tag) throw new Exception("Falha: preservar foco durante tracking de janelas.");
                openApps.Clear(); RebuildOpenApps(); FocusHome("steam-home"); Navigate("BAIXO"); Navigate("BAIXO");
                // A lista comeca pelo primeiro outro jogo, sem repetir o destaque (ADR 0060).
                if ((string)((Control)Keyboard.FocusedElement).Tag != "recent-fixture:one") throw new Exception("Falha: navegar sem aplicativos para o primeiro recente sem destaque.");
            }
            finally
            {
                appAvailable = available; switchApp = activate; appForeground = foreground; switchingApp = null; appSwitchTimer.Stop();
                openApps = original; SetPanel(0);
            }
        }
        public void VerifyOpenAppsLive()
        {
            openApps = Task.Factory.StartNew<List<DesktopWindow>>(delegate { using (var process = Process.GetCurrentProcess()) return DesktopWindows.Read((uint)process.Id); }).Result;
            window.Width = 1920; window.Height = 1080; window.Left = -30000; window.Top = -30000;
            window.Show(); SetPanel(0);
            window.UpdateLayout(); SaveRender("home-janelas-reais");
            ScrollOpenApps(openAppsScroll.ScrollableWidth / 2); window.UpdateLayout(); SaveRender("home-janelas-meio");
            ScrollOpenApps(openAppsScroll.ScrollableWidth); window.UpdateLayout(); SaveRender("home-janelas-fim");
            var report = new List<string>(); report.Add(openApps.Count + " janelas elegiveis; somente leitura, sem alternancia");
            foreach (var item in openApps) report.Add(item.Key + " | " + item.AppName + " | minimizada=" + item.Minimized + " | icone=" + (item.IconPng != null));
            File.WriteAllLines(Path.Combine(Output, "janelas-reais.txt"), report.ToArray()); window.Hide();
            File.WriteAllLines(Path.Combine(Output, "janelas-politica-real.txt"), DesktopWindows.AppWindowDiagnostics(OwnProcessId).ToArray(), Encoding.UTF8);
        }
    }
}

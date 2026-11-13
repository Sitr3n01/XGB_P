// Exercita a variante real depois da suite de regressao do layout publicado. Sem HID ou comandos externos.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XGBP.Input;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        // Desde a ADR 0112 roda sempre no --verify-motion: o layout sem trilho e uma opcao, sem copia com marcador.
        public void VerifySlidingTitle()
        {
            var checks = new List<string>();
            Action<bool, string> check = delegate(bool ok, string why) {
                if (!ok) throw new Exception("Titulo deslizante: " + why);
                checks.Add("OK: " + why);
            };
            bool oldReduced = motionReduced; var oldFocus = controllerFocus; var oldMode = activationMode;
            try
            {
                MotionFixtures(); SettleMotion(); motionReduced = true;
                slidingTitleTesting = true; SetSlidingTitleLayout(true);
                controllerFocus = delegate { return true; }; activationMode = true;
                window.Width = 1920; window.Height = 1080; window.Left = window.Top = -30000;
                SetPanel(0); Show(); window.UpdateLayout();
                check(MotionPart("RailFrame").Visibility == Visibility.Collapsed, "trilho fora da tela e da navegacao por Tab");
                check(Math.Abs(MotionPart("Sidebar").ActualWidth - 480) < .5, "barra ocupa so os 480 DIP do painel");
                check(MotionPart("PanelTitle").Parent == titleHeader && titleHeader.IsVisible, "o titulo existente continua no cabecalho");
                check(titleNext.ActualWidth >= 44 && titleNext.ActualHeight >= 44, "seta tem alvo de pelo menos 44 DIP");
                check(((TextBlock)MotionPart("PanelTitle")).FontSize == 33 &&
                    Math.Abs(SceneBox(MotionPart("PanelTitle")).Left - SceneBox(MotionPart("PanelContent")).Left) < .5,
                    "titulo preserva fonte original e alinhamento a esquerda");
                check(titleHeader.Children.Count == 2 && ((TextBlock)titleNext.Content).Text == "\uE76C",
                    "cabecalho tem apenas o titulo e uma seta a direita");
                var steamEntry = (StackPanel)((Button)actions.Find(c => (string)c.Tag == "steam-home")).Content;
                check(((TextBlock)steamEntry.Children[1]).Text == "Início", "conteudo original da Home preservado");
                DateTime now = DateTime.UtcNow;
                Action<string> press = delegate(string key) {
                    OnController(new ButtonEvent { Button = key, Down = true, Time = now, Device = "titulo-fixture" });
                    OnController(new ButtonEvent { Button = key, Down = false, Time = now.AddMilliseconds(20), Device = "titulo-fixture" });
                    now = now.AddMilliseconds(80);
                };
                string uri = lastSteamUri;
                for (int i = 1; i <= SlidingTitleOrder.Length; i++)
                {
                    press("R1"); int expected = SlidingTitleOrder[i % SlidingTitleOrder.Length];
                    check(panel == expected && Keyboard.FocusedElement == titleNext, "R1 HID: painel e foco " + expected);
                    check(!string.IsNullOrWhiteSpace(((TextBlock)MotionPart("PanelTitle")).Text),
                        "titulo existente presente no painel " + expected);
                }
                check(lastSteamUri == uri, "ciclo de paineis nao abre a Biblioteca nem despacha Steam");
                for (int i = 1; i <= SlidingTitleOrder.Length; i++)
                {
                    press("L1");
                    int expected = SlidingTitleOrder[(SlidingTitleOrder.Length - i) % SlidingTitleOrder.Length];
                    check(panel == expected && NavigationHasFocus(), "L1 HID: painel " + expected);
                }
                titleNext.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check(panel == 6, "clique na seta abre Som");
                Navigate("BAIXO"); check((string)((Control)Keyboard.FocusedElement).Tag == "output", "baixo do cabecalho entra na saida de som");
                Navigate("ESQUERDA"); check(Keyboard.FocusedElement == titleNext, "esquerda da primeira coluna volta a seta");
                Navigate("ESQUERDA"); check(panel == 0, "esquerda na seta volta um painel");
                Navigate("DIREITA"); check(panel == 6, "direita na seta avanca um painel");
                Activate(); check(panel == AmdPanel, "X na seta avanca para o proximo painel");
                SetPanel(6); FocusNavigation(6);
                ControllerRail(); check(Keyboard.FocusedElement == titleNext, "atalho de navegacao foca a seta visivel");
                press("TOUCHPAD"); check(!NavigationHasFocus(), "touchpad do cabecalho entra no painel");
                SetPanel(0); FocusHome("utility-programs"); Navigate("ESQUERDA");
                check(FocusedTag() == "utility-library", "esquerda entre atalhos continua no grid");
                Navigate("ESQUERDA"); check(Keyboard.FocusedElement == titleNext, "borda do grid sobe a seta");
                FocusHome("utility-audio"); Activate();
                check(panel == 6 && returnToHome, "Som pelo Acesso rapido preserva origem");
                Back(); check(panel == 0 && FocusedTag() == "utility-audio", "circulo volta ao atalho de Som");
                FocusHome("utility-programs"); Activate(); FocusNavigation(0);
                check(panel == ProgramsPanel && !ProgramsTyping(), "Programas no cabecalho nao recebe texto");
                OnKey(window, new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.E)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                check(panel == 6 && Keyboard.FocusedElement == titleNext, "E no cabecalho de Programas troca o painel");
                SetPanel(5); OpenSettingsPage("wifi");
                check(((TextBlock)MotionPart("PanelTitle")).Text == "Wi-Fi" && titleHeader.Children.Count == 2,
                    "subpagina Wi-Fi preserva o titulo e a mesma seta");
                Back(); check(panel == 5 && settingsPage == "main", "circulo da subpagina preserva a volta as Configuracoes");
                foreach (var size in new[] { new Size(1920, 1080), new Size(1280, 720), new Size(3840, 2160) })
                {
                    FitScene(size);
                    foreach (int index in SlidingTitleOrder)
                    {
                        SetPanel(index); window.UpdateLayout();
                        var headerBox = SceneBox(titleHeader); var bodyBox = SceneBox(MotionPart("BodyScroll"));
                        check(headerBox.Bottom <= bodyBox.Top + .5 && Math.Abs(MotionPart("Sidebar").ActualWidth - 480) < .5,
                            "cabecalho sem sobreposicao: " + index + " em " + size.Width + "x" + size.Height);
                    }
                }
                FitScene(new Size(1920, 1080));
                foreach (int index in new[] { 0, 6, AmdTuningPanel })
                {
                    SetPanel(index); FocusNavigation(index); window.UpdateLayout();
                    SaveSceneRender("titulo-deslizante-" + index, 1920, 1080);
                }
                SetPanel(0); motionReduced = false; motionSystemOverride = false; motionTesting = true;
                ChangePanel(-1); PumpMotion(60);
                check(heightPresenting && HeightSurface.RailWidth == 0 && HeightSurface.PanelLeft == 0,
                    "animacao de altura desenha so o painel, sem trilho fantasma");
                PumpMotion(300); check(!heightPresenting && panel == 4, "altura e transicao terminam em Energia");
                motionReduced = true; ChangePanel(1);
                check(panel == 0 && motionClocks.Count == 0, "movimento reduzido resolve a troca sem clocks");
                returnToHome = false; FocusNavigation(0); Back();
                check(!window.IsVisible, "circulo no cabecalho fecha a barra");
                File.WriteAllText(Path.Combine(Output, "titulo-deslizante-verificacao.txt"),
                    "Titulo deslizante: " + checks.Count + " verificacoes aprovadas.\r\n" + string.Join("\r\n", checks) +
                    "\r\nFixtures: sem comandos reais, sem HID; aprovacao visual e teste fisico pendentes.\r\n", Encoding.UTF8);
            }
            finally
            {
                SettleMotion(); slidingTitleTesting = false; SetSlidingTitleLayout(false);
                motionReduced = oldReduced; controllerFocus = oldFocus; activationMode = oldMode;
            }
        }
    }
}

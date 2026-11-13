// Navegacao do DualSense: somente eventos HID, sem simular teclado/mouse nem escrever no controle.
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using XGBP.Input;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        static readonly string[] PadDirections = { "CIMA", "BAIXO", "ESQUERDA", "DIREITA" };
        Func<DateTime> controllerNow = delegate { return DateTime.Now; };
#pragma warning disable 0649 // gancho de teste: atribuido somente na build XGB_VERIFY
        Func<bool> controllerFocus;
#pragma warning restore 0649
        DateTime controllerInputSince = DateTime.MinValue, nextScroll = DateTime.MaxValue;
        string scrollButton;
        // Separado de held: abrir/fechar/perder foco limpa a navegacao, mas nao solta fisicamente o PS.
        readonly HashSet<string> guideDownDevices = new HashSet<string>(StringComparer.Ordinal);
        // Ciclo do PS: do primeiro aperto, em qualquer dispositivo, ate a ultima soltura (ADR 0042).
        const int GuideBounceMs = 30, GuideLongMs = 600, GuideMinimumMs = 30, GuideDecisionMs = 40;
        DateTime guideCycleStart, guideCycleEnded = DateTime.MinValue, guideToggleAt;
        // Botoes apertados agora (exceto PS e analogicos), em qualquer dispositivo: PS com um deles e atalho do DSX.
        readonly Dictionary<string, DateTime> buttonsHeldAt = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        readonly Dictionary<string, DateTime> guidePressedAt = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        bool guideCycleActive, guideCycleChord, guideCycleToggled, guideCycleSteam, guideCyclePhysical, guideCycleBounce, guideVirtualWarned;
        // O PS que abre esta barra tambem chegou a Steam: so entao o ciclo do Steam Input e necessario (ADR 0041).
        bool steamGuidePending, steamGuideOpening;

        // Botao ainda apertado quando o foco iria para um jogo (o X que escolheu, o circulo que fechou): o jogo o leria
        // como um aperto novo (12:43:00 e 12:43:07, ADR 0068). A entrega espera a soltura, no maximo 500 ms.
        const int ReleaseWaitMs = 500;
        DateTime releaseWaitUntil = DateTime.MinValue;
        void BeginReleaseWait() { releaseWaitUntil = controllerNow().AddMilliseconds(ReleaseWaitMs); }
        bool ButtonsStillHeld() { return buttonsHeldAt.Count > 0 && controllerNow() < releaseWaitUntil; }
        void ButtonsReleased()
        {
            if (gameTransition != null) TickGameTransition();
            if (gameLaunchRoute != null) TickGameLaunchRoute();
            TrySteamNavigation();
            TickReleaseWait();
        }

        bool ControllerFocused() { return controllerFocus != null ? controllerFocus() : GetForegroundWindow() == handle; }
        bool PadHeld(string key) { bool down; return held.TryGetValue(key, out down) && down; }
        string PadDirection(string prefix, bool digital)
        {
            // O direcional tem prioridade; soltar uma fonte nao solta a outra que continua inclinada.
            if (digital) foreach (string direction in PadDirections) if (PadHeld(direction)) return direction;
            foreach (string direction in PadDirections) if (PadHeld(prefix + direction)) return direction;
            return null;
        }
        static bool DirectionEvent(string button)
        {
            return Array.IndexOf(PadDirections, button) >= 0 || button.StartsWith("LS_", StringComparison.Ordinal) || button.StartsWith("RS_", StringComparison.Ordinal);
        }
        void OnController(ButtonEvent e)
        {
            if (e == null || e.Button == null) return;
            if (HandleResumeGuide(e)) return;
            if (e.Button == "PS" && !e.Down) { ReleaseGuide(e); return; }
            // PS + outro botao e atalho do DSX (PS como modificador): o toque nao abre a barra. Os analogicos nao contam.
            if (e.Button != "PS" && !e.Button.StartsWith("LS_", StringComparison.Ordinal) && !e.Button.StartsWith("RS_", StringComparison.Ordinal))
            {
                if (!e.Down) { if (buttonsHeldAt.Remove(e.Button) && buttonsHeldAt.Count == 0) ButtonsReleased(); }
                else if (e.Gesture != "SEGURADO") { buttonsHeldAt[e.Button] = e.Time; if (guideCycleActive) guideCycleChord = true; }
            }
            if (e.Time < controllerInputSince) return;
            if ((controllerNow() - e.Time).TotalSeconds > 1) { ClearHeld(); return; }
            // Teclado virtual visivel com a barra fechada: o controle e dele, sem foco (ADR 0052). O PS segue para a barra.
            if (e.Button != "PS" && KeyboardController(e)) return;
            if (!ActivationAllowed()) return;
            if (e.Button == "PS") { if (GuideActivationAllowed()) PressGuide(e); return; }
            if (e.Button == "MUTE" || e.Button == "CREATE") return;
            if (!ActivationAllowed() || !window.IsVisible || motionClosing) return;
            // A Steam ainda tem o controle (ADR 0043): so fechar; navegar agora agiria tambem no Big Picture.
            if (steamGate) { if (e.Down && e.Button == "CIRCULO") Dismiss(); return; }
            // Sem primeiro plano a entrada fica com a Steam/jogo; um aperto pode pedir o foco de novo (WindowFocus.cs).
            if (!ControllerFocused() && (!e.Down || !RetakeControllerFocus(e.Button))) return;
            bool was = PadHeld(e.Button); held[e.Button] = e.Down;
            if (DirectionEvent(e.Button))
            {
                string direction = PadDirection("LS_", true), scroll = PadDirection("RS_", false);
                if (direction != repeatButton)
                {
                    repeatButton = direction;
                    if (direction != null) { Navigate(direction); nextRepeat = controllerNow().AddMilliseconds(420); }
                }
                if (scroll != scrollButton)
                {
                    scrollButton = scroll;
                    if (scroll != null) { ScrollController(scroll); nextScroll = controllerNow().AddMilliseconds(250); }
                }
                if (e.Down && !was) WriteLog("entrada: " + e.Button);
                return;
            }
            if (!e.Down) { if (e.Button == "X" && holdSource == "pad") ReleaseHold(); return; }
            if (was) return;
            WriteLog("entrada: " + e.Button);
            if (ProgramsButton(e.Button)) return; // teclado da pesquisa de programas (ADR 0096)
            if (e.Button == "X") { if (!BeginHold("pad")) ActivateFromController(); }
            else if (e.Button == "CIRCULO") Back();
            else if (e.Button == "L1") ChangePanel(-1);
            else if (e.Button == "R1") ChangePanel(1);
            else if (e.Button == "L2" || e.Button == "R2") { if (!ProgramsPage(e.Button == "L2" ? -1 : 1)) PageController(e.Button == "L2" ? -1 : 1); }
            else if (e.Button == "TRIANGULO") ControllerPanel(0);
            else if (e.Button == "OPTIONS") ControllerPanel(5);
            else if (e.Button == "QUADRADO" || e.Button == "R3") ControllerRail();
            else if (e.Button == "L3") { if (flyout == null) FocusFirstAction(); }
            else if (e.Button == "TOUCHPAD")
            {
                if (flyout != null) Back();
                else if (NavigationHasFocus()) FocusFirstAction();
                else ControllerRail();
            }
        }
        // So o controle fisico (Bluetooth) recebeu o PS: a Steam nao o viu, e a barra abre ao soltar um toque curto,
        // como o botao Xbox. O virtual do DSX tambem recebeu: a Steam reage a soltura, entao abre ja no aperto.
        void PressGuide(ButtonEvent e)
        {
            if (e.Gesture == "SEGURADO") return;
            string device = e.Device ?? "";
            if (!guideDownDevices.Add(device)) return;
            UpdateSteamInputGuide();
            guidePressedAt[device] = e.Time;
            if (!guideCycleActive)
            {
                guideCycleActive = true; guideCycleStart = e.Time;
                guideCycleToggled = guideCycleSteam = guideCyclePhysical = false;
                // Botao ja apertado antes do PS (Quadrado 7 ms antes, no Win+Tab do DSX as 14:35:23): atalho.
                guideCycleChord = false;
                foreach (var held in buttonsHeldAt.Values) if ((e.Time - held).TotalSeconds < 5) guideCycleChord = true;
                // Repique do contato: aperto de 4 ms, 2 ms depois da soltura, tambem no controle fisico (13:35:54).
                double gap = (e.Time - guideCycleEnded).TotalMilliseconds;
                guideCycleBounce = gap >= 0 && gap < GuideBounceMs;
                if (guideCycleBounce) WriteLog("entrada: PS repique ignorado | " + device);
            }
            if (device.EndsWith("-BT", StringComparison.Ordinal)) guideCyclePhysical = true;
            else if (!guideCycleSteam)
            {
                // A Steam ve este PS: decidir em 40 ms, antes de ela reagir a soltura, mas depois do botao de um atalho
                // (que chegou 5 a 12 ms depois do PS no teste das 14:35).
                guideCycleSteam = true; guideToggleAt = e.Time.AddMilliseconds(GuideDecisionMs);
            }
            if (guideCycleSteam && guideCyclePhysical && !guideVirtualWarned)
            {
                guideVirtualWarned = true;
                WriteLog("aviso: o PS chegou tambem pelo controle virtual; a Steam o recebe. Silencie o PS sozinho no DSX (ADR 0042)");
            }
        }
        // Decisao do PS visto pela Steam (TickController, a cada 40 ms).
        void TickGuide()
        {
            if (!guideCycleActive || !guideCycleSteam || guideCycleToggled || guideCycleBounce || guideCycleChord || controllerNow() < guideToggleAt) return;
            if (!GuideActivationAllowed()) return;
            guideCycleToggled = true; ToggleByGuide(true);
        }
        void ReleaseGuide(ButtonEvent e)
        {
            string device = e.Device ?? "";
            bool wasDown = guideDownDevices.Remove(device);
            UpdateSteamInputGuide();
            WriteLog("entrada: PS solto | " + e.DurationMs + " ms | " + device);
            // O DSX solta o PS virtual em poucos ms quando reconhece um atalho, com o fisico ainda apertado.
            DateTime pressed;
            if (wasDown && guideCycleActive && !device.EndsWith("-BT", StringComparison.Ordinal) && guidePressedAt.TryGetValue(device, out pressed) &&
                (e.Time - pressed).TotalMilliseconds < GuideMinimumMs)
                foreach (string other in guideDownDevices) if (other.EndsWith("-BT", StringComparison.Ordinal)) guideCycleChord = true;
            if (guideDownDevices.Count > 0 || !guideCycleActive) return;
            guideCycleActive = false; guideCycleEnded = e.Time;
            TrySteamNavigation(); // A ultima soltura conclui o retorno sem esperar o timer.
            int ms = (int)(e.Time - guideCycleStart).TotalMilliseconds;
            if (guideCycleBounce || guideCycleToggled) return;
            if (guideCycleChord) { WriteLog("entrada: PS em atalho | " + ms + " ms; sem abrir"); return; }
            // Toques de 1 a 28 ms no virtual aparecem ao fim dos atalhos do DSX; nao sao um toque da pessoa.
            if (ms < GuideMinimumMs) { WriteLog("entrada: PS curto demais | " + ms + " ms; sem abrir"); return; }
            if (ms >= GuideLongMs) { WriteLog("entrada: PS longo | " + ms + " ms; sem abrir"); return; }
            if ((controllerNow() - e.Time).TotalSeconds > 1 || !GuideActivationAllowed()) return;
            ToggleByGuide(guideCycleSteam);
        }
        void ToggleByGuide(bool steamSaw)
        {
            steamGuidePending = steamSaw;
            try { RequestActivation("PS"); } finally { steamGuidePending = false; }
        }
        // Sem soltura confirmada (dispositivo saiu ou modo mudou): o ciclo termina sem abrir nem fechar.
        void GuideDeviceLost(string device)
        {
            if (guideDownDevices.Contains(device)) CancelSteamNavigation(); // Desconexao nao prova soltura.
            if (steamInputAttempt != null && guideDownDevices.Contains(device)) steamInputAttempt.Cancelled = true;
            guideDownDevices.Remove(device);
            UpdateSteamInputGuide();
            if (guideDownDevices.Count == 0) guideCycleActive = false;
        }
        void ClearGuide() { CancelSteamInputAttempt(); guideDownDevices.Clear(); guidePressedAt.Clear(); buttonsHeldAt.Clear(); guideCycleActive = false; guideCycleEnded = DateTime.MinValue; }
        void TickController()
        {
            CheckExitGamingMode();
            TickGuide();
            TickSteamGate();
            TickReleaseWait();
            KeyboardTick();
            if (!ActivationAllowed() || !window.IsVisible || !ControllerFocused() || motionClosing || steamGate) return;
            DateTime now = controllerNow();
            if (repeatButton != null && now >= nextRepeat) { Navigate(repeatButton); nextRepeat = now.AddMilliseconds(130); }
            if (scrollButton != null && now >= nextScroll) { ScrollController(scrollButton); nextScroll = now.AddMilliseconds(80); }
        }
        void ControllerPanel(int index)
        {
            CancelHold(); ClearControllerMenus(); returnToHome = false;
            if (panel != index) SetPanel(index);
            FocusFirstAction();
        }
        void ClearControllerMenus()
        {
            GamesBack(); AppCloseBack();
            // Sair da subpagina nao executa nenhuma das escolhas pendentes.
            for (int i = 0; i < 3 && ConnectionsBack(); i++) { }
            AudioBack(); PerfBack();
        }
        void ControllerRail()
        {
            CancelHold(); ClearControllerMenus(); FocusNavigation(RailIndex(panel));
        }
        static bool InsideControllerScope(DependencyObject target, DependencyObject scope)
        {
            for (var node = target; node != null; node = VisualTreeHelper.GetParent(node)) if (node == scope) return true;
            return false;
        }
        void PageController(int direction)
        {
            var scope = actions.FindAll(c => c.IsEnabled && c.IsVisible && (flyout == null || InsideControllerScope(c, flyout)));
            if (scope.Count == 0) return;
            int before = scope.IndexOf(Keyboard.FocusedElement as Control);
            int after = before < 0 ? (direction > 0 ? 0 : scope.Count - 1) : Math.Max(0, Math.Min(scope.Count - 1, before + direction * 5));
            if (after != before) { CancelHold(); scope[after].Focus(); scope[after].BringIntoView(); if (!SoundlessMedia(scope[after])) Sound("mover"); }
        }
        static ScrollViewer ControllerScrollIn(DependencyObject node, bool horizontal)
        {
            var scroll = node as ScrollViewer;
            if (scroll != null && (horizontal ? scroll.ScrollableWidth : scroll.ScrollableHeight) > 0) return scroll;
            for (int i = 0; node != null && i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                var found = ControllerScrollIn(VisualTreeHelper.GetChild(node, i), horizontal); if (found != null) return found;
            }
            return null;
        }
        void ScrollController(string direction)
        {
            bool horizontal = direction == "ESQUERDA" || direction == "DIREITA";
            ScrollViewer scroll = null;
            for (var node = Keyboard.FocusedElement as DependencyObject; node != null; node = VisualTreeHelper.GetParent(node))
            {
                var candidate = node as ScrollViewer;
                if (candidate != null && (horizontal ? candidate.ScrollableWidth : candidate.ScrollableHeight) > 0) { scroll = candidate; break; }
                if (node == flyout) break;
            }
            if (scroll == null) scroll = ControllerScrollIn(flyout ?? window.FindName("BodyScroll") as DependencyObject, horizontal);
            if (scroll == null) return;
            RestartDescriptionForInput();
            double step = direction == "CIMA" || direction == "ESQUERDA" ? -64 : 64;
            if (horizontal) scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset + step);
            else scroll.ScrollToVerticalOffset(scroll.VerticalOffset + step);
        }
    }
}

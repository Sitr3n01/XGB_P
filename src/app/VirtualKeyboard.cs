// Teclado virtual (ADR 0052): janela propria por cima e sem foco (WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW) que manda as
// teclas ao app da frente pelo SendInput. O campo de texto continua com o foco, e no modo Xbox o shell nao esconde o app.
// O DualSense chega pelo HID da barra: com o teclado visivel e a barra fechada, o controle e do teclado.
// Abre pelo Acesso rapido da Home (a barra se oculta e devolve o foco ao app), pela bandeja, ou sozinho no modo Xbox quando
// o UI Automation acusa um campo editavel (lib/windows/TextFocus.cs). A barra tem prioridade: abri-la suspende o teclado,
// que volta quando ela devolve o foco ao mesmo app. O log nunca registra o texto digitado.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using XGBP.Desktop;
using XGBP.Games;
using XGBP.Input;

namespace XGBP.Design
{
    [DataContract]
    sealed class KeyboardPreferenceState
    {
        [DataMember(IsRequired = true)] public int Version = 1;
        [DataMember(IsRequired = true)] public bool AutoOpen = true;
        [DataMember(IsRequired = true)] public bool KeySounds = true;
        [DataMember(IsRequired = true)] public string Position = "auto";
        public KeyboardPreferenceState Copy() { return new KeyboardPreferenceState { AutoOpen = AutoOpen, KeySounds = KeySounds, Position = Position }; }
    }
    static class KeyboardSettings
    {
        public static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XGB_P", "settings", "keyboard.json");
        public static KeyboardPreferenceState Read(string path)
        {
            if (!File.Exists(path)) return new KeyboardPreferenceState();
            if (new FileInfo(path).Length > 4096) throw new InvalidDataException("Preferencia do teclado grande demais.");
            var state = GameFiles.Read<KeyboardPreferenceState>(path);
            if (state == null || state.Version != 1 || state.Position != "auto" && state.Position != "bottom" && state.Position != "top")
                throw new InvalidDataException("Preferencia do teclado invalida.");
            return state;
        }
        public static KeyboardPreferenceState Load()
        {
            try { return Read(FilePath); }
            catch (Exception ex)
            {
                Preview.WriteLog("teclado: preferencia invalida; tentando backup | " + ex.Message);
                try { return Read(FilePath + ".bak"); } catch (Exception) { return new KeyboardPreferenceState(); }
            }
        }
    }
    sealed class KeyboardTarget { public uint Pid; public string Path, Name; }

    sealed partial class Preview
    {
        const double KbUnit = 68, KbGap = 6, KbKeyHeight = 60, KbStripHeight = 38, KbRowGap = 6;
        const int KbHoldMs = 450, KbAutoHideMs = 400, KbPendingMs = 6000, KbResumeMs = 1800, KbNoticeMs = 3000;
        const string KbBlockedText = "Este app roda como administrador. Para digitar nele, ligue “Sempre executar como administrador” em Configurações.";
        static readonly string[] KbExcludedExes = { "dsx.exe", "textinputhost.exe", "lockapp.exe", "logonui.exe", "consent.exe", "xgbdesignpreview.exe", "xgb_p.exe" };
        static readonly KeyDef KbBackspaceKey = new KeyDef { Id = "backspace", Kind = KeyKind.Key, Vk = TextInput.Back, Repeat = true };
        static readonly KeyDef KbSpaceKey = new KeyDef { Id = "space", Kind = KeyKind.Key, Vk = TextInput.Space };
        static readonly KeyDef KbEnterKey = new KeyDef { Id = "enter", Kind = KeyKind.Key, Vk = TextInput.Return };
        static readonly KeyDef KbLeftKey = new KeyDef { Id = "left", Kind = KeyKind.Key, Vk = TextInput.Left, Repeat = true, Navigation = true };
        static readonly KeyDef KbRightKey = new KeyDef { Id = "right", Kind = KeyKind.Key, Vk = TextInput.Right, Repeat = true, Navigation = true };

        // Janela e desenho.
        Window kbWindow;
        IntPtr kbHandle;
        Grid kbRoot;
        Canvas kbKeys;
        TextBlock kbStatus;
        Border kbPopupView;
        ScaleTransform kbScale;
        TranslateTransform kbSlide;
        readonly List<List<Border>> kbViews = new List<List<Border>>();
        readonly List<List<TextBlock>> kbLabels = new List<List<TextBlock>>();
        int kbCloseVersion;
        bool kbDockTop;
        string kbDockOverride;
        double kbPlacedLeft, kbPlacedTop, kbPlacedScale = 1;

        // Estado.
        KeyboardPreferenceState kbPrefs = new KeyboardPreferenceState();
        KeyboardLayer kbLayer;
        string kbLayerName = "letters";
        int kbSymbolsPage = 1, kbRow, kbCol, kbSent;
        double kbAnchor = -1;
        bool kbVisible, kbSuspended, kbBlocked, kbPassword, kbCaps, kbCtrlHeld;
        string kbMode, kbTargetPath, kbTargetName, kbTargetIntegrity, kbDismissedKey, kbLastFocusLog, kbNotice, kbPendingReason;
        IntPtr kbTarget, kbPending;
        uint kbTargetPid;
        KeyModifiers kbOneShot;
        DateTime kbShiftAt = DateTime.MinValue, kbPressAt, kbNavNext, kbCaretNext, kbRepeatNext, kbNoticeUntil, kbPendingUntil, kbResumeUntil = DateTime.MinValue;
        DateTime kbAutoHideAt = DateTime.MaxValue, kbNextTargetCheck = DateTime.MinValue, kbNextWatcherCheck = DateTime.MinValue;
        readonly Dictionary<string, bool> kbHeld = new Dictionary<string, bool>();
        string kbNavDirection, kbCaretDirection, kbRepeatSource;
        KeyDef kbRepeatKey, kbPressKey, kbPopupKey;
        List<string> kbPopup;
        int kbPopupIndex;
        TextFocusInfo kbFocus;
        TextFocusWatcher kbWatcher;

        // Substituiveis: a verificacao registra os toques em vez de envia-los e usa janelas de mentira.
        Func<KeyStroke, int> kbSend = TextInput.Send;
        Func<IntPtr> kbForeground = GetForegroundWindow;
        Func<IntPtr, bool> kbWindowAlive = KbIsWindow;
        Func<IntPtr, KeyboardTarget> kbDescribe = KeyboardTargetOf;
        Func<uint, int?> kbIntegrity = ProcessIntegrity.Level;
        Func<int> kbOwnIntegrity = ProcessIntegrity.Current;
        Func<IntPtr, Rect> kbWorkArea = KbWorkAreaOf;
        Func<string, bool> kbSteamOwned = DesktopWindows.SteamOwned;
        Func<TextFocusWatcher> kbWatcherFactory;
        Action<KeyboardPreferenceState> kbWrite;

        // Steam Input enquanto o teclado esta sobre um app da Steam (Big Picture ou jogo).
        bool kbSteamHeld, kbSteamGate;
        DateTime kbSteamGateUntil;
        string kbSteamRestore;
        volatile int kbSteamEpoch;
        Func<string> kbSteamLayout = delegate { return SteamInput.LastLayout(SteamInput.ControllerTail()); };
        Action<Action> kbSteamRun;
        string kbSteamMarker = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XGB_P", "settings", "steam-input-teclado.txt");

        bool KbMotion { get { return !verify && !motionReduced && !SystemMotionOff && !disposed; } }

        void StartKeyboard()
        {
            if (verify)
            {
                kbSend = delegate(KeyStroke stroke) { return TextInput.Expected(stroke); };
                kbForeground = delegate { return IntPtr.Zero; };
                kbWindowAlive = delegate { return true; };
                kbDescribe = delegate(IntPtr hwnd) { return new KeyboardTarget { Pid = 1, Path = @"C:\Apps\app.exe", Name = "App" }; };
                kbIntegrity = delegate { return ProcessIntegrity.Medium; };
                kbOwnIntegrity = delegate { return ProcessIntegrity.Medium; };
                kbWorkArea = delegate { return new Rect(0, 0, 1920, 1080); };
                kbSteamOwned = delegate { return false; };
                kbSteamLayout = delegate { return null; };
                kbSteamRun = delegate(Action body) { body(); };
                kbSteamMarker = Path.Combine(Output, "steam-input-fixture", "steam-input-teclado.txt");
                kbWrite = delegate { };
                return;
            }
            kbPrefs = KeyboardSettings.Load();
            kbWrite = delegate(KeyboardPreferenceState state) { GameFiles.Write(KeyboardSettings.FilePath, state); };
            kbSteamRun = delegate(Action body) {
                if (!steamInputWork.Enqueue(delegate { try { body(); } catch (Exception ex) { WriteLog("teclado: steam input falhou | " + ex.Message); } }))
                {
                    kbSteamGate = true;
                    Feedback("A Steam está ocupada. Feche o teclado e tente novamente.", FeedbackKind.Attention);
                    WriteLog("teclado: fila Steam cheia; diario preservado");
                }
            };
            kbWatcherFactory = delegate {
                var watcher = new TextFocusWatcher(KeyboardExcludedPath);
                watcher.Changed += delegate(TextFocusInfo info) { Dispatch(delegate { OnTextFocus(info); }); };
                watcher.Log += delegate(string text) { WriteLog("teclado: detector | " + text); };
                watcher.Start();
                return watcher;
            };
            // Encerramento no meio de um teclado sobre a Steam: libera o layout (a proxima troca de foco escolhe pelo app).
            if (File.Exists(kbSteamMarker))
            {
                try
                {
                    if (steamInputAvailable()) steamInputLaunch(SteamInput.Release);
                    File.Delete(kbSteamMarker);
                    WriteLog("teclado: steam input liberado apos encerramento anterior");
                }
                catch (Exception ex) { WriteLog("teclado: falha ao liberar a Steam apos encerramento anterior | " + ex.Message); }
            }
            WriteLog("teclado: preferencias | auto=" + kbPrefs.AutoOpen + "; posicao=" + kbPrefs.Position + "; sons=" + kbPrefs.KeySounds);
        }

        static bool KeyboardExcludedPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return true;
            if (DesktopWindows.SteamOwned(path)) return true;
            return Array.IndexOf(KbExcludedExes, Path.GetFileName(path).ToLowerInvariant()) >= 0;
        }

        // ---------- Abrir, fechar, suspender ----------

        // Acesso rapido da Home: a barra se oculta e devolve o foco ao app de tras; o teclado aparece quando ele voltar a frente.
        void OpenKeyboardFromBar()
        {
            var target = previousWindow;
            if (target == null)
            {
                ActionSound("");
                Feedback("Nenhum app aberto para digitar. Abra um app e use o teclado de novo.", FeedbackKind.Attention);
                WriteLog("teclado: abrir pela barra sem app de tras");
                return;
            }
            KbBeginPending(target.Window.Handle, "barra");
            ActionSound(""); // o fechar da barra ja toca
            Dismiss();
        }
        // Bandeja (desktop): o clique da pessoa permite devolver o foco ao primeiro app de cima.
        void OpenKeyboardFromTray()
        {
            if (window.IsVisible && !motionClosing) { OpenKeyboardFromBar(); return; }
            WindowObservation app = null;
            try { app = frontApp(); } catch (Exception ex) { WriteLog("teclado: bandeja sem leitura de janelas | " + ex.Message); }
            if (app == null) { WriteLog("teclado: bandeja sem app para digitar"); return; }
            try { returnWindowFocus(app.Window); } catch (Exception ex) { WriteLog("teclado: foco pela bandeja falhou | " + ex.Message); }
            KbBeginPending(app.Window.Handle, "bandeja");
        }
        void KbBeginPending(IntPtr target, string reason)
        {
            kbPending = target; kbPendingReason = reason;
            kbPendingUntil = controllerNow().AddMilliseconds(KbPendingMs);
            WriteLog("teclado: abrir pela " + reason + " | aguardando o app voltar a frente | hwnd=" + target.ToInt64());
            EnsureKeyboardWatcher();
        }

        void ShowKeyboard(string mode, IntPtr target, string reason)
        {
            if (disposed || target == IntPtr.Zero) return;
            EnsureKeyboardWindow();
            bool wasVisible = kbVisible, resume = kbSuspended && target == kbTarget;
            kbMode = mode; kbSuspended = false; kbResumeUntil = DateTime.MinValue; kbAutoHideAt = DateTime.MaxValue;
            if (wasVisible && target != kbTarget) KbSteamEnd(false);
            SetKeyboardTarget(target);
            if (!wasVisible && !resume)
            {
                kbLayerName = "letters"; kbSymbolsPage = 1; kbOneShot = KeyModifiers.None; kbCaps = false; kbSent = 0; kbDockOverride = null; kbNotice = null;
                kbLayer = KeyboardLayouts.Letters(); KbLocate("char-h");
            }
            if (!wasVisible) KbResetInput(false);
            BuildKeyboardLayer();
            PlaceKeyboard();
            if (!wasVisible)
            {
                kbVisible = true;
                kbWindow.Show();
                if (verify) { kbWindow.Left = -30000; kbWindow.Top = -30000; }
                KbAnimateIn();
                if (!resume) Sound("lista");
            }
            KbSteamBegin();
            UpdateKeyboardStatus();
            WriteLog("teclado: " + (wasVisible ? "novo destino" : resume ? "retomado" : "aberto") + " | " + mode + " | " + reason + " | " + KbTargetDescribe());
            EnsureKeyboardWatcher();
        }
        void HideKeyboard(string reason, bool sound)
        {
            if (!kbVisible && !kbSuspended) return;
            bool shown = kbVisible;
            kbVisible = false; kbSuspended = false;
            KbResetInput(true);
            KbSteamEnd(kbForeground() == kbTarget);
            kbSteamRestore = null;
            if (shown) { if (sound) Sound("voltar"); KbAnimateOut(); }
            WriteLog("teclado: fechado | " + reason + " | " + kbSent + " teclas enviadas");
            kbTarget = IntPtr.Zero; kbAutoHideAt = DateTime.MaxValue; kbNotice = null;
            EnsureKeyboardWatcher();
        }
        void CloseKeyboardByUser()
        {
            // Fechado num campo: nao reabre sozinho ali ate o foco ir para outro elemento.
            if (kbMode == "auto" && kbFocus != null && kbFocus.Window == kbTarget && kbFocus.Key != null) kbDismissedKey = kbFocus.Key;
            HideKeyboard("fechado pelo usuario", true);
        }
        // Abrir a barra (PS, Win+G, bandeja) com o teclado visivel: ele some sem animacao e espera a barra fechar.
        void SuspendKeyboard()
        {
            kbPending = IntPtr.Zero;
            if (!kbVisible) return;
            kbVisible = false; kbSuspended = true; kbResumeUntil = DateTime.MinValue;
            KbResetInput(false);
            kbCloseVersion++;
            if (kbWindow != null) kbWindow.Hide();
            // A barra esta indo para a frente: so liberar, e a troca de foco seguinte escolhe o layout pelo app (ADR 0041).
            KbSteamEnd(false);
            WriteLog("teclado: suspenso pela barra");
        }
        void KeyboardModeChanged()
        {
            kbPending = IntPtr.Zero;
            HideKeyboard("o modo Xbox mudou", false);
            EnsureKeyboardWatcher();
        }
        void KbResetInput(bool all)
        {
            kbHeld.Clear(); kbNavDirection = kbCaretDirection = null; kbRepeatKey = null; kbRepeatSource = null;
            kbPressKey = null; kbPopup = null; kbPopupKey = null; kbCtrlHeld = false;
            if (all) { kbOneShot = KeyModifiers.None; kbCaps = false; }
            if (kbPopupView != null && kbKeys != null) { kbKeys.Children.Remove(kbPopupView); kbPopupView = null; }
        }

        void SetKeyboardTarget(IntPtr hwnd)
        {
            if (hwnd != kbTarget) kbSteamRestore = null;
            kbTarget = hwnd;
            KeyboardTarget info = null;
            try { info = kbDescribe(hwnd); } catch (Exception) { }
            kbTargetPid = info == null ? 0 : info.Pid;
            kbTargetPath = info == null ? null : info.Path;
            kbTargetName = info == null || string.IsNullOrWhiteSpace(info.Name) ? "app" : info.Name;
            int? level = null;
            try { level = kbIntegrity(kbTargetPid); } catch (Exception) { }
            int own = kbOwnIntegrity();
            // Token ilegivel: so presumir administrador quando o app e da Steam (que roda elevada nesta maquina, ADR 0040).
            kbBlocked = level.HasValue ? level.Value > own : own < ProcessIntegrity.High && kbTargetPath != null && kbSteamOwned(kbTargetPath);
            kbTargetIntegrity = ProcessIntegrity.Name(level);
            kbPassword = kbFocus != null && kbFocus.Window == hwnd && kbFocus.Password;
        }
        static KeyboardTarget KeyboardTargetOf(IntPtr hwnd)
        {
            uint pid; GetWindowThreadProcessId(hwnd, out pid);
            string path = DesktopWindows.ProcessPath(pid), name = null;
            if (path != null && !path.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
            {
                try { var version = FileVersionInfo.GetVersionInfo(path); name = string.IsNullOrWhiteSpace(version.FileDescription) ? version.ProductName : version.FileDescription; }
                catch (Exception) { }
                if (string.IsNullOrWhiteSpace(name)) name = Path.GetFileNameWithoutExtension(path);
            }
            if (string.IsNullOrWhiteSpace(name)) { var title = new StringBuilder(256); KbGetWindowText(hwnd, title, title.Capacity); name = title.ToString(); }
            return new KeyboardTarget { Pid = pid, Path = path, Name = name };
        }
        string KbTargetDescribe()
        {
            return "hwnd=" + kbTarget.ToInt64() + "; " + (kbTargetPath == null ? "pid " + kbTargetPid : Path.GetFileName(kbTargetPath)) +
                "; integridade=" + (kbTargetIntegrity ?? "?") + (kbBlocked ? " (envio bloqueado)" : "") + (kbPassword ? "; senha" : "");
        }
        bool KbOwnWindow(IntPtr hwnd) { return hwnd == handle || hwnd != IntPtr.Zero && hwnd == kbHandle; }
        bool KbUsableForeground(IntPtr hwnd) { return hwnd != IntPtr.Zero && !KbOwnWindow(hwnd) && kbWindowAlive(hwnd); }

        // ---------- Tick (40 ms, junto com o controle) ----------

        void KeyboardTick()
        {
            if (systemPowerBusy) return; DateTime now = controllerNow();
            if (now >= kbNextWatcherCheck) { kbNextWatcherCheck = now.AddSeconds(1); EnsureKeyboardWatcher(); }
            if (kbPending != IntPtr.Zero && !window.IsVisible && !motionClosing)
            {
                IntPtr foreground = kbForeground(), target = kbPending;
                if (foreground == target) { kbPending = IntPtr.Zero; ShowKeyboard("manual", target, kbPendingReason); }
                else if (now >= kbPendingUntil)
                {
                    kbPending = IntPtr.Zero;
                    if (KbUsableForeground(foreground)) ShowKeyboard("manual", foreground, kbPendingReason + "; outro app na frente");
                    else WriteLog("teclado: abertura cancelada; nenhum app na frente");
                }
            }
            if (kbSuspended && !window.IsVisible && !motionClosing)
            {
                if (kbResumeUntil == DateTime.MinValue) kbResumeUntil = now.AddMilliseconds(KbResumeMs);
                if (kbForeground() == kbTarget && kbWindowAlive(kbTarget))
                {
                    // Automatico so volta no modo Xbox; a mudanca de modo tambem limpa a suspensao (KeyboardModeChanged).
                    if (kbMode != "auto" || KbXboxModeConfirmed()) ShowKeyboard(kbMode, kbTarget, "a barra devolveu o foco");
                    else
                    {
                        kbSuspended = false; kbTarget = IntPtr.Zero; kbSteamRestore = null;
                        WriteLog("teclado: nao retomado; fora do modo Xbox");
                        EnsureKeyboardWatcher();
                    }
                }
                else if (now >= kbResumeUntil)
                {
                    kbSuspended = false; kbTarget = IntPtr.Zero; kbSteamRestore = null;
                    WriteLog("teclado: nao retomado; outro app ficou na frente");
                    EnsureKeyboardWatcher();
                }
            }
            if (!kbVisible) return;
            if (kbSteamGate && now >= kbSteamGateUntil) KbSteamReady(kbSteamEpoch, null, "limite");
            if (kbNotice != null && now >= kbNoticeUntil) { kbNotice = null; UpdateKeyboardStatus(); }
            if (kbMode == "auto" && now >= kbAutoHideAt) { HideKeyboard("o foco saiu do campo", false); return; }
            if (now >= kbNextTargetCheck) { kbNextTargetCheck = now.AddMilliseconds(250); KbCheckTarget(); if (!kbVisible) return; }
            if (kbPressKey != null && kbPopup == null && now >= kbPressAt.AddMilliseconds(KbHoldMs)) KbOpenPopup(kbPressKey);
            if (kbNavDirection != null && now >= kbNavNext) { KbNavigate(kbNavDirection); kbNavNext = now.AddMilliseconds(130); }
            if (kbCaretDirection != null && now >= kbCaretNext) { KbCaret(kbCaretDirection); kbCaretNext = now.AddMilliseconds(90); }
            if (kbRepeatKey != null && now >= kbRepeatNext) { KbActivate(kbRepeatKey, true); kbRepeatNext = now.AddMilliseconds(70); }
        }
        void KbCheckTarget()
        {
            if (!kbWindowAlive(kbTarget)) { HideKeyboard("o app de destino fechou", false); return; }
            IntPtr foreground = kbForeground();
            // Barra abrindo (Show suspende), propria janela ou nenhuma: nada a decidir agora.
            if (foreground == kbTarget || !KbUsableForeground(foreground)) return;
            if (kbMode == "auto") { KbScheduleAutoHide(); return; }
            KbRetarget(foreground, "acompanha o app da frente");
        }
        // Manual: o teclado digita no app da frente, inclusive depois de um Alt+Tab mandado por ele.
        void KbRetarget(IntPtr foreground, string reason)
        {
            KbSteamEnd(false); // o foco ja mudou: so liberar
            SetKeyboardTarget(foreground);
            KbSteamBegin();
            UpdateKeyboardStatus(); PlaceKeyboard();
            WriteLog("teclado: " + reason + " | " + KbTargetDescribe());
        }

        // ---------- Controle ----------

        bool KbHeld(string button) { bool down; return kbHeld.TryGetValue(button, out down) && down; }
        string KbDirection(string prefix, bool digital)
        {
            if (digital) foreach (string direction in PadDirections) if (KbHeld(direction)) return direction;
            foreach (string direction in PadDirections) if (KbHeld(prefix + direction)) return direction;
            return null;
        }
        // Com o teclado visivel e a barra fechada, todo botao (menos o PS) e do teclado. Devolve false para a barra tratar.
        bool KeyboardController(ButtonEvent e)
        {
            if (!kbVisible || window.IsVisible) return false;
            // PS apertado: o outro botao e um atalho do DSX (Win+Tab, Win+G...), nao uma tecla.
            if (guideCycleActive || guideDownDevices.Count > 0) return true;
            if (e.Gesture == "SEGURADO") return true;
            bool was = KbHeld(e.Button); kbHeld[e.Button] = e.Down;
            if (DirectionEvent(e.Button)) { if (!kbSteamGate) KbDirections(); return true; }
            if (e.Button == "R2") { if (e.Down != was) { kbCtrlHeld = e.Down; UpdateKeyVisuals(); } return true; }
            // A Steam ainda tem o controle (ADR 0043): so fechar; teclar agora tambem agiria no Big Picture ou no jogo.
            if (kbSteamGate) { if (e.Down && !was && e.Button == "CIRCULO") CloseKeyboardByUser(); return true; }
            if (!e.Down) { KbRelease(e.Button); return true; }
            if (was) return true;
            switch (e.Button)
            {
                case "X": KbPressFocused(); break;
                case "CIRCULO": if (kbPopup != null) KbClosePopup(); else CloseKeyboardByUser(); break;
                case "QUADRADO": KbPressRepeat(KbBackspaceKey, e.Button); break;
                case "TRIANGULO": KbActivate(KbSpaceKey, false); break;
                case "OPTIONS": KbActivate(KbEnterKey, false); break;
                case "L1": KbPressRepeat(KbLeftKey, e.Button); break;
                case "R1": KbPressRepeat(KbRightKey, e.Button); break;
                case "L2": KbLayer(kbLayer.Name == "letters" ? "symbols" : "letters"); break;
                case "L3": KbToggleModifier(KeyModifiers.Shift); break;
                case "R3": KbSwapDock(); break;
                case "MUTE": KbAction("dictation"); break;
                case "CREATE": KbAction("emoji"); break;
                case "TOUCHPAD": KbLayer("full"); break;
            }
            return true;
        }
        void KbDirections()
        {
            DateTime now = controllerNow();
            string navigation = KbDirection("LS_", true), caret = KbDirection("RS_", false);
            if (navigation != kbNavDirection)
            {
                kbNavDirection = navigation;
                if (navigation != null) { KbNavigate(navigation); kbNavNext = now.AddMilliseconds(420); }
            }
            if (caret != kbCaretDirection)
            {
                kbCaretDirection = caret;
                if (caret != null) { KbCaret(caret); kbCaretNext = now.AddMilliseconds(350); }
            }
        }
        void KbRelease(string button)
        {
            if (button == "X" && kbPressKey != null)
            {
                var key = kbPressKey; kbPressKey = null;
                if (kbPopup == null) KbActivate(key, false);
            }
            if (button == kbRepeatSource) { kbRepeatKey = null; kbRepeatSource = null; }
        }
        void KbPressFocused()
        {
            if (kbPopup != null) { KbCommitPopup(); return; }
            var key = KbFocusedKey();
            if (key == null) return;
            // Tecla com acentos: decide na soltura (toque) ou no segurar (lista de alternativas).
            if (key.Kind == KeyKind.Char && key.Alternates != null && key.Alternates.Length > 0) { kbPressKey = key; kbPressAt = controllerNow(); return; }
            if (key.Repeat) KbPressRepeat(key, "X"); else KbActivate(key, false);
        }
        void KbPressRepeat(KeyDef key, string source)
        {
            KbActivate(key, false);
            kbRepeatKey = key; kbRepeatSource = source; kbRepeatNext = controllerNow().AddMilliseconds(400);
        }
        // Clique ou toque na tecla (mouse do DSX, tela de toque): sem a lista de acentos.
        void KbPointer(KeyDef key)
        {
            int row, col;
            if (kbLayer != null && kbLayer.Locate(key.Id, out row, out col)) { kbRow = row; kbCol = col; kbAnchor = -1; }
            KbActivate(key, false);
        }
        KeyDef KbFocusedKey() { return kbLayer == null ? null : kbLayer.At(kbRow, kbCol); }
        bool KbLocate(string id)
        {
            int row, col;
            if (kbLayer == null || id == null || !kbLayer.Locate(id, out row, out col)) return false;
            kbRow = row; kbCol = col; kbAnchor = -1; return true;
        }
        void KbNavigate(string direction)
        {
            if (kbPopup != null)
            {
                if (direction == "ESQUERDA" || direction == "DIREITA")
                {
                    int next = Math.Max(0, Math.Min(kbPopup.Count - 1, kbPopupIndex + (direction == "DIREITA" ? 1 : -1)));
                    if (next != kbPopupIndex) { kbPopupIndex = next; RenderPopup(); if (kbPrefs.KeySounds) Sound("mover"); }
                }
                return;
            }
            kbPressKey = null; // andar cancela o segurar
            int row = kbRow, col = kbCol;
            if (KeyboardLayouts.Move(kbLayer, ref row, ref col, direction, ref kbAnchor))
            {
                kbRow = row; kbCol = col; UpdateKeyVisuals();
                if (kbPrefs.KeySounds) Sound("mover");
            }
        }
        void KbCaret(string direction)
        {
            ushort vk = direction == "CIMA" ? TextInput.Up : direction == "BAIXO" ? TextInput.Down : direction == "ESQUERDA" ? TextInput.Left : TextInput.Right;
            KbSendKey(new KeyDef { Id = "caret", Kind = KeyKind.Key, Vk = vk, Navigation = true });
        }

        // ---------- Teclas ----------

        void KbActivate(KeyDef key, bool repeat)
        {
            if (key == null) return;
            if (!repeat && kbPrefs.KeySounds) Sound("selecionar");
            switch (key.Kind)
            {
                case KeyKind.Char: KbTypeValue(KbCase(key.Text, key.Shifted)); break;
                case KeyKind.Key: KbSendKey(key); break;
                case KeyKind.Modifier: KbToggleModifier(key.Modifiers); break;
                case KeyKind.Layer: KbLayer(key.Command); break;
                case KeyKind.Action:
                    if (key.Command != null) KbAction(key.Command);
                    else { KbSend(KeyStroke.Key(key.Vk, key.Modifiers | (kbOneShot & KeyModifiers.Shift))); KbConsume(false); }
                    break;
            }
            UpdateKeyVisuals();
        }
        // Caps vale so para letras; a pontuacao muda so com o Shift, como no Windows. Shift com Caps volta a minuscula.
        string KbCase(string text, string shifted)
        {
            bool shift = (kbOneShot & KeyModifiers.Shift) != 0;
            if (shifted != null) return shift ? shifted : text;
            bool letter = text.Length == 1 && char.IsLetter(text[0]);
            bool upper = letter ? shift ^ kbCaps : shift;
            return upper ? text.ToUpper(new CultureInfo("pt-BR")) : text;
        }
        KeyModifiers KbComboModifiers() { return (kbOneShot & (KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Win)) | (kbCtrlHeld ? KeyModifiers.Ctrl : KeyModifiers.None); }
        // Texto vai como Unicode (sem tecla morta, independente do layout do app); com Ctrl/Alt/Win vira atalho de tecla virtual.
        void KbTypeValue(string value)
        {
            var combo = KbComboModifiers();
            var stroke = KeyStroke.Chars(value);
            ushort vk; KeyModifiers needed;
            if (combo != KeyModifiers.None && value.Length == 1 && TextInput.VkForChar(value[0], out vk, out needed)) stroke = KeyStroke.Key(vk, combo | needed);
            KbSend(stroke);
            KbConsume(false);
        }
        void KbSendKey(KeyDef key)
        {
            KbSend(KeyStroke.Key(key.Vk, KbComboModifiers() | (kbOneShot & KeyModifiers.Shift)));
            KbConsume(key.Navigation);
        }
        // Modificadores de um uso saem depois da tecla; nas setas/Home/End o Shift fica, para estender a selecao.
        void KbConsume(bool navigation)
        {
            kbOneShot = navigation ? kbOneShot & KeyModifiers.Shift : KeyModifiers.None;
            UpdateKeyVisuals();
        }
        void KbToggleModifier(KeyModifiers modifier)
        {
            if (modifier == KeyModifiers.Shift)
            {
                DateTime now = controllerNow();
                bool on = (kbOneShot & KeyModifiers.Shift) != 0;
                if (kbCaps) { kbCaps = false; kbOneShot &= ~KeyModifiers.Shift; }
                else if (on && (now - kbShiftAt).TotalMilliseconds <= 450) { kbCaps = true; kbOneShot &= ~KeyModifiers.Shift; }
                else if (on) kbOneShot &= ~KeyModifiers.Shift;
                else { kbOneShot |= KeyModifiers.Shift; kbShiftAt = now; }
            }
            // Win duas vezes, sem outro modificador: a tecla Windows sozinha.
            else if (modifier == KeyModifiers.Win && (kbOneShot & KeyModifiers.Win) != 0 && (kbOneShot & (KeyModifiers.Ctrl | KeyModifiers.Alt)) == 0)
            {
                kbOneShot &= ~KeyModifiers.Win;
                KbSend(KeyStroke.Key(TextInput.LWin));
            }
            else kbOneShot ^= modifier;
            UpdateKeyVisuals();
        }
        void KbAction(string command)
        {
            if (command == "close") { CloseKeyboardByUser(); return; }
            if (command == "settings")
            {
                Show(); SetPanel(5);
                var option = actions.Find(c => (string)c.Tag == "keyboard-auto");
                if (option != null) { option.Focus(); option.BringIntoView(); }
                return;
            }
            if (command == "dictation") KbSend(KeyStroke.Key(0x48, KeyModifiers.Win));        // Win+H: digitacao por voz do Windows
            else if (command == "emoji") KbSend(KeyStroke.Key(TextInput.OemPeriod, KeyModifiers.Win)); // Win+.: painel de emoji
            KbConsume(false);
        }
        void KbLayer(string command)
        {
            var before = KbFocusedKey();
            string id = before == null ? null : before.Id;
            double center = before == null ? 6.5 : before.Center;
            int row = kbRow;
            if (command == "symbols") { kbLayerName = "symbols"; kbSymbolsPage = 1; }
            else if (command == "letters") kbLayerName = "letters";
            else if (command == "page") kbSymbolsPage = kbSymbolsPage == 1 ? 2 : 1;
            else if (command == "full") kbLayerName = kbLayerName == "full" ? "letters" : "full";
            kbPopup = null; kbPressKey = null;
            BuildKeyboardLayer();
            // Mesma tecla se existir na outra camada (Esc, espaco, ⌫); senao a mesma linha, na mesma posicao.
            if (!KbLocate(id))
            {
                kbRow = Math.Max(0, Math.Min(kbLayer.Rows.Count - 1, row)); kbCol = 0; kbAnchor = -1;
                var keys = kbLayer.Rows[kbRow];
                for (int i = 0; i < keys.Count; i++) if (center >= keys[i].Left && center < keys[i].Left + keys[i].Width) kbCol = i;
            }
            UpdateKeyVisuals();
        }
        void BuildKeyboardLayer()
        {
            kbLayer = kbLayerName == "full" ? KeyboardLayouts.Full() : kbLayerName == "symbols" ? KeyboardLayouts.Symbols(kbSymbolsPage) : KeyboardLayouts.Letters();
            if (kbLayer.At(kbRow, kbCol) == null) { kbRow = 1; kbCol = 0; }
            RenderKeyboard();
        }

        // Toque de verdade no app da frente. Com o app em outro nivel de integridade ou fora da frente, nada sai.
        bool KbSend(KeyStroke stroke)
        {
            if (!KbTargetReady()) return false;
            int expected = TextInput.Expected(stroke), sent;
            try { sent = kbSend(stroke); }
            catch (Exception ex) { WriteLog("teclado: envio falhou | " + stroke.Describe() + " | " + ex.Message); return false; }
            kbSent++;
            if (sent < expected) WriteLog("teclado: envio parcial | " + stroke.Describe() + " | " + sent + " de " + expected);
            return sent > 0;
        }
        bool KbTargetReady()
        {
            if (kbSteamGate) return false;
            IntPtr foreground = kbForeground();
            if (foreground != kbTarget)
            {
                if (kbMode == "manual" && KbUsableForeground(foreground)) KbRetarget(foreground, "acompanha o app da frente");
                else { KbNotice("O app de destino não está na frente."); return false; }
            }
            if (kbBlocked) { KbNotice(KbBlockedText); return false; }
            return true;
        }
        void KbNotice(string text)
        {
            if (kbNotice != text) WriteLog("teclado: aviso | " + text);
            kbNotice = text; kbNoticeUntil = controllerNow().AddMilliseconds(KbNoticeMs);
            UpdateKeyboardStatus();
        }

        // ---------- Lista de acentos ----------

        void KbOpenPopup(KeyDef key)
        {
            kbPressKey = null;
            kbPopupKey = key; kbPopup = new List<string>(); kbPopupIndex = 0;
            foreach (string alternate in key.Alternates) kbPopup.Add(KbCase(alternate, null));
            RenderPopup(); UpdateKeyVisuals();
            if (kbPrefs.KeySounds) Sound("lista");
        }
        void KbClosePopup() { kbPopup = null; kbPopupKey = null; RenderPopup(); UpdateKeyVisuals(); }
        void KbCommitPopup()
        {
            if (kbPopup == null || kbPopup.Count == 0) return;
            string value = kbPopup[kbPopupIndex];
            KbClosePopup();
            if (kbPrefs.KeySounds) Sound("selecionar");
            KbTypeValue(value);
        }

        // ---------- Janela ----------

        void EnsureKeyboardWindow()
        {
            if (kbWindow != null) return;
            kbWindow = new Window {
                WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, Topmost = true,
                ShowInTaskbar = false, ShowActivated = false, ResizeMode = ResizeMode.NoResize, SizeToContent = SizeToContent.Manual,
                Title = "XGB_P · Teclado", FontFamily = new FontFamily("Segoe UI"), Focusable = false, UseLayoutRounding = true };
            kbWindow.SourceInitialized += delegate {
                kbHandle = new WindowInteropHelper(kbWindow).Handle;
                long style = KbGetWindowLong(kbHandle, -20).ToInt64();
                KbSetWindowLong(kbHandle, -20, new IntPtr(style | 0x08000000L | 0x80L)); // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
                var source = HwndSource.FromHwnd(kbHandle);
                if (source != null) source.AddHook(KeyboardWindowProc);
            };
            kbScale = new ScaleTransform(1, 1); kbSlide = new TranslateTransform();
            kbRoot = new Grid { Margin = new Thickness(18), LayoutTransform = kbScale, RenderTransform = kbSlide };
            var frame = new Border {
                Background = KbPanel, BorderBrush = KbSolid("#454545"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6), Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Direction = 270, Opacity = .4, Color = Colors.Black } };
            kbStatus = new TextBlock { FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, Width = 758, Height = 28,
                VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center, Padding = new Thickness(0, 5, 0, 0) };
            kbKeys = new Canvas { Width = KeyboardLayouts.RowUnits * KbUnit - KbGap, Height = KbRowTop(6) - KbRowGap };
            frame.Child = kbKeys; kbRoot.Children.Add(frame);
            kbWindow.Content = kbRoot;
        }
        // Clique ou toque nao ativa a janela: o foco fica no campo de texto do app.
        IntPtr KeyboardWindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == 0x0021) { handled = true; return new IntPtr(3); } // WM_MOUSEACTIVATE -> MA_NOACTIVATE
            return IntPtr.Zero;
        }
        static double KbRowTop(int row) { return row == 0 ? 0 : row == 1 ? 32 : 76 + (row - 2) * (KbKeyHeight + KbRowGap); }

        void RenderKeyboard()
        {
            if (kbKeys == null || kbLayer == null) return;
            kbKeys.Children.Clear(); kbViews.Clear(); kbLabels.Clear(); kbChrome.Clear(); kbPopupView = null;
            KbRenderHeader();
            for (int r = 0; r < kbLayer.Rows.Count; r++)
            {
                var views = new List<Border>(); var labels = new List<TextBlock>();
                foreach (var key in kbLayer.Rows[r])
                {
                    TextBlock label;
                    var view = KbKeyView(key, key.Width * KbUnit - KbGap, r == 0 ? 28 : r == 1 ? KbStripHeight : KbKeyHeight, out label);
                    Canvas.SetLeft(view, key.Left * KbUnit); Canvas.SetTop(view, KbRowTop(r));
                    kbKeys.Children.Add(view); views.Add(view); labels.Add(label);
                }
                kbViews.Add(views); kbLabels.Add(labels);
            }
            RenderPopup();
            UpdateKeyVisuals();
        }
        static string KbAccessibleName(KeyDef key)
        {
            switch (key.Id)
            {
                case "backspace": return "Apagar"; case "enter": return "Enter"; case "space": return "Espaço"; case "close": return "Fechar teclado";
                case "dictation": return "Ditado"; case "left": return "Seta para a esquerda"; case "right": return "Seta para a direita";
                case "up": return "Seta para cima"; case "down": return "Seta para baixo"; case "win": return "Tecla Windows";
                case "shift-left": case "shift-right": return "Shift";
                case "keyboard-settings": return "Configurações do teclado";
            }
            return key.Label ?? key.Id;
        }
        void UpdateKeyVisuals()
        {
            if (kbLayer == null || kbViews.Count != kbLayer.Rows.Count) return;
            for (int r = 0; r < kbLayer.Rows.Count; r++)
                for (int c = 0; c < kbLayer.Rows[r].Count && c < kbViews[r].Count; c++)
                {
                    var key = kbLayer.Rows[r][c];
                    bool focused = r == kbRow && c == kbCol && kbPopup == null;
                    bool active = key.Kind == KeyKind.Modifier && KbModifierActive(key.Modifiers);
                    KbPaintKey(key, kbViews[r][c], focused, active);
                    kbLabels[r][c].Text = KbLabel(key);
                }
            if (kbKeys != null) kbKeys.Opacity = kbSteamGate ? SteamGateOpacity : 1;
            UpdateKeyboardStatus();
        }
        bool KbModifierActive(KeyModifiers modifier)
        {
            if (modifier == KeyModifiers.Shift) return kbCaps || (kbOneShot & KeyModifiers.Shift) != 0;
            if (modifier == KeyModifiers.Ctrl && kbCtrlHeld) return true;
            return (kbOneShot & modifier) != 0;
        }
        string KbLabel(KeyDef key)
        {
            if (key.Kind == KeyKind.Char) return KbCase(key.Text, key.Shifted);
            if (key.Kind == KeyKind.Modifier && key.Modifiers == KeyModifiers.Shift) return kbCaps ? "⇪" : "⇧";
            if (key.Label == null) return key.Glyph ?? "";
            return key.Label;
        }
        void UpdateKeyboardStatus()
        {
            if (kbStatus == null) return;
            string text; bool warn = false;
            if (kbNotice != null && controllerNow() < kbNoticeUntil) { text = kbNotice; warn = true; }
            else if (kbSteamGate) text = "Aguardando a Steam soltar o controle…";
            else if (kbBlocked) { text = KbBlockedText; warn = true; }
            else text = "Digitando em " + (kbTargetName ?? "app") + (kbPassword ? " · campo de senha" : "");
            kbStatus.Text = text;
            kbStatus.Foreground = Brush(warn ? "FeedbackWarning" : "Muted");
            kbStatus.Visibility = warn || kbSteamGate || kbPassword ? Visibility.Visible : Visibility.Hidden;
            if (kbHeaderGrip != null) kbHeaderGrip.Visibility = kbStatus.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
        }
        void RenderPopup()
        {
            if (kbKeys == null) return;
            if (kbPopupView != null) { kbKeys.Children.Remove(kbPopupView); kbPopupView = null; }
            if (kbPopup == null || kbPopupKey == null) return;
            var line = new StackPanel { Orientation = Orientation.Horizontal };
            for (int i = 0; i < kbPopup.Count; i++)
            {
                bool selected = i == kbPopupIndex;
                int index = i;
                var cell = new Border { Width = 52, Height = 52, CornerRadius = new CornerRadius(5), Margin = new Thickness(3), BorderThickness = new Thickness(3), Padding = new Thickness(1),
                    BorderBrush = selected ? KbFocusEdge : Brushes.Transparent, Cursor = Cursors.Hand,
                    Child = new Border { Background = selected ? KbFocusFace : KbLetterFace, CornerRadius = new CornerRadius(2), BorderThickness = new Thickness(1), BorderBrush = KbKeyEdge,
                        Child = new TextBlock { Text = kbPopup[i], FontSize = 24, Foreground = KbInk, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } } };
                cell.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; kbPopupIndex = index; KbCommitPopup(); };
                line.Children.Add(cell);
            }
            kbPopupView = new Border { Background = KbPanel, BorderBrush = KbSolid("#555555"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(3), Child = line, Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Direction = 270, Opacity = .5, Color = Colors.Black } };
            double width = kbPopup.Count * 58 + 8, height = 66;
            double x = Math.Max(0, Math.Min(kbKeys.Width - width, kbPopupKey.Center * KbUnit - KbGap / 2 - width / 2));
            double y = KbRowTop(kbRow) - height - 4;
            if (y < 0) y = KbRowTop(kbRow) + KbKeyHeight + 4;
            Canvas.SetLeft(kbPopupView, x); Canvas.SetTop(kbPopupView, y); Panel.SetZIndex(kbPopupView, 10);
            kbKeys.Children.Add(kbPopupView);
        }

        // Centralizado no monitor do app, embaixo; em cima quando embaixo cobriria o campo (UIA). R3 troca durante o uso.
        void PlaceKeyboard()
        {
            if (kbWindow == null || kbRoot == null) return;
            Rect work = kbWorkArea(kbTarget);
            Matrix toDip = Matrix.Identity;
            var source = PresentationSource.FromVisual(window);
            if (source != null && source.CompositionTarget != null) toDip = source.CompositionTarget.TransformFromDevice;
            double left = work.Left * toDip.M11, top = work.Top * toDip.M22, width = work.Width * toDip.M11, height = work.Height * toDip.M22;
            kbScale.ScaleX = kbScale.ScaleY = 1;
            kbRoot.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double natural = kbRoot.DesiredSize.Width;
            double scale = natural > 0 ? Math.Max(.5, Math.Min(1, width / natural)) : 1;
            kbScale.ScaleX = kbScale.ScaleY = scale;
            kbRoot.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var size = kbRoot.DesiredSize;
            kbWindow.Width = size.Width; kbWindow.Height = size.Height;
            kbDockTop = KbDockTop(work, size.Height / toDip.M22);
            kbPlacedScale = scale;
            kbPlacedLeft = left + (width - size.Width) / 2;
            kbPlacedTop = kbDockTop ? top : top + height - size.Height;
            kbWindow.Left = kbPlacedLeft; kbWindow.Top = kbPlacedTop;
            if (verify) { kbWindow.Left = -30000; kbWindow.Top = -30000; }
        }
        bool KbDockTop(Rect work, double keyboardHeightPx)
        {
            string position = kbDockOverride ?? kbPrefs.Position;
            if (position == "top") return true;
            if (position == "bottom") return false;
            var field = kbFocus;
            if (field == null || field.Window != kbTarget || !field.HasBounds) return false;
            bool coveredBelow = field.Bottom > work.Bottom - keyboardHeightPx;
            bool clearAbove = field.Top >= work.Top + keyboardHeightPx;
            return coveredBelow && clearAbove;
        }
        void KbSwapDock()
        {
            kbDockOverride = kbDockTop ? "bottom" : "top";
            PlaceKeyboard();
            if (kbPrefs.KeySounds) Sound("mover");
            WriteLog("teclado: posicao | " + (kbDockTop ? "em cima" : "embaixo"));
        }
        static Rect KbWorkAreaOf(IntPtr hwnd)
        {
            IntPtr monitor = KbMonitorFromWindow(hwnd == IntPtr.Zero ? GetForegroundWindow() : hwnd, 2); // MONITOR_DEFAULTTONEAREST
            var info = new KbMonitorInfo { Size = Marshal.SizeOf(typeof(KbMonitorInfo)) };
            if (monitor == IntPtr.Zero || !KbGetMonitorInfo(monitor, ref info)) return new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
            return new Rect(info.Work.Left, info.Work.Top, info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top);
        }
        void KbAnimateIn()
        {
            kbCloseVersion++;
            kbRoot.BeginAnimation(UIElement.OpacityProperty, null); kbSlide.BeginAnimation(TranslateTransform.YProperty, null);
            if (!KbMotion) { kbRoot.Opacity = 1; kbSlide.Y = 0; return; }
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            kbRoot.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(MotionEntrance)) { EasingFunction = ease });
            kbSlide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(kbDockTop ? -24 : 24, 0, TimeSpan.FromMilliseconds(MotionEntrance)) { EasingFunction = ease });
        }
        void KbAnimateOut()
        {
            int version = ++kbCloseVersion;
            if (kbWindow == null) return;
            if (!KbMotion) { kbWindow.Hide(); return; }
            var fade = new DoubleAnimation(kbRoot.Opacity, 0, TimeSpan.FromMilliseconds(MotionNormal)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            fade.Completed += delegate {
                if (version != kbCloseVersion || kbVisible) return;
                kbWindow.Hide();
                kbRoot.BeginAnimation(UIElement.OpacityProperty, null); kbSlide.BeginAnimation(TranslateTransform.YProperty, null);
            };
            kbRoot.BeginAnimation(UIElement.OpacityProperty, fade);
            kbSlide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, kbDockTop ? -16 : 16, TimeSpan.FromMilliseconds(MotionNormal)));
        }

        // ---------- Steam Input sobre apps da Steam ----------

        // O layout do app (Big Picture 769 ou o do jogo) navegaria junto: fixa o de desktop (vazio) enquanto o teclado esta
        // aberto e devolve o do app ao fechar, forcando-o antes de liberar (a liberacao sozinha so vale na proxima troca de
        // foco, ADR 0041). Enquanto a Steam nao confirma, o teclado fica apagado e so o ○ fecha, como a barra (ADR 0043).
        void KbSteamBegin()
        {
            if (kbSteamHeld || kbBlocked || kbTargetPath == null || !kbSteamOwned(kbTargetPath)) return;
            bool available;
            try { available = steamInputAvailable(); } catch (Exception) { available = false; }
            if (!available) return;
            kbSteamHeld = true; kbSteamGate = true; kbSteamGateUntil = controllerNow().AddMilliseconds(SteamGateCapMs);
            int epoch = ++kbSteamEpoch;
            KbResetInput(false);
            try { Directory.CreateDirectory(Path.GetDirectoryName(kbSteamMarker)); File.WriteAllText(kbSteamMarker, DateTime.Now.ToString("o")); }
            catch (Exception ex) { WriteLog("teclado: marcador da Steam indisponivel | " + ex.Message); }
            UpdateKeyVisuals();
            var launch = steamInputLaunch; var log = steamInputLog; var layoutRead = kbSteamLayout; var notify = steamInputNotify;
            kbSteamRun(delegate {
                // A fila pode conter uma abertura ja encerrada; nunca forcar depois da sua limpeza.
                if (epoch != kbSteamEpoch || steamInputCancelled) return;
                string layout = null, reason;
                try { layout = layoutRead(); } catch (Exception) { }
                try
                {
                    var read = log();
                    if (epoch != kbSteamEpoch || steamInputCancelled || !steamInputAvailable()) return;
                    launch(SteamInput.ForceDesktop);
                    reason = KbSteamWait(read, SteamInput.Executed(SteamInput.ForceDesktop)) ? "Steam confirmou o desktop" : "sem confirmacao da Steam";
                }
                catch (Exception ex) { reason = "protocolo indisponivel: " + ex.Message; }
                string captured = layout;
                notify(delegate { KbSteamReady(epoch, captured, reason); });
            });
        }
        void KbSteamReady(int epoch, string layout, string reason)
        {
            if (epoch != kbSteamEpoch || !kbSteamHeld) return;
            if (layout != null && layout != SteamInput.DesktopAppId) kbSteamRestore = layout;
            // Sem a linha do Big Picture ainda no log (a Steam demora a registrar o foco), o layout dele e conhecido.
            if (kbSteamRestore == null && kbTargetPath != null && Path.GetFileName(kbTargetPath).Equals("steamwebhelper.exe", StringComparison.OrdinalIgnoreCase)) kbSteamRestore = "769";
            bool wasGate = kbSteamGate;
            kbSteamGate = false; KbResetInput(false);
            UpdateKeyVisuals();
            if (wasGate || reason != "limite")
                WriteLog("teclado: steam input | layout de desktop | " + reason + " | restaurar ao fechar: " + (kbSteamRestore ?? "nenhum"));
        }
        void KbSteamEnd(bool targetInFront)
        {
            if (!kbSteamHeld) return;
            kbSteamHeld = false; kbSteamGate = false;
            int epoch = ++kbSteamEpoch;
            string restore = targetInFront ? kbSteamRestore : null;
            var launch = steamInputLaunch; var log = steamInputLog; var notify = steamInputNotify; string marker = kbSteamMarker;
            kbSteamRun(delegate {
                string error = null; bool released = false;
                try
                {
                    var read = log();
                    if (!steamInputAvailable()) { released = true; return; }
                    if (restore != null && epoch == kbSteamEpoch && !steamInputCancelled) { launch(SteamInput.Force(restore)); KbSteamWait(read, SteamInput.Executed(SteamInput.Force(restore))); }
                    launch(SteamInput.Release);
                    released = KbSteamWait(read, SteamInput.Executed(SteamInput.Release));
                }
                catch (Exception ex) { error = ex.Message; }
                notify(delegate {
                    try { if (released && epoch == kbSteamEpoch && File.Exists(marker)) File.Delete(marker); } catch (Exception) { }
                    WriteLog("teclado: steam input | " + (restore != null ? "layout " + restore + " restaurado e " : "") + "liberado" + (error != null ? "; erro: " + error : ""));
                });
            });
        }
        bool KbSteamWait(Func<string> read, string line)
        {
            for (int waited = 0; ; waited += 50)
            {
                string text = read();
                if (text != null && text.Contains(line)) return true;
                if (waited >= SteamInputTimeoutMs) return false;
                steamInputWait(50);
            }
        }

        // ---------- Configuracoes ----------

        void AddKeyboardSettings()
        {
            Caption("TECLADO VIRTUAL");
            var auto = ToggleSetting(body, "Abrir sozinho no modo Xbox", kbPrefs.AutoOpen, "keyboard-auto", ToggleKeyboardAuto);
            auto.ToolTip = "No modo Xbox, o teclado abre quando um campo de texto recebe o foco. Steam e DSX ficam de fora; a Steam tem o teclado dela.";
            var position = ConnectionButton(body, "Posição do teclado", KbPositionText(kbPrefs.Position), "keyboard-position", CycleKeyboardPosition);
            position.ToolTip = "Automática: embaixo, ou em cima quando embaixo cobriria o campo. R3 troca durante o uso.";
            var sounds = ToggleSetting(body, "Sons das teclas", kbPrefs.KeySounds, "keyboard-sounds", ToggleKeyboardSounds);
            sounds.ToolTip = "Som curto ao teclar e ao andar entre as teclas. Abrir e fechar seguem os sons da interface.";
        }
        static string KbPositionText(string position) { return position == "top" ? "em cima" : position == "bottom" ? "embaixo" : "automática"; }
        void ToggleKeyboardAuto()
        {
            var next = kbPrefs.Copy(); next.AutoOpen = !next.AutoOpen;
            SaveKeyboardPreferences(next, "keyboard-auto", next.AutoOpen ? "Salvo. No modo Xbox, o teclado abre sozinho nos campos de texto." : "Salvo. O teclado só abre pelo Acesso rápido.");
        }
        void CycleKeyboardPosition()
        {
            var next = kbPrefs.Copy(); next.Position = next.Position == "auto" ? "bottom" : next.Position == "bottom" ? "top" : "auto";
            SaveKeyboardPreferences(next, "keyboard-position", "Salvo. Posição do teclado: " + KbPositionText(next.Position) + ".");
        }
        void ToggleKeyboardSounds()
        {
            var next = kbPrefs.Copy(); next.KeySounds = !next.KeySounds;
            SaveKeyboardPreferences(next, "keyboard-sounds", next.KeySounds ? "Salvo. Sons das teclas ligados." : "Salvo. Sons das teclas desligados.");
        }
        void SaveKeyboardPreferences(KeyboardPreferenceState next, string tag, string message)
        {
            try
            {
                kbWrite(next); kbPrefs = next;
                if (window.IsVisible && panel == 5) RebuildSettings(tag);
                Feedback(message);
                WriteLog("teclado: preferencias | auto=" + next.AutoOpen + "; posicao=" + next.Position + "; sons=" + next.KeySounds);
                EnsureKeyboardWatcher();
            }
            catch (Exception ex) { Feedback("Não foi possível salvar a preferência do teclado.", FeedbackKind.Error); WriteLog("teclado: preferencia falhou | " + ex.Message); }
        }

        void DisposeKeyboard()
        {
            kbPending = IntPtr.Zero;
            if (kbWatcher != null) { try { kbWatcher.Dispose(); } catch (Exception) { } kbWatcher = null; }
            if (kbSteamHeld || !verify && File.Exists(kbSteamMarker))
            {
                kbSteamHeld = false;
                try
                {
                    if (steamInputAvailable()) steamInputLaunch(SteamInput.Release);
                    if (File.Exists(kbSteamMarker)) File.Delete(kbSteamMarker);
                    WriteLog("teclado: steam input liberado ao encerrar");
                }
                catch (Exception ex) { WriteLog("teclado: falha ao liberar a Steam ao encerrar | " + ex.Message); }
            }
            if (kbWindow != null) { try { kbWindow.Close(); } catch (Exception) { } kbWindow = null; }
        }

#if XGB_VERIFY
        // Diagnostico somente leitura: le o campo em foco e a integridade do app da frente; nenhuma tecla e enviada.
        public void VerifyKeyboardLive()
        {
            var info = TextFocusWatcher.Query(KeyboardExcludedPath);
            int own = ProcessIntegrity.Current();
            int? front = null;
            if (info.WindowProcess != 0) front = ProcessIntegrity.Level(info.WindowProcess);
            var prefs = KeyboardSettings.Load();
            string report = "Campo em foco: " + info.Describe() + ".\r\nIntegridade: barra=" + ProcessIntegrity.Name(own) + "; app da frente=" + ProcessIntegrity.Name(front) +
                ".\r\nPreferencias: abrir sozinho=" + prefs.AutoOpen + "; posicao=" + prefs.Position + "; sons=" + prefs.KeySounds +
                ".\r\nPlano de Ctrl+C: " + TextInput.Describe(TextInput.Plan(KeyStroke.Key(0x43, KeyModifiers.Ctrl))) +
                ".\r\nDiagnostico somente leitura: sem ganchos, sem SendInput, sem abrir o teclado.\r\n";
            File.WriteAllText(Path.Combine(Output, "teclado-real.txt"), report, Encoding.UTF8);
        }
#endif

        [StructLayout(LayoutKind.Sequential)] struct KbRect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct KbMonitorInfo { public int Size; public KbRect Monitor, Work; public uint Flags; }
        [DllImport("user32.dll", EntryPoint = "IsWindow")] static extern bool KbIsWindow(IntPtr hwnd);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr KbGetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr KbSetWindowLong(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll", EntryPoint = "MonitorFromWindow")] static extern IntPtr KbMonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")] static extern bool KbGetMonitorInfo(IntPtr monitor, ref KbMonitorInfo info);
        [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)] static extern int KbGetWindowText(IntPtr hwnd, StringBuilder text, int count);
    }
}

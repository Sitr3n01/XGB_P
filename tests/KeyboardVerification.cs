// Teclado virtual (ADR 0052) com substitutos: nenhuma tecla e enviada, nenhum gancho e instalado e a Steam nao e
// chamada. Janelas, integridade, foco UIA, relogio e protocolos sao de mentira; o desenho e salvo em PNG.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XGBP.Desktop;
using XGBP.Games;
using XGBP.Input;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyKeyboard()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool ok, string why) { checks++; if (!ok) throw new Exception("Teclado: " + why); };
            var oldSend = kbSend; var oldForeground = kbForeground; var oldAlive = kbWindowAlive; var oldDescribe = kbDescribe;
            var oldIntegrity = kbIntegrity; var oldOwn = kbOwnIntegrity; var oldWork = kbWorkArea; var oldSteamOwned = kbSteamOwned;
            var oldLayout = kbSteamLayout; var oldRun = kbSteamRun; var oldAvailable = steamInputAvailable; var oldLaunch = steamInputLaunch;
            var oldLog = steamInputLog; var oldClock = controllerNow; var oldRead = activationRead; var oldPrefs = kbPrefs; var oldWrite = kbWrite;
            var strokes = new List<string>();
            IntPtr notepad = new IntPtr(0x7101), browser = new IntPtr(0x7102), bigPicture = new IntPtr(0x7103), admin = new IntPtr(0x7104);
            IntPtr foreground = notepad;
            var alive = new HashSet<IntPtr> { notepad, browser, bigPicture, admin };
            var targets = new Dictionary<IntPtr, KeyboardTarget> {
                { notepad, new KeyboardTarget { Pid = 11, Path = @"C:\Windows\notepad.exe", Name = "Bloco de Notas" } },
                { browser, new KeyboardTarget { Pid = 12, Path = @"C:\Program Files\Mozilla Firefox\firefox.exe", Name = "Firefox" } },
                { bigPicture, new KeyboardTarget { Pid = 13, Path = @"C:\Program Files (x86)\Steam\bin\cef\cef.win7x64\steamwebhelper.exe", Name = "Steam" } },
                { admin, new KeyboardTarget { Pid = 14, Path = @"C:\Tools\admin.exe", Name = "Ferramenta" } } };
            var levels = new Dictionary<uint, int?> { { 11, ProcessIntegrity.Medium }, { 12, ProcessIntegrity.Medium }, { 13, null }, { 14, ProcessIntegrity.High } };
            int own = ProcessIntegrity.Medium;
            DateTime now = DateTime.Now.AddMinutes(5);
            bool? mode = true;
            var launches = new List<string>(); var steamText = new StringBuilder(); var pendingSteam = new List<Action>();
            bool deferSteam = false; string layout = "769";
            var report = new StringBuilder();
            try
            {
                Hide(); HideKeyboard("verificacao", false); ClearGuide(); kbPending = IntPtr.Zero;
                kbPrefs = new KeyboardPreferenceState(); kbDismissedKey = null; kbFocus = null;
                kbSend = delegate(KeyStroke stroke) { strokes.Add(stroke.Text != null ? "T:" + stroke.Text : stroke.Describe()); return TextInput.Expected(stroke); };
                kbForeground = delegate { return foreground; };
                kbWindowAlive = delegate(IntPtr hwnd) { return alive.Contains(hwnd); };
                kbDescribe = delegate(IntPtr hwnd) { KeyboardTarget t; return targets.TryGetValue(hwnd, out t) ? t : null; };
                kbIntegrity = delegate(uint pid) { int? level; return levels.TryGetValue(pid, out level) ? level : null; };
                kbOwnIntegrity = delegate { return own; };
                kbSteamOwned = delegate(string path) { return path != null && path.Contains(@"\Steam\"); };
                kbSteamLayout = delegate { return layout; };
                kbSteamRun = delegate(Action body) { if (deferSteam) pendingSteam.Add(body); else body(); };
                steamInputAvailable = delegate { return true; };
                steamInputLaunch = delegate(string uri) { launches.Add(uri); steamText.AppendLine(SteamInput.Executed(uri)); };
                steamInputLog = delegate { return delegate { return steamText.ToString(); }; };
                controllerNow = delegate { return now; };
                activationRead = delegate { return mode; };
                kbWrite = delegate { };
                activationStarted = false; ActivationAllowed(); // marca a entrada no modo Xbox neste relogio
                Action<string, bool> input = delegate(string button, bool down) {
                    now = now.AddMilliseconds(30);
                    OnController(new ButtonEvent { Device = "DualSense#0-BT", Button = button, Down = down, Time = now });
                };
                Action<string> tap = delegate(string button) { input(button, true); input(button, false); };
                Action<int> tick = delegate(int ms) { for (int i = 0; i < ms; i += 40) { now = now.AddMilliseconds(40); KeyboardTick(); } };
                Func<string> last = delegate { return strokes.Count == 0 ? "" : strokes[strokes.Count - 1]; };
                Action<Action> runSteam = delegate(Action unused) { var copy = pendingSteam.ToArray(); pendingSteam.Clear(); foreach (var body in copy) body(); };

                // Camadas e navegacao (dados puros).
                var letters = KeyboardLayouts.Letters();
                KeyboardLayouts.Symbols(1); KeyboardLayouts.Symbols(2); var full = KeyboardLayouts.Full();
                int row, col; double anchor = -1;
                check(letters.Locate("char-ç", out row, out col) && row == 3 && letters.Rows.Count == 6 && full.Locate("f12", out row, out col), "ABNT2 com ç, cabecalho, atalhos e quatro linhas; Completo com F12");
                letters.Locate("char-h", out row, out col);
                Func<string, string> move = delegate(string direction) { KeyboardLayouts.Move(letters, ref row, ref col, direction, ref anchor); return letters.At(row, col).Id; };
                check(move("CIMA") == "char-y" && move("BAIXO") == "char-h" && move("BAIXO") == "char-n" && move("BAIXO") == "space", "cima/baixo mantem a coluna ate o espaco");
                check(move("CIMA") == "char-n" && move("CIMA") == "char-h" && move("CIMA") == "char-y" && move("CIMA") == "emoji", "do espaco volta a mesma coluna e sobe para o atalho mais proximo");
                letters.Locate("char-q", out row, out col); anchor = -1;
                check(move("ESQUERDA") == "esc" && move("ESQUERDA") == "esc", "esquerda para no Esc, sem dar a volta");

                // Plano do SendInput: Unicode para texto, tecla virtual e prefixo estendido para atalhos e setas.
                check(TextInput.Describe(TextInput.Plan(KeyStroke.Chars("ç"))) == "vU+00E7 ^U+00E7", "ç como Unicode");
                check(TextInput.Describe(TextInput.Plan(KeyStroke.Key(0x43, KeyModifiers.Ctrl))) == "v11 v43 ^43 ^11", "Ctrl+C em um lote, soltura invertida");
                check(TextInput.Describe(TextInput.Plan(KeyStroke.Key(TextInput.Left, KeyModifiers.Ctrl | KeyModifiers.Shift))) == "v11 v10 v25e ^25e ^10 ^11", "Ctrl+Shift+seta estendida");
                check(TextInput.Describe(TextInput.Plan(KeyStroke.Key(0x48, KeyModifiers.Win))) == "v5Be v48 ^48 ^5Be", "Win+H");
                check(TextInput.Plan(KeyStroke.Chars("\U0001F600")).Count == 4, "emoji em dois surrogates");

                // Acesso rapido: a barra se oculta e o teclado abre sobre o app de tras quando ele volta a frente.
                Show(); SetPanel(0);
                previousWindow = null; FocusHome("utility-keyboard"); Activate();
                check(window.IsVisible && kbPending == IntPtr.Zero && !kbVisible && ((System.Windows.Controls.TextBlock)window.FindName("Feedback")).Text.StartsWith("Nenhum app"), "sem app de tras a barra fica e avisa");
                int soundsBefore = soundLog.Count;
                previousWindow = new WindowObservation { Window = new DesktopWindow { Handle = notepad, ProcessId = 11, ProcessCreated = 1, Title = "Bloco de Notas" } };
                foreground = IntPtr.Zero;
                FocusHome("utility-keyboard"); Activate();
                check(!window.IsVisible && kbPending == notepad && !kbVisible, "atalho da Home oculta a barra e espera o app");
                check(soundLog.GetRange(soundsBefore, soundLog.Count - soundsBefore).Contains("fechar") && !soundLog.GetRange(soundsBefore, soundLog.Count - soundsBefore).Contains("selecionar"), "um efeito: o fechar da barra");
                tick(200); check(!kbVisible, "sem o app na frente o teclado espera");
                foreground = notepad; tick(40);
                check(kbVisible && kbMode == "manual" && kbTarget == notepad && kbWindow.IsVisible && soundLog[soundLog.Count - 1] == "lista", "teclado abre quando o app volta a frente");
                var toDip = PresentationSource.FromVisual(window).CompositionTarget.TransformFromDevice;
                check(Math.Abs(kbPlacedLeft + kbWindow.Width / 2 - 1920 * toDip.M11 / 2) < 1 && Math.Abs(kbPlacedTop + kbWindow.Height - 1080 * toDip.M22) < 1 && kbPlacedScale == 1,
                    "centralizado embaixo, sem reduzir em 1080p");
                kbWindow.UpdateLayout(); SaveKeyboardRender("teclado-letras");
                check(KbFocusedKey().Id == "char-h" && kbStatus.Text == "Digitando em Bloco de Notas", "comeca no h, com o app no status");

                // Teclas pelo DualSense.
                strokes.Clear();
                tap("X"); check(last() == "T:h", "✕ tecla o h");
                tap("DIREITA"); tap("X"); check(last() == "T:j", "direcional move e tecla");
                tap("L3"); tap("X"); tap("X"); check(strokes[strokes.Count - 2] == "T:J" && last() == "T:j", "Shift de um uso");
                tap("L3"); tap("L3"); tap("X"); tap("X"); check(kbCaps && last() == "T:J" && strokes[strokes.Count - 2] == "T:J", "dois toques no L3: Caps");
                kbWindow.UpdateLayout(); SaveKeyboardRender("teclado-caps");
                tap("L3"); check(!kbCaps, "L3 desliga o Caps");
                tap("QUADRADO"); check(last() == "0x08", "□ apaga");
                int before = strokes.Count; input("QUADRADO", true); tick(600); input("QUADRADO", false);
                check(strokes.Count - before >= 4 && last() == "0x08", "□ segurado repete");
                tap("TRIANGULO"); check(last() == "0x20", "△ espaco");
                tap("OPTIONS"); check(last() == "0x0D", "Options Enter");
                tap("L1"); check(last() == "0x25", "L1 seta esquerda");
                tap("R1"); check(last() == "0x27", "R1 seta direita");
                KbLocate("char-c"); input("R2", true); tap("X"); input("R2", false);
                check(last() == "Ctrl+0x43" && !kbCtrlHeld, "R2 segurado vira Ctrl");
                int beforeMute = strokes.Count; tap("MUTE");
                check(strokes.Count == beforeMute, "microfone fica com o DSX: o MUTE nao abre o ditado (ADR 0107)");
                KbLocate("dictation"); tap("X"); check(last() == "Win+0x48", "tecla de ditado da tela continua com Win+H");
                tap("CREATE"); check(last() == "Win+0xBE", "Create: painel de emoji");
                tap("L3"); input("RS_DIREITA", true); tick(400); input("RS_DIREITA", false);
                check(last() == "Shift+0x27" && (kbOneShot & KeyModifiers.Shift) != 0, "analogico direito move o cursor e o Shift seleciona");
                KbLocate("char-x"); tap("X"); check(last() == "T:X" && kbOneShot == KeyModifiers.None, "letra consome o Shift");
                // Acentos: segurar ✕ abre a lista; o toque curto tecla a letra.
                KbLocate("char-a"); tap("X"); check(last() == "T:a", "toque curto no a");
                input("X", true); tick(500); input("X", false);
                check(kbPopup != null && kbPopup[0] == "á" && last() == "T:a", "segurar ✕ abre a lista sem teclar");
                kbWindow.UpdateLayout(); SaveKeyboardRender("teclado-acentos");
                tap("DIREITA"); tap("X"); check(last() == "T:à" && kbPopup == null, "lista escolhe à");
                input("X", true); tick(500); input("X", false); tap("CIRCULO");
                check(kbPopup == null && kbVisible, "○ fecha so a lista");
                KbLocate("ctrl"); tap("X"); KbLocate("char-v"); tap("X"); check(last() == "Ctrl+0x56" && kbOneShot == KeyModifiers.None, "Ctrl na tela, de um uso");
                KbLocate("win"); tap("X"); tap("X"); check(last() == "0x5B", "Win duas vezes: tecla Windows");
                KbLocate("copy"); tap("X"); check(last() == "Ctrl+0x43", "faixa: copiar");
                KbLocate("emoji"); tap("X"); check(last() == "Win+0xBE", "faixa: emoji");
                tap("L2"); check(kbLayer.Name == "symbols1", "L2: simbolos");
                kbWindow.UpdateLayout(); SaveKeyboardRender("teclado-simbolos");
                KbLocate("page-left"); tap("X"); check(kbLayer.Name == "symbols2", "pagina 2 dos simbolos");
                kbWindow.UpdateLayout(); SaveKeyboardRender("teclado-simbolos-2");
                tap("L2"); check(kbLayer.Name == "letters", "L2 volta as letras");
                tap("TOUCHPAD"); check(kbLayer.Name == "full", "touchpad: Completo");
                kbWindow.UpdateLayout(); SaveKeyboardRender("teclado-completo");
                KbLocate("f4"); KbLocate("alt"); tap("X"); KbLocate("f4"); tap("X"); check(last() == "Alt+0x73", "Alt+F4 pelo Completo");
                tap("TOUCHPAD"); check(kbLayer.Name == "letters", "touchpad volta as letras");
                tap("R3"); check(kbDockTop && Math.Abs(kbPlacedTop) < 1, "R3 leva para cima");
                tap("R3"); check(!kbDockTop, "R3 volta para baixo");
                // PS com outro botao e atalho do DSX: nao vira tecla.
                before = strokes.Count;
                input("PS", true); input("QUADRADO", true); input("QUADRADO", false); input("PS", false);
                check(strokes.Count == before && kbVisible && !window.IsVisible, "PS + □ fica com o DSX");

                // O log nunca leva o texto digitado.
                string logPath = Path.Combine(Output, "preview.log"); long logStart = File.Exists(logPath) ? new FileInfo(logPath).Length : 0;
                KbLocate("char-y"); input("X", true); tick(500); input("X", false); tap("DIREITA"); tap("DIREITA"); tap("X");
                check(last() == "T:ÿ", "ÿ pela lista");
                check(!ReadLogFrom(logPath, logStart).Contains("ÿ"), "texto fora do log");

                // Destino: manual acompanha o app da frente; sem app na frente ou acima da integridade, nada sai.
                foreground = browser; KbLocate("char-b"); tap("X");
                check(kbTarget == browser && last() == "T:b" && kbStatus.Text == "Digitando em Firefox", "manual acompanha o app da frente");
                foreground = IntPtr.Zero; before = strokes.Count; tap("X");
                check(strokes.Count == before && kbStatus.Text == "O app de destino não está na frente.", "sem app na frente nao envia");
                foreground = admin; tap("X");
                check(strokes.Count == before && kbBlocked && kbStatus.Text == KbBlockedText, "app elevado acima da barra: aviso, sem envio");
                kbWindow.UpdateLayout(); SaveKeyboardRender("teclado-aviso-administrador");
                own = ProcessIntegrity.High; foreground = notepad; tap("X");
                check(kbTarget == notepad && !kbBlocked && last() == "T:b", "volta a digitar no app comum");

                // A barra tem prioridade: suspende o teclado e ele volta quando ela devolve o foco ao mesmo app.
                Show();
                check(!kbVisible && kbSuspended && !kbWindow.IsVisible, "abrir a barra suspende o teclado");
                before = strokes.Count;
                check(!KeyboardController(new ButtonEvent { Device = "DualSense#0-BT", Button = "X", Down = true, Time = now }) && strokes.Count == before, "barra aberta: o controle e dela");
                Hide(); tick(40);
                check(kbVisible && !kbSuspended && kbTarget == notepad, "barra fechada: teclado retomado no mesmo app");
                Show(); Hide(); foreground = browser; tick(2000);
                check(!kbVisible && !kbSuspended, "outro app na frente: nao retoma");

                // Abertura automatica no modo Xbox, pelo foco do UIA.
                Func<IntPtr, string, bool, bool, TextFocusInfo> focus = delegate(IntPtr hwnd, string key, bool editable, bool password) {
                    return new TextFocusInfo { Window = hwnd, WindowProcess = 12, ControlType = "Edit", Editable = editable, Password = password, Key = key,
                        HasBounds = true, Left = 100, Top = 900, Right = 600, Bottom = 940 };
                };
                foreground = browser; now = now.AddMilliseconds(KbModeSettleMs); // a entrada do inicio ja passou
                OnTextFocus(focus(browser, "campo-1", true, false));
                check(kbVisible && kbMode == "auto" && kbTarget == browser, "campo editavel abre sozinho no modo Xbox");
                check(kbDockTop, "campo embaixo: teclado em cima");
                tap("CIRCULO"); check(!kbVisible && kbDismissedKey == "campo-1", "○ fecha e lembra o campo");
                OnTextFocus(focus(browser, "campo-1", true, false)); check(!kbVisible, "nao reabre no campo fechado");
                OnTextFocus(focus(browser, "campo-2", true, true)); check(kbVisible && kbPassword && kbStatus.Text.EndsWith("campo de senha"), "outro campo reabre; senha no status");
                OnTextFocus(focus(browser, "botao", false, false)); tick(200); check(kbVisible, "espera antes de fechar");
                tick(300); check(!kbVisible, "foco fora do campo fecha o automatico");
                OnTextFocus(focus(notepad, "campo-3", true, false)); check(!kbVisible, "campo de outra janela que nao esta na frente: nao abre");
                var excluded = focus(browser, "campo-4", true, false); excluded.Excluded = true;
                OnTextFocus(excluded); check(!kbVisible, "app excluido nao abre");
                Show(); OnTextFocus(focus(browser, "campo-5", true, false)); check(!kbVisible, "barra aberta: nao abre");
                Hide();
                kbPrefs.AutoOpen = false; OnTextFocus(focus(browser, "campo-6", true, false)); check(!kbVisible, "opcao desligada: nao abre");
                kbPrefs.AutoOpen = true;
                mode = false; ActivationAllowed(); OnTextFocus(focus(browser, "campo-7", true, false)); check(!kbVisible, "desktop: nao abre sozinho");
                // Entrada no modo Xbox: o app do desktop segue na frente; o campo que ja tinha o foco nao abre, nem depois.
                mode = true; ActivationAllowed();
                OnTextFocus(focus(browser, "campo-7", true, false)); check(!kbVisible && kbDismissedKey == "campo-7", "entrada no modo Xbox: campo do desktop nao abre");
                tick(KbModeSettleMs); OnTextFocus(focus(browser, "campo-7", true, false)); check(!kbVisible, "o mesmo campo segue fechado depois da transicao");
                OnTextFocus(focus(browser, "campo-8", true, false)); check(kbVisible && kbMode == "auto", "campo novo no modo Xbox abre");
                mode = false; ActivationAllowed(); check(!kbVisible, "sair do modo Xbox fecha o teclado");
                // A API e lida na hora: modo trocado antes da notificacao, API incerta ou energia em andamento nao abrem.
                mode = true; ActivationAllowed(); tick(KbModeSettleMs);
                mode = false; OnTextFocus(focus(browser, "campo-9", true, false)); check(!kbVisible, "desktop antes da notificacao: nao abre");
                mode = null; OnTextFocus(focus(browser, "campo-10", true, false)); check(!kbVisible, "API incerta: nao abre");
                mode = true; ActivationAllowed(); tick(KbModeSettleMs);
                systemPowerBusy = true; OnTextFocus(focus(browser, "campo-11", true, false)); systemPowerBusy = false;
                check(!kbVisible, "energia em andamento: nao abre");
                // Retomada depois da barra confere o modo na hora.
                OnTextFocus(focus(browser, "campo-12", true, false)); check(kbVisible && kbMode == "auto", "modo Xbox confirmado: abre");
                Show(); check(kbSuspended, "barra suspende o automatico");
                Hide(); mode = false; tick(40); check(!kbVisible && !kbSuspended, "desktop ao fechar a barra: automatico nao volta");
                mode = true; ActivationAllowed();

                // Steam: layout de desktop enquanto aberto, o do app de volta ao fechar; espera apagada ate a Steam confirmar.
                deferSteam = true; launches.Clear(); foreground = bigPicture; layout = "769";
                ShowKeyboard("manual", bigPicture, "verificacao");
                check(kbSteamHeld && kbSteamGate && launches.Count == 0 && pendingSteam.Count == 1 && File.Exists(kbSteamMarker), "sobre o Big Picture: espera e marcador");
                before = strokes.Count; tap("X"); check(strokes.Count == before && kbStatus.Text.StartsWith("Aguardando a Steam"), "sem teclar enquanto a Steam nao solta");
                kbWindow.UpdateLayout(); SaveKeyboardRender("teclado-aguardando-steam");
                runSteam(null);
                check(!kbSteamGate && launches.Count == 1 && launches[0] == SteamInput.ForceDesktop && kbSteamRestore == "769", "Steam confirmou: desktop fixado, 769 lembrado");
                tap("X"); check(strokes.Count == before + 1, "tecla depois da confirmacao");
                Show(); runSteam(null);
                check(kbSuspended && launches.Count == 2 && launches[1] == SteamInput.Release, "barra por cima: so libera");
                layout = SteamInput.DesktopAppId; Hide(); tick(40); runSteam(null);
                check(kbVisible && launches.Count == 3 && launches[2] == SteamInput.ForceDesktop && kbSteamRestore == "769", "retomado: fixa de novo e mantem o 769");
                tap("CIRCULO"); runSteam(null);
                check(!kbVisible && launches.Count == 5 && launches[3] == SteamInput.Force("769") && launches[4] == SteamInput.Release && !File.Exists(kbSteamMarker),
                    "fechar: forca o 769 e libera");
                layout = null; launches.Clear();
                ShowKeyboard("manual", bigPicture, "verificacao"); runSteam(null);
                check(kbSteamRestore == "769", "sem linha no log, o Big Picture usa o 769");
                tap("CIRCULO"); runSteam(null);
                ShowKeyboard("manual", bigPicture, "verificacao"); tick(3100);
                check(!kbSteamGate && kbSteamHeld, "limite de 3 s libera a navegacao");
                tap("CIRCULO"); runSteam(null); deferSteam = false;
                // Uma abertura antiga em FIFO nao pode forcar depois de fechar, mesmo com excesso recusado.
                var usualSteamRun = kbSteamRun; Action drainSteam = null;
                var steamQueue = new SteamInputWorkQueue(delegate(Action body) { drainSteam = body; });
                ShowKeyboard("manual", bigPicture, "verificacao"); launches.Clear();
                int refused = 0;
                kbSteamRun = delegate(Action body) { if (!steamQueue.Enqueue(body)) refused++; };
                for (int i = 0; i < 20; i++) { HideKeyboard("fila", false); ShowKeyboard("manual", bigPicture, "fila"); }
                HideKeyboard("fim da fila", false); drainSteam(); kbSteamRun = usualSteamRun;
                check(refused > 0 && launches.Count > 0 && launches.TrueForAll(u => u == SteamInput.Release),
                    "fila cheia conserva limpeza aceita e ignora aberturas ja encerradas");
                check(File.Exists(kbSteamMarker), "limpeza de geracao antiga nao apaga diario da geracao recusada");
                own = ProcessIntegrity.Medium; launches.Clear();
                ShowKeyboard("manual", bigPicture, "verificacao");
                check(kbBlocked && !kbSteamHeld && launches.Count == 0, "Steam acima da barra: aviso, sem prender a Steam");
                HideKeyboard("verificacao", false);

                // App de destino fechado.
                foreground = notepad; ShowKeyboard("manual", notepad, "verificacao"); alive.Remove(notepad); tick(300);
                check(!kbVisible, "app fechado fecha o teclado");
                alive.Add(notepad);

                // Evidencia visual adicional com as mesmas fixtures: modificador, hover e escala reduzida.
                ShowKeyboard("manual", notepad, "render visual");
                KbLocate("ctrl"); KbPointer(KbFocusedKey());
                kbWindow.UpdateLayout(); SaveKeyboardRender("teclado-modificador");
                KbPointer(KbFocusedKey()); KbLocate("char-h");
                kbChrome["char-j"].Hovered = true; UpdateKeyVisuals();
                kbWindow.UpdateLayout(); SaveKeyboardRender("teclado-hover");
                kbChrome["char-j"].Hovered = false; UpdateKeyVisuals();
                var wideWork = kbWorkArea;
                kbWorkArea = delegate { return new Rect(0, 0, 1280, 720); };
                PlaceKeyboard(); kbWindow.UpdateLayout(); SaveKeyboardRender("teclado-720p");
                kbWorkArea = delegate { return new Rect(0, 0, 800, 600); };
                PlaceKeyboard(); kbWindow.UpdateLayout(); SaveKeyboardRender("teclado-800px");
                kbWorkArea = wideWork;
                KbLocate("keyboard-settings"); KbPointer(KbFocusedKey());
                check(window.IsVisible && kbSuspended && !kbVisible && FocusedTag() == "keyboard-auto", "engrenagem abre a secao do teclado e suspende a janela sem foco");
                Hide(); tick(100);
                check(kbVisible && !kbSuspended, "fechar as configuracoes retoma o teclado sobre o mesmo app");
                HideKeyboard("fim dos renders", false);

                // Preferencias e Configuracoes.
                string folder = Path.Combine(Output, "teclado-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
                string prefsPath = Path.Combine(folder, "keyboard.json");
                GameFiles.Write(prefsPath, new KeyboardPreferenceState { AutoOpen = false, KeySounds = false, Position = "top" });
                var read = KeyboardSettings.Read(prefsPath);
                check(!read.AutoOpen && !read.KeySounds && read.Position == "top" && KeyboardSettings.Read(Path.Combine(folder, "ausente.json")).AutoOpen, "preferencias persistem; padrao abre sozinho");
                File.WriteAllText(prefsPath, "{\"Version\":1,\"AutoOpen\":true,\"KeySounds\":true,\"Position\":\"lado\"}");
                bool rejected = false; try { KeyboardSettings.Read(prefsPath); } catch (InvalidDataException) { rejected = true; }
                check(rejected, "posicao invalida recusada");
                int writes = 0; kbWrite = delegate(KeyboardPreferenceState state) { writes++; GameFiles.Write(prefsPath, state); };
                Show(); SetPanel(5); window.UpdateLayout();
                check(actions.Exists(c => (string)c.Tag == "keyboard-auto") && actions.Exists(c => (string)c.Tag == "keyboard-position") && actions.Exists(c => (string)c.Tag == "keyboard-sounds"), "secao Teclado virtual nas Configuracoes");
                actions.Find(c => (string)c.Tag == "keyboard-position").Focus(); Activate();
                check(writes == 1 && kbPrefs.Position == "bottom" && KeyboardSettings.Read(prefsPath).Position == "bottom" && FocusedTag() == "keyboard-position", "posicao salva e foco mantido");
                actions.Find(c => (string)c.Tag == "keyboard-auto").Focus(); Activate();
                check(writes == 2 && !kbPrefs.AutoOpen && KeyboardSettings.Read(prefsPath).AutoOpen == false, "abrir sozinho desligado e salvo");
                Hide();
                report.AppendLine("OK: " + checks + " verificacoes do teclado virtual (ADR 0052).");
                report.AppendLine("Camadas ABNT2/simbolos/Completo: quatro linhas de 13 unidades, cabecalho e faixa de atalhos com intervalos; navegacao pelas posicoes reais; plano do SendInput (Unicode, atalhos, prefixo estendido, surrogates).");
                report.AppendLine("Engrenagem abre a secao do teclado nas Configuracoes; suspensao e retomada sobre o mesmo app. Renders de Caps, modificador, hover e areas de 1280/800 pixels.");
                report.AppendLine("Acesso rapido oculta a barra e abre sobre o app de tras; mapeamento do DualSense (✕, □ repetido, △, Options, L1/R1, L2, L3/Caps, R2 Ctrl, R3, MUTE sem ditado (fica com o DSX), Create, touchpad, analogico direito); acentos ao segurar; atalhos da faixa; PS + botao fica com o DSX.");
                report.AppendLine("Texto fora do log; destino acompanha o app da frente; sem envio sem app na frente ou acima da integridade; barra suspende e retoma.");
                report.AppendLine("Automatico so no modo Xbox, com campo editavel na frente, sem app excluido, sem a barra e respeitando o campo fechado; foco fora do campo fecha.");
                report.AppendLine("Modo Xbox lido na hora (desktop antes da notificacao, API incerta e energia em andamento nao abrem); 3 s apos entrar no modo, o campo que ja tinha o foco no desktop fica fechado; retomada depois da barra confere o modo.");
                report.AppendLine("Steam: desktop fixado com espera, 769 lembrado e restaurado, so liberar com a barra por cima, limite de 3 s, Steam elevada sem prender. Preferencias e Configuracoes.");
                report.AppendLine("Nao confirma: UIA, SendInput, Steam e janela sem foco reais. Ver --verify-keyboard-live e teste fisico no modo Xbox.");
                File.WriteAllText(Path.Combine(Output, "teclado-verificacao.txt"), report.ToString(), Encoding.UTF8);
                WriteLog("verificacao do teclado OK | " + checks + " verificacoes");
            }
            finally
            {
                HideKeyboard("fim da verificacao", false); kbPending = IntPtr.Zero; kbFocus = null; kbDismissedKey = null;
                kbSend = oldSend; kbForeground = oldForeground; kbWindowAlive = oldAlive; kbDescribe = oldDescribe; kbIntegrity = oldIntegrity;
                kbOwnIntegrity = oldOwn; kbWorkArea = oldWork; kbSteamOwned = oldSteamOwned; kbSteamLayout = oldLayout; kbSteamRun = oldRun;
                steamInputAvailable = oldAvailable; steamInputLaunch = oldLaunch; steamInputLog = oldLog; controllerNow = oldClock;
                activationRead = oldRead; kbPrefs = oldPrefs; kbWrite = oldWrite; previousWindow = null;
                activationStarted = false; ActivationAllowed(); ClearGuide();
                if (window.IsVisible) Hide();
            }
        }
        static string ReadLogFrom(string path, long start)
        {
            FlushLog();
            if (!File.Exists(path)) return "";
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                file.Seek(Math.Min(start, file.Length), SeekOrigin.Begin);
                using (var reader = new StreamReader(file, Encoding.UTF8)) return reader.ReadToEnd();
            }
        }
        void SaveKeyboardRender(string name)
        {
            if (kbRoot == null) return;
            kbRoot.UpdateLayout();
            int width = (int)Math.Ceiling(kbRoot.ActualWidth + 36), height = (int)Math.Ceiling(kbRoot.ActualHeight + 36);
            if (width <= 36 || height <= 36) return;
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                context.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x10, 0x18, 0x10)), null, new Rect(0, 0, width, height));
                context.DrawRectangle(new VisualBrush(kbRoot), null, new Rect(18, 18, kbRoot.ActualWidth, kbRoot.ActualHeight));
            }
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(Output, name + ".png"))) encoder.Save(file);
        }
    }
}

// Teclado sobre jogos (ADR 0101) com substitutos: nenhuma tecla e enviada, o foco nao muda de verdade e a Steam nao e chamada.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XGBP.Desktop;
using XGBP.Games;
using XGBP.Input;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyKeyboardCompose()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool ok, string why) { checks++; if (!ok) throw new Exception("Teclado modo jogo: " + why); };
            var oldSend = kbSend; var oldForeground = kbForeground; var oldAlive = kbWindowAlive; var oldDescribe = kbDescribe;
            var oldIntegrity = kbIntegrity; var oldOwn = kbOwnIntegrity; var oldWork = kbWorkArea; var oldSteamOwned = kbSteamOwned;
            var oldLayout = kbSteamLayout; var oldRun = kbSteamRun; var oldAvailable = steamInputAvailable; var oldLaunch = steamInputLaunch;
            var oldLog = steamInputLog; var oldClock = controllerNow; var oldRead = activationRead; var oldPrefs = kbPrefs; var oldWrite = kbWrite;
            var oldTake = kbTakeFocus; var oldReturn = kbReturnFocus; var oldView = gamesView;
            var strokes = new List<string>(); var launches = new List<string>(); var steamText = new StringBuilder();
            IntPtr notepad = new IntPtr(0x7201), genshin = new IntPtr(0x7202), neva = new IntPtr(0x7203);
            IntPtr foreground = genshin;
            var targets = new Dictionary<IntPtr, KeyboardTarget> {
                { notepad, new KeyboardTarget { Pid = 31, Path = @"C:\Windows\notepad.exe", Name = "Bloco de Notas", Created = 301 } },
                { genshin, new KeyboardTarget { Pid = 32, Path = @"D:\Games\HoYoPlay\games\Genshin Impact game\GenshinImpact.exe", Name =
                    "Genshin Impact", Created = 302
                    } },
        { neva, new KeyboardTarget { Pid = 33, Path = @"C:\Program Files (x86)\Steam\steamapps\common\Neva\Neva.exe", Name = "Neva", Created = 303 } } };
            int own = ProcessIntegrity.High, takes = 0, returns = 0; bool grant = true, giveBack = true;
            DateTime now = DateTime.Now.AddMinutes(10);
            try
            {
                Hide(); HideKeyboard("verificacao", false); ClearGuide(); kbPending = IntPtr.Zero;
                kbPrefs = new KeyboardPreferenceState(); kbDismissedKey = null; kbFocus = null;
            kbSend = delegate(KeyStroke stroke) { strokes.Add(stroke.Text != null ? "T:" + stroke.Text : stroke.Describe()); return
                TextInput.Expected(stroke); };
                kbForeground = delegate { return foreground; };
                kbWindowAlive = delegate(IntPtr hwnd) { return targets.ContainsKey(hwnd); };
                kbDescribe = delegate(IntPtr hwnd) { KeyboardTarget t; return targets.TryGetValue(hwnd, out t) ? t : null; };
                kbIntegrity = delegate { return ProcessIntegrity.High; };
                kbOwnIntegrity = delegate { return own; };
                kbSteamOwned = delegate(string path) { return path != null && path.Contains(@"\Steam\"); };
                kbSteamLayout = delegate { return "3166698800"; };
                kbSteamRun = delegate(Action body) { body(); };
                steamInputAvailable = delegate { return true; };
                steamInputLaunch = delegate(string uri) { launches.Add(uri); steamText.AppendLine(SteamInput.Executed(uri)); };
                steamInputLog = delegate { return delegate { return steamText.ToString(); }; };
                controllerNow = delegate { return now; };
                activationRead = delegate { return true; };
                kbWrite = delegate { };
                kbTakeFocus = delegate(IntPtr own2) { takes++; if (grant) foreground = own2; return grant ? "concedido" : "negado"; };
                kbReturnFocus = delegate(DesktopWindow w) { returns++; if (giveBack) foreground = w.Handle; return giveBack; };
                // Genshin: atalho da Steam fora das pastas dela; Neva: jogo da Steam. O Bloco de Notas nao e jogo.
                gamesView = new GamesView();
                gamesView.Activities.Add(new GameActivity { Key = "shortcut:genshin:3166698800",
                    Window = new DesktopWindow { Handle = genshin, ProcessId = 32, ProcessCreated = 302 } });
                gamesView.Activities.Add(new GameActivity { Key = "steam:neva:1812620", Window = new DesktopWindow { Handle = neva, ProcessId =
                    33, ProcessCreated = 303 }
                    });
                activationStarted = false; ActivationAllowed();
                Action<string, bool> input = delegate(string button, bool down) {
                    now = now.AddMilliseconds(30);
                    OnController(new ButtonEvent { Device = "DualSense#0-BT", Button = button, Down = down, Time = now });
                };
                Action<string> tap = delegate(string button) { input(button, true); input(button, false); };
                Action<int> tick = delegate(int ms) { for (int i = 0; i < ms; i += 40) { now = now.AddMilliseconds(40); KeyboardTick(); } };

                // Programa do desktop: sem foco, digita direto (comportamento de antes).
                foreground = notepad; ShowKeyboard("manual", notepad, "verificacao");
                check(!kbCompose && takes == 0 && !KbGameTarget() && !KbSteamTarget(), "programa do desktop fica sem foco");
                KbLocate("char-h"); tap("X"); check(strokes.Count == 1 && strokes[0] == "T:h", "programa recebe a tecla direto");
                HideKeyboard("verificacao", false); strokes.Clear();

                // Jogo por atalho da Steam fora das pastas: o layout de desktop e fixado pela identidade e o teclado toma o foco.
                foreground = genshin; ShowKeyboard("manual", genshin, "verificacao");
                check(KbSteamTarget() && kbSteamHeld && launches.Count == 1 && launches[0] == SteamInput.ForceDesktop && kbSteamRestore == "3166698800",
                    "atalho da Steam pela identidade");
            check(kbCompose && takes == 1 && foreground == kbHandle && (KbGetWindowLong(kbHandle, -20).ToInt64() & 0x08000000L) == 0,
                "jogo: o teclado toma o foco");
                check(kbStatus.Text.StartsWith("Escreva para Genshin Impact") && kbStatus.FontSize == 15, "linha de composicao no cabecalho");
                KbLocate("char-h"); tap("X"); tap("TRIANGULO"); KbTypeValue("ola"); tap("QUADRADO");
                check(strokes.Count == 0 && kbComposeText == "h ol" && kbComposeCaret == 4, "texto fica no teclado, nada vai ao jogo");
                tap("L1"); tap("L1"); KbTypeValue("😀"); tap("QUADRADO"); tap("QUADRADO");
                check(kbComposeText == "hol" && kbComposeCaret == 1 && kbStatus.Text == "h▏ol", "cursor, emoji inteiro e apagar");
                input("R2", true); tap("X"); input("R2", false); tap("MUTE");
                check(strokes.Count == 0 && kbComposeText == "hol" && kbNotice == "No jogo, o teclado envia só texto.", "atalhos e ditado nao vao ao jogo");
                kbNotice = null; UpdateKeyboardStatus(); kbWindow.UpdateLayout(); SaveKeyboardRender("teclado-modo-jogo");

                // ≡ envia: espera soltar, devolve o foco, assenta e manda o texto com Enter; depois devolve o layout do jogo.
                launches.Clear(); input("OPTIONS", true);
                check(kbComposeFinishing == "send" && returns == 0 && strokes.Count == 0, "espera soltar o ≡ antes de devolver o foco");
                input("OPTIONS", false); tick(40);
                check(returns == 1 && foreground == genshin && strokes.Count == 0, "devolve o foco e assenta antes de enviar");
                tick(200);
                check(strokes.Count == 2 && strokes[0] == "T:hol" && strokes[1] == KeyStroke.Key(TextInput.Return).Describe() && !kbVisible && !kbCompose,
                    "texto e Enter enviados ao jogo");
                check(launches.Count == 2 && launches[0] == SteamInput.Force("3166698800") && launches[1] == SteamInput.Release &&
                    (KbGetWindowLong(kbHandle, -20).ToInt64() & 0x08000000L) != 0, "layout do jogo restaurado e teclado sem foco de novo");

                // ○ cancela: nada enviado e o foco volta ao jogo.
                strokes.Clear(); ShowKeyboard("manual", genshin, "verificacao"); KbTypeValue("x"); tap("CIRCULO"); tick(200);
                check(strokes.Count == 0 && !kbVisible && foreground == genshin && returns == 2, "cancelar devolve o foco sem enviar");
                // Texto vazio: ≡ so manda o Enter.
                ShowKeyboard("manual", genshin, "verificacao"); tap("OPTIONS"); tick(200);
                check(strokes.Count == 1 && strokes[0] == KeyStroke.Key(TextInput.Return).Describe(), "vazio: so Enter");
                strokes.Clear();

                // O jogo nao volta para a frente: o texto fica e o teclado retoma o foco.
                ShowKeyboard("manual", genshin, "verificacao"); KbTypeValue("oi"); giveBack = false; int before = takes;
                tap("OPTIONS"); tick(1400);
                check(kbVisible && kbCompose && kbComposeFinishing == null && kbComposeText == "oi" && strokes.Count == 0 && takes == before + 1 &&
                    kbNotice != null && kbNotice.StartsWith("O jogo não voltou"), "sem o jogo na frente: texto mantido");
                giveBack = true; kbNotice = null;
                // O jogo toma o foco de volta: o teclado retoma, no maximo tres vezes seguidas se o Windows negar.
            foreground = genshin; before = takes; tick(1100); check(takes == before + 1 && foreground == kbHandle, "jogo na frente de novo: o teclado retoma");
                grant = false; foreground = genshin; before = takes; tick(5000);
                check(takes == before + 3 && kbNotice == "O jogo continua com o foco e pode receber o controle.", "negado: tres tentativas e aviso");
                grant = true; kbComposeRetries = 0; tick(1100); kbNotice = null;
                // Barra por cima: suspende sem perder o texto e retoma compondo quando o jogo volta.
                Show(); check(kbSuspended && kbCompose && kbComposeText == "oi", "barra suspende sem perder o texto");
                Hide(); foreground = genshin; before = takes; tick(80);
                check(kbVisible && kbCompose && kbComposeText == "oi" && takes == before + 1, "retomado compondo, com o texto");
                // Outro app na frente: deixa de compor e acompanha o app.
                foreground = notepad; tick(300);
                check(kbVisible && !kbCompose && kbTarget == notepad, "outro app: volta a digitar direto");
                HideKeyboard("verificacao", false);
                // Automatico, integridade abaixo do jogo e jogo da Steam pela pasta.
foreground = genshin; ShowKeyboard("auto", genshin, "verificacao"); check(!kbCompose, "automatico nao compoe"); HideKeyboard("verificacao", false);
            own = ProcessIntegrity.Medium; ShowKeyboard("manual", genshin, "verificacao"); check(kbBlocked && !kbCompose,
                "bloqueado pela integridade nao compoe");
                HideKeyboard("verificacao", false); own = ProcessIntegrity.High;
                foreground = neva; launches.Clear(); ShowKeyboard("manual", neva, "verificacao");
                check(kbCompose && kbSteamHeld && launches[0] == SteamInput.ForceDesktop, "jogo da Steam pela pasta continua fixado e compondo");
                HideKeyboard("verificacao", false);
File.WriteAllText(Path.Combine(Output, "teclado-modo-jogo-verificacao.txt"), "OK: " + checks + " verificacoes do teclado sobre jogos (ADR 0101). " +
"Programa do desktop sem foco e direto; atalho da Steam fora das pastas reconhecido pela identidade; " +"jogo: teclado toma o foco, texto na linha, " +
                    "cursor/emoji/apagar, atalhos e ditado bloqueados; ≡ espera soltar, devolve o foco, assenta e envia t"
                        +"exto+Enter, restaura o layout e volta sem foco; "
                        +
                    "○ cancela; vazio manda so Enter; jogo fora da frente mantem o texto; retomada do foco limitada a tre" +
                        "s tentativas; barra suspende e retoma com o texto; " +
                    "outro app volta ao direto; automatico e integridade bloqueada nao compoem. Foco, envio e Steam subst"
                        +"ituidos; nao confirma o Genshin real.\r\n",
                        Encoding.UTF8);
                WriteLog("verificacao do teclado modo jogo OK | " + checks + " verificacoes");
            }
            finally
            {
                HideKeyboard("fim da verificacao", false); kbPending = IntPtr.Zero; kbFocus = null; kbDismissedKey = null; kbNotice = null;
                kbSend = oldSend; kbForeground = oldForeground; kbWindowAlive = oldAlive; kbDescribe = oldDescribe; kbIntegrity = oldIntegrity;
                kbOwnIntegrity = oldOwn; kbWorkArea = oldWork; kbSteamOwned = oldSteamOwned; kbSteamLayout = oldLayout; kbSteamRun = oldRun;
                steamInputAvailable = oldAvailable; steamInputLaunch = oldLaunch; steamInputLog = oldLog; controllerNow = oldClock;
                activationRead = oldRead; kbPrefs = oldPrefs; kbWrite = oldWrite; kbTakeFocus = oldTake; kbReturnFocus = oldReturn; gamesView = oldView;
                previousWindow = null; activationStarted = false; ActivationAllowed(); ClearGuide();
                if (window.IsVisible) Hide();
            }
        }
    }
}

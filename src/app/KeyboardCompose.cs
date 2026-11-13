// Teclado virtual sobre jogos (ADR 0101): compor e enviar. Sem foco, o jogo continuava com o foco e lia o controle por
// conta propria (Genshin com o Steam Input desligado no atalho: o mesmo aperto andava no teclado e no jogo, 06/11).
// Sobre uma janela de jogo acompanhado pela barra, o teclado toma o foco, como a barra, e o texto fica numa linha dele;
// ≡ devolve o foco ao jogo depois de soltar os botoes e manda o texto com Enter; ○ cancela e devolve o foco.
// Programas do desktop continuam sem foco, digitando direto no campo (pedido do usuario: comportamentos diferentes).
using System;
using System.Windows;
using XGBP.Desktop;
using XGBP.Input;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        const int KbComposeFocusMs = 1200, KbComposeSettleMs = 150, KbComposeRetakeMs = 1000, KbComposeMax = 500;
        bool kbCompose;
        string kbComposeText = "", kbComposeFinishing;
        int kbComposeCaret, kbComposePhase;
        long kbTargetCreated;
        DateTime kbComposeDeadline, kbComposeRetakeAt;
        // Substituiveis: a verificacao nao toma nem devolve o foco de verdade.
        Func<IntPtr, string> kbTakeFocus = delegate(IntPtr own) { return DesktopWindows.TakeForeground(own, false); };
        Func<DesktopWindow, bool> kbReturnFocus = DesktopWindows.FocusOnly;
        int kbComposeRetries;

        DesktopWindow KbTargetWindow()
        {
            return new DesktopWindow { Handle = kbTarget, ProcessId = kbTargetPid, ProcessCreated = kbTargetCreated, ExecutablePath = kbTargetPath };
        }
        // Steam disputa o controle: pasta da Steam ou jogo de atalho rastreado pela sessao (fora das pastas, ADR 0066).
        bool KbSteamTarget()
        {
            if (kbTargetPath != null && kbSteamOwned(kbTargetPath)) return true;
            return kbTargetPid != 0 && SteamManagedGame(KbTargetWindow());
        }
        // Jogo acompanhado pela barra (qualquer loja ou .exe do catalogo), pela mesma janela ou processo com a mesma criacao.
        bool KbGameTarget()
        {
            if (kbTarget == IntPtr.Zero || kbTargetPid == 0 || gamesView == null) return false;
            var target = KbTargetWindow();
            return gamesView.Activities.Exists(a => GameOwnsWindow(a, target));
        }

        // Abertura ou retomada: so no manual, sobre um jogo e sem bloqueio de integridade. keep mantem o texto (retomada).
        void KbComposeBegin(bool keep)
        {
            if (kbMode != "manual" || kbBlocked || !KbGameTarget()) { if (kbCompose) { KbComposeEnd(); UpdateKeyboardStatus(); } return; }
            if (!keep || !kbCompose) { kbComposeText = ""; kbComposeCaret = 0; }
            kbCompose = true; kbComposeFinishing = null; kbComposePhase = 0; kbComposeRetries = 0;
            KbComposeTakeFocus("compor e enviar");
            UpdateKeyboardStatus();
        }
        void KbComposeTakeFocus(string reason)
        {
            KbSetActivatable(true);
            string result;
            try { result = kbTakeFocus(kbHandle); } catch (Exception ex) { result = "erro: " + ex.Message; }
            kbComposeRetakeAt = controllerNow().AddMilliseconds(KbComposeRetakeMs);
            WriteLog("teclado: modo jogo | " + reason + " | foco do teclado: " + result + " | " + KbTargetDescribe());
            if (kbForeground() == kbHandle) { kbComposeRetries = 0; return; }
            kbComposeRetries++;
            KbNotice("O jogo continua com o foco e pode receber o controle.");
        }
        void KbComposeEnd()
        {
            if (!kbCompose) return;
            kbCompose = false; kbComposeFinishing = null; kbComposePhase = 0;
            KbSetActivatable(false);
        }
        // Sem foco (WS_EX_NOACTIVATE) no desktop; ativavel so enquanto compoe sobre um jogo.
        void KbSetActivatable(bool on)
        {
            if (kbHandle == IntPtr.Zero) return;
            long style = KbGetWindowLong(kbHandle, -20).ToInt64();
            long next = on ? style & ~0x08000000L : style | 0x08000000L;
            if (next != style) KbSetWindowLong(kbHandle, -20, new IntPtr(next));
        }

        // Toque do teclado enquanto compoe: texto e edicao ficam na linha; Enter envia; atalhos nao vao ao jogo.
        bool KbComposeStroke(KeyStroke stroke)
        {
            if (kbComposeFinishing != null || kbSteamGate) return false;
            if (stroke.Text != null)
            {
                if (kbComposeText.Length + stroke.Text.Length > KbComposeMax) { KbNotice("Limite de " + KbComposeMax + " caracteres."); return false; }
                kbComposeText = kbComposeText.Insert(kbComposeCaret, stroke.Text); kbComposeCaret += stroke.Text.Length;
            }
            else if ((stroke.Modifiers & (KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Win)) != 0) {
                KbNotice("No jogo, o teclado envia só texto."); return
                false; }
            else if (stroke.Vk == TextInput.Return) { KbComposeFinish(true); return true; }
            else if (stroke.Vk == TextInput.Space) return KbComposeStroke(KeyStroke.Chars(" "));
            else if (stroke.Vk == TextInput.Back) { if (kbComposeCaret == 0) return false; int start = KbComposeStep(-1); kbComposeText =
                kbComposeText.Remove(start,
                kbComposeCaret - start); kbComposeCaret = start; }
            else if (stroke.Vk == TextInput.Delete) { if (kbComposeCaret >= kbComposeText.Length) return false; kbComposeText =
                kbComposeText.Remove(kbComposeCaret,
                KbComposeStep(1) - kbComposeCaret); }
            else if (stroke.Vk == TextInput.Left) kbComposeCaret = KbComposeStep(-1);
            else if (stroke.Vk == TextInput.Right) kbComposeCaret = KbComposeStep(1);
            else if (stroke.Vk == TextInput.Home) kbComposeCaret = 0;
            else if (stroke.Vk == TextInput.End) kbComposeCaret = kbComposeText.Length;
            else if (stroke.Vk == TextInput.Up || stroke.Vk == TextInput.Down) return false;
            else { KbNotice("No jogo, o teclado envia só texto."); return false; }
            UpdateKeyboardStatus();
            return true;
        }
        // Proxima posicao do cursor sem partir um par substituto (emoji).
        int KbComposeStep(int delta)
        {
            int at = kbComposeCaret;
            if (delta < 0) return at <= 0 ? 0 : at >= 2 && char.IsLowSurrogate(kbComposeText[at - 1]) && char.IsHighSurrogate(kbComposeText[at -
                2]) ? at - 2 : at -
                1;
            return at >= kbComposeText.Length ? at : at + 1 < kbComposeText.Length && char.IsHighSurrogate(kbComposeText[at]) &&
                char.IsLowSurrogate(kbComposeText[at
                + 1]) ? at + 2 : at + 1;
        }
        // ○ ou fechar do teclado enquanto compoe: cancela e devolve o foco ao jogo (o jogo nao fica sem foco).
        bool KbComposeClose()
        {
            if (!kbCompose) return false;
            KbComposeFinish(false);
            return true;
        }
        void KbComposeFinish(bool send)
        {
            if (!kbCompose || kbComposeFinishing != null) return;
            kbComposeFinishing = send ? "send" : "cancel"; kbComposePhase = 0;
            kbComposeDeadline = controllerNow().AddMilliseconds(ReleaseWaitMs);
            KbResetInput(false);
            UpdateKeyboardStatus();
            KbComposeTick(controllerNow());
        }
        // Fases: 0 espera soltar os botoes (o jogo leria o ≡ ou o ○ como aperto novo, ADR 0068); 1 devolve o foco e espera
        // o jogo na frente; 2 espera o jogo assentar e envia. Sem envio, o jogo que tomou o foco de volta perde de novo.
        void KbComposeTick(DateTime now)
        {
            if (!kbCompose) return;
            if (kbComposeFinishing == null)
            {
                // No maximo tres tentativas seguidas: com o Windows negando, nao insistir a cada segundo.
                if (kbForeground() == kbTarget && now >= kbComposeRetakeAt && kbComposeRetries < 3) KbComposeTakeFocus("o jogo voltou para a frente");
                return;
            }
            if (kbComposePhase == 0)
            {
                if (buttonsHeldAt.Count > 0 && now < kbComposeDeadline) return;
                kbComposePhase = 1; kbComposeDeadline = now.AddMilliseconds(KbComposeFocusMs);
                bool asked = false;
try { asked = kbReturnFocus(KbTargetWindow()); } catch (Exception ex) { WriteLog("teclado: modo jogo | devolver o foco falhou | " + ex.Message); }
                WriteLog("teclado: modo jogo | devolvendo o foco ao jogo | " + (asked ? "aceito" : "negado") + " | " + (kbComposeFinishing ==
                    "send" ? "enviar" :
                    "cancelar"));
            }
            if (kbComposePhase == 1)
            {
                if (kbForeground() == kbTarget) { kbComposePhase = 2; kbComposeDeadline = now.AddMilliseconds(KbComposeSettleMs); return; }
                if (now < kbComposeDeadline) return;
                if (kbComposeFinishing == "cancel") { HideKeyboard("fechado pelo usuario; o jogo nao voltou para a frente", true); return; }
                // O texto fica: o teclado retoma o foco e a pessoa tenta de novo.
                WriteLog("teclado: modo jogo | o jogo nao voltou para a frente; texto mantido");
                kbComposeFinishing = null; kbComposePhase = 0;
                KbComposeTakeFocus("envio sem o jogo na frente");
                KbNotice("O jogo não voltou para a frente. O texto continua aqui; tente de novo.");
                return;
            }
            if (now < kbComposeDeadline) return;
            if (kbComposeFinishing == "cancel") { HideKeyboard("fechado pelo usuario", true); return; }
            int length = kbComposeText.Length, expected = 0, sent = 0;
        foreach (var stroke in length > 0 ? new[] { KeyStroke.Chars(kbComposeText), KeyStroke.Key(TextInput.Return) } : new[] {
            KeyStroke.Key(TextInput.Return) })
            {
                expected += TextInput.Expected(stroke);
                try { sent += kbSend(stroke); kbSent++; } catch (Exception ex) { WriteLog("teclado: modo jogo | envio falhou | " + ex.Message); }
            }
            // O log nunca registra o texto, so o tamanho.
            WriteLog("teclado: modo jogo | texto enviado | " + length + " caracteres e Enter" + (sent < expected ? " | envio parcial: " + sent +
                " de " + expected :
                ""));
            kbComposeText = ""; kbComposeCaret = 0;
            HideKeyboard("texto enviado ao jogo", false);
        }
        // Linha do texto no cabecalho: maior, com o cursor e o fim visivel quando nao cabe. Fora do modo, o status normal.
        bool KbComposeStatus()
        {
            if (kbStatus == null) return false;
            bool notice = kbNotice != null && controllerNow() < kbNoticeUntil;
            if (!kbCompose || kbSteamGate || notice) { kbStatus.FontSize = 11; return false; }
            string name = kbTargetName ?? "jogo", text;
            if (kbComposeFinishing == "send") text = "Enviando para " + name + "…";
            else if (kbComposeFinishing == "cancel") text = "Voltando para " + name + "…";
            else if (kbComposeText.Length == 0) text = "Escreva para " + name + " · ≡ envia com Enter · " + ButtonLabel("○") + " cancela";
            else
            {
                text = kbComposeText.Insert(kbComposeCaret, "▏");
                if (text.Length > 72) text = "…" + text.Substring(Math.Max(0, Math.Min(text.Length - 71, kbComposeCaret - 50)), 71);
            }
            kbStatus.Text = text; kbStatus.FontSize = 15;
            kbStatus.Foreground = Brush(kbComposeText.Length > 0 && kbComposeFinishing == null ? "TextPrimary" : "Muted");
            kbStatus.Visibility = Visibility.Visible;
            if (kbHeaderGrip != null) kbHeaderGrip.Visibility = Visibility.Hidden;
            return true;
        }
    }
}

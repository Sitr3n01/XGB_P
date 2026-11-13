// Acordar pelo PS (ADR 0102, metodo BootPS): estado do despertar nas Configuracoes (CONTROLE) e na confirmacao de
// Suspender. Somente leitura: corrigir sao os scripts do usuario. A leitura (lib/power/WakeReadiness.cs) roda em worker
// e vale por 30 s; o botao da linha le de novo.
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using XGBP.Desktop;
using XGBP.Power;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        const int WakeFreshSeconds = 30;
        Func<WakeReadiness> wakeRead = WakeReadinessReader.Read;
        WakeReadiness wakeState;
        DateTime wakeReadAt = DateTime.MinValue;
        bool wakeReading;
        string wakeLogged;

        void StartWake()
        {
            if (verify) wakeRead = delegate { return new WakeReadiness { Status = WakeStatus.Ready, KeyPresent = true, KeyCorrect = true, Paired
                = true, RemoteWake =
                1, Present = true }; };
        }
        static WakeReadiness SafeWakeRead(Func<WakeReadiness> read)
        {
            try { return read() ?? new WakeReadiness { Error = "sem leitura" }; }
            catch (Exception ex) { return new WakeReadiness { Error = ex.Message }; }
        }
        // Ao montar uma tela que mostra o estado: le se estiver velho. Na verificacao, na hora e sem reconstruir.
        void EnsureWake()
        {
            if (wakeReading || wakeState != null && (DateTime.UtcNow - wakeReadAt).TotalSeconds < WakeFreshSeconds) return;
            if (verify) { WakeArrived(SafeWakeRead(wakeRead), false); return; }
            StartWakeRead();
        }
        void ForceWake()
        {
            if (verify) { WakeArrived(SafeWakeRead(wakeRead), true); return; }
            if (!wakeReading) { wakeState = null; RebuildWakeScreens(); StartWakeRead(); }
        }
        void StartWakeRead()
        {
            wakeReading = true; var read = wakeRead;
            Task.Factory.StartNew(delegate { var state = SafeWakeRead(read); Dispatch(delegate { WakeArrived(state, true); }); });
        }
        void WakeArrived(WakeReadiness state, bool rebuild)
        {
            wakeReading = false; wakeState = state; wakeReadAt = DateTime.UtcNow;
            string text = state.Describe();
            if (text != wakeLogged) { wakeLogged = text; WriteLog("despertar pelo PS: " + text); }
            if (rebuild) RebuildWakeScreens();
        }
        void RebuildWakeScreens()
        {
            if (disposed || !window.IsVisible) return;
            if (panel == 5 && settingsPage == "main")
            {
                var focused = Keyboard.FocusedElement as Control;
                RebuildSettings(focused != null && actions.Contains(focused) ? focused.Tag as string : null);
            }
            else if (panel == 4 && powerConfirmation == SystemPowerAction.Suspend) RebuildPowerConfirmation();
        }

        static string WakeRowText(WakeReadiness s)
        {
            if (s == null) return "Lendo…";
            switch (s.Status)
            {
                case WakeStatus.Ready: return "Pronto";
                case WakeStatus.ConnectOnce: return "Ausente agora";
                case WakeStatus.NotArmed: return "Falta armar o controle";
                case WakeStatus.NotConfigured: return "Não configurado";
                case WakeStatus.Divergent: return "Configuração divergente";
                case WakeStatus.NeverPaired: return "Controle não pareado";
            }
            return "Indisponível";
        }
        static string WakeNote(WakeReadiness s)
        {
            if (s == null) return null;
            string note;
            switch (s.Status)
            {
                case WakeStatus.Ready: note = "Suspenda e aperte o PS: o PC acorda pelo controle."; break;
                case WakeStatus.ConnectOnce: note = "O controle está ausente agora. A permissão de despertar está gravada, mas esta leitura não confirma " +
                    "se o Bluetooth continua armado. Conecte o controle com o PC ligado para conferir."; break;
                case WakeStatus.NotArmed: note = "Com o controle conectado, rode scripts\\ativar-despertar-ps.ps1 como administrador."; break;
                case WakeStatus.NotConfigured: note = "O Windows não deixa o controle acordar o PC. Rode scripts\\ativar-despertar-ps.ps1 como administrado" +
                    "r e reinicie."; break;
                case WakeStatus.Divergent: note = "A chave do Bluetooth não tem as três entradas (teclado, apontador e gamepad). Confira com scripts\\v" +
                    "erificar-despertar-ps.ps1."; break;
                case WakeStatus.NeverPaired: note = "Pareie o DualSense por Bluetooth para ele poder acordar o PC."; break;
                default: note = "Não foi possível ler o despertar" + (s.Error != null ? ": " + s.Error : "") + "."; break;
            }
            if (s.OthersAwake.Count > 0) note += " Também acordam o PC: " + string.Join(", ", s.OthersAwake.ToArray()) + ".";
            if (s.OthersError != null) note += " A lista de outros dispositivos está incompleta: " + s.OthersError + ".";
            return note;
        }
        // Na confirmacao de Suspender: como o PC vai acordar. Sem leitura valida, nada.
        static string WakeSuspendText(WakeReadiness s)
        {
            if (s == null || s.Status == WakeStatus.Unknown) return null;
            if (s.Status == WakeStatus.Ready) return "O PS do DualSense acorda o PC.";
            if (s.Status == WakeStatus.ConnectOnce) return
                "O controle está ausente agora; não foi possível confirmar o despertar pelo PS. Use o teclado ou o bo" +
                "tão de energia se necessário.";
            return "O PS não acorda o PC (" + WakeRowText(s).ToLowerInvariant() + "). Use o teclado ou o botão de energia.";
        }

        void AddWakeSetting()
        {
            EnsureWake();
            var row = ConnectionButton(body, "Acordar pelo PS", WakeRowText(wakeState), "wake-ps", ForceWake);
            row.ToolTip = "Estado do despertar pelo botão PS do DualSense (método BootPS). Aperte para ler de novo. Corrigir é " +
                "com os scripts do XGB_P, como administrador.";
            string note = WakeNote(wakeState);
            if (note != null) ConnectionNote(note);
            var last = wakeTrace == null ? null : wakeTrace.Latest;
    if (last != null) ConnectionNote(last.Summary() + ". O PS observado depois da retomada é uma correlação; não comprova a origem física do despertar.");
        }
        void AddWakeSuspendNote()
        {
            EnsureWake();
            string text = WakeSuspendText(wakeState);
            if (text == null) return;
            var line = Text(text, 15, wakeState.Status == WakeStatus.Ready ? "Muted" : "FeedbackWarning");
            line.Margin = new System.Windows.Thickness(0, 4, 0, 8); body.Children.Add(line);
        }

        // Diagnostico somente leitura: a mesma leitura do app, com o tempo gasto. Nada e escrito no sistema.
    }
}

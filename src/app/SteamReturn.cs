// Retorno ao Big Picture: observar contexto novo depois do foco, sem forcar um perfil permanente.
using System;
using System.Threading.Tasks;
using XGBP.Desktop;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        sealed class SteamReturn
        {
            public DesktopWindow Target;
            public Func<string> Read;
            public int Mode, Epoch, Reads;
            public bool Busy;
            public DateTime Deadline, StableSince, ObservedAt;
            public string Context, Error;
            public FeedbackNotice Notice;
        }
        SteamReturn steamReturn;
        Func<Func<string>> steamReturnContextLog = delegate { return new SteamInputLogCursor(SteamInput.ControllerLog()).ReadNew; };
        Func<string> CreateSteamReturnCursor()
        {
            try { return steamReturnContextLog(); }
            catch (Exception ex) { WriteLog("Steam: cursor de retorno indisponivel | " + ex.Message); return null; }
        }
        void RestoreSteamClosingVisual()
        {
            bool restore = steamClosingVisual; steamClosingVisual = false;
            if (!restore) return;
            motionClosing = false; ++motionCloseVersion;
            if (window.IsVisible && ControllerFocused() && !disposed) { RestoreDeferredWindow(); MotionOpen(false); }
        }
        void BeginSteamReturn(DesktopWindow target, Func<string> read)
        {
            steamClosingVisual = false;
            if (window.IsVisible || !appForeground(target) || disposed) return;
            var pending = new SteamReturn { Target = target, Read = read, Mode = activationEpoch, Epoch = gameNavigationEpoch,
                Deadline = feedbackNow().AddMilliseconds(1800),
                Notice = BeginFeedback("Steam em foco; verificando o controle…", "0", 2) };
            steamReturn = pending;
            WriteLog("Steam: foco entregue apos soltura | alvo=" + target.Key + "; PS=" + guideDownDevices.Count);
            steamNavigationTimer.Start(); TickSteamReturn();
        }
        void FinishSteamReturn(SteamReturn pending, bool confirmed)
        {
            if (steamReturn != pending) return;
            steamReturn = null; steamNavigationTimer.Stop();
            FinishFeedback(pending.Notice, confirmed ? "Steam pronta para o controle." :
                "A Steam recebeu o foco, mas não confirmou o controle. Abra a barra e tente novamente.",
                confirmed ? FeedbackKind.Success : FeedbackKind.Attention);
            WriteLog("Steam: retorno " + (confirmed ? "confirmado" : "sem confirmacao de entrada") +
                " | alvo=" + pending.Target.Key + "; contexto=" + (pending.Context ?? "ausente") +
                "; leituras=" + pending.Reads + "; erro=" + (pending.Error ?? ""));
        }
        void TickSteamReturn()
        {
            var pending = steamReturn; if (pending == null) return;
            if (disposed || window.IsVisible || pending.Mode != activationEpoch || pending.Epoch != gameNavigationEpoch ||
                guideDownDevices.Count > 0 || !appForeground(pending.Target))
            { steamReturn = null; steamNavigationTimer.Stop(); return; }
            if (pending.Read == null || pending.Error != null || feedbackNow() >= pending.Deadline)
            { FinishSteamReturn(pending, false); return; }
            if (pending.Busy) return;
            pending.Busy = true;
            Action<string, string> apply = delegate(string context, string error) {
                if (steamReturn != pending) return;
                pending.Busy = false; pending.Reads++; pending.Context = context; pending.Error = error;
                // Uma amostra atrasada ou outra janela nao confirma a entrega desta geracao.
                if (pending.Mode != activationEpoch || pending.Epoch != gameNavigationEpoch || window.IsVisible ||
                    guideDownDevices.Count > 0 || !appForeground(pending.Target)) return;
                DateTime now = feedbackNow();
                if (context != SteamInput.ClientUiAppId || error != null ||
                    pending.ObservedAt != DateTime.MinValue && (now - pending.ObservedAt).TotalMilliseconds > 500)
                    pending.StableSince = DateTime.MinValue;
                if (context == SteamInput.ClientUiAppId && error == null)
                {
                    if (pending.StableSince == DateTime.MinValue) pending.StableSince = now;
                    else if ((now - pending.StableSince).TotalMilliseconds >= 250 && now < pending.Deadline)
                        FinishSteamReturn(pending, true);
                }
                pending.ObservedAt = now;
            };
            Action read = delegate {
                string context = null, error = null;
                try { context = SteamInputObservation.LastContext(pending.Read(), SteamInput.ClientUiAppId); }
                catch (Exception ex) { error = ex.Message; }
                if (verify) apply(context, error); else Dispatch(delegate { apply(context, error); });
            };
            if (verify) read(); else Task.Run(read);
        }
    }
}

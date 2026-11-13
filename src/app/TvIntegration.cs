// Host da TV e retomada: inicializacao fora da interface; nenhum efeito real nos modos de verificacao.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XGBP.Tv;
using XGBP.Power;
using XGBP.Desktop;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        readonly object tvHostGate = new object();
        readonly ResumeGesture resumeGesture = new ResumeGesture();
        TvService tvService;
        PowerEvents powerEvents;
        bool tvStopping, tvBusy, tvInterrupting;
        TvRequestKind? tvWorkKind;
        int barSuspendArmed;
        TvCoreConfig tvConfig;
        string tvInitError;
        Func<TvRequestKind, bool, Task<TvReply>> tvCommand;
        Func<TvCoreConfig, Task<TvReply>> tvSave;
        Func<TvRuntime> tvRuntime;
        string tvConfirm;
        bool controllerInvocation;
        void ActivateFromController() { controllerInvocation = true; try { Activate(); } finally { controllerInvocation = false; } }
        readonly object resumeJournalGate = new object();
        long resumeJournalVersion;
        WakeTraceJournal wakeTrace;
        void StartTvIntegration()
        {
            if (verify) return;
            wakeTrace = WakeTraceJournal.Open(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XGB_P", "wake-trace"),
                delegate(WakeTraceRecord record) {
                WriteLog("despertar: ciclo=" + record.Session.CycleId + "; " + record.Summary() + "; classificacao=" + record.Classification +
                    "; evento=" + (record.Windows == null ? "indisponivel" : record.Windows.RecordId.ToString()) + "; completo=" + record.ObservationComplete +
                    (record.EventReadError == null ? "" : "; erro=" + record.EventReadError));
                Dispatch(delegate { if (!disposed) RebuildWakeScreens(); });
            }, WriteLog);
            try { powerEvents = new PowerEvents(TvExternalSuspend, TvResume, TvDisplayEvent, TvSessionEnding, TvNetworkEvent); }
            catch (Exception ex) { WriteLog("energia: observacao indisponivel | " + ex.GetType().Name); }
            StartTvHost();
        }
        void StartTvHost()
        {
            if (verify || tvStopping || tvBusy || tvService != null) return;
            tvBusy = true; tvInitError = null;
            Task.Run(delegate {
                TvService service = null; string error = null;
                try { service = TvService.Open(TvStore.Folder, TvSetup.Folder); }
                catch (Exception ex) { error = ex is FileNotFoundException || ex is DirectoryNotFoundException ?
                    "A configuração do Orchestrator não foi encontrada neste usuário." :
                    "Não foi possível carregar a integração. Confira se o Orchestrator está encerrado e se a configuração" +" está acessível."; WriteLog(
                        "tv: falha ao carregar | " + ex.GetType().Name); }
                lock (tvHostGate)
                {
                    if (tvStopping) { if (service != null) service.CloseAsync().GetAwaiter().GetResult(); return; }
                    tvService = service;
                }
                Dispatch(delegate {
                    tvBusy = false; tvInitError = error;
                    if (disposed) return;
                    if (service != null)
                    {
                    tvConfig = service.Configuration; tvCommand = service.SendAsync; tvSave = service.SaveOptionsAsync; tvRuntime = delegate {
                        return service.Runtime; };
                        tvRead = delegate { return TvStatusReader.Read(service.Configuration, service.AuthorizationKnown); };
                        service.AutomaticResult += TvAutomaticResult;
                    WriteLog(service.Imported ? "tv: configuracao importada para o XGB_P; automacao inicialmente desligada" :
                        "tv: configuracao propria carregada");
                        service.Startup();
                    }
                    else WriteLog("tv: inicializacao indisponivel; nenhum comando automatico");
                    tvState = null; tvReadAt = DateTime.MinValue; RebuildTvScreens();
                });
            });
        }
        static FeedbackKind TvKind(TvReply reply)
        {
            if (reply.Outcome == TvOutcome.Confirmed) return FeedbackKind.Success;
            if (reply.Outcome == TvOutcome.PersistenceFailure || reply.Outcome == TvOutcome.QueryFailed) return FeedbackKind.Error;
            if (reply.Outcome == TvOutcome.NoAction) return FeedbackKind.Info;
            return FeedbackKind.Warning;
        }
        static string TvReplyText(TvReply reply)
        {
            if (reply == null) return "TV: resultado não confirmado.";
            return "TV: " + reply.Detail + (reply.Delivery == DeliveryStatus.PossiblySent ? " Entrega possível; não repetir o comando." :
                reply.Delivery == DeliveryStatus.Sent && reply.Outcome != TvOutcome.Confirmed ? " Enviado; confirme a tela." : "");
        }
        void TvAutomaticResult(TvReply reply)
        {
            WriteLog(TvReplyText(reply));
            Dispatch(delegate {
                if (disposed) return;
                RecordHistory("5/tv", TvReplyText(reply), reply.Outcome == TvOutcome.Disabled ? FeedbackKind.Info : TvKind(reply));
                if (tvService != null) tvConfig = tvService.Configuration;
                RebuildTvScreens();
            });
        }
        void SaveResumeJournal()
        {
            var snapshot = resumeGesture.Snapshot;
            long version = Interlocked.Increment(ref resumeJournalVersion);
            if (!verify) Task.Run(delegate {
                try {
                    var trace = wakeTrace; if (trace != null) trace.Observe(snapshot);
                    lock (resumeJournalGate) { if (version != Interlocked.Read(ref resumeJournalVersion)) return;
                    TvJson.Write(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XGB_P", "settings",
                        "wake-session.json"), snapshot,
                        CancellationToken.None); } }
                catch (Exception ex) { WriteLog("retomada: diario indisponivel | " + ex.GetType().Name); }
            });
        }
        void TvResume()
        {
            if (!resumeGesture.Resume()) return;
            if (controller != null) controller.ResumeScanning();
            var service = tvService; if (service != null) service.Resume();
            SaveResumeJournal();
            Dispatch(delegate { ClearHeld(); ClearGuide(); RecordHistory("5/wake", "PC retomado; aguardando um PS válido por até 20 s.", FeedbackKind.Info); });
        }
        bool HandleResumeGuide(XGBP.Input.ButtonEvent e)
        {
            if (e.Button != "PS" || verify && !verifyResumeInput) return false;
            bool first; bool consumed = resumeGesture.Handle(e.Device, e.Down, e.Gesture == "SEGURADO", new DateTimeOffset(e.Time), out first);
            if (first)
            {
                var service = tvService; if (service != null) service.VerifiedPs();
                RecordHistory("5/wake", "PS válido após a suspensão; aperto consumido sem abrir a barra.", FeedbackKind.Info);
                WriteLog("retomada: PS valido; sem ativar a barra"); SaveResumeJournal();
                if (tvConfig != null && tvConfig.WakeSoundEnabled) PlayWakeSound();
            }
            return consumed;
        }
        bool verifyResumeInput = false;
        static readonly string WakeSoundFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XGB_P", "sons",
            "ps5-wake.wav");
        void PlayWakeSound()
        {
            if (verify || !File.Exists(WakeSoundFile)) return;
            Task.Run(delegate { try { using (var sound = new System.Media.SoundPlayer(WakeSoundFile)) sound.PlaySync(); } catch (Exception ex) { WriteLog(
                "retomada: som indisponivel | " + ex.GetType().Name); } });
        }
        void TvBeforePower(SystemPowerAction action, bool pad)
        {
            if (verify) return;
            if (action == SystemPowerAction.Suspend)
            {
                var service = tvService;
                if (service != null) service.BeforeSuspendAsync(true, pad).GetAwaiter().GetResult();
                resumeGesture.Suspend(); Interlocked.Exchange(ref barSuspendArmed, 1); SaveResumeJournal();
            }
        }
        void TvExternalSuspend()
        {
            if (Interlocked.Exchange(ref barSuspendArmed, 0) == 1) return;
            resumeGesture.Suspend(); SaveResumeJournal();
            var service = tvService;
            if (service != null) service.BeforeSuspendAsync(false, false).Wait(2800);
        }
        void TvDisplayEvent() { var s = tvService; if (s != null) s.DisplayChanged(); }
        void TvNetworkEvent() { var s = tvService; if (s != null) s.NetworkRecovered(); }
        void TvSessionEnding() { var s = tvService; if (s != null) s.SessionEnding().Wait(2800); }
        void TvPowerFailed()
        {
            Interlocked.Exchange(ref barSuspendArmed, 0); resumeGesture.CancelSuspend(); SaveResumeJournal();
            var s = tvService; if (s != null) s.SendAsync(TvRequestKind.CancelSuspend).ContinueWith(delegate(Task<TvReply> result) {
                if (result.IsFaulted) WriteLog("tv: cancelamento da suspensao sem confirmacao");
            }, TaskScheduler.Default);
        }
        void StopTvIntegration()
        {
            lock (tvHostGate) tvStopping = true;
            if (powerEvents != null) { powerEvents.Dispose(); powerEvents = null; }
            var service = tvService; if (service != null) { service.AutomaticResult -= TvAutomaticResult; service.CloseAsync().GetAwaiter().GetResult(); }
        }
    }
}

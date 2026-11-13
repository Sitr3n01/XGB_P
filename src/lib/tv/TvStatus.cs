// Estado da TV Samsung para as Configuracoes (ADR 0110, fase 2): junta configuracao importada, tela (CCD), rede e o
// Samsung TV Orchestrator (processo e entrada de inicializacao). Somente leitura; roda em worker, fora da interface.
// A energia nunca e afirmada: rede acessivel nao prova tela ligada.
using System;
using System.Diagnostics;
using Microsoft.Win32;
using XGBP.Desktop;

namespace XGBP.Tv
{
    public sealed class TvStatus
    {
        public TvSetupState Setup = new TvSetupState();
        public TvDisplayState Display = new TvDisplayState();
        public TvNetworkState Network = new TvNetworkState();
        public bool OrchestratorRunning, OrchestratorRegistered;
        public bool? OrchestratorStartup;   // null: formato de aprovacao desconhecido
        public string Error;
        public long ElapsedMs;
        public string Describe()
        {
            return Setup.Describe() + " | " + Display.Describe() + " | " + Network.Describe() + " | orchestrator=" + (OrchestratorRunning ?
                "em execucao" : "parado")
                +
                ", inicializacao=" + (!OrchestratorRegistered ? "sem entrada" : OrchestratorStartup == true ? "ligada" : OrchestratorStartup ==
                    false ? "desligada" : "?")
                    +
                (Error != null ? " | erro=" + Error : "");
        }
    }

    public static class TvStatusReader
    {
        public const string OrchestratorProcess = "SamsungTvOrchestrator", OrchestratorStartupName = "Samsung TV Orchestrator";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ApprovalKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

        public static TvStatus Read()
        { return Read(null, false); }
        public static TvStatus Read(TvCoreConfig config, bool authorized)
        {
            var watch = Stopwatch.StartNew();
            var status = new TvStatus();
            try
            {
                status.Setup = config == null ? TvSetup.Read() : new TvSetupState { Found = true, Schema = 1, Enabled = config.Enabled,
                    Mac = config.Mac, Duid = config.Duid, Ip = config.Ip, Fragment = config.Fragment, Model = config.Model,
                    KeepHdrOff = config.KeepHdrOff, Credential = authorized ? TvCredential.Protected : TvCredential.Missing };
                status.Display = TvDisplay.Read(status.Setup.Fragment);
                status.Network = status.Setup.Found ? TvProbe.Probe(status.Setup.Ip, status.Setup.Mac, status.Setup.Duid, 1000, 2000)
                    : new TvNetworkState { Status = TvNetwork.NotConfigured, Detail = "sem configuracao" };
                ReadOrchestrator(status);
            }
            catch (Exception ex) { status.Error = ex.Message; }
            status.ElapsedMs = watch.ElapsedMilliseconds;
            return status;
        }

        static void ReadOrchestrator(TvStatus status)
        {
            var running = Process.GetProcessesByName(OrchestratorProcess);
            status.OrchestratorRunning = running.Length > 0;
            foreach (var p in running) p.Dispose();
            using (var run = Registry.CurrentUser.OpenSubKey(RunKey))
                status.OrchestratorRegistered = run != null && run.GetValue(OrchestratorStartupName) is string;
            if (!status.OrchestratorRegistered) return;
            using (var approval = Registry.CurrentUser.OpenSubKey(ApprovalKey))
                status.OrchestratorStartup = StartupRegistration.Approval(approval == null ? null : approval.GetValue(OrchestratorStartupName) as byte[]);
        }
    }
}

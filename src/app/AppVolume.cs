// Ao abrir a barra, o som do app em foco (normalmente o jogo) cai pela metade e volta ao fechar (ADR 0092).
// So o volume das sessoes desse app no mixer do Windows; o volume geral e os outros apps ficam como estao.
// O Windows guarda o volume de cada app: um diario local devolve o original se a barra cair com o som reduzido
// ou se o app fechar antes da restauracao.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using XGBP.Audio;
using XGBP.Games;

namespace XGBP.Design
{
    [DataContract]
    sealed class AppVolumeEntry
    {
        [DataMember] public string DeviceId, SessionId, SessionKey;
        [DataMember] public float Original, Target;
        [DataMember] public long SavedUtc;
        public float LastSet; // ultimo nivel aplicado pela barra; fora do diario
    }
    [DataContract]
    sealed class AppVolumeJournal
    {
        [DataMember(IsRequired = true)] public int Version = 1;
        [DataMember] public List<AppVolumeEntry> Entries;
    }

    sealed partial class Preview
    {
        const float AppVolumeFactor = .5f, AppVolumeTolerance = .02f;
        const int AppVolumeSteps = 5, AppVolumeStepMs = 30;
        static readonly string AppVolumeJournalFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XGB_P",
            "settings", "volume-reduzido.json");
        readonly object appVolumeLock = new object();
        // Estado do worker, so dentro de appVolumeLock: sessoes reduzidas agora e sessoes que sairam antes de restaurar.
        List<AppVolumeEntry> appVolumeDucked = new List<AppVolumeEntry>(), appVolumePending = new List<AppVolumeEntry>();
        ICollection<uint> appVolumePids; string appVolumeExe;
        int appVolumeGeneration;
        Timer appVolumeRecovery;
        // Defaults seguros (sem audio real) ate StartAppVolume ligar o backend no fluxo normal.
        Func<ICollection<uint>, string, List<SessionLevel>> appVolumeRead = delegate { return new List<SessionLevel>(); };
        Func<IList<SessionLevel>, int> appVolumeWrite = delegate { return 0; };
        Action<Action> appVolumeRun = delegate(Action work) { work(); };
        Action<int> appVolumeSleep = delegate { };
        string appVolumeJournal;

        void StartAppVolume()
        {
            if (verify) { appVolumeJournal = Path.Combine(Output, "app-volume-tests", "volume-reduzido.json"); return; }
            appVolumeJournal = AppVolumeJournalFile;
            appVolumeRead = CoreAudio.SessionLevels; appVolumeWrite = CoreAudio.SetSessionLevels;
            appVolumeRun = delegate(Action work) { Task.Factory.StartNew(delegate { try { work(); } catch (Exception ex) { WriteLog("volume do app: falha | " +
                ex.Message); } }); };
            appVolumeSleep = Thread.Sleep;
            if (File.Exists(appVolumeJournal)) appVolumeRun(delegate { lock (appVolumeLock) { LoadAppVolumeJournal(); RecoverAppVolume("abertura do XGB_P");
                } });
        }

        // UI: o app da frente antes de abrir. Com janela de jogo, todos os processos do jogo (lancador e filhos).
        void ReduceAppVolumeOnOpen()
        {
            if (launchPreferences.KeepAppVolume || previousWindow == null || previousWindow.Window == null) return;
            var w = previousWindow.Window;
            if (w.ProcessId == 0 || w.ProcessId == OwnProcessId) return;
            var pids = new HashSet<uint> { w.ProcessId };
            var game = GameForWindow(w);
            if (game != null) foreach (uint pid in game.Pids) pids.Add(pid);
            pids.Remove(OwnProcessId);
            string exe = w.ExecutablePath;
            int generation = Interlocked.Increment(ref appVolumeGeneration);
            appVolumeRun(delegate { ReduceAppVolume(generation, pids, exe); });
        }
        void RestoreAppVolumeOnClose(string reason)
        {
            int generation = Interlocked.Increment(ref appVolumeGeneration); // interrompe uma reducao ainda em rampa
            appVolumeRun(delegate {
                lock (appVolumeLock) { if (generation == Thread.VolatileRead(ref appVolumeGeneration)) RestoreDucked(reason); }
            });
        }
        void RestoreAppVolumeNow()
        {
            Interlocked.Increment(ref appVolumeGeneration);
            try { lock (appVolumeLock) RestoreDucked("XGB_P encerrando"); }
            catch (Exception ex) { WriteLog("volume do app: restauracao ao encerrar falhou | " + ex.Message); }
            if (appVolumeRecovery != null) { appVolumeRecovery.Dispose(); appVolumeRecovery = null; }
        }

        void ReduceAppVolume(int generation, ICollection<uint> pids, string exe)
        {
            lock (appVolumeLock)
            {
                if (generation != Thread.VolatileRead(ref appVolumeGeneration)) return;
                RestoreDucked("nova abertura"); // nunca reduzir sobre um nivel ja reduzido
                RecoverAppVolume("antes de reduzir");
                if (generation != Thread.VolatileRead(ref appVolumeGeneration)) return;
                var entries = new List<AppVolumeEntry>();
                long now = DateTime.UtcNow.ToFileTimeUtc();
                foreach (var s in appVolumeRead(pids, exe))
                {
                    if (s.Level <= .01f) continue; // app ja mudo no mixer: nada a reduzir
                    entries.Add(new AppVolumeEntry { DeviceId = s.DeviceId, SessionId = s.SessionId, SessionKey = s.SessionKey,
                        Original = s.Level, Target = s.Level * AppVolumeFactor, LastSet = s.Level, SavedUtc = now });
                }
                if (entries.Count == 0) return;
                appVolumeDucked = entries; appVolumePids = pids; appVolumeExe = exe;
                SaveAppVolumeJournal(); // antes da rampa: uma queda no meio ainda e recuperavel
                bool complete = RampAppVolume(entries, false, generation);
                WriteLog("volume do app: reduzido ao abrir a barra | sessoes=" + entries.Count + " | " + Math.Round(entries[0].Original * 100) + "% -> " +
                    Math.Round(entries[0].LastSet * 100) + "%" + (complete ? "" : " | interrompido pelo fechamento"));
            }
        }
        // Somente dentro de appVolumeLock. Sessao ajustada pelo usuario com a barra aberta fica como ele deixou.
        void RestoreDucked(string reason)
        {
            if (appVolumeDucked.Count == 0) return;
            var current = appVolumeRead(appVolumePids, appVolumeExe);
            var back = new List<AppVolumeEntry>(); int changed = 0, gone = 0;
            foreach (var e in appVolumeDucked)
            {
                var live = current.Find(s => s.DeviceId == e.DeviceId && s.SessionId == e.SessionId);
                if (live == null) { gone++; appVolumePending.Add(e); continue; } // app saiu: o Windows guardou o nivel reduzido
                if (Math.Abs(live.Level - e.LastSet) > AppVolumeTolerance) { changed++; continue; }
                back.Add(e);
            }
            RampAppVolume(back, true, -1);
            appVolumeDucked = new List<AppVolumeEntry>();
            SaveAppVolumeJournal();
            WriteLog("volume do app: restaurado | " + reason + " | sessoes=" + back.Count + "; ajustadas pelo usuario=" + changed + "; encerradas=" + gone);
            if (gone > 0) ScheduleAppVolumeRecovery();
        }
        // Rampa curta (5 passos de 30 ms). So a reducao e interrompida; a restauracao sempre termina.
        bool RampAppVolume(List<AppVolumeEntry> entries, bool restore, int generation)
        {
            if (entries.Count == 0) return true;
            var start = entries.ConvertAll(e => e.LastSet);
            for (int step = 1; step <= AppVolumeSteps; step++)
            {
                if (generation >= 0 && generation != Thread.VolatileRead(ref appVolumeGeneration)) return false;
                var items = new List<SessionLevel>();
                for (int i = 0; i < entries.Count; i++)
                {
                    float goal = restore ? entries[i].Original : entries[i].Target;
                    float level = step == AppVolumeSteps ? goal : start[i] + (goal - start[i]) * step / AppVolumeSteps;
                    items.Add(new SessionLevel { DeviceId = entries[i].DeviceId, SessionId = entries[i].SessionId, Level = Math.Max(0, Math.Min(1, level)) });
                }
                appVolumeWrite(items);
                for (int i = 0; i < entries.Count; i++) entries[i].LastSet = items[i].Level;
                if (step < AppVolumeSteps) appVolumeSleep(AppVolumeStepMs);
            }
            return true;
        }
        // Somente dentro de appVolumeLock. Sessao do mesmo app (identificador persistente) com o nivel que a barra deixou
        // volta ao original; qualquer outro nivel e escolha do usuario. Entradas sem app aberto esperam ate 7 dias.
        void RecoverAppVolume(string reason)
        {
            if (appVolumePending.Count == 0) return;
            var all = appVolumeRead(null, null);
            var fix = new List<SessionLevel>(); var waiting = new List<AppVolumeEntry>(); int restored = 0, kept = 0;
            long expired = DateTime.UtcNow.AddDays(-7).ToFileTimeUtc();
            foreach (var e in appVolumePending)
            {
                if (e.SavedUtc < expired) continue;
                var live = all.FindAll(s => s.DeviceId == e.DeviceId && (s.SessionId == e.SessionId || s.SessionKey == e.SessionKey));
                if (live.Count == 0) { waiting.Add(e); continue; }
                float low = Math.Min(e.Target, e.Original) - AppVolumeTolerance, high = Math.Max(e.Target, e.Original) + AppVolumeTolerance;
                foreach (var s in live)
                {
                    if (s.Level >= low && s.Level <= high) { fix.Add(new SessionLevel { DeviceId = s.DeviceId, SessionId = s.SessionId, Level = e.Original });
                        restored++; }
                    else kept++;
                }
            }
            if (fix.Count > 0) appVolumeWrite(fix);
            appVolumePending = waiting; SaveAppVolumeJournal();
            if (restored + kept > 0) WriteLog("volume do app: recuperado | " + reason + " | restauradas=" + restored + "; ajustadas pelo usuario=" + kept +
                "; aguardando=" + waiting.Count);
        }
        void ScheduleAppVolumeRecovery()
        {
            if (verify || appVolumeRecovery != null) return;
            appVolumeRecovery = new Timer(delegate {
                try
                {
                    lock (appVolumeLock)
                    {
                        RecoverAppVolume("app reaberto");
                        if (appVolumePending.Count == 0 && appVolumeRecovery != null) { appVolumeRecovery.Dispose(); appVolumeRecovery = null; }
                    }
                }
                catch (Exception ex) { WriteLog("volume do app: recuperacao falhou | " + ex.Message); }
            }, null, 5000, 5000);
        }
        void LoadAppVolumeJournal()
        {
            try
            {
                var journal = GameFiles.Read<AppVolumeJournal>(appVolumeJournal);
                if (journal.Version != 1 || journal.Entries == null) throw new InvalidDataException("Diario de volume invalido.");
                appVolumePending.AddRange(journal.Entries.FindAll(e => e != null && e.DeviceId != null && (e.SessionId != null || e.SessionKey != null)));
                if (appVolumePending.Count > 0) ScheduleAppVolumeRecovery();
            }
            catch (Exception ex) { WriteLog("volume do app: diario ilegivel preservado ate a proxima gravacao | " + ex.Message); }
        }
        void SaveAppVolumeJournal()
        {
            var entries = new List<AppVolumeEntry>(appVolumePending); entries.AddRange(appVolumeDucked);
            try
            {
                if (entries.Count == 0)
                {
                    foreach (string path in new[] { appVolumeJournal, appVolumeJournal + ".bak" }) if (File.Exists(path)) File.Delete(path);
                    return;
                }
                GameFiles.Write(appVolumeJournal, new AppVolumeJournal { Entries = entries });
            }
            catch (Exception ex) { WriteLog("volume do app: diario nao gravado | " + ex.Message); }
        }

        void AddAppVolumeSetting()
        {
            var toggle = ToggleSetting(body, "Abaixar o som do app ao abrir", !launchPreferences.KeepAppVolume, "app-volume", ToggleAppVolumeSetting);
            toggle.ToolTip = "Ao abrir a barra, o som do jogo ou app em foco cai pela metade e volta ao fechar. Ajustes feitos no mixer com a barra aberta " +
                "são mantidos.";
        }
        void ToggleAppVolumeSetting()
        {
            var next = launchPreferences.Copy(); next.KeepAppVolume = !launchPreferences.KeepAppVolume;
            try
            {
                launchWrite(next); launchPreferences = next; RebuildSettings("app-volume");
                if (next.KeepAppVolume) RestoreAppVolumeOnClose("opcao desligada");
                Feedback(next.KeepAppVolume ? "Salvo. O som dos apps não muda ao abrir a barra." :
                    "Salvo. Na próxima abertura, o som do app em foco cai pela metade enquanto a barra estiver aberta.");
                WriteLog("volume do app: preferencia abaixar ao abrir=" + !next.KeepAppVolume);
            }
            catch (Exception ex) { RebuildSettings("app-volume"); Feedback("Não foi possível salvar a preferência de volume.", FeedbackKind.Error);
                WriteLog("volume do app: preferencia falhou | " + ex.Message); }
        }
    }
}

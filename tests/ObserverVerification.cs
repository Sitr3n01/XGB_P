// Carga real de leitura do observador, com historico isolado. Nao mede a residencia completa nem opera jogos.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using XGBP.Desktop;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        public void VerifyObserverLive()
        {
            string fixture = Path.Combine(Output, "observer-fixture", Guid.NewGuid().ToString("N"), "history.json");
            var catalog = SteamCatalog.Read(GameFiles.Read<GameSettings>(Path.Combine(GameFiles.Folder, "catalog.json")));
            var sessions = new GameSessions(new GameHistory(fixture));
            var samples = new List<double>();
            int count = 0, windows = 0;
            using (var me = Process.GetCurrentProcess())
            {
                for (int n = 0; n < 3; n++)
                {
                    var warm = GameProcess.Read();
                    sessions.Update(catalog, warm, DesktopWindows.Read((uint)me.Id), 0, DateTime.UtcNow);
                }
                GC.Collect(); GC.WaitForPendingFinalizers(); me.Refresh();
                var started = Stopwatch.StartNew();
                var cpu = me.TotalProcessorTime;
                int handles = me.HandleCount;
                for (int i = 0; i < 20; i++)
                {
                    var clock = Stopwatch.StartNew();
                    var processes = GameProcess.Read();
                    var visible = DesktopWindows.Read((uint)me.Id);
                    sessions.Update(catalog, processes, visible, 0, DateTime.UtcNow);
                    clock.Stop(); samples.Add(clock.Elapsed.TotalMilliseconds);
                    count = processes.Count; windows = visible.Count;
                    Thread.Sleep(1500);
                }
                samples.Sort(); GC.Collect(); GC.WaitForPendingFinalizers(); me.Refresh();
                double p95 = samples[(int)Math.Ceiling(samples.Count * .95) - 1];
                double cpuPercent = (me.TotalProcessorTime - cpu).TotalMilliseconds / started.Elapsed.TotalMilliseconds * 100;
                if (p95 >= 1000) throw new InvalidOperationException("Observador excedeu 1000 ms no percentil 95.");
                if (cpuPercent >= 5) throw new InvalidOperationException("Observador excedeu 5% de um nucleo nesta carga de leitura.");
                if (me.HandleCount - handles > 16) throw new InvalidOperationException("Observador reteve mais de 16 handles apos aquecimento e coleta.");
                var report = new[] {
                    "OK: 3 coletas de aquecimento + 20 medidas; somente leitura; historico isolado; GC antes/depois para handles.",
                    "Processos=" + count + "; janelas=" + windows + "; jogos no catalogo=" + catalog.Entries.Count,
                    "p95_ms=" + p95.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        "; CPU_percentual_de_um_nucleo=" + cpuPercent.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                    "handles_antes=" + handles + "; handles_depois=" + me.HandleCount +
                        "; duracao_s=" + started.Elapsed.TotalSeconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                    "Limites: nao mede o residente com HID, FSE, jogos, UIA, animacoes ou uso prolongado. Nenhum app iniciado/fechado, nenhum foco solicitado."
                };
                File.WriteAllLines(Path.Combine(Output, "observador-real.txt"), report, Encoding.UTF8);
            }
        }
    }
}

// Regressao do identificador de retomadas: eventos, tempo e persistencia substituidos; nenhum wake real.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using XGBP.Power;
using XGBP.Tv;

namespace XGBP.Design
{
    static class WakeTraceSuite
    {
        public static int Run(string output)
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool value, string why) { checks++; if (!value) throw new Exception("Registro de retomada: " + why); };
            var at = new DateTimeOffset(2026, 11, 8, 5, 33, 26, TimeSpan.Zero);
            var session = new ResumeSession { CycleId = Guid.NewGuid().ToString("N"), SuspendedAt = at.AddMinutes(-36), ResumedAt = at };
            var unknown = new WindowsWakeEvent { RecordId = 60378, SourceType = 0, SleepAt = session.SuspendedAt.Value, WakeAt = at };
            check(WindowsWakeReader.Matches(unknown, session), "evento pertence ao ciclo");
            check(!WindowsWakeReader.Matches(unknown, new ResumeSession { ResumedAt = at.AddMinutes(2) }), "nao herda evento antigo");
            check(!WindowsWakeReader.Matches(unknown, new ResumeSession { ResumedAt = at, SuspendedAt = at.AddHours(-1) }),
                "horario de sono evita misturar ciclos proximos");
            string xml = "<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'><EventData><Data Name='SleepTime'>2026-11-08T04:57:26Z</Data>" +
                "<Data Name='WakeTime'>2026-11-08T05:33:26Z</Data><Data Name='WakeSourceType'>0</Data><Data Name='WakeSourceText'></Data></EventData></Event>";
            var parsed = WindowsWakeReader.Parse(xml, 60378);
            check(parsed.SourceType == 0 && parsed.SourceText == "" && parsed.RawXml == xml && parsed.WakeAt == at,
                "XML original e Unknown preservados sem localizar texto");
            bool bad = false; try { WindowsWakeReader.Parse("<Event><EventData/></Event>", 2); } catch (Exception) { bad = true; }
            check(bad, "evento malformado nao vira Unknown valido");
            var writes = new List<WakeTraceRecord>(); int reads = 0;
            var journal = new WakeTraceJournal(delegate { reads++; return reads < 3 ? null : unknown; }, r => writes.Add(r), null,
                why => { throw new Exception(why); }, ms => Task.FromResult(0));
            journal.Observe(session);
check(reads == 3 && journal.Latest.Windows.RecordId == 60378 && journal.Latest.ObservationComplete, "evento atrasado recuperado e janela concluida");
            check(journal.Latest.Classification == "WindowsUnknownNoPsObserved" && !journal.Latest.PhysicalCauseConfirmed,
                "Unknown sem PS continua sem atribuicao fisica");
            var ps = session.Copy(); ps.PsConsumed = true; ps.PsAt = at.AddMilliseconds(125); ps.ResumeToPsMs = 125; ps.PsDevice = "DualSense-BT-fixture";
            journal.Observe(ps);
            check(journal.Latest.Classification == "WindowsUnknownPsObserved" && journal.Latest.Session.PsDevice == "DualSense-BT-fixture" &&
                !journal.Latest.PhysicalCauseConfirmed,
                "PS correlacionado sem atribuir causa fisica");
            journal.Observe(session);
            check(journal.Latest.Session.PsConsumed && journal.Latest.Session.ResumeToPsMs == 125, "worker antigo nao apaga PS confirmado");
            var newer = new ResumeSession { CycleId = Guid.NewGuid().ToString("N"), SuspendedAt = at.AddHours(1), ResumedAt = at.AddHours(2) };
            journal.Observe(newer); journal.Observe(ps);
            check(journal.Latest.Session.CycleId == newer.CycleId, "worker de outro ciclo nao troca a ultima retomada");
            check(writes.Exists(r => r.Session.CycleId == session.CycleId) && writes.Exists(r => r.Session.CycleId == newer.CycleId),
                "historico preserva ambos os identificadores");
            check(journal.Latest.Classification == "WindowsEventUnavailableNoPsObserved", "evento ausente nao vira Unknown");
            var close = new ResumeSession { CycleId = Guid.NewGuid().ToString("N"), SuspendedAt = session.SuspendedAt, ResumedAt = at.AddSeconds(2) };
            journal.Observe(close);
            check(writes.FindLast(r => r.Session.CycleId == close.CycleId).Windows == null, "mesmo evento nao e atribuido a dois ciclos");
            var known = new WakeTraceRecord { Session = ps, Windows = new WindowsWakeEvent { SourceType = 1 }, UpdatedAt = at };
            check(WakeTraceRecord.Classify(known) == "WindowsReportedSource" && known.Summary().Contains("botão de energia"),
                "PS posterior nao substitui origem informada pelo Windows");
            var failed = new WakeTraceJournal(s => { throw new IOException("teste de leitura"); }, r => writes.Add(r), null, null, ms => Task.FromResult(0));
            failed.Observe(session);
            check(failed.Latest.EventReadAttempts == 4 && failed.Latest.EventReadError.Contains("teste de leitura") && failed.Latest.Windows == null,
                "falhas tentadas novamente e preservadas sem inventar origem");
            var clock = at; long tick = 1;
            var gesture = new ResumeGesture(() => clock, () => tick, start => TimeSpan.FromMilliseconds(tick - start));
            gesture.Suspend(); string first = gesture.Snapshot.CycleId; gesture.Resume();
            check(first == gesture.Snapshot.CycleId && first.Length == 32, "mesmo identificador na suspensao e retomada");
            gesture.Suspend(); check(first != gesture.Snapshot.CycleId, "novo identificador por ciclo");
            string json = TvJson.Serialize(journal.Latest); var round = TvJson.Parse<WakeTraceRecord>(json);
            check(round.Session.CycleId == journal.Latest.Session.CycleId && !round.PhysicalCauseConfirmed, "registro serializavel e limites preservados");
            File.WriteAllText(Path.Combine(output, "despertar-correlacao-verificacao.txt"), "OK: " + checks +
                " verificacoes do identificador de retomadas. Eventos, leitura, tempo e armazenamento substituidos; n" +"enhuma API de energia.\r\n",
                Encoding.UTF8);
            return checks;
        }
    }
}

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
        public void VerifyWakeLive()
        {
            var watch = Stopwatch.StartNew();
            var state = SafeWakeRead(WakeReadinessReader.Read);
File.WriteAllText(Path.Combine(Output, "despertar-real.txt"), "Despertar pelo PS (ADR 0102): " + state.Describe() + ".\r\nLinha nas Configuracoes: " +
            WakeRowText(state) + ". Confirmacao de Suspender: " + (WakeSuspendText(state) ?? "(sem linha)") + "\r\nLeitura em " + watch.ElapsedMilliseconds +
                " ms. Diagnostico somente leitura: registro, cfgmgr32 e WMI root\\wmi; nada alterado.\r\n", Encoding.UTF8);
        }
    }
}

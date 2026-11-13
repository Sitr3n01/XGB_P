// Despertar pelo PS (ADR 0102) com leitura substituida: nenhum registro, WMI ou dispositivo real e consultado.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XGBP.Desktop;
using XGBP.Power;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyWake()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool ok, string why) { checks++; if (!ok) throw new Exception("Despertar pelo PS: " + why); };
            var oldRead = wakeRead; var oldState = wakeState; var oldAt = wakeReadAt;
            byte[] full = { 0, 0, 0x1F, 0xFC, 0, 0, 5, 0x40, 0, 0, 0x1F, 0xFC, 0, 0, 5, 0x80, 0, 0, 0x1F, 0xFC, 0, 0, 5, 0x08 };
            byte[] original = { 0, 0, 0x1F, 0xFC, 0, 0, 5, 0x40, 0, 0, 0x1F, 0xFC, 0, 0, 5, 0x80 };
            byte[] gamepadOnly = { 0, 0, 0x1F, 0xFC, 0, 0, 5, 0x08 };
            byte[] extra = new byte[32]; Array.Copy(full, extra, 24); extra[26] = 0x1F; extra[27] = 0xFC; extra[30] = 5; extra[31] = 0x10;
            check(WakeReadinessReader.KeyHasEntries(full) && !WakeReadinessReader.KeyHasEntries(extra), "tres pares exatos; entrada adicional recusada");
            var reversed = (byte[])full.Clone(); Array.Reverse(reversed);
            check(!WakeReadinessReader.KeyHasEntries(reversed), "ordem dos bytes preservada");
            check(!WakeReadinessReader.KeyHasEntries(original) && !WakeReadinessReader.KeyHasEntries(gamepadOnly) &&
                !WakeReadinessReader.KeyHasEntries(new byte[] { 1, 2, 3 }) && !WakeReadinessReader.KeyHasEntries(null),
                    "sem gamepad, sem teclado/apontador ou malformada"
                    );
            Func<bool, bool, bool, int, bool, WakeStatus> classify = delegate(bool key, bool correct, bool paired, int remote, bool present) {
                return WakeReadinessReader.Classify(new WakeReadiness { KeyPresent = key, KeyCorrect = correct, Paired = paired, RemoteWake =
                    remote, Present = present
                    }); };
            check(classify(false, false, true, 1, true) == WakeStatus.NotConfigured && classify(true, false, true, 1, true) == WakeStatus.Divergent &&
                classify(true, true, false, -1, false) == WakeStatus.NeverPaired && classify(true, true, true, -1, true) == WakeStatus.NotArmed &&
                classify(true, true, true, 0, true) == WakeStatus.NotArmed && classify(true, true, true, 1, false) == WakeStatus.ConnectOnce &&
                classify(true, true, true, 1, true) == WakeStatus.Ready, "classificacao dos elos");
            check(WakeReadinessReader.Classify(new WakeReadiness { KeyPresent = true, KeyCorrect = true, Paired = true, Present = true, RemoteWake = 1, Error =
                "leitura parcial" }) == WakeStatus.Unknown,
                "erro critico nao vira Pronto");
            var selected = new WakeReadiness();
            WakeReadinessReader.ConsiderController(selected, "antigo", 1, false); WakeReadinessReader.ConsiderController(selected, "atual", 0, true);
            check(selected.Instance == "atual" && selected.RemoteWake == 0 && selected.Present, "no presente prevalece sobre historico armado");
            WakeReadinessReader.ConsiderController(selected, "outro-antigo", 1, false);
            check(selected.Instance == "atual", "no antigo posterior tambem nao mascara o presente");
            checks += WakeTraceSuite.Run(Output);

            var fixture = new WakeReadiness { Status = WakeStatus.ConnectOnce, KeyPresent = true, KeyCorrect = true, Paired = true, RemoteWake = 1 };
            fixture.OthersAwake.AddRange(new[] { "G512 RGB MECHANICAL GAMING KEYBOARD", "Razer DeathAdder V2" });
            int reads = 0;
            try
            {
                wakeRead = delegate { reads++; return fixture; };
                wakeState = null; wakeReadAt = DateTime.MinValue;
                Show(); SetPanel(5);
                check(reads == 1 && ActionLabel("wake-ps") == "Acordar pelo PS · Ausente agora", "Configuracoes leem ao abrir e mostram o estado");
                check(BodyHas("O controle está ausente agora. A permissão de despertar está gravada, mas esta leitura não confirma " +
                    "se o Bluetooth continua armado. Conecte o controle com o PC ligado para conferir." +
                    " Também acordam o PC: G512 RGB MECHANICAL GAMING KEYBOARD, Razer DeathAdder V2."), "o que falta e quem mais acorda o PC");
                SetPanel(5); check(reads == 1, "leitura recente nao repete");
                window.UpdateLayout(); SaveRender("configuracoes-despertar");
                fixture = new WakeReadiness { Status = WakeStatus.Ready, KeyPresent = true, KeyCorrect = true, Paired = true, RemoteWake = 1, Present = true };
                FocusHome("wake-ps"); Activate();
                check(reads == 2 && ActionLabel("wake-ps") == "Acordar pelo PS · Pronto" && FocusedTag() == "wake-ps" && BodyHas(
                    "Suspenda e aperte o PS: o PC acorda pelo controle."),
                    "apertar a linha le de novo e mantem o foco");
                var cases = new Dictionary<WakeStatus, string> {
                    { WakeStatus.NotArmed, "Falta armar o controle" }, { WakeStatus.NotConfigured, "Não configurado" },
            { WakeStatus.Divergent, "Configuração divergente" }, { WakeStatus.NeverPaired, "Controle não pareado" }, { WakeStatus.Unknown, "Indisponível" } };
                foreach (var c in cases)
                {
                    fixture = new WakeReadiness { Status = c.Key, Error = c.Key == WakeStatus.Unknown ? "WMI indisponível" : null };
                    FocusHome("wake-ps"); Activate();
                    check(ActionLabel("wake-ps") == "Acordar pelo PS · " + c.Value, "estado " + c.Key);
                }
                check(BodyHas("Não foi possível ler o despertar: WMI indisponível."), "erro de leitura visivel");
                fixture = new WakeReadiness { Status = WakeStatus.Ready, OthersError = "WMI indisponível" };
                FocusHome("wake-ps"); Activate();
                check(BodyHas("Suspenda e aperte o PS: o PC acorda pelo controle. A lista de outros dispositivos está incompleta: W" +"MI indisponível."),
                    "falha secundaria permanece visivel sem invalidar permissao conhecida");

                // Confirmacao de Suspender: diz se o PS acorda; Reiniciar nao fala de despertar.
                fixture = new WakeReadiness { Status = WakeStatus.Ready }; wakeState = null;
                SetPanel(4); RequestSystemPower(SystemPowerAction.Suspend);
                check(BodyHas("O PS do DualSense acorda o PC."), "suspender pronto");
                fixture = new WakeReadiness { Status = WakeStatus.ConnectOnce }; wakeState = null;
                SetPanel(4); RequestSystemPower(SystemPowerAction.Suspend);
                check(BodyHas("O controle está ausente agora; não foi possível confirmar o despertar pelo PS. Use o teclado ou o bo"
                    +"tão de energia se necessário."),
                    "suspender sem inferir historico pela ausencia");
                fixture = new WakeReadiness { Status = WakeStatus.NotConfigured }; wakeState = null;
                SetPanel(4); RequestSystemPower(SystemPowerAction.Suspend);
                check(BodyHas("O PS não acorda o PC (não configurado). Use o teclado ou o botão de energia."), "suspender sem configuracao");
                window.UpdateLayout(); SaveRender("suspender-despertar");
                fixture = new WakeReadiness { Status = WakeStatus.Unknown }; wakeState = null;
                SetPanel(4); RequestSystemPower(SystemPowerAction.Suspend);
                check(!BodyHas("O PS do DualSense acorda o PC.") && WakeSuspendText(fixture) == null, "sem leitura valida, sem linha");
                fixture = new WakeReadiness { Status = WakeStatus.Ready }; wakeState = null;
                SetPanel(4); RequestSystemPower(SystemPowerAction.Restart);
                check(!BodyHas("O PS do DualSense acorda o PC."), "reiniciar nao fala de despertar");
                powerConfirmation = null;
                File.WriteAllText(Path.Combine(Output, "despertar-verificacao.txt"), "OK: " + checks + " verificacoes do despertar pelo PS (ADR 0102). " +
                    "Chave com as tres entradas, malformada ou incompleta; classificacao dos elos; Configuracoes leem ao abrir, nao repetem leitura recente, " +
"mostram o que falta e quem mais acorda o PC, releem pela linha mantendo o foco, todos os estados e e" +"rro; confirmacao de Suspender pronta, " +
                    "sem o controle nesta sessao, sem configuracao, sem leitura valida e Reiniciar sem linha. Leitura sub"
                        +"stituida; nada consultado ou alterado.\r\n",
                        Encoding.UTF8);
            }
            finally
            {
                wakeRead = oldRead; wakeState = oldState; wakeReadAt = oldAt; powerConfirmation = null;
                SetPanel(0);
                if (window.IsVisible) Hide();
            }
        }
    }
}

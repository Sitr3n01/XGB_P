// Regressao do leitor do Guia pelo XInput e dos icones PlayStation/Xbox (ADR 0080). Slots, relogio e gravacao substituidos.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using XGBP.Games;
using XGBP.Input;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyButtonIcons()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool ok, string reason) { checks++; if (!ok) throw new Exception("Icones/XInput: " + reason); };
            var scheme = buttonIcons; bool present = xboxPadPresent; var write = appearanceWrite;
            try
            {
                // Leitor: slots conectados a cada passada, vazios so na varredura lenta; Guia vira PS com duracao na soltura.
                DateTime now = new DateTime(2026, 10, 29, 12, 0, 0);
                var slots = new ushort?[4]; var reads = new int[4];
                var monitor = new XInputGuideMonitor { Now = delegate { return now; } };
                monitor.ReadButtons = delegate(int slot) { reads[slot]++; return slots[slot]; };
                var events = new List<ButtonEvent>(); var lost = new List<string>(); var presence = new List<bool>();
                monitor.ButtonChanged += events.Add; monitor.DeviceLost += lost.Add; monitor.PresenceChanged += presence.Add;
                monitor.Poll(); check(events.Count == 0 && presence.Count == 0 && reads[0] == 1 && reads[3] == 1, "sem controle: uma varredura, nada emitido");
                now = now.AddMilliseconds(8); monitor.Poll(); check(reads[0] == 1, "slot vazio fora da varredura lenta nao e consultado");
                slots[0] = 0x1000; now = now.AddSeconds(2); monitor.Poll();
                check(monitor.AnyConnected && presence.Count == 1 && presence[0] && events.Count == 0, "controle conectado sem Guia apertado");
                slots[0] = 0x1000 | XInputGuideMonitor.GuideMask; now = now.AddMilliseconds(8); monitor.Poll();
                check(events.Count == 1 && events[0].Down && events[0].Button == "PS" && events[0].Device == "XInput#0" && events[0].Gesture == null, "Guia apertado vira PS do XInput");
                now = now.AddMilliseconds(8); monitor.Poll(); check(events.Count == 1, "Guia mantido nao repete");
                slots[0] = 0; now = now.AddMilliseconds(120); monitor.Poll();
                check(events.Count == 2 && !events[1].Down && events[1].DurationMs == 128, "soltura com duracao");
                int emptyReads = reads[1]; now = now.AddMilliseconds(8); monitor.Poll(); check(reads[1] == emptyReads && reads[0] > 3, "controle conectado consultado a cada passada, vazios nao");
                slots[0] = XInputGuideMonitor.GuideMask; now = now.AddMilliseconds(8); monitor.Poll();
                slots[0] = null; now = now.AddMilliseconds(8); monitor.Poll();
                check(lost.Count == 1 && lost[0] == "XInput#0" && !monitor.AnyConnected && presence.Count == 2 && !presence[1] && events.Count == 3, "desconexao avisa perda sem inventar soltura");

                // Icones: Automatico segue o XInput; PlayStation e Xbox fixos; nomes no padrao PlayStation viram os do Xbox.
                buttonIcons = ButtonIconScheme.Auto; xboxPadPresent = false;
                check(!XboxIcons && ButtonLabel("✕") == "✕" && GuideName == "PS", "automatico sem XInput usa PlayStation");
                XboxPadPresence(true);
                check(XboxIcons && ButtonLabel("✕") == "A" && ButtonLabel("○") == "B" && ButtonLabel("□") == "X" && ButtonLabel("△") == "Y" &&
                    ButtonLabel("L1") == "LB" && ButtonLabel("R2") == "RT" && ButtonLabel("R3") == "RS" && ButtonLabel("≡") == "≡" && ButtonLabel("mic") == "mic" &&
                    GuideName == "Botão Xbox", "automatico com XInput usa Xbox; Menu e microfone mantem o desenho");
                var badge = KbControllerBadge(ButtonLabel("□")) as TextBlock;
                check(badge != null && badge.Text == "X" && !(KbControllerBadge("□") is TextBlock), "teclado troca o desenho do quadrado pela letra X");
                buttonIcons = ButtonIconScheme.PlayStation; check(!XboxIcons && ButtonLabel("L2") == "L2", "PlayStation fixo ignora o XInput");
                buttonIcons = ButtonIconScheme.Xbox; XboxPadPresence(false); check(XboxIcons && ButtonLabel("L2") == "LT", "Xbox fixo sem XInput");

                // Configuracoes: ciclo Automatico -> PlayStation -> Xbox -> Automatico, salvo e com a Abertura acompanhando.
                string path = Path.Combine(Output, "appearance-icons-" + Guid.NewGuid().ToString("N"), "appearance.json");
                appearanceWrite = state => GameFiles.Write(path, state);
                buttonIcons = ButtonIconScheme.Auto; Show(); SetPanel(5);
                check(ActionLabel("button-icons") == "Ícones dos botões · Automático (PlayStation)" && BodyStarts("PS ou Win\u00a0+\u00a0G abre a barra"), "entrada mostra o automatico resolvido");
                FocusHome("button-icons"); Activate();
                check(buttonIcons == ButtonIconScheme.PlayStation && ReadAppearancePreferences(path).ButtonIcons == 1 && FocusedTag() == "button-icons", "primeiro toque fixa PlayStation e salva");
                Activate(); check(buttonIcons == ButtonIconScheme.Xbox && ActionLabel("button-icons") == "Ícones dos botões · Xbox" && BodyStarts("Botão Xbox ou Win\u00a0+\u00a0G abre a barra"), "segundo toque fixa Xbox e a Abertura acompanha");
                window.UpdateLayout(); SaveRender("configuracoes-icones-xbox");
                Activate(); check(buttonIcons == ButtonIconScheme.Auto && ReadAppearancePreferences(path).ButtonIcons == 0, "terceiro toque volta ao automatico");
                buttonIcons = ButtonIconScheme.Xbox; RestoreAppearancePreferences(path); check(buttonIcons == ButtonIconScheme.Auto, "leitura restaura o esquema salvo");
                File.WriteAllText(path, "{\"ButtonIcons\":7,\"ReducedMotion\":false,\"Version\":1}", new UTF8Encoding(false));
                RestoreAppearancePreferences(path); check(buttonIcons == ButtonIconScheme.Auto, "valor desconhecido volta ao automatico");
                File.WriteAllText(path, "{\"ReducedMotion\":false,\"Version\":1}", new UTF8Encoding(false));
                buttonIcons = ButtonIconScheme.Xbox; RestoreAppearancePreferences(path); check(buttonIcons == ButtonIconScheme.Auto, "arquivo antigo sem o campo usa automatico");
                File.WriteAllText(Path.Combine(Output, "icones-xinput-verificacao.txt"), "OK: " + checks + " verificacoes. XInput: varredura lenta de slots vazios, consulta continua do conectado, " +
                    "Guia como PS do XInput, sem repeticao, soltura com duracao, desconexao como perda e presenca. Icones: automatico pelo XInput, PlayStation/Xbox fixos, " +
                    "A/B/X/Y, LB/RB, LT/RT, LS/RS, Menu e microfone mantidos, teclado com letras, ciclo nas Configuracoes salvo, Abertura acompanha, valor desconhecido e arquivo antigo. " +
                    "Slots, relogio e gravacao substituidos; nenhum controle real lido.\r\n", Encoding.UTF8);
            }
            finally
            {
                buttonIcons = scheme; xboxPadPresent = present; appearanceWrite = write; SetPanel(0);
            }
        }
    }
}

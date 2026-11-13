// Preferencias da TV: interruptor geral separado dos comandos manuais; mudancas persistidas antes de aplicar.
using System;
using XGBP.Tv;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        // Subopcoes so com a automacao ligada (padrao do painel AMD, aprovado em 07/11). O interruptor "Usar PS" virou a primeira
        // opcao da lista do PS; "Acompanhar as telas" virou as quatro regras com Manter; o estado confirmado esta na linha da Energia.
        void AddTvOptions()
        {
            Caption("AUTOMAÇÃO");
            var enabled = ToggleSetting(body, "Automação da TV", tvConfig.Enabled, "tv-enabled", delegate { TvRun(TvRequestKind.Enable, !tvConfig.Enabled); });
            enabled.IsEnabled = !tvBusy;
if (!tvConfig.Enabled) { ConnectionNote("Desligada: só os comandos acima. Ligada: a barra segue as regras abaixo enquanto estiver aberta."); return; }
            TvChoice("Ao suspender o PC", "tv-suspend", tvConfig.Suspend == TvAction.Off ? 1 : 0, new[] { "Manter", "Desligar a TV" }, (c, v) =>
                c.Suspend = v == 1 ?
                TvAction.Off : TvAction.Keep);
            if (tvConfig.Suspend == TvAction.Off)
            {
                TvToggle("Confirmar ligada pelo controle", "tv-implicit", tvConfig.ImplicitSuspendConfirmation, c => c.ImplicitSuspendConfirmation =
                    !c.ImplicitSuspendConfirmation);
                ConnectionNote("Ao suspender pelo controle, com a Samsung como única tela, conta como \"Ligada\". Se a tela estiver apagada, pode ligá-la.");
            }
            TvChoice("Ao retomar o PC", "tv-resume", (int)tvConfig.Resume, new[] { "Restaurar intenção", "Manter", "Desligar a TV" }, (c, v) => c.Resume =
                (TvResume)v);
            int ps = !tvConfig.PsWakeEnabled ? 0 : (int)tvConfig.DualSense;
            TvChoice("PS na retomada", "tv-ps-action", ps, new[] { "Não fazer nada", "Ligar a TV", "Desligar a TV" },
                (c, v) => { c.PsWakeEnabled = v != 0; c.DualSense = (TvAction)v; });
            if (ps != 0) TvToggle("Som ao reconhecer o PS", "tv-wake-sound", tvConfig.WakeSoundEnabled, c => c.WakeSoundEnabled = !c.WakeSoundEnabled);
            string[] screen = { "Manter", "Ligar a TV", "Desligar a TV" };
            TvChoice("Ao estender as telas", "tv-extend", (int)tvConfig.Extend, screen, (c, v) => { c.Extend = (TvAction)v; c.TopologyEnabled = true; });
            TvChoice("Só a segunda tela", "tv-external", (int)tvConfig.External, screen, (c, v) => { c.External = (TvAction)v; c.TopologyEnabled = true; });
            TvChoice("Ao duplicar as telas", "tv-clone", (int)tvConfig.Clone, screen, (c, v) => { c.Clone = (TvAction)v; c.TopologyEnabled = true; });
            TvChoice("Só a tela do PC", "tv-pc", (int)tvConfig.PcOnly, screen, (c, v) => { c.PcOnly = (TvAction)v; c.TopologyEnabled = true; });
            TvToggle("Manter HDR desligado na Samsung", "tv-hdr", tvConfig.KeepHdrOff, c => c.KeepHdrOff = !c.KeepHdrOff);
        }
        void TvToggle(string title, string tag, bool on, Action<TvCoreConfig> change)
        { ToggleSetting(body, title, on, tag, delegate { TvChange(change, tag); }).IsEnabled = !tvBusy; }
        void TvChange(Action<TvCoreConfig> change, string tag)
        {
            if (tvBusy || tvSave == null) return;
            var next = tvConfig.Copy(); change(next);
            TvWork("Salvando opções da TV…", delegate { return tvSave(next); }, tag, next);
        }
        void TvChoice(string title, string tag, int value, string[] labels, Action<TvCoreConfig, int> set)
        {
            var row = ConnectionButton(body, title, labels[value] + "  ›", tag, delegate { connectionMenu = connectionMenu == tag ? null : tag;
                RebuildSettings(tag);
                });
            row.IsEnabled = !tvBusy;
            if (connectionMenu != tag) return;
            var box = MenuBox();
            for (int i = 0; i < labels.Length; i++)
            { int selected = i; ConnectionButton(box, labels[i], i == value ? "✓" : "", tag + "-" + i, delegate { connectionMenu = null; TvChange(c => set(c,
                selected), tag); }); }
        }
    }
}

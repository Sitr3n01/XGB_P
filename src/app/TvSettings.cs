// TV Samsung nas Configuracoes (ADR 0110, fase 2, porte do Samsung TV Orchestrator): estado da tela, da rede e do
// Orchestrator, somente leitura. Nenhum comando vai a TV nesta fase. A leitura (lib/tv) roda em worker e vale por 30 s;
// apertar a linha le de novo.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using XGBP.Tv;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        const int TvFreshSeconds = 30;
        Func<TvStatus> tvRead = TvStatusReader.Read;
        TvStatus tvState;
        DateTime tvReadAt = DateTime.MinValue;
        bool tvReading;
        string tvLogged;

        void StartTv()
        {
            if (verify) tvRead = delegate { return new TvStatus(); };
            StartTvIntegration();
        }
        static TvStatus SafeTvRead(Func<TvStatus> read)
        {
            try { return read() ?? new TvStatus { Error = "sem leitura" }; }
            catch (Exception ex) { return new TvStatus { Error = ex.Message }; }
        }
        void EnsureTv()
        {
            if (tvReading || tvState != null && (DateTime.UtcNow - tvReadAt).TotalSeconds < TvFreshSeconds) return;
            if (verify) { TvArrived(SafeTvRead(tvRead), false); return; }
            StartTvRead();
        }
        void ForceTv()
        {
            if (verify) { TvArrived(SafeTvRead(tvRead), true); return; }
            if (!tvReading) { tvState = null; RebuildTvScreens(); StartTvRead(); }
        }
        void StartTvRead()
        {
            tvReading = true; var read = tvRead;
            Task.Factory.StartNew(delegate { var state = SafeTvRead(read); Dispatch(delegate { TvArrived(state, true); }); });
        }
        void TvArrived(TvStatus state, bool rebuild)
        {
            tvReading = false; tvState = state; tvReadAt = DateTime.UtcNow;
            string text = state.Describe();   // sem MAC, DUID ou IP
            if (text != tvLogged) { tvLogged = text; WriteLog("tv samsung: " + text); }
            if (rebuild) RebuildTvScreens();
        }
        void RebuildTvScreens()
        {
            if (disposed || !window.IsVisible || panel != 5 || settingsPage != "main" && settingsPage != "tv") return;
            var focused = Keyboard.FocusedElement as Control;
            RebuildSettings(focused != null && actions.Contains(focused) ? focused.Tag as string : null);
        }

        static string TvRowText(TvStatus s)
        {
            if (s == null) return "Lendo…";
            if (s.Error != null) return "Indisponível";
            if (!s.Setup.Found) return s.Setup.Error == "ilegivel" ? "Configuração ilegível" : "Não configurada";
            switch (s.Network.Status)
            {
                case TvNetwork.Reachable: return "Na rede";
                case TvNetwork.Unreachable: return "Fora da rede";
                case TvNetwork.IdentityMismatch: return "Outro aparelho no endereço";
                case TvNetwork.QueryFailed: return "Sem identidade";
            }
            return "Sem endereço";
        }

        static string TvTopologyText(TvDisplayState d)
        {
            switch (d.Topology)
            {
                case TvTopology.ExternalOnly: return "única tela ativa";
                case TvTopology.Extend: return "estendendo outra tela";
                case TvTopology.Clone: return "duplicando outra tela";
                case TvTopology.PcScreenOnly: return "conectada, mas inativa (só a tela do PC)";
                case TvTopology.NoActiveDisplay: return "nenhuma tela ativa";
            }
            return "indefinida";
        }
        static string TvDisplayNote(TvDisplayState d)
        {
            if (d.Error != null) return "Tela: não foi possível ler as saídas de vídeo (" + d.Error + ").";
            if (d.Ambiguous) return "Tela: mais de uma Samsung corresponde; a TV não é identificada.";
            if (!d.SamsungFound) return "Tela: a Samsung não aparece nas saídas de vídeo.";
            string text = "Tela: " + TvTopologyText(d);
            if (!d.SamsungActive) return text + ".";
            var t = d.Samsung;
            text += " (" + t.OutputTechnology + (t.Width > 0 ? ", " + t.Width + "×" + t.Height + " a " + Math.Round(t.RefreshHz) + " Hz" : "") + ")";
            if (d.HdrSupported.HasValue) text += d.HdrSupported.Value ? (d.HdrEnabled == true ? " · HDR ligado" : " · HDR desligado") : " · sem HDR";
            return text + ".";
        }
        static string TvNetworkNote(TvStatus s)
        {
            if (!s.Setup.Found)
                return s.Setup.Error == "ilegivel" ? "A configuração do Samsung TV Orchestrator não pôde ser lida; nada foi alterado."
                    : "Nenhuma configuração do Samsung TV Orchestrator neste usuário. Sem ela, a barra não tem o endereço nem a identidade da TV.";
            switch (s.Network.Status)
            {
                case TvNetwork.Reachable:
                    string power = s.Network.ReportedPower == "on" ? " A TV informa que está ligada." : s.Network.ReportedPower == "off" ?
                        " A TV informa que está apagada." :
                        "";
                    return "Energia não confirmada: a rede responde igual com a tela acesa ou em standby." + power;
                case TvNetwork.Unreachable: return "Sem resposta na rede: a TV pode estar em sono profundo (apagada há mais de 2 min) ou desconectada.";
            case TvNetwork.IdentityMismatch: return
                "O aparelho no endereço salvo não é a TV configurada (MAC e DUID diferentes). Nenhum comando seria permitido.";
                case TvNetwork.QueryFailed: return "A porta da TV respondeu, mas a identidade não pôde ser lida.";
            }
            return "A configuração não tem o endereço da TV.";
        }
        static string TvOrchestratorNote(TvStatus s)
        {
            string text = s.OrchestratorRunning ? "Samsung TV Orchestrator em execução: ele controla a TV. Encerre-o para usar os comandos da barra."
                : "Samsung TV Orchestrator parado" + (s.OrchestratorRegistered && s.OrchestratorStartup == false ? ", com a inicialização desativada" :
                    s.OrchestratorRegistered && s.OrchestratorStartup == true ? ", mas inicia com o Windows" : "") + ".";
            if (!s.Setup.Found) return text;
            switch (s.Setup.Credential)
            {
                case TvCredential.Legacy: text += " A autorização salva está no formato antigo: será preciso autorizar de novo na TV."; break;
                case TvCredential.OtherTv: text += " A autorização salva é de outra TV."; break;
                case TvCredential.Invalid: text += " A autorização salva está ilegível."; break;
                case TvCredential.Missing: text += " Não há autorização salva na TV."; break;
            }
            if (s.Setup.KeepHdrOff) text += " O Orchestrator mantinha o HDR da Samsung desligado.";
            return text;
        }
        static List<string> TvNotes(TvStatus s)
        {
            var notes = new List<string>();
            if (s == null) return notes;
            if (s.Error != null) { notes.Add("Não foi possível ler a TV: " + s.Error + "."); return notes; }
            notes.Add(TvDisplayNote(s.Display)); notes.Add(TvNetworkNote(s)); notes.Add(TvOrchestratorNote(s));
            return notes;
        }

        string TvName()
        { return tvState != null && tvState.Setup.Found && tvState.Setup.Model.Length > 0 ? "TV Samsung " + tvState.Setup.Model : "TV Samsung"; }
        // Pagina principal: so uma linha, para nao empurrar o resto das Configuracoes. Os detalhes ficam na pagina da TV.
        void AddTvSetting()
        {
            EnsureTv();
            var row = ConnectionButton(body, TvName(), TvRowText(tvState) + "  ›", "page-tv", delegate { OpenSettingsPage("tv"); });
            row.ToolTip = "Estado e comandos da TV Samsung: ligar, desligar, confirmar a tela, autorização e automação.";
        }
        void BuildTvPage()
        {
            ((TextBlock)window.FindName("PanelTitle")).Text = "TV Samsung";
            EnsureTv();
            Caption("ESTADO");
            var row = ConnectionButton(body, TvName(), TvRowText(tvState), "tv-read", ForceTv);
            row.ToolTip = "Aperte para ler de novo o estado da TV. A leitura não envia comandos.";
            var notes = TvNotes(tvState);
            for (int i = 0; i < notes.Count; i++)
                if (tvConfig == null || i == 0 || i == 1 && (tvState == null || tvState.Network.Status != TvNetwork.Reachable)) ConnectionNote(notes[i]);
            AddTvControls();
        }

        // Diagnostico somente leitura: a mesma leitura do app, sem MAC, DUID ou IP na saida.
    }
}

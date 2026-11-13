// Comandos explicitos da TV, feedback vinculado a operacao e confirmacoes sem efeito ao cancelar.
using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using XGBP.Tv;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void AddTvControls()
        {
            if (tvConfig == null)
            {
                if (!verify) { ConnectionNote(tvInitError ?? "Preparando a integração da TV…"); ConnectionButton(body,
                    "Tentar carregar a integração", "", "tv-retry",
                    StartTvHost).IsEnabled = !tvBusy; }
                return;
            }
            // Uma linha por funcao (aprovado em 07/11): Ligada/Apagada/Esquecer na lista "Tela confirmada"; Autorizar/Refazer numa linha.
            Caption("ENERGIA");
            TvButton("Ligar TV", "tv-on", delegate { TvRun(TvRequestKind.PowerOn); });
            TvButton("Desligar TV", "tv-off", delegate { TvAsk("off"); });
            if (tvConfirm == "off") AddTvConfirmation();
            AddTvScreenRow();
            if (TvInterruptible()) ConnectionNote("Ligar TV ainda aguarda a tela. Se ela já acendeu, escolha \"Ligada\" em Tela confirmada para encerra" +
                "r a espera.");
            AddTvOptions();
            Caption("AUTORIZAÇÃO");
            bool known = TvAuthorized();
            var auth = ConnectionButton(body, "Conexão com a TV", known ? "Autorizada" : "Não autorizada", "tv-pair",
                delegate { if (known) TvAsk("pair"); else TvRun(TvRequestKind.Pair); });
            auth.IsEnabled = !tvBusy;
            if (tvConfirm == "pair") AddTvConfirmation();
        }
#if XGB_VERIFY
        Func<bool> tvAuthorizedOverride;
#endif
        bool TvAuthorized()
        {
#if XGB_VERIFY
            if (tvAuthorizedOverride != null) return tvAuthorizedOverride();
#endif
            return tvService != null && tvService.AuthorizationKnown;
        }
        // Estado da tela informado pelo usuario: uma linha com lista (Ligada / Apagada / Nao confirmada) no lugar de tres botoes.
        void AddTvScreenRow()
        {
            var runtime = tvRuntime == null ? null : tvRuntime();
            int current = runtime != null && runtime.HasAuthority ? (runtime.Power.State == TvState.On ? 0 : 1) : 2;
            string[] labels = { "Ligada", "Apagada", "Não confirmada" };
            var row = ConnectionButton(body, "Tela confirmada", labels[current] + "  ›", "tv-screen",
                delegate { connectionMenu = connectionMenu == "tv-screen" ? null : "tv-screen"; RebuildSettings("tv-screen"); });
            row.IsEnabled = !tvBusy || TvInterruptible() && !tvInterrupting;
            if (connectionMenu != "tv-screen") return;
            var box = MenuBox();
            ConnectionQuestion(box, "Como está a tela agora? Vale por até " + tvConfig.ConfirmedSeconds +
                " s e é consumida antes de alternar a energia; a rede não confirma a tela.");
            var kinds = new[] { TvRequestKind.ConfirmOn, TvRequestKind.ConfirmOff, TvRequestKind.Forget };
            for (int i = 0; i < 3; i++)
            { var kind = kinds[i]; ConnectionButton(box, labels[i], i == current ? "✓" : "", "tv-screen-" + i, delegate { connectionMenu = null; TvConfirm(kind,
                "tv-screen"); }); }
        }
        Button TvButton(string label, string tag, Action action)
        { var row = ConnectionButton(body, label, "", tag, action); row.IsEnabled = !tvBusy; return row; }
        // "Ligar TV" pode esperar minutos pelo sono profundo da TV (porte fiel do Orchestrator). Nessa espera, e so nela,
        // a confirmacao explicita continua disponivel: o nucleo cancela o PowerOn ativo antes de registra-la, sem reenviar comandos.
        bool TvInterruptible() { return tvBusy && tvWorkKind == TvRequestKind.PowerOn && tvCommand != null; }
        void TvConfirmButton(string label, string tag, TvRequestKind kind)
    { var row = ConnectionButton(body, label, "", tag, delegate { TvConfirm(kind, tag); }); row.IsEnabled = !tvBusy || TvInterruptible() && !tvInterrupting; }
        void TvConfirm(TvRequestKind kind, string tag)
        {
            if (!TvInterruptible()) { TvRun(kind); return; }
            if (tvInterrupting) return;
            tvInterrupting = true; var command = tvCommand;
            var notice = BeginFeedback("Encerrando a espera de Ligar TV…", null, 315);
            WriteLog("tv: confirmacao durante ligar | " + kind); RebuildTvUi(tag);
            TvReply result = null;
            ConnectionWork(delegate { result = command(kind, false).GetAwaiter().GetResult(); }, delegate(Exception error) {
                tvInterrupting = false;
                if (error != null) WriteLog("tv: confirmacao falhou | " + error.GetType().Name);
                if (error != null || result == null) result = new TvReply(Guid.Empty, TvOutcome.QueryFailed,
                    "Não foi possível registrar a confirmação. Confira a tela antes de tentar novamente.");
                FinishFeedback(notice, TvReplyText(result), TvKind(result));
                tvReadAt = DateTime.MinValue; RebuildTvUi(tag);
            });
        }
        void TvAsk(string kind) { if (tvBusy) return; tvConfirm = kind; RebuildTvUi("tv-cancel"); }
        void AddTvConfirmation()
        {
            if (tvConfirm == null) return;
            var box = MenuBox(); bool off = tvConfirm == "off";
            ConnectionQuestion(box, off ? "Confira a tela antes de continuar. Confirma que a TV está ligada e deseja desligá-la?" :
                "Refazer a autorização substitui o certificado salvo. Confirme a identidade e permita a conexão na TV.");
            ConnectionButton(box, "Cancelar", "", "tv-cancel", delegate { tvConfirm = null; RebuildTvUi(off ? "tv-off" : "tv-pair"); });
            ConnectionButton(box, off ? "A TV está ligada · desligar" : "Refazer autorização", "", "tv-accept", delegate {
                if (tvConfirm == null || tvBusy) return;
                tvConfirm = null;
                if (off) TvWork("Desligando a TV…", async delegate {
                    var confirmed = await tvCommand(TvRequestKind.ConfirmOn, false).ConfigureAwait(false);
                    return confirmed.Outcome == TvOutcome.Confirmed ? await tvCommand(TvRequestKind.PowerOff, false).ConfigureAwait(false) : confirmed;
                }, "tv-off");
                else TvRun(TvRequestKind.Pair, true);
            }).IsEnabled = !tvBusy;
        }
        bool TvBack()
        {
            if (tvConfirm == null) return false;
            string tag = tvConfirm == "off" ? "tv-off" : "tv-pair"; tvConfirm = null; RebuildTvUi(tag); return true;
        }
        void RebuildTvUi(string tag)
        {
            if (disposed || !window.IsVisible) return;
            if (panel == 5 && settingsPage == "tv") { RebuildSettings(tag); return; }
            if (panel != 4 || powerConfirmation.HasValue) return;
            CancelElementMotion(body); body.Children.Clear(); actions.Clear(); BuildPowerPanel(); window.UpdateLayout();
            var target = actions.Find(c => (string)c.Tag == tag && c.IsEnabled); if (target != null) target.Focus();
        }
        void TvRun(TvRequestKind kind, bool value = false)
        { TvWork("Enviando pedido à TV…", delegate { return tvCommand(kind, value); }, kind == TvRequestKind.PowerOn ? "tv-on" : kind == TvRequestKind.Pair ?
            "tv-pair" : "tv-read", null, kind); }
        void TvWork(string text, Func<Task<TvReply>> work, string tag, TvCoreConfig next = null, TvRequestKind? kind = null)
        {
            if (tvBusy || tvCommand == null) return;
            tvBusy = true; tvWorkKind = kind; var notice = BeginFeedback(text, null, 315); RebuildTvUi(tag);
            TvReply result = null;
            ConnectionWork(delegate { result = work().GetAwaiter().GetResult(); }, delegate(Exception error) {
                tvBusy = false; tvWorkKind = null;
                if (error != null) { WriteLog("tv: operacao falhou | " + error.GetType().Name); result = new TvReply(Guid.Empty, TvOutcome.QueryFailed,
                    "Não foi possível confirmar a operação. Confira o resultado antes de tentar novamente."); }
                if (result == null) result = new TvReply(Guid.Empty, TvOutcome.QueryFailed, "Operação sem resultado.");
                if (tvService != null) tvConfig = tvService.Configuration;
                else if (next != null && result.Outcome == TvOutcome.NoAction) tvConfig = next.Copy();
                FinishFeedback(notice, TvReplyText(result), TvKind(result));
                tvReadAt = DateTime.MinValue; RebuildTvUi(tag);
            });
        }
        void AddTvPowerButtons()
        {
            if (tvConfig == null) return;
            Caption("TV SAMSUNG");
            TvButton("Ligar TV", "tv-on", delegate { TvRun(TvRequestKind.PowerOn); });
            TvButton("Desligar TV", "tv-off", delegate { TvAsk("off"); }); AddTvConfirmation();
            if (TvInterruptible()) TvConfirmButton("A TV está ligada", "tv-confirm-on", TvRequestKind.ConfirmOn);
        }
        void AddTvSuspendNote()
        {
            if (tvConfig == null || !tvConfig.Enabled) return;
            ConnectionNote(tvConfig.ImplicitSuspendConfirmation ?
                "TV: confirmar pelo controle, com a Samsung como única tela, autoriza desligá-la antes de suspender." :
                "TV: será solicitado desligar antes de suspender, se houver confirmação recente de que a tela está ligada.");
        }
    }
}

// Interface, comandos e preferencias da TV com backends substituidos. Nenhuma conexao/dispositivo real.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using XGBP.Tv;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        public void VerifyTvIntegration()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool ok, string why) { checks++; if (!ok) throw new Exception("Integracao TV: " + why); };
            var oldConfig = tvConfig; var oldCommand = tvCommand; var oldSave = tvSave; var oldRuntime = tvRuntime; var oldRead = tvRead;
            var oldState = tvState; var oldAt = tvReadAt;
            var sent = new List<TvRequestKind>(); int saves = 0; bool fail = false;
            try
            {
                tvConfig = TvCoreSuite.Config(); tvConfig.Enabled = false;
                tvRead = delegate { return new TvStatus { Setup = new TvSetupState { Found = true, Model = "TV de teste" }, Network = new
                    TvNetworkState { Status =
                    TvNetwork.Reachable } }; };
                tvState = null; tvReadAt = DateTime.MinValue; tvRuntime = delegate { return null; };
                tvCommand = delegate(TvRequestKind kind, bool value) {
                    sent.Add(kind); if (kind == TvRequestKind.Enable) tvConfig.Enabled = value;
                    return Task.FromResult(new TvReply(Guid.NewGuid(), kind == TvRequestKind.ConfirmOn || kind == TvRequestKind.ConfirmOff ?
                        TvOutcome.Confirmed :
                        kind == TvRequestKind.PowerOff ? TvOutcome.PossiblySent : TvOutcome.NoAction, "Resultado substituido.", true,
                        kind == TvRequestKind.PowerOff ? DeliveryStatus.PossiblySent : DeliveryStatus.NotSent)); };
                tvSave = delegate(TvCoreConfig config) { saves++; return Task.FromResult(new TvReply(Guid.NewGuid(), fail ? TvOutcome.PersistenceFailure :
                    TvOutcome.NoAction, fail ? "Falha ficticia." : "Opcoes salvas.")); };
                bool authorized = false; tvAuthorizedOverride = delegate { return authorized; };
                Show(); SetPanel(5); OpenSettingsPage("tv");
                Action<string> press = delegate(string tag) { FocusHome(tag); Activate(); };
                Func<string, bool> has = delegate(string tag) { return actions.Exists(a => (string)a.Tag == tag); };
                Func<string, bool> enabledTag = delegate(string tag) { var c = actions.Find(a => (string)a.Tag == tag); return c != null && c.IsEnabled; };
                Func<string> tags = delegate { var list = new List<string>(); foreach (var a in actions) list.Add((string)a.Tag); return
                    string.Join(" ", list.ToArray());
                    };
                // Uma linha por funcao: seis controles com a automacao desligada e nenhum botao antigo redundante.
                check(tags() == "tv-read tv-on tv-off tv-screen tv-enabled tv-pair", "pagina enxuta com a automacao desligada: " + tags());
                check(!has("tv-confirm-on") && !has("tv-confirm-off") && !has("tv-forget") && !has("tv-pair-replace") && !has("tv-ps") &&
                    !has("tv-topology") && !has(
                    "tv-suspend"),
                    "botoes redundantes e regras ocultas com a automacao desligada");
                press("tv-off"); check(sent.Count == 0 && tvConfirm == "off" && FocusedTag() == "tv-cancel", "desligar so pede confirmacao e foca Cancelar");
                Back(); check(sent.Count == 0 && tvConfirm == null && FocusedTag() == "tv-off", "circulo cancela sem enviar");
                press("tv-off"); press("tv-accept");
            check(sent.Count == 2 && sent[0] == TvRequestKind.ConfirmOn && sent[1] == TvRequestKind.PowerOff,
                "confirmacao visual precede um unico desligamento");
                check(FeedbackText().Contains("Entrega possível"), "entrega incerta permanece visivel sem anunciar sucesso");
                int count = sent.Count; press("tv-on"); check(sent.Count == count + 1 && sent[sent.Count - 1] == TvRequestKind.PowerOn,
                    "ligar manual funciona com automacao desligada");
                // Tela confirmada: uma lista no lugar de Ligada/Apagada/Esquecer.
                check(ActionLabel("tv-screen") == "Tela confirmada · Não confirmada", "linha mostra o estado confirmado: " + ActionLabel("tv-screen"));
                press("tv-screen");
                check(connectionMenu == "tv-screen" && has("tv-screen-0") && has("tv-screen-1") && ActionLabel("tv-screen-2") == "Não confirmada · ✓",
                    "lista da tela com a escolha atual marcada");
            count = sent.Count; Back(); check(connectionMenu == null && sent.Count == count && FocusedTag() == "tv-screen", "circulo fecha a lista sem enviar");
                var screen = new[] { TvRequestKind.ConfirmOn, TvRequestKind.ConfirmOff, TvRequestKind.Forget };
                for (int i = 0; i < 3; i++)
                {
                    press("tv-screen"); count = sent.Count; press("tv-screen-" + i);
                    check(sent.Count == count + 1 && sent[count] == screen[i] && connectionMenu == null, "escolha da tela envia so " + screen[i]);
                }
                // Conexao com a TV: autoriza quando falta; com autorizacao, pede confirmacao para refazer.
                check(ActionLabel("tv-pair") == "Conexão com a TV · Não autorizada", "linha de autorizacao sem credencial");
                count = sent.Count; press("tv-pair"); check(sent.Count == count + 1 && sent[count] == TvRequestKind.Pair && tvConfirm == null,
                    "sem credencial, autoriza direto ao pedir");
                authorized = true; RebuildSettings("tv-pair");
                check(ActionLabel("tv-pair") == "Conexão com a TV · Autorizada", "linha de autorizacao com credencial");
                count = sent.Count; press("tv-pair"); check(sent.Count == count && tvConfirm == "pair" && FocusedTag() == "tv-cancel",
                    "refazer pede confirmacao e foca Cancelar");
                Back(); check(sent.Count == count && tvConfirm == null && FocusedTag() == "tv-pair", "refazer autorizacao pode ser cancelado");
                press("tv-pair"); press("tv-accept"); check(sent.Count == count + 1 && sent[count] == TvRequestKind.Pair, "refazer so depois de confirmar");
                // Automacao: as regras aparecem so com ela ligada.
                press("tv-enabled"); check(tvConfig.Enabled && sent[sent.Count - 1] == TvRequestKind.Enable, "interruptor geral separado dos comandos manuais");
                check(tags() == "tv-read tv-on tv-off tv-screen tv-enabled tv-suspend tv-implicit tv-resume tv-ps-action tv-wake-soun" +
                    "d tv-extend tv-external tv-clone tv-pc tv-hdr tv-pair",
                    "regras visiveis com a automacao ligada: " + tags());
                press("tv-ps-action"); press("tv-ps-action-0");
                check(!tvConfig.PsWakeEnabled && tvConfig.DualSense == TvAction.Keep && saves == 1 && !has("tv-wake-sound"),
                    "PS sem acao desliga o reconhecimento e esconde o som");
                press("tv-ps-action"); press("tv-ps-action-1");
            check(tvConfig.PsWakeEnabled && tvConfig.DualSense == TvAction.On && saves == 2 && has("tv-wake-sound"), "PS ligando a TV volta a mostrar o som");
                bool hdr = tvConfig.KeepHdrOff; fail = true; press("tv-hdr");
                check(tvConfig.KeepHdrOff == hdr && FeedbackText().Contains("Falha ficticia"), "falha ao salvar nao altera preferencia exibida"); fail = false;
                tvConfig.TopologyEnabled = false; press("tv-extend"); press("tv-extend-0");
                check(tvConfig.Extend == TvAction.Keep && tvConfig.TopologyEnabled && connectionMenu == null,
                    "regra de telas aplica escolha, reativa as telas e fecha a lista");
                press("tv-suspend"); press("tv-suspend-0"); check(tvConfig.Suspend == TvAction.Keep && !has("tv-implicit"),
                    "sem desligar ao suspender, a confirmacao implicita some");
                press("tv-suspend"); press("tv-suspend-1"); check(tvConfig.Suspend == TvAction.Off && has("tv-implicit"),
                    "desligar ao suspender mostra a confirmacao implicita");
                press("tv-implicit"); check(tvConfig.ImplicitSuspendConfirmation, "confirmacao implicita exige opcao explicita");
                press("tv-wake-sound"); check(tvConfig.WakeSoundEnabled, "som opcional fica em preferencia propria");
                window.UpdateLayout(); SaveRender("tv-integrada-configuracoes");
                ((ScrollViewer)window.FindName("BodyScroll")).ScrollToTop(); window.UpdateLayout(); SaveRender("tv-integrada-topo");
                SetPanel(4); check(has("tv-on") && has("tv-off") && !has("tv-confirm-on"), "Energia inclui so os atalhos Ligar/Desligar TV");
                press("tv-off"); count = sent.Count; SetPanel(0); SetPanel(4);
                check(tvConfirm == null && !has("tv-accept") && sent.Count == count, "troca de painel invalida confirmacao antiga");
                tvBusy = true; TvRun(TvRequestKind.PowerOn); check(sent.Count == count && PowerBlockedReason().Contains("TV"),
                    "operacao em voo bloqueia repeticao e suspensao concorrente"); tvBusy = false;
                // Ligar TV em espera (simulado em voo): so a confirmacao da tela continua disponivel e encerra a espera.
                tvBusy = true; tvWorkKind = TvRequestKind.PowerOn; SetPanel(0); SetPanel(4);
                check(enabledTag("tv-confirm-on") && !enabledTag("tv-on") && !enabledTag("tv-off") && PowerBlockedReason().Contains("A TV está ligada"),
                    "Energia oferece so a confirmacao durante Ligar TV e explica o bloqueio da suspensao");
                count = sent.Count; press("tv-confirm-on");
                check(sent.Count == count + 1 && sent[count] == TvRequestKind.ConfirmOn && tvBusy && !tvInterrupting &&
                    FeedbackText().Contains("Resultado substituido"),
                    "confirmar ligada durante a espera envia so a confirmacao, sem novo comando de energia");
                SetPanel(5); OpenSettingsPage("tv");
                const string waitNote = "Ligar TV ainda aguarda a tela. Se ela já acendeu, escolha \"Ligada\" em Tela confirmada para encerrar a espera.";
                check(enabledTag("tv-screen") && !enabledTag("tv-on") && !enabledTag("tv-off") && !enabledTag("tv-pair") && !enabledTag("tv-enabled") &&
                    BodyHas(waitNote),
                    "pagina da TV libera so a tela confirmada durante Ligar TV");
                press("tv-screen"); count = sent.Count; press("tv-screen-1"); check(sent.Count == count + 1 && sent[count] == TvRequestKind.ConfirmOff,
                    "apagada tambem encerra a espera");
                tvWorkKind = TvRequestKind.Pair; RebuildSettings("tv-read"); count = sent.Count;
                check(!enabledTag("tv-screen") && !BodyHas(waitNote), "outras operacoes em voo continuam bloqueando a tela confirmada");
            TvConfirm(TvRequestKind.ConfirmOn, "tv-screen"); check(sent.Count == count, "confirmacao fora da espera de Ligar TV nao fura a operacao em voo");
                tvBusy = false; tvWorkKind = null; RebuildSettings("tv-read");
                check(enabledTag("tv-screen") && enabledTag("tv-on"), "sem operacao em voo, todos os comandos voltam");
                SetPanel(0); SetPanel(4);
                verifyResumeInput = true; resumeGesture.Suspend(); resumeGesture.Resume(); count = sent.Count;
                var e = new XGBP.Input.ButtonEvent { Button = "PS", Device = "fixture-BT", Down = true, Time = DateTime.Now, Gesture = "APERTO" };
                bool visible = window.IsVisible; OnController(e);
                check(window.IsVisible == visible && sent.Count == count && resumeGesture.Snapshot.PsConsumed,
                    "PS da retomada nao abre/fecha a barra nem envia por backend de teste");
                e.Down = false; e.Time = DateTime.Now; OnController(e);
                check(window.IsVisible == visible, "soltura do PS consumido tambem nao alterna a barra");
                File.WriteAllText(Path.Combine(Output, "tv-integracao-verificacao.txt"), "OK: " + checks +
                    " verificacoes da interface TV e retomada. Comandos, armazenamento, som, energia e leitor substituido" +"s; nenhuma TV real.\r\n",
                    Encoding.UTF8);
            }
            finally
            {
                tvConfig = oldConfig; tvCommand = oldCommand; tvSave = oldSave; tvRuntime = oldRuntime; tvRead = oldRead; tvState = oldState; tvReadAt = oldAt;
                tvBusy = false; tvWorkKind = null; tvInterrupting = false; tvConfirm = null; tvAuthorizedOverride = null; connectionMenu = null;
                    verifyResumeInput =
                    false; resumeGesture.CancelSuspend(); settingsPage = "main"; SetPanel(0); if (window.IsVisible) Hide();
            }
        }
    }
}

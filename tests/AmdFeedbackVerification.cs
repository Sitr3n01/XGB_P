// Politica do rodape AMD: confirma por estado; so avisos relevantes interrompem. Relogio e comandos substituidos.
using System;
using System.IO;
using System.Text;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyAmdFeedback()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool ok, string why) { checks++; if (!ok) throw new Exception("AMD feedback: " + why); };
            var oldNow = feedbackNow;
            DateTime now = new DateTime(2026, 11, 7, 15, 0, 0, DateTimeKind.Utc);
            try
            {
                feedbackNow = delegate { return now; };
                SetPanel(AmdTuningPanel); ClearFeedback();
                var change = BeginFeedback("Aplicando: Limite de FPS…", AmdTuningScope, 15);
                check(FeedbackText() == "" && LatestHistoryText() == change.Text, "andamento rotineiro so no historico");
                FinishFeedback(change, "Limite de FPS: 120 FPS.", FeedbackKind.Success);
                check(FeedbackText() == "" && LatestHistoryText() == "Limite de FPS: 120 FPS.", "sucesso rotineiro so no historico");
                Feedback("Aguarde o ajuste anterior terminar.", FeedbackKind.Pending);
                check(FeedbackText() == "", "bloqueio breve nao repete o Aplicando da linha");
                Feedback("O driver recusou o valor.", FeedbackKind.Error);
                check(FeedbackText() == "O driver recusou o valor.", "erro aparece");
                change = BeginFeedback("Ligando: Radeon Chill…", AmdTuningScope, 15);
                check(FeedbackText() == "O driver recusou o valor.", "novo ajuste nao apaga erro vigente");
                FinishFeedback(change, "Radeon Chill: ligado.", FeedbackKind.Success);
                check(FeedbackText() == "O driver recusou o valor." && LatestHistoryText() == "Radeon Chill: ligado.",
                    "sucesso silencioso preserva erro e registra resultado");
                now = now.AddSeconds(6); RenderFeedback();
                check(FeedbackText() == "O driver recusou o valor.", "erro mantem os 12 s mesmo depois de o sucesso expirar");
                now = now.AddSeconds(7); RenderFeedback();
                check(FeedbackText() == "", "erro expira sem reaparecer");
                Feedback("O driver também desligou Nitidez da imagem.", FeedbackKind.Warning);
                check(FeedbackText().Contains("Nitidez"), "mudanca de outra opcao aparece");
                Feedback("Valor atualizado.");
                check(FeedbackText().Contains("Nitidez"), "informacao rotineira nao apaga aviso");
                ClearFeedback(); check(FeedbackText() == "", "limpar remove tambem aviso preservado");
                var waiting = BeginFeedback("Aplicando…", AmdTuningScope, 15);
                now = now.AddSeconds(16); RenderFeedback();
                check(FeedbackText().Contains("sem confirmação") && waiting.Kind == FeedbackKind.Warning, "demora sem confirmacao vira aviso visivel");
                ClearFeedback();
                var reset = BeginFeedback("Redefinindo o cache do sombreador…", AmdTuningScope, 15);
                reset.Pinned = true; RenderFeedback();
                check(FeedbackText() == reset.Text, "manutencao relevante mostra andamento");
                amdTuningPending = "resetcache";
                check(!AmdTuningIdle() && FeedbackText() == reset.Text && feedbackNotices[AmdTuningScope] == reset &&
                    LatestHistoryText() == "Aguarde o ajuste anterior terminar.", "toque durante manutencao nao substitui operacao nem perde callback");
                amdTuningPending = null;
                FinishFeedback(reset, "Cache redefinido. Os jogos recompilam os shaders.", FeedbackKind.Success);
                check(FeedbackText() == reset.Text, "resultado da manutencao continua visivel");
                now = now.AddSeconds(6); RenderFeedback(); check(FeedbackText() == "", "confirmacao da manutencao expira");
                Feedback("Confira o AMD Software.", FeedbackKind.Attention);
                check(FeedbackText() == "Confira o AMD Software.", "aviso que pede acao aparece");
                window.UpdateLayout(); SaveRender("amd-feedback-atencao");
                ClearFeedback(); window.UpdateLayout(); SaveRender("amd-feedback-limpo");
                SetPanel(6); Feedback("Saída de som alterada.", FeedbackKind.Success);
                check(FeedbackText() == "Saída de som alterada.", "outros paineis preservam a politica anterior");
                File.WriteAllText(Path.Combine(Output, "amd-feedback-verificacao.txt"), "OK: " + checks +
                    " verificacoes de rotina silenciosa, historico, erros/avisos visiveis, preservacao e expiracao, timeout, cache e isolamento por painel. " +
                    "Relogio e comandos substituidos; nenhum ajuste da GPU alterado.\r\n", Encoding.UTF8);
            }
            finally { amdTuningPending = null; feedbackNotices.Remove(AmdTuningScope); feedbackNotices.Remove("6"); feedbackNow = oldNow; SetPanel(0);
                RenderFeedback(); }
        }
    }
}

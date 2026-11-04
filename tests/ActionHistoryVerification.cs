// Regressao do historico de acoes e problemas (ADR 0078). Relogio e gravacao substituidos; arquivos so na pasta da verificacao.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Controls;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyActionHistory()
        {
            var clock = feedbackNow; var write = historyWrite; int checks = 0;
            Action<bool, string> check = delegate(bool ok, string reason) { checks++; if (!ok) throw new Exception("Historico: " + reason); };
            DateTime now = new DateTime(2026, 10, 29, 15, 0, 0, DateTimeKind.Utc);
            var written = new List<ActionHistoryState>();
            try
            {
                feedbackNow = delegate { return now; }; feedbackNotices.Clear(); actionHistory.Clear(); historyId = 0; historySeen = 0;
                historyWrite = delegate(ActionHistoryState state) { written.Add(state); };
                Show(); SetPanel(5);

                var op = BeginFeedback("Ligando a Luz noturna…"); long started = op.History.Started;
                now = now.AddSeconds(2); FinishFeedback(op, "Luz noturna ligada.", FeedbackKind.Success);
                check(actionHistory.Count == 1 && op.History.Text == "Luz noturna ligada." && op.History.Type == FeedbackKind.Success &&
                    op.History.Started == started && op.History.Updated > started && op.History.Area == "Configurações", "uma entrada por operacao, atualizada ate o resultado");
                for (int i = 0; i < 3; i++) Feedback("Aguarde a operação anterior terminar.");
                check(actionHistory.Count == 2 && actionHistory[1].Count == 3, "repeticao imediata vira contador");
                now = now.AddSeconds(11); Feedback("Aguarde a operação anterior terminar.");
                check(actionHistory.Count == 3, "repeticao depois de 10 s e uma entrada nova");
                check(HistoryArea("0") == "Início" && HistoryArea("5/wifi") == "Wi-Fi" && HistoryArea("5/bluetooth") == "Bluetooth" &&
                    HistoryArea("5/main") == "Configurações" && HistoryArea("7") == "Lojas" && HistoryArea("6") == "Som", "area pelo painel da operacao");
                check(RecordHistory("0", new string('a', 2000), FeedbackKind.Info).Text.Length == HistoryTextLimit, "texto longo limitado");

                // Problemas novos aparecem na entrada das Configuracoes ate a pagina ser vista.
                actionHistory.Clear(); historySeen = HistoryEpoch(now);
                now = now.AddSeconds(1); NewFeedback("O Windows negou pausar o jogo.", FeedbackKind.Error, "0");
                NewFeedback("O jogo não fechou. Ele pode estar pedindo confirmação na própria tela.", FeedbackKind.Attention, "0");
                NewFeedback("Jogo aberto.", FeedbackKind.Success, "0");
                check(UnseenProblems() == 2, "dois problemas novos");
                SetPanel(5); var row = actions.Find(c => (string)c.Tag == "page-history");
                check(row != null && ((TextBlock)((Grid)((Button)row).Content).Children[1]).Text.StartsWith("2 problemas novos"), "entrada mostra problemas novos");
                row.Focus(); Activate();
                check(settingsPage == "history" && UnseenProblems() == 0 && historyDirty, "ver a pagina marca como vistos e agenda gravacao");
                check(actions.Exists(c => (string)c.Tag == "hist-" + actionHistory[2].Id) && actions.FindIndex(c => ((string)c.Tag).StartsWith("hist-")) ==
                    actions.FindIndex(c => (string)c.Tag == "hist-" + actionHistory[2].Id), "mais recente primeiro");
                window.UpdateLayout(); SaveRender("historico-acoes");
                actions.Find(c => (string)c.Tag == "history-filter").Focus(); Activate();
                check(historyProblemsOnly && actions.FindAll(c => ((string)c.Tag).StartsWith("hist-")).Count == 2, "filtro mostra so problemas");
                window.UpdateLayout(); SaveRender("historico-problemas");
                Activate(); check(!historyProblemsOnly && actions.FindAll(c => ((string)c.Tag).StartsWith("hist-")).Count == 3, "filtro volta para tudo");

                // Pagina aberta acompanha operacoes novas depois da acao em curso.
                long version = historyVersion; Feedback("Volume ajustado.");
                check(historyVersion > version && historyRenderQueued, "mudanca agenda atualizacao da pagina"); PumpMotion(20);
                check(actions.FindAll(c => ((string)c.Tag).StartsWith("hist-")).Count == 4, "pagina mostra a entrada nova");

                actions.Find(c => (string)c.Tag == "history-clear").Focus(); Activate();
                check(connectionConfirm == "history-clear" && FocusedTag() == "history-cancel", "apagar pede confirmacao");
                Back(); check(connectionConfirm == null && FocusedTag() == "history-clear" && actionHistory.Count == 4, "voltar cancela sem apagar");
                Activate(); actions.Find(c => (string)c.Tag == "history-clear-confirm").Focus(); Activate();
                check(actionHistory.Count == 0 && connectionConfirm == null && !actions.Exists(c => (string)c.Tag == "history-clear"), "confirmar apaga o historico");
                Back(); check(settingsPage == "main" && FocusedTag() == "page-history", "voltar retorna as Configuracoes");

                // Arquivo: ida e volta, retencao, limite, pendencia interrompida e entradas invalidas descartadas.
                string folder = Path.Combine(Output, "history-fixture"); Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "actions.json");
                foreach (var stale in new[] { path, path + ".bak" }) if (File.Exists(stale)) File.Delete(stale);
                for (int i = 0; i < HistoryLimit + 5; i++) RecordHistory("0", "Registro " + i, FeedbackKind.Info);
                check(actionHistory.Count == HistoryLimit && actionHistory[0].Text == "Registro 5", "limite descarta os mais antigos");
                actionHistory.Clear();
                RecordHistory("0", "Antigo", FeedbackKind.Error).Updated = HistoryEpoch(now.AddDays(-8));
                RecordHistory("0", "Abrindo jogo…", FeedbackKind.Pending);
                RecordHistory("5/wifi", "Wi-Fi ligado.", FeedbackKind.Success);
                RecordHistory("0", "Do futuro", FeedbackKind.Info).Updated = HistoryEpoch(now.AddDays(3));
                RecordHistory("0", "Tipo inválido", FeedbackKind.Info).Kind = 99;
                var snapshot = new ActionHistoryState { Seen = 42, Entries = actionHistory.ConvertAll(e => e.Copy()) };
                GameFiles.Write(path, snapshot);
                var back = ReadActionHistory(path, now);
                check(back.Seen == 42 && back.Entries.Count == 2 && back.Entries[0].Type == FeedbackKind.Warning && back.Entries[0].Text.Contains("Sem resultado") &&
                    back.Entries[1].Text == "Wi-Fi ligado." && back.Entries[1].Area == "Wi-Fi", "leitura descarta antigos, futuros e invalidos e fecha pendencia interrompida");
                check(HistorySnapshot().Entries.Count == 4, "gravacao descarta o que passou da retencao");
                GameFiles.Write(path, snapshot); File.WriteAllText(path, "{ invalido", Encoding.UTF8);
                LoadActionHistory(path);
                check(actionHistory.Count == 2 && historySeen == 42 && historyId >= actionHistory[1].Id, "arquivo corrompido usa o backup");
                File.WriteAllText(path, "{\"Version\":2,\"Seen\":0,\"Entries\":[]}", new UTF8Encoding(false));
                bool refused = false; try { ReadActionHistory(path, now); } catch (InvalidDataException) { refused = true; }
                check(refused, "versao desconhecida recusada");

                // Gravacao: agrupada e fora da UI; ao encerrar, imediata.
                written.Clear(); historyDirty = false; historyFlushAt = DateTime.MaxValue;
                RecordHistory("0", "Nova ação", FeedbackKind.Info);
                FlushActionHistory(false); check(written.Count == 0 && historyDirty, "gravacao espera o agrupamento");
                FlushActionHistory(true); check(written.Count == 1 && !historyDirty && written[0].Sequence == historyVersion, "encerramento grava na hora");
                File.WriteAllText(Path.Combine(Output, "historico-verificacao.txt"), "OK: " + checks + " verificacoes. Uma entrada por operacao ate o resultado, contador de repeticao, area pelo painel, " +
                    "texto limitado, problemas novos na entrada das Configuracoes, pagina marca vistos, mais recente primeiro, filtro de problemas, atualizacao da pagina aberta, " +
                    "apagar com confirmacao e retorno, limite de " + HistoryLimit + ", retencao de 7 dias, pendencia interrompida, invalidos descartados, backup, versao recusada e gravacao agrupada/imediata. " +
                    "Relogio e gravacao substituidos; arquivo real do perfil nao foi lido nem escrito.\r\n", Encoding.UTF8);
            }
            finally
            {
                feedbackNow = clock; historyWrite = write; feedbackNotices.Clear(); actionHistory.Clear(); historyId = 0; historySeen = historyPageSince = 0;
                historyDirty = historyProblemsOnly = false; historyFlushAt = DateTime.MaxValue; connectionConfirm = null; settingsPage = "main";
                SetPanel(0); RenderFeedback();
            }
        }
    }
}

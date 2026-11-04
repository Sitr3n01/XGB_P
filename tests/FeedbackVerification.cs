// Regressao dos estados do rodape. Relogio, sessoes e comandos substituidos; nenhum jogo/app real e operado.
using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        static void FeedbackAssert(bool value, string reason) { if (!value) throw new Exception("Feedback: " + reason); }
        void VerifyOperationFeedback()
        {
            var clock = feedbackNow; var launch = gameLaunch; var close = gameCloseWindow; var kill = gameKill; var suspend = gameSuspend; var view = gamesView;
            DateTime now = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
            try
            {
                feedbackNow = delegate { return now; }; feedbackNotices.Clear(); pauseNotices.Clear(); launchingGame = unconfirmedLaunch = closingGame = gameBusy = null;
                gameLaunch = delegate { }; gameCloseWindow = delegate { }; gameKill = gameSuspend = delegate { return null; };
                gamesView = ExampleGames(); gamesView.Activities.Clear(); gamesView.Current = null;
                // Ciclo de vida completo num painel comum; o Inicio filtra o rodape (ADR 0078, abaixo).
                returnToHome = false; Show(); SetPanel(5);
                Feedback("Mensagem concluída", FeedbackKind.Success);
                FeedbackAssert(FeedbackText() == "Mensagem concluída", "mensagem e imediata");
                now = now.AddSeconds(5); RenderFeedback(); FeedbackAssert(FeedbackText() == "", "sucesso some em 5 s");
                var pending = BeginFeedback("Aguardando confirmação", "5/main", 180);
                now = now.AddSeconds(15); RenderFeedback(); FeedbackAssert(FeedbackText() == pending.Text, "pendencia nao recebe duracao de sucesso");
                Feedback("Operação mais recente"); FinishFeedback(pending, "Resposta antiga", FeedbackKind.Success);
                FeedbackAssert(FeedbackText() == "Operação mais recente" && pending.History.Text == "Resposta antiga", "resposta atrasada nao sobrescreve operacao nova, mas fica no historico");
                SetPanel(6); var scoped = BeginFeedback("Operação do Som");
                SetPanel(5); Feedback("Operação das Configurações"); FinishFeedback(scoped, "Som concluído", FeedbackKind.Success);
                FeedbackAssert(FeedbackText() == "Operação das Configurações", "conclusao de outro painel nao contamina o atual");
                now = now.AddSeconds(15); RenderFeedback(); SetPanel(6); FeedbackAssert(FeedbackText() == "", "conclusao oculta nao reaparece velha");

                SetPanel(0);
                Feedback("Sucesso no Início", FeedbackKind.Success);
                FeedbackAssert(FeedbackText() == "" && LatestHistoryText() == "Sucesso no Início", "Inicio: sucesso so no historico");
                var homeOperation = BeginFeedback("Operação do Início em andamento", "0");
                FeedbackAssert(FeedbackText() == "", "Inicio: andamento so no historico");
                Feedback("Aviso de verificação", FeedbackKind.Warning); Feedback("Informação do Início");
                FeedbackAssert(FeedbackText() == "", "Inicio: aviso de verificacao e informacao ficam no historico");
                homeOperation = BeginFeedback("Operação do Início em andamento", "0");
                FinishFeedback(homeOperation, "A operação falhou.", FeedbackKind.Error);
                FeedbackAssert(FeedbackText() == "A operação falhou." && homeOperation.History.Text == "A operação falhou.", "Inicio: erro aparece e atualiza a mesma entrada");
                Feedback("O jogo pede confirmação.", FeedbackKind.Attention);
                FeedbackAssert(FeedbackText() == "O jogo pede confirmação.", "Inicio: aviso que pede acao aparece");
                var longWait = BeginFeedback("Aguardando a Steam concluir a sessão anterior…", "0", 46); longWait.Pinned = true; RenderFeedback();
                FeedbackAssert(FeedbackText() == longWait.Text, "Inicio: espera longa marcada aparece");
                FinishFeedback(longWait, "Jogo aberto.", FeedbackKind.Success);
                FeedbackAssert(FeedbackText() == "" && LatestHistoryText() == "Jogo aberto.", "Inicio: espera concluida volta ao historico");
                now = now.AddSeconds(15); RenderFeedback();

                RequestGameLaunch("fixture:one");
                FeedbackAssert(!window.IsVisible && launchingGame == "fixture:one" && gameLaunchFeedback.Kind == FeedbackKind.Pending, "despacho e confirmado separadamente");
                now = now.AddSeconds(11); CheckGameLaunch();
                FeedbackAssert(gameLaunchFeedback.Kind == FeedbackKind.Pending && gameLaunchFeedback.Text.Contains("pode já estar aberto"), "demora nao continua afirmando iniciando");
                gamesView.Activities.Add(new GameActivity { Key = "fixture:one", Pid = 901 });
                CheckGameLaunch(false); FeedbackAssert(launchingGame != null, "leitura falha nao confirma abertura pelo snapshot preservado");
                CheckGameLaunch(true, launchVersion - 1); FeedbackAssert(launchingGame != null, "leitura anterior ao comando ignorada");
                CheckGameLaunch(true, launchVersion);
                FeedbackAssert(launchingGame == null && gameLaunchFeedback.Kind == FeedbackKind.Success && gameLaunchFeedback.Text == "Jogo de teste one aberto.", "runtime observado substitui pendencia");
                now = now.AddSeconds(6); Show(); FeedbackAssert(FeedbackText() == "", "reabrir depois de jogar nao mostra iniciando nem sucesso velho");

                gamesView.Activities.Clear(); RequestGameLaunch("fixture:two"); now = now.AddSeconds(181); CheckGameLaunch();
                FeedbackAssert(!window.IsVisible && launchingGame == null && unconfirmedLaunch == "fixture:two" && gameLaunchFeedback.Kind == FeedbackKind.Warning, "timeout nao rouba foco nem declara falha de abertura como fato");
                gamesView.Activities.Add(new GameActivity { Key = "fixture:two", Pid = 902 }); CheckGameLaunch();
                FeedbackAssert(unconfirmedLaunch == null && gameLaunchFeedback.Kind == FeedbackKind.Success, "processo identificado depois do limite atualiza resultado");
                gamesView.Activities.Clear(); RequestGameLaunch("fixture:one"); now = now.AddSeconds(183); FeedbackTick();
                FeedbackAssert(launchingGame == null && gameLaunchFeedback.Kind == FeedbackKind.Warning && !window.IsVisible, "worker sem leitura nao deixa pendencia infinita");
                Show(); gameLaunch = delegate { throw new IOException("falha simulada"); }; RequestGameLaunch("fixture:one");
                FeedbackAssert(launchingGame == null && gameLaunchFeedback.Kind == FeedbackKind.Error && window.IsVisible, "erro do despacho e imediato");
                gameLaunch = delegate { };

                gamesView = ExampleGames(); SetPanel(0); Show();
                AskCloseGame("fixture:current", "close-fixture:current"); ConfirmCloseGame();
                FeedbackAssert(closingGame == "fixture:current" && gameCloseFeedback.Kind == FeedbackKind.Pending && !window.IsVisible, "pedido de fechar oculta sem anunciar sucesso");
                gamesView.Activities.Clear(); CheckGameClose(false); FeedbackAssert(closingGame != null, "snapshot falho nao prova saida");
                CheckGameClose(true, closeVersion - 1); FeedbackAssert(closingGame != null, "snapshot anterior ao pedido nao prova fechamento");
                CheckGameClose(true, closeVersion);
                FeedbackAssert(closingGame == null && gameCloseFeedback.Kind == FeedbackKind.Success && !window.IsVisible, "saida confirmada termina fechamento sem reabrir");
                gamesView = ExampleGames(); Show(); AskCloseGame("fixture:current", "close-fixture:current"); gameCloseForce = true; ConfirmCloseGame();
                FeedbackAssert(closingGame != null && gameCloseFeedback.Kind == FeedbackKind.Pending && !window.IsVisible, "encerramento forcado tambem oculta e espera processo sair");
                gamesView.Activities.Clear(); CheckGameClose(); FeedbackAssert(gameCloseFeedback.Kind == FeedbackKind.Success, "encerramento forcado confirmado por observacao");

                gamesView = ExampleGames(); SetPanel(0); Show(); ToggleGamePause("fixture:current");
                var pause = pauseNotices["fixture:current"];
                FeedbackAssert(pause.Notice.Kind == FeedbackKind.Pending, "API de pausa nao substitui verificacao dos threads");
                gamesView.Activity("fixture:current").SuspendedShare = .9;
                CheckPauseFeedback(false, long.MaxValue); FeedbackAssert(pauseNotices.Count == 1, "erro de leitura nao confirma pausa");
                CheckPauseFeedback(true, gameCommandVersion);
                FeedbackAssert(pauseNotices.Count == 0 && pause.Notice.Kind == FeedbackKind.Success, "observacao confirma pausa");

                SetPanel(0); Feedback("Não foi possível concluir esta operação. Confira a janela do jogo; o progresso não salvo pode exigir sua confirmação.", FeedbackKind.Error);
                window.UpdateLayout();
                FeedbackAssert(((TextBlock)window.FindName("Feedback")).TextWrapping == TextWrapping.Wrap && ((TextBlock)window.FindName("Feedback")).TextTrimming == TextTrimming.None, "Home nao corta mensagens importantes");
                SaveRender("feedback-erro-completo");
                var final = BeginFeedback("Jogo fechado. Aguardando a Steam concluir a sessão anterior…", "0", 46); final.Pinned = true; RenderFeedback(); window.UpdateLayout(); SaveRender("feedback-em-andamento");
                FinishFeedback(final, "Abertura cancelada: o jogo atual ainda não fechou.", FeedbackKind.Attention); window.UpdateLayout(); SaveRender("feedback-precisa-de-acao");
                now = now.AddSeconds(13); RenderFeedback(); FeedbackAssert(FeedbackText() == "", "rodape final volta ao estado limpo");
                File.WriteAllText(Path.Combine(Output, "feedback-verificacao.txt"),
                    "OK: estados imediatos e expiracao de sucesso, pendencia separada, identidade impede resposta antiga (resultado real no historico), isolamento por painel, mensagens ocultas expiram; " +
                    "Inicio mostra so erro, aviso que pede acao e espera longa marcada (sucesso, andamento, informacao e aviso de verificacao ficam no historico); " +
                    "jogo observado substitui abertura, leitura falha/anterior ao comando nao confirma, reabertura sem iniciando, timeout nao reabre barra, identificacao tardia e worker sem leitura; " +
                    "erro de despacho, fechar/forcar aguardam saida, pausa aguarda observacao, Home quebra texto sem reticencias. Relogio/sessoes/comandos substituidos.\r\n", Encoding.UTF8);
            }
            finally
            {
                feedbackNotices.Clear(); pauseNotices.Clear(); launchingGame = unconfirmedLaunch = closingGame = gameBusy = null;
                gameLaunchFeedback = gameCloseFeedback = null; pausedByPreview.Clear();
                feedbackNow = clock; gameLaunch = launch; gameCloseWindow = close; gameKill = kill; gameSuspend = suspend; gamesView = view;
                UpdateGamesPolling(); SetPanel(0); RenderFeedback();
            }
        }
    }
}

// Layout da Home com snapshots: nenhum comando de jogo ou alteracao de preferencias.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyHomeGamesLayout()
        {
            var checks = new List<string>();
            Action<bool, string> check = delegate(bool ok, string why) {
                if (!ok) throw new Exception("Home/jogos: " + why); checks.Add("OK: " + why);
            };
            var original = gamesView;
            try
            {
                gamesView = ExampleGames(); var primary = gamesView.Current;
                gamesView.Entries[0].Name = "Genshin Impact"; gamesView.Entries[1].Name = "Neva";
                var second = TransitionFixture("fixture:one", 600, 601, 999);
                gamesView.Activities.Add(second); primary.LastFocused = 1;
                SetPanel(0); Show(); ClearFeedback(); window.UpdateLayout();
                check(gamesView.OpenGames().Count == 2 && gamesView.OpenGames()[0].Key == primary.Key, "atual precede outro aberto mesmo com timestamp maior");
                check(gameOpenRows.Count == 1 && gameOpenRows[0].Count == 3, "outro aberto fica em Voltar ao jogo com pausar e fechar");
                check(homeRows.IndexOf(gameOpenRows[0]) == homeRows.IndexOf(gameResumeRow) + 1 &&
                    homeRows.IndexOf(gameOpenRows[0]) < homeRows.IndexOf(openAppsRow), "todos os abertos sao contiguos antes dos aplicativos");
                check(gamesView.ClosedRecent().TrueForAll(e => gamesView.Activity(e.Key) == null) &&
                    !gameRecentRows.Exists(r => r.Exists(c => (string)c.Tag == "recent-fixture:one")), "abertos nao duplicam nos recentes");
                check(PrimaryGameTitle() == "Genshin Impact", "nome do atual e titulo do primeiro cartao");
                double cardHeight = gameResumeRow[0].ActualHeight;
                check(cardHeight >= 76 && Math.Abs(gameOpenRows[0][0].ActualHeight - cardHeight) < .5,
                    "principal e outro aberto tem a mesma altura e respiro: " + cardHeight + "/" + gameOpenRows[0][0].ActualHeight);
                check(gameRecentRows.TrueForAll(r => r.Count == 0 || Math.Abs(r[0].ActualHeight - cardHeight) < .5),
                    "recentes conservam a mesma proporcao dos jogos abertos");
                check(Math.Abs(gameOpenRows[0][1].ActualHeight - cardHeight) < .5 && Math.Abs(gameOpenRows[0][2].ActualHeight - cardHeight) < .5,
                    "pausar e fechar acompanham a altura uniforme do cartao");
                Func<string> where = () => { var f = Keyboard.FocusedElement; return f == null ? "nulo" : f.GetType().Name + ":" + FocusedTag(); };
                FocusHome("resume"); string t1 = where(); SettleMotion(); window.UpdateLayout(); string t2 = where(); PumpMotion(40); string t3 = where();
                SaveRender("home-jogos-agrupados");
                string fromTag = where() + " (apos FocusHome " + t1 + ", Settle " + t2 + ", Pump " + t3 + ")"; Navigate("BAIXO");
                check(Keyboard.FocusedElement == gameOpenRows[0][0], "controle desce do principal para o outro aberto" +
                    (Keyboard.FocusedElement == gameOpenRows[0][0] ? "" : ": de " + fromTag + " para " + FocusedTag() + ", linha " + homeRows.FindIndex(r =>
                        r.Contains(Keyboard.FocusedElement as Control)) +
                    ", principal na linha " + homeRows.IndexOf(gameResumeRow) + ", outro na linha " + homeRows.IndexOf(gameOpenRows[0])));
                Navigate("DIREITA"); check(Keyboard.FocusedElement == gameOpenRows[0][1], "pausa continua a direita do cartao");
                gamesView.Current = second; RenderGames(false); window.UpdateLayout();
                check(PrimaryGameTitle() == "Neva" && (string)((Control)Keyboard.FocusedElement).Tag == "pause-fixture:one",
                    "troca de destaque reordena sem perder a acao e identidade do foco");
                FocusHome("resume"); gamesView.Current = primary; RenderGames(false); window.UpdateLayout();
                check((string)((Control)Keyboard.FocusedElement).Tag == "recent-fixture:one", "foco no cartao acompanha o jogo que passou a segundo");
                second.Paused = true; gamesView.Find(second.Key).Available = false; RenderGames(false);
                check(gameOpenRows.Count == 1 && gameOpenRows[0].Count == 3 && gameOpenRows[0][0].IsEnabled,
                    "aberto pausado ou indisponivel para novo lancamento continua no grupo");
                second.Paused = false; gamesView.Find(second.Key).Available = true;
                AskCloseGame(second.Key, "recent-close-" + second.Key); SettleMotion(); window.UpdateLayout(); PumpMotion(40);
                check(gameRecentCaption.Text == "FECHAR JOGO" && TextOnlyGameOption("game-close-confirm") && TextOnlyGameOption("game-cancel"),
                    "confirmar e cancelar fechamento so tem texto, sem imagem ou coluna vazia");
                check((string)((Control)Keyboard.FocusedElement).Tag == "game-cancel", "confirmacao continua iniciando em Cancelar");
                SaveRender("home-fechar-texto");
                gamesView.Current = second; RenderGames(false);
                check(gameCloseOrigin == "close-" + second.Key, "origem do fechar acompanha o jogo que virou principal durante a confirmacao");
                gameCloseForce = true; RenderGames(true);
                check(TextOnlyGameOption("game-close-confirm") && TextOnlyGameOption("game-cancel"), "forcar fechamento tambem usa somente texto");
                CancelGame(); check((string)((Control)Keyboard.FocusedElement).Tag == "close-" + second.Key,
                    "cancelar retorna ao fechar do jogo escolhido no grupo de abertos");
                gamesView.Current = primary; RenderGames(false);
                gamesView.Activities.Remove(second); RenderGames(false);
            check(gameOpenRows.Count == 0 && gamesView.ClosedRecent().Exists(e => e.Key == second.Key), "jogo fechado migra para recentes e sai do grupo aberto");
                SelectGame(second.Key); SettleMotion(); window.UpdateLayout();
            check(TextOnlyGameOption("game-confirm") && TextOnlyGameOption("game-cancel"), "troca de jogo tambem apresenta opcoes sem icones ou coluna vazia");
                check((string)((Control)Keyboard.FocusedElement).Tag == "game-cancel", "troca de jogo comeca em Cancelar");
                var option = (Button)actions.Find(c => (string)c.Tag == "game-confirm");
                check(((TextBlock)((StackPanel)((Grid)option.Content).Children[0]).Children[0]).Text == "Fechar jogo e iniciar",
                    "opcao explicita que o jogo atual deve fechar antes de iniciar outro");
                SaveRender("home-fechar-iniciar"); CancelGame();
                for (int i = 0; i < 14; i++)
                {
                    string key = "fixture:open-" + i;
                    gamesView.Entries.Add(new GameEntry { Key = key, Name = "Jogo aberto " + (i + 1), Kind = "Game", Available = true });
                    gamesView.Activities.Add(TransitionFixture(key, (uint)(800 + i), 900 + i, i));
                }
                RenderGames(false); StopMotion(); window.UpdateLayout();
                check(gameOpenRows.Count == 14 && gameOpenRows.TrueForAll(r => r.Count == 3), "mais de tres abertos continuam todos no mesmo grupo");
                check(homeRows.IndexOf(gameOpenRows[13]) < homeRows.IndexOf(openAppsRow), "ultimo aberto continua antes dos apps");
                gameOpenRows[13][0].Focus(); window.UpdateLayout();
                var scroll = (ScrollViewer)MotionPart("BodyScroll");
                check(scroll.ScrollableHeight > 0 && scroll.VerticalOffset > 0, "lista longa rola pelo corpo e acompanha o foco do controle");
                gamesView.Current = null; gamesView.Activities.Clear(); RenderGames(true); window.UpdateLayout();
                check(gameOpenRows.Count == 0 && gameResumeRow.Count == 1 && gameResumeRow[0].IsEnabled,
                    "sem abertos conserva o cartao do ultimo jogo sem botoes de processo");
                check(gamesView.ClosedRecent().Count <= 3, "recentes fechados mantem o limite de tres");
                check(primary.LastFocused == 1 && gamesView.Find(primary.Key).Observed == 0,
                    "layout nao grava atividade nem modifica historico ou foco observado");
            }
            finally
            {
                gameCloseConfirmation = gameConfirmation = null; gameCloseForce = false;
                gamesView = original; SetPanel(0); ClearFeedback();
            }
            checks.Insert(0, "OK: " + checks.Count + " verificacoes de layout da Home.");
            checks.Add("Snapshots e renders de fixtures. Nenhum jogo, Steam, FSE ou preferencia real operado.");
            File.WriteAllLines(Path.Combine(Output, "home-jogos-layout-verificacao.txt"), checks, Encoding.UTF8);
        }
        string PrimaryGameTitle()
        { return ((TextBlock)((StackPanel)((Grid)((Button)gameResumeRow[0]).Content).Children[1]).Children[0]).Text; }
        bool TextOnlyGameOption(string tag)
        {
            var button = actions.Find(c => (string)c.Tag == tag) as Button;
            var grid = button == null ? null : button.Content as Grid;
            if (grid == null || grid.ColumnDefinitions.Count != 1 || grid.Children.Count != 1) return false;
            var labels = grid.Children[0] as StackPanel;
            if (labels == null || labels.Margin.Left != 0) return false;
            foreach (var item in labels.Children) if (!(item is TextBlock)) return false;
            return true;
        }
    }
}

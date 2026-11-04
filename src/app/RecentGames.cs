// Home real: catalogo, sessoes e recentes em worker. Acoes externas somente por escolha.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using XGBP.Desktop;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        const string GameGlyph = "\uE7FC";
        readonly DispatcherTimer gamesTimer = new DispatcherTimer();
        readonly List<Control> gameControls = new List<Control>(), gameResumeRow = new List<Control>();
        readonly List<List<Control>> gameRecentRows = new List<List<Control>>();
        readonly List<List<Control>> gameOpenRows = new List<List<Control>>();
        TextBlock gameRecentCaption;
        string renderedPrimaryGame;
        StackPanel gameResumeSlot, gameRecentsSlot;
        GamesView gamesView;
        GameCatalogState gameCatalog;
        GameSessions gameSessions;
        GameImages gameImages;
        readonly Dictionary<string, BitmapImage> gameBitmaps = new Dictionary<string, BitmapImage>();
        bool gamesBusy;
        bool gamesSnapshotFresh;
        long gamesSnapshotVersion;
        DateTime gamesNextCatalog = DateTime.MinValue;
        string gamesSignature, gameConfirmation, launchingGame, lastGamesStatus, gamesRepeated = "";
        DateTime launchDeadline;
        FeedbackNotice gameLaunchFeedback, gameCloseFeedback, gameBusyFeedback;
        string unconfirmedLaunch, launchingName;
        bool launchSlow;
        long gameCommandVersion, launchVersion, closeVersion;
        DateTime launchRequested;
        sealed class PauseNotice { public string Key, Name; public bool Paused; public long Version; public DateTime Deadline; public FeedbackNotice Notice; }
        readonly Dictionary<string, PauseNotice> pauseNotices = new Dictionary<string, PauseNotice>();
        void UpdateGamesPolling()
        {
            bool pending = launchingGame != null || gameLaunchRoute != null || closingGame != null || gameBusy != null || pauseNotices.Count > 0;
            bool quiet = !window.IsVisible && activationMode != true && (gamesView == null || gamesView.Activities.Count == 0);
            gamesTimer.Interval = TimeSpan.FromMilliseconds(pending ? 500 : quiet ? 5000 : 2000);
        }
        Action<GameEntry> gameLaunch = LaunchGame;
        // Jogos abertos: pausar/retomar e fechar, sempre por escolha. Substituiveis nos testes; a verificacao nunca toca processos reais.
        string gameCloseConfirmation, gameCloseOrigin, gameBusy, closingGame, closingName;
        bool gameCloseForce;
        DateTime closeDeadline;
        // Cada processo pausado pela previa, com a criacao: um PID reaproveitado nunca e retomado por engano,
        // e os demais sao retomados mesmo se o processo principal ja saiu.
        readonly PausedGameRegistry pausedByPreview = new PausedGameRegistry();
        readonly GameOperationCoordinator gameOperations = new GameOperationCoordinator();
        Func<IList<uint>, string> gameSuspend = GameControl.Suspend, gameResume = GameControl.Resume, gameKill = GameControl.Kill;
        Func<IEnumerable<uint>, List<uint>> gameTree = GameControl.Tree;
        Action<IntPtr> gameCloseWindow = GameControl.Close, gameMinimize = GameControl.Minimize;

        void StartGames()
        {
            StartGameProcessService();
            StartGameTransitions();
            StartSteamSessionObservation();
#if XGB_VERIFY
            if (verify)
            {
                gamesView = ExampleGames(); gamesSnapshotFresh = true; gamesSnapshotVersion = long.MaxValue; gameLaunch = delegate { };
                gameSuspend = gameResume = gameKill = delegate { return null; }; gameCloseWindow = gameMinimize = delegate { };
                gameTree = delegate(IEnumerable<uint> roots) { return new List<uint>(roots); };
                return;
            }
#endif
            StartPauseRecovery();
            UpdateGamesPolling(); gamesTimer.Tick += delegate { GamesTick(); }; gamesTimer.Start();
            GamesTick();
        }
        void GamesTick()
        {
            if (verify || disposed || gamesBusy) return;
            bool readCatalog = DateTime.UtcNow >= gamesNextCatalog;
            // Sem catalogo (a leitura falhou), espera a nova tentativa em vez de reler tudo a cada tick de 2 s.
            if (gameCatalog == null && !readCatalog) return;
            gamesBusy = true;
            long readVersion = gameCommandVersion;
            IntPtr fg = GetForegroundWindow(); uint fgPid = 0;
            if (fg != IntPtr.Zero && fg != handle) GetWindowThreadProcessId(fg, out fgPid);
            gamesNextCatalog = readCatalog ? DateTime.UtcNow.AddSeconds(60) : gamesNextCatalog;
            Task.Factory.StartNew(delegate {
                GamesView view = null; string error = null; bool fresh = false; SteamReleaseSnapshot steamState = null;
                try
                {
                    if (readCatalog)
                    {
                        var settings = GameFiles.Read<GameSettings>(Path.Combine(GameFiles.Folder, "catalog.json"));
                        var catalog = SteamCatalog.Read(settings);
                        string repeated = string.Join(", ", catalog.Repeated.ToArray());
                        if (repeated != gamesRepeated) { gamesRepeated = repeated; if (repeated.Length > 0) WriteLog("jogos: registros repetidos ignorados | " + repeated); }
                        gameCatalog = catalog;
                    }
                    if (gameImages == null) gameImages = new GameImages(Path.Combine(GameFiles.Folder, "images"));
                    if (readCatalog) gameImages.RefreshDesktop();
                    if (gameSessions == null) gameSessions = new GameSessions(new GameHistory(Path.Combine(GameFiles.Folder, "history.json")));
                    var windows = DesktopWindows.Read((uint)Process.GetCurrentProcess().Id);
                    var processes = GameProcess.Read();
                    view = gameSessions.Update(gameCatalog, processes, windows, fgPid, DateTime.UtcNow);
                    fresh = true;
                    // Pausa observada pelos threads: vale tambem para pausas feitas por outra ferramenta ou antes de reiniciar a previa.
                    foreach (var a in view.Activities) { a.SuspendedShare = GameControl.SuspendedShare(a.Pid); a.Paused = a.SuspendedShare > .5; }
                    gameImages.Fill(view);
                    if (steamSessions == null) steamSessions = new SteamSessionRelease(SteamInput.ConsoleLog());
                    steamState = steamSessions.Update(processes, view, DateTime.UtcNow);
                }
                catch (Exception ex) { error = ex.Message; }
                Dispatch(delegate {
                    gamesBusy = false;
                    gamesSnapshotFresh = fresh; if (fresh) gamesSnapshotVersion = readVersion;
                    ObserveSteamSessionsOnUi(steamState, readVersion);
                    if (view != null)
                    {
                        // Usar a janela anterior a barra para a retomada, sem inventar uma leitura de atividade.
                        if (window.IsVisible && previousForegroundPid > 0)
                        {
                            var previous = view.Activities.Find(a => a.Pids.Contains((uint)previousForegroundPid));
                            if (previous != null) view.Current = previous;
                        }
                        if (gameBusy == null && readVersion >= gameCommandVersion) ApplyPausedState(view);
                        ObserveGameExit(view, fresh, readVersion, fgPid);
                        gamesView = view;
                    }
                    else if (gamesView == null) gamesView = new GamesView();
                    string status = error == null ? gamesView.Status : "Não foi possível atualizar os jogos. Última leitura preservada: " + error;
                    if (status != lastGamesStatus) { lastGamesStatus = status; if (!string.IsNullOrEmpty(status)) WriteLog("jogos: " + status); }
                    if (error != null) gamesNextCatalog = DateTime.UtcNow.AddSeconds(10);
                    CheckGameLaunch(fresh, readVersion); CheckGameClose(fresh, readVersion); CheckPauseFeedback(fresh, readVersion); UpdateGamesPolling(); RenderGames(false);
                });
            });
        }
        void BuildGameResume()
        {
            gameControls.Clear(); gameRecentRows.Clear(); gameOpenRows.Clear(); gamesSignature = null;
            var heading = HomeCaption("VOLTAR AO JOGO");
            ((FrameworkElement)heading.Parent).Margin = new Thickness(0, 0, 0, 3);
            gameResumeSlot = new StackPanel(); body.Children.Add(gameResumeSlot);
            gameResumeRow.Clear(); homeRows.Add(gameResumeRow);
        }
        void BuildGameRecents()
        {
            var hint = HomeCaption("JOGOS RECENTES");
            gameRecentCaption = (TextBlock)((DockPanel)hint.Parent).Children[1];
            gameRecentsSlot = new StackPanel(); body.Children.Add(gameRecentsSlot);
            for (int i = 0; i < 3; i++) { var row = new List<Control>(); gameRecentRows.Add(row); homeRows.Add(row); }
            RenderGames(true);
        }
        FrameworkElement GameImage(GameEntry game)
        {
            if (game != null && game.IconPng != null) try
            {
                BitmapImage bitmap; string key = game.IconKey ?? game.Key;
                if (!gameBitmaps.TryGetValue(key, out bitmap))
                {
                    bitmap = new BitmapImage();
                    using (var stream = new MemoryStream(game.IconPng)) { bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); }
                    if (gameBitmaps.Count >= 128) gameBitmaps.Clear(); gameBitmaps[key] = bitmap;
                }
                var image = new Image { Source = bitmap, Width = 32, Height = 32, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var layer = new Grid { Width = 32, Height = 32 };
                var fallback = Symbol(GameGlyph, 23); layer.Children.Add(fallback); layer.Children.Add(image);
                if (motionImages.Add(key) && MotionEnabled)
                {
                    MotionTo(fallback, UIElement.OpacityProperty, 0, MotionNormal, 1);
                    MotionTo(image, UIElement.OpacityProperty, 1, MotionNormal, 0, 0, delegate { layer.Children.Remove(fallback); });
                }
                else layer.Children.Remove(fallback);
                return layer;
            }
            catch { }
            return Symbol(GameGlyph, 23);
        }
        Button GameButton(StackPanel parent, List<Control> row, string title, string detail, string tag, Action action, bool resume, GameEntry game = null, GameActivity open = null, bool textOnly = false)
        {
            var content = new Grid();
            if (!textOnly)
            {
                content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
                content.Children.Add(new Border { Background = Brush("SurfaceHover"), CornerRadius = new CornerRadius(6), Height = 44, Child = GameImage(game) });
            }
            content.ColumnDefinitions.Add(new ColumnDefinition());
            var labels = new StackPanel { Margin = new Thickness(textOnly ? 0 : 13, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var name = Text(title, resume ? 22 : 20, "TextPrimary"); name.TextWrapping = TextWrapping.NoWrap; name.TextTrimming = TextTrimming.CharacterEllipsis;
            labels.Children.Add(name); var sub = Text(detail, 14, "Muted"); sub.TextWrapping = TextWrapping.NoWrap; sub.TextTrimming = TextTrimming.CharacterEllipsis; labels.Children.Add(sub);
            Grid.SetColumn(labels, textOnly ? 0 : 1); content.Children.Add(labels);
            var button = HomeButton(content, action); button.Tag = tag; button.ToolTip = title + " · " + detail;
            System.Windows.Automation.AutomationProperties.SetName(button, title + " · " + detail);
            // Mesma proporcao para todos os jogos; hierarquia pelo texto/status, nunca comprimindo o cartao.
            button.Padding = resume || game != null ? new Thickness(12, 11, 12, 11) : new Thickness(10, 6, 10, 6);
            if (game != null) button.MinHeight = 76;
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            row.Add(button); gameControls.Add(button);
            if (open == null || game == null) { parent.Children.Add(button); return button; }
            // Jogo aberto: pausar/retomar e fechar a direita do cartao, na altura dele e no estilo dos atalhos da Home.
            var line = new Grid();
            line.ColumnDefinitions.Add(new ColumnDefinition());
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.Children.Add(button);
            // Outros jogos abertos podem estar na lista; etiquetas proprias preservam a identidade do foco.
            string key = game.Key, gameName = game.Name, prefix = resume ? "" : "recent-", closeTag = prefix + "close-" + key;
            row.Add(GameIcon(line, 1, open.Paused ? "\uE768" : "\uE769", open.Paused ? "Retomar " + gameName : "Pausar " + gameName + " · jogos online podem desconectar", prefix + "pause-" + key, delegate { ToggleGamePause(key); }));
            var close = GameIcon(line, 2, "\uE711", "Fechar " + gameName, closeTag, delegate { AskCloseGame(key, closeTag); });
            close.Style = (Style)window.FindResource("CloseAction"); row.Add(close);
            parent.Children.Add(line); return button;
        }
        Button GameIcon(Grid line, int column, string glyph, string label, string tag, Action action)
        {
            var button = HomeButton(Symbol(glyph, 20), action); button.Tag = tag; button.ToolTip = label;
            System.Windows.Automation.AutomationProperties.SetName(button, label);
            button.Width = 52; button.Padding = new Thickness(0); button.HorizontalContentAlignment = HorizontalAlignment.Center; button.Margin = new Thickness(8, 0, 0, 8);
            Grid.SetColumn(button, column); line.Children.Add(button); gameControls.Add(button); return button;
        }
        static string PlayedLabel(GameEntry e)
        {
            DateTime date = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(e.LastPlayed).ToLocalTime();
            string when = date.Date == DateTime.Today ? "hoje" : date.Date == DateTime.Today.AddDays(-1) ? "ontem" : date.ToString("dd/MM/yyyy");
            return "Último acesso · " + when;
        }
        void RenderGames(bool force)
        {
            if (panel != 0 || gameRecentsSlot == null || gameRecentRows.Count != 3) return;
            // Estado da operacao continua imediato; apenas os controles aguardam a entrada.
            if (gameCloseConfirmation != null && (gamesView == null || gamesView.Activity(gameCloseConfirmation) == null)) { gameCloseConfirmation = null; gameCloseForce = false; }
            if (!force && DeferOpeningVisual(2)) return;
            var focused = Keyboard.FocusedElement as Control;
            bool hadFocus = gameControls.Contains(focused);
            string tag = focused == null ? null : focused.Tag as string;
            var openGames = gamesView == null ? new List<GameEntry>() : gamesView.OpenGames();
            var featured = openGames.Count > 0 ? openGames[0] : gamesView == null ? null : gamesView.Featured;
            var recent = gamesView == null ? new List<GameEntry>() : gamesView.ClosedRecent();
            tag = GameHomeFocusTag(tag, renderedPrimaryGame, featured == null ? null : featured.Key);
            gameCloseOrigin = GameHomeFocusTag(gameCloseOrigin, renderedPrimaryGame, featured == null ? null : featured.Key);
            // A lista segue sempre o snapshot canonico, sem recuperar linhas antigas para fixar
            // posicoes. O foco e restaurado por identidade ao final, ou volta ao destaque se saiu.
            var playing = featured == null || gamesView == null ? null : gamesView.Activity(featured.Key);
            bool running = playing != null;
            string signature = DateTime.Today.Ticks + "|" + (gamesView == null ? "loading" : "ready") + "|" + gameConfirmation + "|" + gameCloseConfirmation + gameCloseForce + "|" +
                (featured == null ? "" : featured.Key + featured.Name + featured.Available + featured.LastPlayed + featured.IconKey) + "|" + GameState(playing);
            foreach (var e in recent) signature += "|" + e.Key + e.LastPlayed + e.Name + e.Source + e.IconKey + GameState(gamesView.Activity(e.Key));
            foreach (var e in openGames) signature += "|open:" + e.Key + e.Name + e.IconKey + GameState(gamesView.Activity(e.Key));
            signature += "|" + lastGamesStatus;
            if (!force && signature == gamesSignature) return; gamesSignature = signature;
            var oldTags = new HashSet<string>(); foreach (var c in gameControls) oldTags.Add((string)c.Tag);
            var wanted = new HashSet<string> { "resume" };
            if (running) { wanted.Add("pause-" + featured.Key); wanted.Add("close-" + featured.Key); }
            foreach (var e in openGames) if (e != featured)
            { wanted.Add("recent-" + e.Key); wanted.Add("recent-pause-" + e.Key); wanted.Add("recent-close-" + e.Key); }
            if (gameConfirmation != null) { wanted.Add("game-confirm"); wanted.Add("game-cancel"); }
            else if (gameCloseConfirmation != null) { wanted.Add("game-close-confirm"); wanted.Add("game-cancel"); }
            else foreach (var e in recent)
            {
                wanted.Add("recent-" + e.Key);
                if (gamesView.Activity(e.Key) != null) { wanted.Add("recent-pause-" + e.Key); wanted.Add("recent-close-" + e.Key); }
            }
            foreach (var c in gameControls) if (!wanted.Contains((string)c.Tag)) MotionExit(c);
            CancelElementMotion(gameResumeSlot); CancelElementMotion(gameRecentsSlot);
            double offset = ((ScrollViewer)window.FindName("BodyScroll")).VerticalOffset;
            foreach (var c in gameControls) actions.Remove(c); gameControls.Clear(); gameResumeRow.Clear(); foreach (var row in gameRecentRows) row.Clear();
            foreach (var row in gameOpenRows) homeRows.Remove(row); gameOpenRows.Clear();
            gameResumeSlot.Children.Clear(); gameRecentsSlot.Children.Clear();
            string detail = featured == null ? gamesView == null ? "Buscando último jogo…" : "Nenhum jogo executado ainda" :
                running ? playing.Paused ? "Jogo atual · pausado" : "Jogo atual · em execução" : featured.Available ? "Último jogo executado" : "Indisponível";
            string primaryKey = featured == null ? null : featured.Key;
            var resume = GameButton(gameResumeSlot, gameResumeRow, featured == null ? "Voltar ao jogo" : featured.Name, detail, "resume", delegate { if (primaryKey != null) SelectGame(primaryKey); }, true, featured, playing);
            renderedPrimaryGame = primaryKey;
            resume.IsEnabled = featured != null && (running || featured.Available);
            if (featured != null) resume.ToolTip = featured.Name + (running ? playing.Paused ? " · retomar e voltar ao jogo" : " · retomar janela" : " · iniciar novamente");
            int openRow = homeRows.IndexOf(gameResumeRow) + 1;
            foreach (var entry in openGames) if (entry.Key != primaryKey)
            {
                string key = entry.Key; var open = gamesView.Activity(key); var row = new List<Control>();
                gameOpenRows.Add(row); homeRows.Insert(openRow++, row);
                GameButton(gameResumeSlot, row, entry.Name, open.Paused ? "Pausado" : "Em execução", "recent-" + key,
                    delegate { SelectGame(key); }, false, entry, open);
            }
            gameRecentCaption.Text = gameCloseConfirmation != null ? "FECHAR JOGO" : gameConfirmation != null ? "ABRIR OUTRO JOGO" : "JOGOS RECENTES";
            if (gameCloseConfirmation != null)
            {
                var target = gamesView.Find(gameCloseConfirmation); string name = target == null ? "o jogo" : target.Name;
                var question = Text(gameCloseForce ? name + " não fechou. Forçar o encerramento?" : "Fechar " + name + "? O progresso não salvo pode ser perdido.", 16, "TextPrimary");
                question.Margin = new Thickness(0, 0, 0, 8); gameRecentsSlot.Children.Add(question);
                GameButton(gameRecentsSlot, gameRecentRows[0], gameCloseForce ? "Forçar fechamento" : "Fechar jogo",
                    gameCloseForce ? "Encerra o processo sem salvar" : "Pede ao jogo para fechar normalmente", "game-close-confirm", ConfirmCloseGame, false, null, null, true);
                GameButton(gameRecentsSlot, gameRecentRows[1], "Cancelar", "Manter o jogo aberto", "game-cancel", CancelGame, false, null, null, true);
            }
            else if (gameConfirmation != null)
            {
                var target = gamesView.Find(gameConfirmation);
                string names = gamesView == null ? "o jogo atual" : string.Join(", ", gamesView.OpenGames().ConvertAll(e => e.Name).ToArray());
                var question = Text("Fechar " + names + " para iniciar " + (target == null ? "este jogo" : target.Name) + "? O progresso não salvo pode ser perdido.", 16, "TextPrimary");
                question.Margin = new Thickness(0, 0, 0, 8); gameRecentsSlot.Children.Add(question);
                GameButton(gameRecentsSlot, gameRecentRows[0], "Fechar jogo e iniciar", gamesView.Activities.Count > 1 ? "Iniciar depois que todos os jogos abertos encerrarem" : "Iniciar depois que o jogo atual encerrar", "game-confirm", ConfirmGame, false, null, null, true);
                GameButton(gameRecentsSlot, gameRecentRows[1], "Cancelar", "Manter o jogo atual aberto", "game-cancel", CancelGame, false, null, null, true);
            }
            else if (recent.Count == 0)
            {
                var empty = Text(gamesView == null ? "Buscando jogos…" : string.IsNullOrEmpty(lastGamesStatus) ? "Nenhum jogo recente disponível" : "Histórico de jogos indisponível", 15, "Muted");
                empty.ToolTip = lastGamesStatus; gameRecentsSlot.Children.Add(empty);
            }
            else for (int i = 0; i < recent.Count; i++)
            {
                string key = recent[i].Key; var e = recent[i]; var open = gamesView.Activity(key);
                var button = GameButton(gameRecentsSlot, gameRecentRows[i], e.Name, open == null ? PlayedLabel(e) : open.Paused ? "Pausado" : "Em execução", "recent-" + key, delegate { SelectGame(key); }, false, e, open);
                button.ToolTip = e.Name + " · " + e.Source + (open != null ? open.Paused ? " · pausado" : " · aberto" : e.Observed >= e.Imported && e.Observed > 0 ? " · sessão observada neste PC" : " · histórico informado pela Steam");
            }
            if (!force) foreach (var c in gameControls) if (!oldTags.Contains((string)c.Tag)) MotionReveal(c);
            if ((gameConfirmation != null || gameCloseConfirmation != null) && !oldTags.Contains("game-cancel")) MotionReveal(gameRecentsSlot, MotionNormal, 24);
            if (!force) { window.UpdateLayout(); ((ScrollViewer)window.FindName("BodyScroll")).ScrollToVerticalOffset(offset); }
            // Botao do jogo que acabou de fechar: o foco volta ao cartao do destaque, nao ao inicio da Home.
            if (hadFocus) FocusHome(actions.Exists(c => (string)c.Tag == tag) ? tag : "resume");
        }
        static string GameState(GameActivity a) { return a == null ? "closed" : (a.Window == null ? "nowindow" : a.Window.Key) + (a.Paused ? ":paused" : ":running"); }
        bool GameConfirmationNavigate(string key)
        {
            if (gameConfirmation == null && gameCloseConfirmation == null) return false;
            FocusHome(key == "CIMA" || key == "ESQUERDA" ? gameConfirmation != null ? "game-confirm" : "game-close-confirm" : "game-cancel"); return true;
        }
        bool GamesBack() { if (gameConfirmation == null && gameCloseConfirmation == null) return false; CancelGame(); return true; }
        void CancelGame()
        {
            if (gameCloseConfirmation != null)
            {
                gameCloseConfirmation = null; gameCloseForce = false; RenderGames(true);
                FocusHome(actions.Exists(c => (string)c.Tag == gameCloseOrigin) ? gameCloseOrigin : "resume"); return;
            }
            string key = gameConfirmation; gameConfirmation = null; RenderGames(true); FocusHome(gamesView != null && gamesView.Featured != null && gamesView.Featured.Key == key ? "resume" : "recent-" + key);
        }
        void ConfirmGame() { string key = gameConfirmation; gameConfirmation = null; RenderGames(true); BeginGameLaunchRoute(key); }
        void RequestGameLaunch(string key)
        {
            var e = gamesView == null ? null : gamesView.Find(key);
            if (e == null || !e.Available || !verify && !GameFiles.Ready(e))
            { Feedback("O jogo está indisponível. Confira o caminho ou abra a Biblioteca.", FeedbackKind.Attention); gamesNextCatalog = DateTime.MinValue; return; }
            if (launchingGame != null || gameLaunchRoute != null) { FinishFeedback(gameLaunchFeedback, "Há uma abertura aguardando confirmação. Confira o jogo antes de tentar novamente.", FeedbackKind.Pending); return; }
            if (gamesView.Activity(key) != null) { ResumeOpenGame(key, gamesView.Activity(key)); return; }
            if (gamesView.Activities.Count > 0)
            { gameCloseConfirmation = null; gameConfirmation = key; RenderGames(true); FocusHome("game-cancel"); return; }
            // Tambem cobre fechar manualmente/nativamente e escolher um recente em seguida.
            if (!SteamSessionCanLaunch()) { BeginGameLaunchRoute(key); return; }
            gameLaunchFeedback = BeginFeedback("Abertura solicitada para " + e.Name + ". Aguardando o processo do jogo…", "0", 180);
            launchingName = e.Name; unconfirmedLaunch = null; launchSlow = false;
            DispatchGameLaunch(e);
        }
        void DispatchGameLaunch(GameEntry e)
        {
            launchRequested = feedbackNow(); launchVersion = ++gameCommandVersion;
            try
            {
                gameLaunch(e); launchingGame = e.Key; launchDeadline = feedbackNow().AddSeconds(180); UpdateGamesPolling();
                // Como no Xbox, iniciar um jogo tem som proprio no lugar do de selecionar.
                ActionSound("jogo");
                WriteLog("jogos: inicio solicitado | " + e.Key); Hide();
            }
            catch (Exception ex) { FinishFeedback(gameLaunchFeedback, "Não foi possível iniciar " + e.Name + ".", FeedbackKind.Error); WriteLog("jogos: falha de despacho | " + ex.Message); }
        }
        static void LaunchGame(GameEntry e)
        {
            if (!string.IsNullOrEmpty(e.LaunchUri)) LaunchSteamProtocol(e.LaunchUri);
            else
            {
                if (!string.Equals(Path.GetExtension(e.LaunchFile), ".exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Cadastre um executável de jogo.");
                Process.Start(new ProcessStartInfo { FileName = e.LaunchFile, Arguments = e.Arguments ?? "", WorkingDirectory = e.WorkingDirectory ?? Path.GetDirectoryName(e.LaunchFile), UseShellExecute = true });
            }
        }
        void CheckGameLaunch(bool fresh = true, long readVersion = long.MaxValue)
        {
            string key = launchingGame ?? unconfirmedLaunch;
            if (key == null) return;
            if (!fresh || readVersion < launchVersion)
            {
                if (launchingGame != null && feedbackNow() >= launchDeadline)
                { unconfirmedLaunch = launchingGame; launchingGame = null; FinishFeedback(gameLaunchFeedback, "Não foi possível verificar a abertura de " + launchingName + ". Confira o jogo antes de tentar novamente.", FeedbackKind.Warning); UpdateGamesPolling(); }
                return;
            }
            if (gamesView != null && gamesView.Activities.Exists(a => a.Key == key))
            { WriteLog("jogos: processo de runtime observado | " + key); FinishFeedback(gameLaunchFeedback, launchingName + " aberto.", FeedbackKind.Success); launchingGame = unconfirmedLaunch = null; UpdateGamesPolling(); return; }
            if (launchingGame == null) return;
            if (feedbackNow() >= launchDeadline)
            { unconfirmedLaunch = launchingGame; launchingGame = null; FinishFeedback(gameLaunchFeedback, "Não foi possível confirmar a abertura de " + launchingName + ". O jogo pode estar aberto; confira antes de tentar novamente.", FeedbackKind.Warning); UpdateGamesPolling(); }
            else if (!launchSlow && feedbackNow() - launchRequested >= TimeSpan.FromSeconds(10))
            { launchSlow = true; FinishFeedback(gameLaunchFeedback, "Abertura de " + launchingName + " solicitada. O jogo ainda não foi identificado e pode já estar aberto.", FeedbackKind.Pending); }
        }
        void CheckPauseFeedback(bool fresh, long readVersion)
        {
            if (!fresh) return;
            foreach (var key in new List<string>(pauseNotices.Keys))
            {
                var pending = pauseNotices[key]; if (readVersion < pending.Version) continue;
                var activity = gamesView == null ? null : gamesView.Activity(key);
                if (activity == null) { FinishFeedback(pending.Notice, pending.Name + " foi fechado durante a operação.", FeedbackKind.Warning); pauseNotices.Remove(key); }
                else if (activity.SuspendedShare >= 0 && activity.Paused == pending.Paused)
                { FinishFeedback(pending.Notice, pending.Name + (pending.Paused ? " pausado." : " retomado."), FeedbackKind.Success); pauseNotices.Remove(key); }
                else if (feedbackNow() >= pending.Deadline)
                { FinishFeedback(pending.Notice, "Não foi possível confirmar " + (pending.Paused ? "a pausa de " : "a retomada de ") + pending.Name + ".", FeedbackKind.Warning); pauseNotices.Remove(key); }
            }
        }
#if XGB_VERIFY
        static GamesView ExampleGames()
        {
            long t = GameFiles.Epoch(DateTime.UtcNow.AddDays(-1));
            var view = new GamesView();
            foreach (string id in new[] { "current", "one", "two", "three" }) view.Entries.Add(new GameEntry {
                Key = "fixture:" + id, Name = id == "current" ? "Jogo em execução" : "Jogo de teste " + id, Source = "Teste", Kind = "Game", Available = true,
                Imported = t--, LaunchUri = "steam://rungameid/42" });
            var current = new GameActivity { Key = "fixture:current", Pid = 500, Pids = new List<uint> { 500 },
                Window = new DesktopWindow { Handle = new IntPtr(501), ProcessId = 500, Title = "Jogo de teste", AppName = "Jogo de teste" } };
            view.Current = current; view.Activities.Add(current); return view;
        }
#endif
#if XGB_VERIFY
        public void VerifyGamesLive()
        {
            var settings = GameFiles.Read<GameSettings>(Path.Combine(GameFiles.Folder, "catalog.json"));
            var catalog = SteamCatalog.Read(settings); var lines = new List<string>();
            var history = new GameHistory(Path.Combine(GameFiles.Folder, "history.json"));
            uint pid = 0; GetWindowThreadProcessId(GetForegroundWindow(), out pid);
            gamesView = new GameSessions(history).Update(catalog, GameProcess.Read(), DesktopWindows.Read((uint)Process.GetCurrentProcess().Id), 0, DateTime.UtcNow);
            var images = new GameImages(Path.Combine(Output, "games-images-live-cache")); images.RefreshDesktop(); images.Fill(gamesView);
            foreach (var e in gamesView.Entries) lines.Add(e.Name + " | " + e.Key + " | " + e.Kind + " | disponivel=" + e.Available + " | ultimo=" + e.LastPlayed + " | " + e.LaunchUri + " | runtime=" + string.Join(";", e.RuntimePaths));
            lines.Add("Estado: " + catalog.Status); lines.Add("Sem lancamentos e sem observacao de foreground/persistencia neste diagnostico.");
            foreach (var e in gamesView.Entries) if (e.IconPng != null) lines.Add("Imagem: " + e.Name + " | " + e.IconSource + " | " + e.IconKey);
            lines.Add("Cache de imagens deste diagnostico somente em bin/design-preview/games-images-live-cache.");
            File.WriteAllLines(Path.Combine(Output, "jogos-reais.txt"), lines, Encoding.UTF8);
            lastGamesStatus = catalog.Status; window.Width = 1920; window.Height = 1080; window.Left = -30000; window.Top = -30000;
            window.Show(); SetPanel(0); window.UpdateLayout(); SaveRender("home-jogos-reais"); window.Hide();
        }
#endif
    }
}

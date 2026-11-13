// Historico de acoes e problemas (ADR 0078): uma entrada por operacao, atualizada ate o resultado.
// Fica no perfil local, ate 300 entradas e 7 dias; a gravacao e agrupada e sai da interface.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using XGBP.Games;

namespace XGBP.Design
{
    // Horarios em milissegundos UTC: evita as conversoes de fuso do DateTime no serializador JSON.
    [DataContract]
    sealed class ActionHistoryEntry
    {
        [DataMember(IsRequired = true)] public long Id;
        [DataMember(IsRequired = true)] public long Started;
        [DataMember(IsRequired = true)] public long Updated;
        [DataMember(IsRequired = true)] public string Area;
        [DataMember(IsRequired = true)] public string Text;
        [DataMember(IsRequired = true)] public int Kind;
        [DataMember] public int Count;
        public long Touched; // ordem da ultima mudanca nesta sessao, independente de relogios substituidos
        public FeedbackKind Type { get { return (FeedbackKind)Kind; } }
        public ActionHistoryEntry Copy() { return (ActionHistoryEntry)MemberwiseClone(); }
    }
    [DataContract]
    sealed class ActionHistoryState
    {
        [DataMember(IsRequired = true)] public int Version = 1;
        [DataMember] public long Seen;
        [DataMember] public List<ActionHistoryEntry> Entries = new List<ActionHistoryEntry>();
        public long Sequence; // so na memoria: uma gravacao atrasada nao sobrescreve uma mais nova
    }
    sealed partial class Preview
    {
        const int HistoryLimit = 300, HistoryShown = 60, HistoryTextLimit = 800;
        static readonly TimeSpan HistoryRetention = TimeSpan.FromDays(7);
        static readonly string ActionHistoryFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XGB_P", "history", "actions.json");
        readonly List<ActionHistoryEntry> actionHistory = new List<ActionHistoryEntry>(); // mais antiga primeiro
        readonly object historyFileLock = new object();
        Action<ActionHistoryState> historyWrite;
        long historyId, historyVersion, historySeen, historyPageSince, historyWritten;
        bool historyDirty, historyWriting, historyProblemsOnly, historyRenderQueued, historyWriteWarned;
        DateTime historyFlushAt = DateTime.MaxValue;

        static long HistoryEpoch(DateTime utc) { return (long)(utc.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds; }
        static DateTime HistoryTime(long ms) { return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ms); }
        static bool HistoryProblem(FeedbackKind kind) { return kind == FeedbackKind.Warning || kind == FeedbackKind.Error || kind == FeedbackKind.Attention; }

        void StartActionHistory()
        {
            if (verify) { historyWrite = delegate { }; return; }
            LoadActionHistory(ActionHistoryFile);
            historyWrite = delegate(ActionHistoryState state) {
                lock (historyFileLock)
                {
                    if (state.Sequence <= historyWritten) return;
                    GameFiles.Write(ActionHistoryFile, state); historyWritten = state.Sequence;
                }
            };
        }
        static ActionHistoryState ReadActionHistory(string path, DateTime now)
        {
            if (!File.Exists(path)) return new ActionHistoryState();
            if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("Histórico grande demais.");
            var state = GameFiles.Read<ActionHistoryState>(path);
            if (state == null || state.Version != 1) throw new InvalidDataException("Histórico inválido.");
            long cutoff = HistoryEpoch(now - HistoryRetention), future = HistoryEpoch(now.AddDays(1));
            var valid = new List<ActionHistoryEntry>();
            if (state.Entries != null) foreach (var e in state.Entries)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.Text) || e.Text.Length > HistoryTextLimit || string.IsNullOrEmpty(e.Area) || e.Area.Length > 60) continue;
                if (e.Kind < 0 || e.Kind > (int)FeedbackKind.Attention || e.Updated < cutoff || e.Updated > future) continue;
                if (e.Count < 1) e.Count = 1;
                // Operacao sem resultado quando a barra encerrou: fica registrada como nao confirmada.
                if (e.Type == FeedbackKind.Pending) { e.Kind = (int)FeedbackKind.Warning; e.Text = Clip(e.Text + " Sem resultado: o XGB_P foi encerrado antes da confirmação."); }
                valid.Add(e);
            }
            valid.Sort((a, b) => a.Id.CompareTo(b.Id));
            if (valid.Count > HistoryLimit) valid.RemoveRange(0, valid.Count - HistoryLimit);
            state.Entries = valid;
            return state;
        }
        void LoadActionHistory(string path)
        {
            ActionHistoryState state;
            try { state = ReadActionHistory(path, feedbackNow()); }
            catch (Exception ex)
            {
                WriteLog("historico: arquivo invalido; tentando backup | " + ex.Message);
                try { state = ReadActionHistory(path + ".bak", feedbackNow()); }
                catch (Exception) { state = new ActionHistoryState(); }
            }
            actionHistory.Clear(); actionHistory.AddRange(state.Entries);
            historySeen = state.Seen; historyId = 0;
            foreach (var e in actionHistory) historyId = Math.Max(historyId, e.Id);
            historyVersion++;
        }

        ActionHistoryEntry RecordHistory(string scope, string text, FeedbackKind kind)
        {
            if (string.IsNullOrEmpty(text)) return null;
            long now = HistoryEpoch(feedbackNow()); string area = HistoryArea(scope); text = Clip(text);
            var last = actionHistory.Count > 0 ? actionHistory[actionHistory.Count - 1] : null;
            // O mesmo aviso repetido em seguida (apertar de novo durante uma espera) vira um contador.
            if (last != null && kind != FeedbackKind.Pending && last.Kind == (int)kind && last.Text == text && last.Area == area && now - last.Updated < 10000)
            { last.Count++; last.Updated = now; HistoryChanged(); last.Touched = historyVersion; return last; }
            var entry = new ActionHistoryEntry { Id = ++historyId, Started = now, Updated = now, Area = area, Text = text, Kind = (int)kind, Count = 1 };
            actionHistory.Add(entry);
            if (actionHistory.Count > HistoryLimit) actionHistory.RemoveRange(0, actionHistory.Count - HistoryLimit);
            HistoryChanged(); entry.Touched = historyVersion;
            return entry;
        }
        void UpdateHistory(ActionHistoryEntry entry, string text, FeedbackKind kind)
        {
            if (entry == null || string.IsNullOrEmpty(text)) return;
            text = Clip(text);
            if (entry.Text == text && entry.Kind == (int)kind) return;
            entry.Text = text; entry.Kind = (int)kind; entry.Updated = HistoryEpoch(feedbackNow());
            HistoryChanged(); entry.Touched = historyVersion;
        }
        static string Clip(string text) { return text.Length <= HistoryTextLimit ? text : text.Substring(0, HistoryTextLimit - 1) + "…"; }
        void HistoryChanged()
        {
            historyVersion++; MarkHistoryDirty();
            // Pagina aberta: atualiza depois da acao em curso, sem reconstruir no meio dela.
            if (panel == 5 && settingsPage == "history" && window.IsVisible && !historyRenderQueued)
            { historyRenderQueued = true; QueueUi(delegate { historyRenderQueued = false; RenderSettings(); }, DispatcherPriority.Background); }
        }
        void MarkHistoryDirty()
        {
            historyDirty = true;
            if (historyFlushAt == DateTime.MaxValue) historyFlushAt = DateTime.UtcNow.AddSeconds(2);
        }
        // Agrupada: 2 s depois da primeira mudanca, num worker. Ao encerrar, grava na hora.
        void FlushActionHistory(bool now)
        {
            if (!historyDirty || historyWrite == null || (!now && (historyWriting || DateTime.UtcNow < historyFlushAt))) return;
            historyDirty = false; historyFlushAt = DateTime.MaxValue;
            var state = HistorySnapshot(); var write = historyWrite;
            if (now) { try { write(state); } catch (Exception ex) { WriteLog("historico: nao foi possivel salvar | " + ex.Message); } return; }
            historyWriting = true;
            Task.Factory.StartNew(delegate {
                string error = null;
                try { write(state); } catch (Exception ex) { error = ex.Message; }
                Dispatch(delegate {
                    historyWriting = false;
                    if (error == null) return;
                    historyDirty = true; historyFlushAt = DateTime.UtcNow.AddSeconds(30);
                    if (!historyWriteWarned) { historyWriteWarned = true; WriteLog("historico: nao foi possivel salvar | " + error); }
                });
            });
        }
        ActionHistoryState HistorySnapshot()
        {
            long cutoff = HistoryEpoch(feedbackNow() - HistoryRetention);
            actionHistory.RemoveAll(e => e.Updated < cutoff);
            return new ActionHistoryState { Seen = historySeen, Entries = actionHistory.ConvertAll(e => e.Copy()), Sequence = historyVersion };
        }
        static string HistoryArea(string scope)
        {
            switch (scope)
            {
                case "0": return "Início";
                case "3": return "Desempenho";
                case "4": return "Controle e energia";
                case "6": return "Som";
                case "7": return "Lojas";
                case "8": return "Gravação";
                case "9": return "AMD";
                case "5/wifi": return "Wi-Fi";
                case "5/bluetooth": return "Bluetooth";
                case "5/tv": return "TV Samsung";
                case "5/wake": return "Retomada pelo PS";
            }
            return scope != null && scope.StartsWith("5/", StringComparison.Ordinal) ? "Configurações" : "XGB_P";
        }
        int UnseenProblems() { return actionHistory.FindAll(e => HistoryProblem(e.Type) && e.Updated > historySeen).Count; }
        // A entrada mudada por ultimo: o resultado mais recente, mesmo que o Inicio nao o mostre.
        ActionHistoryEntry LatestHistory()
        {
            ActionHistoryEntry latest = null;
            foreach (var e in actionHistory) if (latest == null || e.Touched > latest.Touched) latest = e;
            return latest;
        }
        string LatestHistoryText() { var e = LatestHistory(); return e == null ? "" : e.Text; }

        // Configuracoes: entrada do historico, com a contagem de problemas ainda nao vistos.
        void AddHistorySetting()
        {
            int fresh = UnseenProblems(), total = actionHistory.Count;
            string detail = fresh > 0 ? fresh + (fresh == 1 ? " problema novo" : " problemas novos") : total == 0 ? "Vazio" : total + (total == 1 ? " registro" : " registros");
            var row = ConnectionButton(body, "Histórico de ações e problemas", detail + "  ›", "page-history", OpenHistoryPage);
            if (fresh > 0) ((TextBlock)((Grid)row.Content).Children[1]).Foreground = Brush("FeedbackWarning");
        }
        void OpenHistoryPage() { historyPageSince = historySeen; historyProblemsOnly = false; OpenSettingsPage("history"); }
        void BuildHistoryPage()
        {
            ((TextBlock)window.FindName("PanelTitle")).Text = "Histórico";
            // Ver a pagina marca os problemas como vistos; os novos desde a visita anterior ficam destacados.
            if (UnseenProblems() > 0) { historySeen = HistoryEpoch(feedbackNow()); MarkHistoryDirty(); }
            var shown = actionHistory.FindAll(e => !historyProblemsOnly || HistoryProblem(e.Type));
            ConnectionButton(body, "Mostrar", historyProblemsOnly ? "Só problemas" : "Tudo", "history-filter", delegate { historyProblemsOnly = !historyProblemsOnly; RebuildSettings("history-filter"); });
            Caption((historyProblemsOnly ? "PROBLEMAS" : "AÇÕES E PROBLEMAS") + " · " + shown.Count);
            if (shown.Count == 0) ConnectionNote(historyProblemsOnly ? "Nenhum problema registrado." : "Nenhuma ação registrada ainda.");
            for (int i = shown.Count - 1, n = 0; i >= 0 && n < HistoryShown; i--, n++) HistoryRow(shown[i]);
            if (shown.Count > HistoryShown) ConnectionNote("Mostrando os " + HistoryShown + " mais recentes.");
            ConnectionNote("No Início aparecem só erros e avisos que pedem ação. Guardado neste PC por 7 dias, até " + HistoryLimit + " registros.");
            if (actionHistory.Count == 0) return;
            ConnectionButton(body, "Apagar histórico", "", "history-clear", delegate { connectionConfirm = "history-clear"; RebuildSettings("history-cancel"); ActionSound("lista"); });
            if (connectionConfirm != "history-clear") return;
            var box = MenuBox();
            ConnectionQuestion(box, "Apagar todo o histórico de ações e problemas deste PC?");
            ConnectionButton(box, "Apagar histórico", "", "history-clear-confirm", ClearActionHistory);
            ConnectionButton(box, "Cancelar", "", "history-cancel", delegate { connectionConfirm = null; RebuildSettings("history-clear"); });
        }
        void ClearActionHistory()
        {
            actionHistory.Clear(); connectionConfirm = null; historySeen = historyPageSince = HistoryEpoch(feedbackNow());
            WriteLog("historico: apagado pelo usuario");
            HistoryChanged(); RebuildSettings("history-filter");
        }
        // Linha focavel sem acao: o controle percorre a lista, e a rolagem acompanha o foco.
        void HistoryRow(ActionHistoryEntry e)
        {
            var kind = e.Type;
            var button = new Button { Tag = "hist-" + e.Id, Style = (Style)window.FindResource("Action"), FontSize = 15,
                Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 0, 0, 5), MinHeight = 40, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); grid.ColumnDefinitions.Add(new ColumnDefinition());
            var icon = new TextBlock { Text = FeedbackGlyph(kind), FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 15,
                Foreground = Brush(FeedbackColor(kind)), Margin = new Thickness(0, 3, 10, 0), VerticalAlignment = VerticalAlignment.Top };
            var stack = new StackPanel(); Grid.SetColumn(stack, 1);
            stack.Children.Add(Text(e.Text, 15, "TextPrimary"));
            bool fresh = HistoryProblem(kind) && e.Updated > historyPageSince;
            string when = HistoryWhen(e.Updated, feedbackNow()) + " · " + e.Area + (e.Count > 1 ? " · " + e.Count + " vezes" : "") +
                (kind == FeedbackKind.Pending ? " · em andamento" : "") + (fresh ? " · novo" : "");
            var meta = Text(when, 13, fresh ? "FeedbackWarning" : "Muted"); meta.Margin = new Thickness(0, 2, 0, 0); stack.Children.Add(meta);
            grid.Children.Add(icon); grid.Children.Add(stack); button.Content = grid;
            System.Windows.Automation.AutomationProperties.SetName(button, HistoryKindName(kind) + ": " + e.Text + " · " + when);
            WireMotion(button);
            button.GotKeyboardFocus += delegate { button.BringIntoView(); };
            body.Children.Add(button); actions.Add(button);
        }
        static string HistoryWhen(long ms, DateTime nowUtc)
        {
            DateTime local = HistoryTime(ms).ToLocalTime(), today = nowUtc.ToLocalTime().Date;
            var culture = CultureInfo.GetCultureInfo("pt-BR");
            if (local.Date == today) return local.ToString("HH:mm:ss", culture);
            if (local.Date == today.AddDays(-1)) return "ontem " + local.ToString("HH:mm", culture);
            return local.ToString("dd/MM HH:mm", culture);
        }
        static string HistoryKindName(FeedbackKind kind)
        {
            switch (kind)
            {
                case FeedbackKind.Error: return "Erro";
                case FeedbackKind.Attention: return "Precisa de ação";
                case FeedbackKind.Warning: return "Aviso";
                case FeedbackKind.Pending: return "Em andamento";
                case FeedbackKind.Success: return "Concluído";
            }
            return "Informação";
        }
    }
}

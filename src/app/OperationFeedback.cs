// Feedback por contexto e operacao: respostas antigas nao sobrescrevem uma escolha nova.
// Estados finais expiram mesmo com a barra oculta; pendencias so terminam por resultado/limite da operacao.
// Tudo vai para o historico (ActionHistory.cs); no Inicio, o rodape mostra so o critico (ADR 0078).
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace XGBP.Design
{
    // Valores persistidos no historico: novos tipos entram sempre no fim.
    // Attention: nada quebrou, mas a pessoa precisa agir (confirmacao no jogo, Steam com o controle, administrador).
    enum FeedbackKind { Info, Pending, Success, Warning, Error, Attention }
    sealed class FeedbackNotice
    {
        public string Scope, Text;
        public FeedbackKind Kind;
        public DateTime Started, Expires, Deadline;
        public ActionHistoryEntry History;
        public bool Pinned; // espera longa que aparece no Inicio enquanto pendente
    }
    sealed partial class Preview
    {
        readonly Dictionary<string, FeedbackNotice> feedbackNotices = new Dictionary<string, FeedbackNotice>();
        readonly DispatcherTimer feedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        Func<DateTime> feedbackNow = delegate { return DateTime.UtcNow; };
        string FeedbackScope { get { return panel == 5 ? "5/" + settingsPage : panel.ToString(); } }
        void StartFeedback() { StartActionHistory(); feedbackTimer.Tick += delegate { FeedbackTick(); }; feedbackTimer.Start(); }
        void FeedbackTick()
        {
            FlushActionHistory(false);
            // Fallback para worker atrasado/falho: nao infere sucesso a partir do ultimo snapshot preservado.
            if (launchingGame != null && feedbackNow() >= launchDeadline.AddSeconds(2)) CheckGameLaunch(false);
            if (closingGame != null && feedbackNow() >= closeDeadline.AddSeconds(2)) CheckGameClose(false);
            if (closingApp != null && feedbackNow() >= appCloseDeadline.AddSeconds(2)) CheckAppClose(false);
            foreach (var key in new List<string>(pauseNotices.Keys))
                if (feedbackNow() >= pauseNotices[key].Deadline.AddSeconds(2))
                { FinishFeedback(pauseNotices[key].Notice, "Não foi possível verificar a pausa/retomada de " + pauseNotices[key].Name + ".", FeedbackKind.Warning); pauseNotices.Remove(key); UpdateGamesPolling(); }
            RenderFeedback();
        }
        FeedbackNotice BeginFeedback(string text, string scope = null, int timeoutSeconds = 30)
        { var notice = NewFeedback(text, FeedbackKind.Pending, scope ?? FeedbackScope); notice.Deadline = feedbackNow().AddSeconds(timeoutSeconds); return notice; }
        FeedbackNotice NewFeedback(string text, FeedbackKind kind, string scope)
        {
            var notice = new FeedbackNotice { Scope = scope, Text = text, Kind = kind, Started = feedbackNow(), Expires = DateTime.MaxValue };
            notice.History = RecordHistory(scope, text, kind);
            feedbackNotices[scope] = notice; FinishFeedback(notice, text, kind); return notice;
        }
        void Feedback(string text, FeedbackKind kind = FeedbackKind.Info)
        {
            if (string.IsNullOrEmpty(text)) { ClearFeedback(); return; }
            NewFeedback(text, kind, FeedbackScope);
        }
        void FinishFeedback(FeedbackNotice notice, string text, FeedbackKind kind)
        {
            if (notice == null) return;
            WriteLog(text);
            // O historico guarda o resultado real, mesmo de uma resposta que ja nao aparece no rodape.
            UpdateHistory(notice.History, text, kind);
            FeedbackNotice current;
            if (!feedbackNotices.TryGetValue(notice.Scope, out current) || current != notice) return;
            if (notice.Kind != FeedbackKind.Pending && notice.Expires <= feedbackNow()) { feedbackNotices.Remove(notice.Scope); RenderFeedback(); return; }
            notice.Text = text; notice.Kind = kind;
            notice.Expires = kind == FeedbackKind.Pending ? DateTime.MaxValue : feedbackNow().AddSeconds(kind == FeedbackKind.Success ? 5 : HistoryProblem(kind) ? 12 : 6);
            RenderFeedback();
        }
        void ClearFeedback(bool keepPending = false)
        {
            FeedbackNotice notice;
            if (feedbackNotices.TryGetValue(FeedbackScope, out notice) && (!keepPending || notice.Kind != FeedbackKind.Pending)) feedbackNotices.Remove(FeedbackScope);
            RenderFeedback();
        }
        void RenderFeedback()
        {
            foreach (var waiting in feedbackNotices.Values)
                if (waiting.Kind == FeedbackKind.Pending && waiting.Deadline != DateTime.MinValue && waiting.Deadline <= feedbackNow())
                {
                    waiting.Text = "A operação continua sem confirmação. Confira o resultado antes de tentar novamente."; waiting.Kind = FeedbackKind.Warning; waiting.Expires = feedbackNow().AddSeconds(12);
                    WriteLog(waiting.Text); UpdateHistory(waiting.History, waiting.Text, waiting.Kind);
                }
            foreach (var key in new List<string>(feedbackNotices.Keys)) if (feedbackNotices[key].Expires <= feedbackNow()) feedbackNotices.Remove(key);
            FeedbackNotice notice; feedbackNotices.TryGetValue(FeedbackScope, out notice);
            if (notice != null && FeedbackScope == "0" && !HomeShows(notice)) notice = null;
            var text = (TextBlock)window.FindName("Feedback");
            var icon = (TextBlock)window.FindName("FeedbackIcon");
            string value = notice == null ? "" : notice.Text;
            double before = value != text.Text && !motionBuilding && window.IsVisible ? ReleaseSidebarHeight() : 0;
            // O texto ja fica correto no mesmo ciclo; a animacao so afeta sua apresentacao.
            MotionText(text, value, MotionNormal);
            icon.Text = notice == null ? "" : FeedbackGlyph(notice.Kind);
            icon.Foreground = Brush(notice == null ? "Muted" : FeedbackColor(notice.Kind));
            if (before > 0) MotionHeight(before);
            RefreshPresentationTimers();
        }
        // Inicio: erros, avisos que pedem acao e as esperas longas marcadas; o resto fica no historico.
        static bool HomeShows(FeedbackNotice notice)
        {
            return notice.Kind == FeedbackKind.Error || notice.Kind == FeedbackKind.Attention || notice.Pinned && notice.Kind == FeedbackKind.Pending;
        }
        static string FeedbackGlyph(FeedbackKind kind)
        {
            return kind == FeedbackKind.Pending ? "\uE895" : kind == FeedbackKind.Success ? "\uE73E" : kind == FeedbackKind.Warning || kind == FeedbackKind.Attention ? "\uE7BA" : kind == FeedbackKind.Error ? "\uEA39" : "\uE946";
        }
        static string FeedbackColor(FeedbackKind kind)
        {
            return kind == FeedbackKind.Success ? "AccentText" : kind == FeedbackKind.Error ? "Critical" : kind == FeedbackKind.Warning || kind == FeedbackKind.Attention ? "FeedbackWarning" : "Muted";
        }
    }
}

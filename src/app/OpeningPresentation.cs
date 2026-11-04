// Entrada: agrupa apenas reconstrucoes visuais e mede callbacks WPF, nao FPS do compositor.
using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Media;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        bool openingActive, openingFlushPending, openingRendering;
        int openingWork, openingDeferred, openingFrames, openingSlowFrames;
        long openingEpoch, openingStarted, openingRequested;
        double openingFirstFrame, openingLastFrame, openingMaxFrame;
        TimeSpan openingRenderTime;

        void BeginOpeningPresentation()
        {
            EndOpeningPresentation("reiniciada", false);
            if (!MotionEnabled) return;
            openingActive = true; openingEpoch++; openingStarted = Stopwatch.GetTimestamp();
            openingDeferred = openingFrames = openingSlowFrames = 0;
            openingFirstFrame = openingLastFrame = openingMaxFrame = 0; openingRenderTime = TimeSpan.MinValue;
            if (!verify) { CompositionTarget.Rendering += ObserveOpeningFrame; openingRendering = true; }
        }
        static double OpeningElapsed(long started)
        { return (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency; }
        void ObserveOpeningFrame(object sender, EventArgs args)
        {
            var frame = args as RenderingEventArgs;
            if (!openingActive || frame != null && frame.RenderingTime == openingRenderTime) return;
            if (frame != null) openingRenderTime = frame.RenderingTime;
            double now = OpeningElapsed(openingStarted);
            if (openingFrames == 0) openingFirstFrame = OpeningElapsed(openingRequested);
            else { double gap = now - openingLastFrame; openingMaxFrame = Math.Max(openingMaxFrame, gap); if (gap > 33.34) openingSlowFrames++; }
            openingLastFrame = now; openingFrames++;
        }
        // Bits fixos: no maximo uma reconstrução de apps e uma de jogos, sempre com o snapshot atual.
        bool DeferOpeningVisual(int work)
        {
            if (!openingActive) return false;
            openingWork |= work; openingDeferred++; return true;
        }
        void EndOpeningPresentation(string reason, bool flush)
        {
            bool wasActive = openingActive; openingActive = false;
            if (openingRendering) { CompositionTarget.Rendering -= ObserveOpeningFrame; openingRendering = false; }
            if (wasActive && !verify)
                WriteLog("abertura WPF: geracao=" + openingEpoch + "; motivo=" + reason +
                    "; duracaoMs=" + OpeningElapsed(openingStarted).ToString("F1", CultureInfo.InvariantCulture) +
                    "; primeiroFrameMs=" + openingFirstFrame.ToString("F1", CultureInfo.InvariantCulture) +
                    "; maiorIntervaloMs=" + openingMaxFrame.ToString("F1", CultureInfo.InvariantCulture) +
                    "; quadros=" + openingFrames + "; intervalosAcima33ms=" + openingSlowFrames + "; atualizacoesAdiadas=" + openingDeferred);
            if (flush) QueueOpeningVisuals();
        }
        void QueueOpeningVisuals()
        {
            if (openingFlushPending || openingWork == 0) return;
            openingFlushPending = true;
            DispatchPresentation(delegate {
                openingFlushPending = false;
                if (openingActive || !window.IsVisible) return;
                int work = openingWork; openingWork = 0;
                if ((work & 1) != 0) RebuildOpenApps();
                if ((work & 2) != 0) RenderGames(false);
            });
        }
        int OpeningGateFadeMs()
        {
            // Se confirmou durante a entrada, terminar junto dela. Depois, apenas um ajuste curto,
            // sem reiniciar a entrada nem a cascata de animacoes do conteudo.
            return openingActive ? Math.Max(0, (int)Math.Ceiling(MotionEntrance - OpeningElapsed(openingStarted))) : MotionFast;
        }
    }
}

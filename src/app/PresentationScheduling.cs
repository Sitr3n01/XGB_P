// Desliga trabalho de apresentacao sem desligar HID, ativacao, operacoes ou historico.
using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace XGBP.Design
{
    sealed partial class Preview
    {
#pragma warning disable 0649 // gancho de teste: atribuido somente na build XGB_VERIFY
        bool presentationReady, presentationSchedulingTesting;
#pragma warning restore 0649
        DispatcherTimer[] presentationTimers;
        void StartPresentationScheduling()
        {
            presentationTimers = new[] { clock, repeat, audioTimer, openAppsTimer, mediaTimer, perfTimer, connectionsTimer, feedbackTimer };
            presentationReady = true;
            window.IsVisibleChanged += delegate { RefreshPresentationTimers(); };
            window.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(delegate { RefreshPresentationTimers(); }), true);
            window.AddHandler(RangeBase.ValueChangedEvent, new RoutedPropertyChangedEventHandler<double>(delegate { RefreshPresentationTimers(); }), true);
            foreach (var timer in PresentationTimers())
                timer.Tick += delegate { RefreshPresentationTimers(); };
            RefreshPresentationTimers();
        }
        DispatcherTimer[] PresentationTimers()
        {
            return presentationTimers;
        }
        static void NeedTimer(DispatcherTimer timer, bool needed)
        {
            if (timer.IsEnabled == needed) return;
            if (needed) timer.Start(); else timer.Stop();
        }
        void RefreshPresentationTimers()
        {
            if (!presentationReady || verify && !presentationSchedulingTesting) return;
            if (disposed) { foreach (var timer in PresentationTimers()) timer.Stop(); return; }
            bool visible = window.IsVisible;
            // A decisao PS, teclado e saida do FSE precisam de 40 ms mesmo com a barra fechada.
            bool fast = visible || guideCycleActive || gamingModeExitPending || steamGate || kbVisible || kbSuspended || kbPending != IntPtr.Zero;
            bool watch = kbPrefs != null && kbPrefs.AutoOpen && activationMode == true;
            TimeSpan interval = TimeSpan.FromMilliseconds(fast ? 40 : 1000);
            if (repeat.Interval != interval) repeat.Interval = interval;
            NeedTimer(repeat, fast || watch);
            NeedTimer(clock, visible);
            NeedTimer(audioTimer, audioInitial || audioPending.Count > 0 || audioInFlight.Count > 0 ||
                visible && (panel == 6 || panel == 0 && mediaShown != null));
            NeedTimer(openAppsTimer, visible && panel == 0);
            NeedTimer(mediaTimer, visible && MediaPanel() || mediaSeekTarget >= 0 || mediaDeferred != null);
            NeedTimer(perfTimer, visible && panel == 3);
            NeedTimer(connectionsTimer, visible && panel == 5);
            NeedTimer(feedbackTimer, feedbackNotices.Count > 0 || launchingGame != null || closingGame != null || closingApp != null || pauseNotices.Count > 0);
        }
        // Snapshots ja possuem no maximo uma leitura em voo por subsistema. Render/input precedem sua aplicacao.
        void DispatchPresentation(Action action) { QueueUi(action, DispatcherPriority.Background); }
        void QueueUi(Action action, DispatcherPriority priority)
        {
            if (disposed || app.Dispatcher.HasShutdownStarted) return;
            app.Dispatcher.BeginInvoke(priority, new Action(delegate {
                if (disposed) return;
                try { action(); } finally { RefreshPresentationTimers(); }
            }));
        }
    }
}

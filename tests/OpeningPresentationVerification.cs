// Reabertura e reconstrucoes visuais coalescidas; fixtures sem dispositivos ou comandos reais.
using System;
using System.Diagnostics;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyOpeningPresentation()
        {
            Hide(); Show(); SetPanel(0);
            PerformanceCheck(openingActive, "entrada nao inicia fase visual");
            long generation = openingEpoch;
            for (int i = 0; i < 100; i++) { RebuildOpenApps(); RenderGames(false); }
            PerformanceCheck(openingWork == 3 && openingDeferred >= 200, "rajada nao se limita a dois tipos de atualizacao");
            int fade = OpeningGateFadeMs();
            PerformanceCheck(fade >= 0 && fade <= MotionEntrance, "confirmacao durante entrada ultrapassa a duracao restante");
            EndOpeningPresentation("fixture", true);
            PerformanceCheck(openingFlushPending, "fim da entrada nao agenda reconstrucao");
            BeginOpeningPresentation(); PumpMotion(1);
            PerformanceCheck(openingActive && openingWork == 3 && openingEpoch > generation, "callback anterior atravessou a reabertura");
            EndOpeningPresentation("fixture", true); PumpMotion(20);
            PerformanceCheck(!openingFlushPending && openingWork == 0, "ultimo snapshot nao foi aplicado apos entrada");
            PerformanceCheck(OpeningGateFadeMs() == MotionFast, "confirmacao tardia reinicia fade longo");
            Hide(); Show(); DeferOpeningVisual(3); Hide(); PumpMotion(20);
            PerformanceCheck(!openingActive && !openingRendering, "ocultar deixou observacao de frames ativa");
            Show(); PumpMotion(320);
            PerformanceCheck(!openingActive && openingWork == 0 && !openingRendering, "nova abertura nao concluiu trabalho pendente");
            Hide();
        }
    }
}

// Abertura dos jogos do Xbox / Game Pass pelo identificador do aplicativo (ADR 0117).
using System;
using System.Threading;
using XGBP.Desktop;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        string xboxCatalogStatus;
        void ReportXboxCatalog(GameCatalogState catalog, string problem)
        {
            string status = "jogos=" + catalog.Entries.FindAll(e => e.LaunchApp != null).Count + (problem.Length == 0 ? "" : "; " + problem);
            if (status == xboxCatalogStatus) return;
            xboxCatalogStatus = status; WriteLog("jogos: Xbox | " + status);
        }
        // Como na pesquisa (ADR 0096): com a barra elevada o pedido vai pelo Explorer, para o jogo nao nascer como administrador.
        // Thread STA propria: um Explorer travado nao congela a barra.
        static void LaunchXboxGame(string app)
        {
            LaunchXboxGame(app, LaunchSettings.Elevated, ProgramLauncher.OpenThroughShell, ProgramLauncher.OpenDirect);
        }
        internal static void LaunchXboxGame(string app, bool elevated, Func<string, bool, bool> shell, Action<string, bool> direct)
        {
            int mark = app == null ? -1 : app.IndexOf('!');
            if (mark <= 0 || mark == app.Length - 1 || app.IndexOfAny(new[] { '\\', '/', '"', ' ' }) >= 0)
                throw new InvalidOperationException("Identificador de jogo inválido.");
            string target = @"shell:AppsFolder\" + app; Exception error = null;
            var thread = new Thread(delegate() {
                try
                {
                    if (elevated && shell(target, false)) { WriteLog("jogos: Xbox aberto pelo explorer | " + app); return; }
                    direct(target, false); WriteLog("jogos: Xbox aberto direto" + (elevated ? " sem explorer" : "") + " | " + app);
                }
                catch (Exception ex) { error = ex; WriteLog("jogos: Xbox falhou ao abrir | " + app + " | " + ex.Message); }
            }) { IsBackground = true, Name = "XGBP jogo Xbox" };
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
            // Uma recusa imediata vira erro na Home; alem disso, quem confirma e o processo do jogo.
            if (thread.Join(3000) && error != null) throw new InvalidOperationException(error.Message, error);
        }
    }
}

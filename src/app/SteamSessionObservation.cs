// Adaptador do observador de sessoes: IO no worker de jogos; interface consulta apenas snapshots.
using System;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        SteamSessionRelease steamSessions;
        SteamReleaseSnapshot steamSessionSnapshot;
        long steamSessionVersion;
        string steamSessionStatus;
        Func<SteamReleaseSnapshot> steamSessionRead;
        void StartSteamSessionObservation()
        {
            steamSessionSnapshot = new SteamReleaseSnapshot { Readable = verify, CaughtUp = verify, Utc = feedbackNow() };
            steamSessionVersion = verify ? long.MaxValue : -1;
            steamSessionRead = delegate { if (verify) steamSessionSnapshot.Utc = feedbackNow(); return steamSessionSnapshot; };
        }
        bool SteamSessionCanLaunch()
        {
            var state = steamSessionRead();
            if (state == null || !state.Ready || steamSessionVersion < gameCommandVersion) return false;
            var age = feedbackNow() - state.Utc;
            return age >= TimeSpan.Zero && age < TimeSpan.FromSeconds(5);
        }
        void ObserveSteamSessionsOnUi(SteamReleaseSnapshot state, long version)
        {
            steamSessionSnapshot = state ?? new SteamReleaseSnapshot { Error = "Não foi possível ler a sessão da Steam." };
            steamSessionVersion = version;
            foreach (var confirmed in steamSessionSnapshot.Confirmed) WriteLog("Steam sessao: encerramento confirmado | " + confirmed);
            string status = "legivel=" + steamSessionSnapshot.Readable + "; atualizado=" + steamSessionSnapshot.CaughtUp +
                "; pendentes=" + string.Join(", ", steamSessionSnapshot.Pending.ToArray()) + "; erro=" + steamSessionSnapshot.Error;
            if (status != steamSessionStatus) { steamSessionStatus = status; WriteLog("Steam sessao: " + status); }
        }
    }
}

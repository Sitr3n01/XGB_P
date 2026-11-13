// Pesquisa de programas (ADR 0096): leitura do indice e abertura.
// - O indice e as janelas abertas sao lidos numa thread STA; a lista anterior aparece na hora e so redesenha se mudar.
// - Icones do Iniciar vem depois, numa thread propria, e redesenham uma vez.
// - Abrir: programa ja aberto volta para a janela; jogo da Steam do catalogo segue o fluxo dos jogos pela Home (com a
//   confirmacao de troca); o resto vai ao Explorer quando a barra esta elevada, para nao herdar o administrador.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using XGBP.Desktop;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void ReadPrograms()
        {
            if (programsBusy) return;
            programsBusy = true;
            var read = programsRead; var windows = programWindowsRead;
            ThreadStart work = delegate {
                List<ProgramEntry> found = null; List<DesktopWindow> open = null; string error = null;
                var watch = Stopwatch.StartNew();
                try { found = read(); } catch (Exception ex) { error = ex.Message; }
                long ms = watch.ElapsedMilliseconds;
                try { open = windows(); } catch (Exception ex) { WriteLog("programas: janelas abertas nao lidas | " + ex.Message); }
                Action done = delegate {
                    programsBusy = false;
                    if (error != null) WriteLog("programas: leitura falhou | " + error);
                    if (open != null) programWindows = open;
                    if (found != null)
                    {
                        if (programs != null) foreach (var entry in found)
                        {
                            var old = programs.Find(p => p.Key == entry.Key);
                            if (old != null) { entry.IconPng = old.IconPng; entry.IconRead = old.IconRead; }
                        }
                        programs = found;
                        WriteLog("programas: " + found.Count + " no indice | " + ms + " ms");
                    }
                    else if (programs == null) programs = new List<ProgramEntry>();
                    if (panel == ProgramsPanel && ProgramsSignature() != programSignature) RebuildProgramResults();
                    LoadProgramIcons();
                };
                if (verify) done(); else Dispatch(done);
            };
            if (verify) { work(); return; }
            var thread = new Thread(work) { IsBackground = true, Name = "XGBP programas" };
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
        }
        void LoadProgramIcons()
        {
            if (programIconsBusy || programs == null) return;
            var pending = programs.FindAll(p => !p.IconRead);
            if (pending.Count == 0) return;
            programIconsBusy = true;
            var icon = programIcon;
            ThreadStart work = delegate {
                int loaded = 0;
                foreach (var entry in pending)
                {
                    try { entry.IconPng = icon(entry); if (entry.IconPng != null) loaded++; }
                    catch (Exception) { entry.IconPng = null; }
                    entry.IconRead = true;
                }
                Action done = delegate {
                    programIconsBusy = false;
                    if (!verify) WriteLog("programas: icones lidos | " + loaded + " de " + pending.Count);
                    if (panel == ProgramsPanel && ProgramsSignature() != programSignature) RebuildProgramResults();
                };
                if (verify) done(); else Dispatch(done);
            };
            if (verify) { work(); return; }
            var thread = new Thread(work) { IsBackground = true, Name = "XGBP programas icones" };
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
        }

        void OpenProgram(ProgramEntry entry, bool maximized, bool asAdmin)
        {
            if (programLaunchBusy) { Feedback("Aguarde a abertura anterior.", FeedbackKind.Pending); return; }
            var running = ProgramWindow(entry);
            if (running != null && !maximized && !asAdmin)
            {
                WriteLog("programas: ja aberto, indo para a janela | " + entry.Kind + " | " + entry.Key);
                OpenApp(running); return;
            }
            if (entry.Kind == "steam" && gamesView != null)
            {
                var game = gamesView.Entries.Find(g => string.Equals(g.LaunchUri, "steam://rungameid/" + entry.SteamAppId, StringComparison.OrdinalIgnoreCase));
                if (game != null)
                {
                    // A Home mostra "Fechar jogo e iniciar" quando outro jogo esta aberto; nada e fechado sem a escolha.
                    WriteLog("programas: jogo do catalogo, fluxo dos jogos | " + game.Key);
                    returnToHome = false; motionPanelAxis = "home"; motionPanelDirection = -1; SetPanel(0);
                    SelectGame(game.Key);
                    return;
                }
            }
            bool elevated = programElevated(), viaShell = elevated && !asAdmin;
            programLaunchBusy = true;
            var notice = BeginFeedback("Abrindo " + entry.Name + "…", null, 15); // escopo do painel: o rodape mostra o resultado
            string target = entry.Target; var shellOpen = programShellOpen; var directOpen = programDirectOpen;
            ThreadStart work = delegate {
                string route = null, error = null;
                try
                {
                    if (viaShell && shellOpen(target, maximized)) route = "explorer";
                    else { directOpen(target, maximized); route = viaShell ? "direto sem explorer" : asAdmin ? "administrador" : "direto"; }
                }
                catch (Exception ex) { error = ex.Message; }
                Action done = delegate { ProgramOpened(entry, notice, route, error); };
                if (verify) done(); else Dispatch(done);
            };
            if (verify) { work(); return; }
            var thread = new Thread(work) { IsBackground = true, Name = "XGBP programas abrir" };
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
        }
        void ProgramOpened(ProgramEntry entry, FeedbackNotice notice, string route, string error)
        {
            programLaunchBusy = false;
            if (error != null)
            {
                FinishFeedback(notice, "Não foi possível abrir " + entry.Name + ".", FeedbackKind.Error);
                WriteLog("programas: falha ao abrir | " + entry.Kind + " | " + entry.Key + " | " + error);
                return;
            }
            ProgramHistory.Record(ProgramUses(), entry.Key, programNow());
            try { GameFiles.Write(programHistoryPath, ProgramUses()); }
            catch (Exception ex) { WriteLog("programas: historico nao gravado | " + ex.Message); }
            WriteLog("programas: abertura pedida | " + entry.Kind + " | " + route + " | " + entry.Key);
            // O shell aceitou o pedido; isso nao confirma a janela do programa.
            if (route == "direto sem explorer")
                FinishFeedback(notice, entry.Name + " foi aberto como administrador, porque o Explorer não respondeu.", FeedbackKind.Attention);
            else FinishFeedback(notice, "Abertura de " + entry.Name + " solicitada.", FeedbackKind.Info);
            if (window.IsVisible) Hide();
        }
    }
}

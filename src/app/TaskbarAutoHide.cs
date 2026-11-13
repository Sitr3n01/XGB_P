// Modo Xbox sem o espaco da barra de tarefas: barras como a do Start11 somem no modo Xbox mas continuam
// reservando a area de trabalho (04/11: trabalho 0,0-1920,1030). A barra liga a ocultacao automatica do
// Windows ao entrar no modo Xbox confirmado e devolve ao sair. O marcador local desfaz depois de uma queda (ADR 0091).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XGBP.Desktop;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        static readonly string TaskbarMarkerFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XGB_P", "settings",
            "barra-tarefas-modo-xbox.txt");
        // Defaults seguros: a verificacao nunca le nem muda a barra real; StartTaskbar liga o backend no fluxo normal.
        Func<bool?> taskbarRead = delegate { return null; };
        Func<bool, bool> taskbarSet = delegate { throw new InvalidOperationException("Barra de tarefas real bloqueada fora do fluxo normal."); };
        Func<string> taskbarArea = delegate { return "fixture"; };
        string taskbarMarker;

        void StartTaskbar()
        {
            if (verify) { taskbarMarker = Path.Combine(Output, "taskbar-tests", "barra-tarefas-modo-xbox.txt"); return; }
            taskbarMarker = TaskbarMarkerFile;
            taskbarRead = Taskbar.AutoHide; taskbarSet = Taskbar.SetAutoHide; taskbarArea = Taskbar.WorkArea;
            try { WriteLog("barra de tarefas: inicio | ocultacao automatica=" + Describe(taskbarRead()) + " | " + taskbarArea() + " | marcador=" +
                File.Exists(taskbarMarker)); }
            catch (Exception ex) { WriteLog("barra de tarefas: leitura inicial falhou | " + ex.Message); }
        }
        static string Describe(bool? state) { return state == true ? "sim" : state == false ? "nao" : "indisponivel"; }

        // Chamado em cada mudanca confirmada de modo (e na primeira leitura). API incerta nao mexe em nada.
        void TaskbarModeChanged(bool? mode)
        {
            try
            {
                if (mode == true && !launchPreferences.KeepTaskbar) HideTaskbarForXbox();
                else if (mode == false) RestoreTaskbar("desktop confirmado");
            }
            catch (Exception ex) { WriteLog("barra de tarefas: falha | " + ex.Message); }
        }
        void HideTaskbarForXbox()
        {
            bool? hidden = taskbarRead();
            // Ja oculta pelo usuario: a escolha e dele e a saida do modo Xbox nao a desfaz.
            if (hidden != false) { if (hidden == null) WriteLog("barra de tarefas: estado indisponivel; nada alterado"); return; }
            string before = taskbarArea();
            // Marcador antes da mudanca: uma queda logo depois de ligar ainda e desfeita na proxima abertura.
            Directory.CreateDirectory(Path.GetDirectoryName(taskbarMarker));
            File.WriteAllText(taskbarMarker, "Ocultacao automatica ligada pelo XGB_P no modo Xbox em " + DateTime.UtcNow.ToString("o") + "\r\n", Encoding.UTF8);
            bool confirmed = taskbarSet(true);
    WriteLog("barra de tarefas: ocultacao automatica ligada no modo Xbox | confirmada=" + confirmed + " | antes: " + before + " | depois: " + taskbarArea());
        }
        void RestoreTaskbar(string reason)
        {
            if (string.IsNullOrEmpty(taskbarMarker) || !File.Exists(taskbarMarker)) return;
            bool? hidden = taskbarRead();
            if (hidden == null) { WriteLog("barra de tarefas: estado indisponivel; restauracao adiada | " + reason); return; }
            if (hidden == true)
            {
                bool confirmed = taskbarSet(false);
                WriteLog("barra de tarefas: ocultacao automatica devolvida | " + reason + " | confirmada=" + confirmed + " | " + taskbarArea());
                if (!confirmed) return; // marcador preservado: a proxima confirmacao do desktop tenta de novo
            }
            else WriteLog("barra de tarefas: ocultacao ja desligada pelo usuario; nada a desfazer | " + reason);
            File.Delete(taskbarMarker);
        }

        void AddTaskbarSetting()
        {
            var toggle = ToggleSetting(body, "Ocultar a barra de tarefas", !launchPreferences.KeepTaskbar, "xbox-taskbar", ToggleTaskbarSetting);
            toggle.ToolTip = "Liga a ocultação automática da barra de tarefas ao entrar no modo Xbox e devolve ao sair, para os ap" +
                "ps do desktop usarem a tela inteira.";
        }
        void ToggleTaskbarSetting()
        {
            var next = launchPreferences.Copy(); next.KeepTaskbar = !launchPreferences.KeepTaskbar;
            try
            {
                launchWrite(next); launchPreferences = next;
                bool? mode = ReadGamingMode(activationRead);
                if (mode == true) { if (next.KeepTaskbar) RestoreTaskbar("opcao desligada no modo Xbox"); else HideTaskbarForXbox(); }
                RebuildSettings("xbox-taskbar");
                Feedback(next.KeepTaskbar ? "Salvo. A barra de tarefas não será alterada no modo Xbox." :
                    "Salvo. No modo Xbox, a barra de tarefas fica oculta automaticamente.");
                WriteLog("barra de tarefas: preferencia ocultar no modo Xbox=" + !next.KeepTaskbar);
            }
            catch (Exception ex) { RebuildSettings("xbox-taskbar"); Feedback("Não foi possível alterar a barra de tarefas: " + ex.Message, FeedbackKind.Error);
                WriteLog("barra de tarefas: preferencia falhou | " + ex.Message); }
        }

    }
}

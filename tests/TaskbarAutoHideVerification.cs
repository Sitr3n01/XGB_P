// Verificacao da barra de tarefas no modo Xbox (ADR 0091): barra, area, modo e preferencias substituidos.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XGBP.Desktop;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyTaskbarAutoHide()
        {
            var checks = new List<string>();
            Action<bool, string> check = delegate(bool ok, string why) { if (!ok) throw new Exception("Barra de tarefas: " + why); checks.Add("OK: " + why); };
            var oldRead = taskbarRead; var oldSet = taskbarSet; var oldArea = taskbarArea; var oldMode = activationRead;
            var oldPreferences = launchPreferences; var oldWrite = launchWrite;
            bool? state = false; int sets = 0; bool fail = false; var writes = new List<LaunchPreferenceState>(); bool? mode = true;
            try
            {
                if (File.Exists(taskbarMarker)) File.Delete(taskbarMarker);
                taskbarRead = delegate { return state; };
                taskbarSet = delegate(bool on) { sets++; if (fail) throw new IOException("fixture: recusa"); state = on; return true; };
                taskbarArea = delegate { return state == true ? "trabalho 0,0-1920,1080" : "trabalho 0,0-1920,1030"; };
                activationRead = delegate { return mode; };
                launchWrite = delegate(LaunchPreferenceState s) { writes.Add(s); };
                launchPreferences = new LaunchPreferenceState();

                TaskbarModeChanged(true);
                check(state == true && sets == 1 && File.Exists(taskbarMarker), "modo Xbox confirmado liga a ocultacao e grava o marcador");
                TaskbarModeChanged(true); check(sets == 1, "segunda confirmacao do modo nao repete a mudanca");
                TaskbarModeChanged(false);
                check(state == false && sets == 2 && !File.Exists(taskbarMarker), "desktop confirmado devolve a barra e apaga o marcador");
                TaskbarModeChanged(false); check(sets == 2, "desktop sem marcador nao mexe na barra");

                state = true; sets = 0; TaskbarModeChanged(true); TaskbarModeChanged(false);
                check(sets == 0 && state == true && !File.Exists(taskbarMarker), "ocultacao ja ligada pelo usuario nao e assumida nem desfeita");
                state = false; TaskbarModeChanged(true); state = false; TaskbarModeChanged(false);
                check(sets == 1 && !File.Exists(taskbarMarker), "usuario que desliga durante o modo Xbox prevalece na saida");

                sets = 0; TaskbarModeChanged(null); check(sets == 0 && !File.Exists(taskbarMarker), "API do modo incerta nao altera a barra");
                state = null; TaskbarModeChanged(true); check(sets == 0 && !File.Exists(taskbarMarker), "barra de tarefas ilegivel nao e alterada");

                state = false; TaskbarModeChanged(true); state = null; TaskbarModeChanged(false);
                check(File.Exists(taskbarMarker), "leitura incerta na saida preserva o marcador para a proxima confirmacao");
                state = true; TaskbarModeChanged(false); check(state == false && !File.Exists(taskbarMarker),
                    "marcador preservado restaura na confirmacao seguinte");
                state = true; File.WriteAllText(taskbarMarker, "queda", Encoding.UTF8); sets = 0; TaskbarModeChanged(false);
                check(sets == 1 && state == false && !File.Exists(taskbarMarker), "abertura no desktop com marcador desfaz a mudanca de uma queda");

                launchPreferences = new LaunchPreferenceState { KeepTaskbar = true }; state = false; sets = 0; TaskbarModeChanged(true);
                check(sets == 0 && state == false, "opcao desligada mantem a barra como esta");
                launchPreferences = new LaunchPreferenceState(); fail = true; TaskbarModeChanged(true);
                check(sets == 1 && state == false, "falha do Windows ao ligar nao interrompe a barra");
                fail = false; TaskbarModeChanged(false); check(!File.Exists(taskbarMarker) && sets == 1,
                    "falha anterior nao deixa marcador ativo depois do desktop");

                Show(); SetPanel(5); window.UpdateLayout(); mode = true; state = false; sets = 0;
                var toggle = actions.Find(c => (string)c.Tag == "xbox-taskbar") as SettingToggle;
                check(toggle != null && toggle.State == true, "interruptor aparece ligado por padrao em Configuracoes");
                toggle.Focus(); window.UpdateLayout(); SaveRender("configuracoes-barra-tarefas");
                ToggleTaskbarSetting();
                check(launchPreferences.KeepTaskbar && writes.Count == 1 && sets == 0, "desligar fora de uma mudanca feita pela barra apenas salva");
                ToggleTaskbarSetting(); check(!launchPreferences.KeepTaskbar && state == true && sets == 1, "religar no modo Xbox oculta na hora");
                ToggleTaskbarSetting(); check(state == false && sets == 2 && !File.Exists(taskbarMarker), "desligar no modo Xbox devolve na hora");
                var copy = new LaunchPreferenceState { AlwaysAdmin = true, DesktopWinG = true, KeepTaskbar = true, KeepAppVolume = true }.Copy();
                check(copy.AlwaysAdmin && copy.DesktopWinG && copy.KeepTaskbar && copy.KeepAppVolume, "copia da preferencia preserva todos os campos");
            }
            finally
            {
                taskbarRead = oldRead; taskbarSet = oldSet; taskbarArea = oldArea; activationRead = oldMode;
                launchPreferences = oldPreferences; launchWrite = oldWrite;
                if (File.Exists(taskbarMarker)) File.Delete(taskbarMarker);
                SetPanel(0);
            }
            checks.Insert(0, "OK: " + checks.Count + " verificacoes da barra de tarefas no modo Xbox.");
            checks.Add("Barra de tarefas, area de trabalho, modo e preferencias substituidos; nenhuma mudanca real na barra do Windows.");
            File.WriteAllLines(Path.Combine(Output, "barra-tarefas-verificacao.txt"), checks, Encoding.UTF8);
        }
    }
}

// Verificacao da inicializacao: substitutos para escrita no registro, elevacao e abertura de telas externas.
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using XGBP.Desktop;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        static void LaunchAssert(bool condition, string message) { if (!condition) throw new Exception("Inicializacao: " + message); }
        void VerifyLaunchSettings()
        {
            var read = startupRead; var set = startupSet; var write = launchWrite; var open = settingsLaunch;
            var previous = startupState; var preference = launchPreferences; string previousError = startupReadError;
            string folder = Path.Combine(Output, "launch-settings-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(folder, "launch.json");
            try
            {
                LaunchAssert(!LaunchSettings.Read(path).AlwaysAdmin && !File.Exists(path), "padrao desligado sem gravacao automatica");
                startupState = new StartupState(); startupReadError = null; launchPreferences = new LaunchPreferenceState();
                int mutations = 0; string opened = null;
                startupRead = delegate { return startupState; };
                startupSet = delegate(bool enabled) { mutations++; startupState = new StartupState { Registered = enabled, Owned = enabled, Enabled = enabled }; };
                launchWrite = delegate(LaunchPreferenceState value) { GameFiles.Write(path, value); };
                settingsLaunch = delegate(string uri) { opened = uri; };
                SetPanel(5); Show(); window.UpdateLayout();
                LaunchAssert(ActionLabel("launch-startup") == "Iniciar com o Windows · Desativado" && ActionLabel("launch-admin") == "Sempre executar como administrador · Desativado", "duas opcoes inicialmente desligadas");
                FocusHome("launch-startup"); Activate();
                LaunchAssert(mutations == 1 && startupState.Registered && ActionLabel("launch-startup").EndsWith("Ativado"), "registrar so por clique");
                Activate(); LaunchAssert(mutations == 2 && !startupState.Registered, "desligar remove registro proprio");
                startupState = new StartupState { Registered = true, Owned = true, Enabled = false }; RebuildSettings("launch-startup");
                Activate(); LaunchAssert(mutations == 2 && opened == "ms-settings:startupapps", "desativado no Windows abre seu controle sem forcar reativacao");
                Show(); startupState = new StartupState { Registered = true, Owned = false, Enabled = true }; RebuildSettings("launch-startup");
                LaunchAssert(!actions.Find(c => (string)c.Tag == "launch-startup").IsEnabled, "entrada de outro caminho protegida");
                startupState = new StartupState(); RebuildSettings("launch-startup");
                startupSet = delegate { throw new IOException("falha simulada"); }; Activate();
                LaunchAssert(!startupState.Registered && FeedbackText().Contains("falha simulada"), "erro nao finge ativacao");
                FocusHome("launch-admin"); Activate();
                LaunchAssert(LaunchSettings.Read(path).AlwaysAdmin && launchPreferences.AlwaysAdmin && FeedbackText().Contains("próxima abertura"), "administrador salva para proximo inicio sem elevar nesta acao");
                launchPreferences = LaunchSettings.Read(path); RebuildSettings("launch-admin");
                LaunchAssert(ActionLabel("launch-admin").EndsWith("Ativado"), "preferencia restaurada");
                Activate(); LaunchAssert(!LaunchSettings.Read(path).AlwaysAdmin, "desligar administrador persiste");
                launchWrite = delegate { throw new IOException("disco bloqueado"); }; Activate();
                LaunchAssert(!launchPreferences.AlwaysAdmin && !LaunchSettings.Read(path).AlwaysAdmin, "falha ao salvar preserva preferencia anterior");
                LaunchAssert(StartupRegistration.Command(@"C:\Pasta com espaco\XgbDesignPreview.exe") == "\"" + @"C:\Pasta com espaco\XgbDesignPreview.exe" + "\" --startup", "caminho completo entre aspas e modo bandeja");
                foreach (byte value in new byte[] { 2, 3, 6, 7, 99 })
                {
                    var bytes = new byte[12]; bytes[0] = value; bytes[5] = 123; var copy = (byte[])bytes.Clone();
                    bool? approved = StartupRegistration.Approval(bytes);
                    LaunchAssert(bytes.SequenceEqual(copy), "bytes de StartupApproved preservados");
                    LaunchAssert(approved == (value == 99 ? (bool?)null : value == 2 || value == 6), "estado do Windows interpretado " + value);
                }
                LaunchAssert(StartupRegistration.Approval(new byte[1]) == null, "formato desconhecido nao habilitado por suposicao");
                LaunchAssert(!LaunchSettings.StartupAllowed(new StartupState { Registered = true, Owned = true, Enabled = false }) && !LaunchSettings.StartupAllowed(new StartupState()), "startup desativado ou removido nao executa");
                LaunchAssert(LaunchSettings.StartupAllowed(new StartupState { Registered = true, Owned = true, Enabled = true }), "startup proprio habilitado executa");
                LaunchAssert(LaunchSettings.NeedsElevation(true, false) && !LaunchSettings.NeedsElevation(true, true) && !LaunchSettings.NeedsElevation(false, false), "politica eleva apenas quando necessario");
                var request = LaunchSettings.ElevationRequest(true);
                LaunchAssert(request.Verb == "runas" && request.UseShellExecute && request.Arguments == "--startup --elevated" && request.FileName == LaunchSettings.Exe, "UAC normal preserva startup sem shell intermediario");
                int attempts = 0, warnings = 0;
                int result = LaunchSettings.RequestElevation(false, delegate { attempts++; throw new Win32Exception(1223); }, delegate { warnings++; });
                LaunchAssert(result == 1 && attempts == 1 && warnings == 0, "cancelar UAC sai sem fallback comum nem novo pedido");
                result = LaunchSettings.RequestElevation(false, delegate { return false; }, delegate { warnings++; });
                LaunchAssert(result == 1 && warnings == 1, "falha ao iniciar elevado e informada");
                launchWrite = delegate { }; RebuildSettings("launch-admin");
                ((System.Windows.Controls.TextBlock)window.FindName("Feedback")).Text = "";
                window.UpdateLayout(); SaveRender("configuracoes-inicializacao");
                File.WriteAllText(Path.Combine(Output, "inicializacao-verificacao.txt"),
                    "OK: duas opcoes, defaults desligados, startup so por clique, remover registro proprio, Windows desativado respeitado, conflito protegido, falha de registro; " +
                    "administrador salvo/restaurado e erro preserva estado, comando entre aspas e --startup, StartupApproved nunca alterado, formato desconhecido preservado, " +
                    "politica de elevacao, runas, cancelamento UAC sem fallback/loop e erro informado. Registro, elevacao e telas externas substituidos; JSON isolado em bin.\r\n", Encoding.UTF8);
            }
            finally
            {
                startupRead = read; startupSet = set; launchWrite = write; settingsLaunch = open;
                startupState = previous; launchPreferences = preference; startupReadError = previousError; SetPanel(0);
                foreach (var file in new[] { path, path + ".bak", path + ".tmp" }) if (File.Exists(file)) File.Delete(file);
                if (Directory.Exists(folder)) Directory.Delete(folder);
            }
        }
        public void VerifyLaunchLive()
        {
            var state = StartupRegistration.Read(LaunchSettings.Exe);
            int count = StartupRegistration.SystemEntries(LaunchSettings.Exe);
            LaunchAssert(!state.Owned || count == 1, "registro proprio deve aparecer na enumeracao independente");
            var lines = new[] { "StdRegProv/HKEY_USERS: " + state.Describe(), "Win32_StartupCommand: " + count + " entrada(s) correspondentes",
                "Token administrativo atual: " + LaunchSettings.Elevated, "So leitura: nenhum registro, StartupApproved, tarefa, servico ou elevacao alterado.",
                "Sem entrada registrada, visibilidade no Gerenciador de Tarefas apos ativacao e logon/UAC reais dependem do usuario." };
            File.WriteAllLines(Path.Combine(Output, "inicializacao-real.txt"), lines, Encoding.UTF8);
        }
    }
}

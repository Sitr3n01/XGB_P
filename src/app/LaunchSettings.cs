// Politica de abertura da previa: inicializacao normal por usuario e elevacao com o UAC do Windows.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Principal;
using XGBP.Desktop;
using XGBP.Games;
using Forms = System.Windows.Forms;

namespace XGBP.Design
{
    [DataContract]
    sealed class LaunchPreferenceState
    {
        [DataMember(IsRequired = true)] public int Version = 1;
        [DataMember(IsRequired = true)] public bool AlwaysAdmin;
        [DataMember(EmitDefaultValue = false)] public bool DesktopWinG;
        // Comportamentos ligados por padrao; o arquivo guarda so a escolha de desligar (ADRs 0091 e 0092).
        [DataMember(EmitDefaultValue = false)] public bool KeepTaskbar;
        [DataMember(EmitDefaultValue = false)] public bool KeepAppVolume;
        public LaunchPreferenceState Copy()
        {
            return new LaunchPreferenceState { AlwaysAdmin = AlwaysAdmin, DesktopWinG = DesktopWinG, KeepTaskbar = KeepTaskbar, KeepAppVolume = KeepAppVolume };
        }
    }
    static class LaunchSettings
    {
        public static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XGB_P", "settings", "launch.json");
        public static string Exe { get { return Assembly.GetExecutingAssembly().Location; } }
        public static bool Elevated { get { using (var id = WindowsIdentity.GetCurrent()) return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator); } }
        // TokenUIAccess (26): o Windows so concede a variante assinada e instalada em pasta protegida (ADR 0040).
        public static bool UiAccess
        {
            get
            {
                using (var id = WindowsIdentity.GetCurrent()) { int value, size; return GetTokenInformation(id.Token, 26, out value, 4, out size) && value != 0; }
            }
        }
        [System.Runtime.InteropServices.DllImport("advapi32.dll", SetLastError = true)]
        static extern bool GetTokenInformation(IntPtr token, int infoClass, out int info, int length, out int returned);
        public static LaunchPreferenceState Read(string path)
        {
            if (!File.Exists(path)) return new LaunchPreferenceState();
            if (new FileInfo(path).Length > 4096) throw new InvalidDataException("Preferencia de abertura grande demais.");
            var state = GameFiles.Read<LaunchPreferenceState>(path);
            if (state == null || state.Version != 1) throw new InvalidDataException("Preferencia de abertura invalida.");
            return state;
        }
        public static LaunchPreferenceState Load()
        {
            try { return Read(FilePath); }
            catch (Exception ex)
            {
                Preview.WriteLog("abertura: preferencia invalida; tentando backup | " + ex.Message);
                try { return Read(FilePath + ".bak"); } catch (Exception) { return new LaunchPreferenceState(); }
            }
        }
        public static ProcessStartInfo ElevationRequest(bool startup)
        {
            return new ProcessStartInfo(Exe, startup ? "--startup --elevated" : "--elevated") {
                UseShellExecute = true, Verb = "runas", WorkingDirectory = Path.GetDirectoryName(Exe) };
        }
        public static bool NeedsElevation(bool preferred, bool elevated) { return preferred && !elevated; }
        public static bool StartupAllowed(StartupState state) { return state.Registered && state.Owned && state.Enabled == true; }
        // Chamado so no fluxo normal, antes de registrar atalhos/HID e criar a interface.
        public static int RequestElevation(bool startup)
        {
            return RequestElevation(startup, delegate(ProcessStartInfo request) {
                using (var child = Process.Start(request)) return child != null;
            }, delegate(string error) { Forms.MessageBox.Show(error, "XGB_P"); });
        }
        internal static int RequestElevation(bool startup, Func<ProcessStartInfo, bool> launch, Action<string> warn)
        {
            try
            {
                if (!launch(ElevationRequest(startup))) throw new IOException("O Windows não iniciou o processo elevado.");
                return 0;
            }
            catch (Win32Exception ex)
            {
                Preview.WriteLog("abertura: elevacao recusada | " + ex.NativeErrorCode);
                if (ex.NativeErrorCode != 1223) warn("Não foi possível iniciar como administrador: " + ex.Message);
                return 1; // cancelar nunca inicia uma copia comum contrariando a preferencia
            }
            catch (Exception ex) { Preview.WriteLog("abertura: elevacao falhou | " + ex.Message); warn(ex.Message); return 1; }
        }
    }

    sealed partial class Preview
    {
        LaunchPreferenceState launchPreferences = new LaunchPreferenceState();
        StartupState startupState = new StartupState();
        string startupReadError;
        Func<StartupState> startupRead;
        Action<bool> startupSet;
        Action<LaunchPreferenceState> launchWrite;
        Forms.ToolStripMenuItem desktopWinGTray;
        void StartLaunchSettings()
        {
            if (verify)
            {
                startupRead = delegate { return startupState; };
                startupSet = delegate(bool enabled) { startupState = new StartupState { Registered = enabled, Owned = enabled, Enabled = enabled }; };
                launchWrite = delegate { }; return;
            }
            launchPreferences = LaunchSettings.Load();
            startupRead = delegate { return StartupRegistration.Read(LaunchSettings.Exe); };
            startupSet = delegate(bool enabled) { StartupRegistration.Set(LaunchSettings.Exe, enabled); };
            launchWrite = delegate(LaunchPreferenceState state) { GameFiles.Write(LaunchSettings.FilePath, state); };
            RefreshStartupState();
        }
        void RefreshStartupState()
        {
            try { startupState = startupRead(); startupReadError = null; }
            catch (Exception ex) { startupReadError = ex.Message; WriteLog("abertura: leitura da inicializacao falhou | " + ex.Message); }
        }
        void AddLaunchSettings()
        {
            Caption("INICIALIZAÇÃO E ABERTURA");
            bool? startupOn = startupReadError != null || startupState.Registered && (!startupState.Owned || !startupState.Enabled.HasValue)
                ? (bool?)null : startupState.Registered && startupState.Enabled == true;
            string startupDetail = startupReadError != null ? "Indisponível" : startupState.Registered && (!startupState.Owned || startupState.Enabled != true) ? startupState.Describe() : null;
            var startup = ToggleSetting(body, "Iniciar com o Windows", startupOn, "launch-startup", ToggleStartup, startupDetail);
            startup.ToolTip = "Inicia na bandeja ao entrar no Windows. Também pode ser desativado pelo Gerenciador de Tarefas ou pelos Aplicativos de inicialização.";
            startup.IsEnabled = startupReadError == null && (!startupState.Registered || startupState.Owned);
            var admin = ToggleSetting(body, "Sempre executar como administrador", launchPreferences.AlwaysAdmin, "launch-admin", ToggleAlwaysAdmin);
            admin.ToolTip = "Vale na próxima abertura. O Windows pedirá confirmação do UAC; cancelar impede a abertura. Desligar vale na próxima execução.";
            var desktop = ToggleSetting(body, "Permitir Win+G no desktop", launchPreferences.DesktopWinG, "launch-desktop-wing", ToggleDesktopWinG);
            desktop.ToolTip = "No desktop, somente Win+G abre e fecha a barra. " + (XboxIcons ? "O botão Xbox" : "O botão PS") + " continua restrito ao modo Xbox. Também disponível na bandeja.";
            ConnectionButton(body, "Aplicativos de inicialização", "↗", "launch-windows", delegate { OpenWindowsPage("ms-settings:startupapps", "Aplicativos de inicialização"); });
        }
        void ToggleStartup()
        {
            try
            {
                RefreshStartupState();
                if (startupReadError != null) throw new IOException(startupReadError);
                // O Windows e a autoridade: reativar um item bloqueado passa pela tela dele, sem reescrever StartupApproved.
                if (startupState.Owned && startupState.Enabled != true) { OpenWindowsPage("ms-settings:startupapps", "Aplicativos de inicialização"); return; }
                startupSet(!startupState.Registered); RefreshStartupState(); RebuildSettings("launch-startup");
                if (startupReadError != null) throw new IOException(startupReadError);
                Feedback(startupState.Owned && startupState.Enabled != true ? "A entrada foi registrada; habilite XGB_P nos Aplicativos de inicialização do Windows." : startupState.Registered ? "O XGB_P iniciará na bandeja ao entrar no Windows." : "Inicialização com o Windows desligada.");
            }
            catch (Exception ex) { RebuildSettings("launch-startup"); Feedback("Não foi possível alterar a inicialização: " + ex.Message, FeedbackKind.Error); WriteLog("abertura: registro falhou | " + ex.Message); }
        }
        void ToggleAlwaysAdmin()
        {
            var next = launchPreferences.Copy(); next.AlwaysAdmin = !launchPreferences.AlwaysAdmin;
            try
            {
                launchWrite(next); launchPreferences = next; RebuildSettings("launch-admin");
                Feedback(next.AlwaysAdmin ? "Salvo. Na próxima abertura, confirme o UAC para executar como administrador." : "Salvo. Na próxima abertura, o XGB_P não solicitará elevação.");
            }
            catch (Exception ex) { Feedback("Não foi possível salvar a preferência de administrador.", FeedbackKind.Error); WriteLog("abertura: preferencia falhou | " + ex.Message); }
        }
        void ToggleDesktopWinG()
        {
            var next = launchPreferences.Copy(); next.DesktopWinG = !launchPreferences.DesktopWinG;
            try
            {
                launchWrite(next); launchPreferences = next;
                if (desktopWinGTray != null) desktopWinGTray.Checked = next.DesktopWinG;
                ActivationAllowed();
                if (window.IsVisible && panel == 5) RebuildSettings("launch-desktop-wing");
                Feedback(next.DesktopWinG ? "Salvo. Win+G pode abrir a barra no desktop; " + GuideName + " continua somente no modo Xbox." : "Salvo. Win+G no desktop desligado.");
                WriteLog("ativacao: preferencia Win+G desktop=" + next.DesktopWinG);
            }
            catch (Exception ex) { Feedback("Não foi possível salvar a preferência de Win+G no desktop.", FeedbackKind.Error); WriteLog("ativacao: preferencia desktop falhou | " + ex.Message); }
        }
    }
}

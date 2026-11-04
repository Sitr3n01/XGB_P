// Contratos da V.2.1.0: nenhum efeito de energia real nos testes.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using XGBP.Desktop;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifySystemPower()
        {
            var execute = systemPowerExecute; var prepare = systemPowerPrepare; var appearanceWriter = appearanceWrite;
            var activationReader = activationRead; var preferences = launchPreferences; var sessions = gameSessions;
            string historyFolder = Path.Combine(Output, "power-history-" + Guid.NewGuid().ToString("N"));
            var checks = new List<string>(); int sent = 0, prepared = 0;
            Action<bool, string> check = (ok, why) => { if (!ok) throw new Exception("Energia: " + why); checks.Add("OK: " + why); };
            try
            {
                systemPowerExecute = delegate { sent++; }; systemPowerPrepare = delegate { prepared++; };
                foreach (SystemPowerAction action in Enum.GetValues(typeof(SystemPowerAction)))
                {
                    Show(); SetPanel(4); int before = sent;
                    RequestSystemPower(action);
                    check(sent == before && prepared == before && powerConfirmation == action, action + ": primeiro clique apenas confirma");
                    check((Keyboard.FocusedElement as Control).Tag as string == "system-power-cancel", action + ": foco inicial em Cancelar");
                    Activate(); check(sent == before && powerConfirmation == null, action + ": cancelar nao envia comando");
                    RequestSystemPower(action); Back(); check(sent == before && powerConfirmation == null, action + ": voltar cancela");
                    RequestSystemPower(action); SetPanel(0); ConfirmSystemPower(); check(sent == before, action + ": trocar painel invalida confirmacao");
                    SetPanel(4); RequestSystemPower(action); ConfirmSystemPower();
                    check(sent == before + 1 && prepared == before + 1 && !systemPowerBusy && !window.IsVisible, action + ": confirmar prepara e despacha uma vez");
                    ConfirmSystemPower(); check(sent == before + 1, action + ": confirmacao consumida nao repete");
                }
                Show(); SetPanel(4); RequestSystemPower(SystemPowerAction.Shutdown); int at = sent;
                steamInputPending = true; ConfirmSystemPower(); steamInputPending = false;
                check(sent == at && window.IsVisible && powerConfirmation.HasValue, "Steam pendente bloqueia sem perder confirmacao");
                kbSuspended = true; ConfirmSystemPower(); kbSuspended = false;
                check(sent == at && FeedbackText().Contains("teclado"), "teclado suspenso exige fechamento antes de energia");
                gameBusy = "fixture:power"; ConfirmSystemPower(); gameBusy = null;
                check(sent == at, "operacao de jogo em andamento bloqueia energia");
                appearanceDirty = true; appearanceWrite = delegate { throw new IOException("armazenamento bloqueado"); };
                ConfirmSystemPower(); check(sent == at && powerConfirmation.HasValue && FeedbackText().Contains("preferências"), "falha de persistencia bloqueia envio e permite tentar novamente");
                appearanceDirty = false; appearanceWrite = appearanceWriter;
                systemPowerPrepare = delegate { throw new IOException("recuperacao pendente"); };
                ConfirmSystemPower(); check(sent == at && window.IsVisible && !systemPowerBusy && FeedbackText().Contains("recuperacao pendente"), "falha de preparacao preserva UI e impede energia");
                systemPowerPrepare = delegate { }; systemPowerExecute = delegate { sent++; throw new IOException("entrega incerta"); };
                RequestSystemPower(SystemPowerAction.Restart); ConfirmSystemPower(); ConfirmSystemPower();
                check(sent == at + 1 && FeedbackText().Contains("entrega incerta"), "falha ou entrega incerta nunca repete automaticamente");
                systemPowerExecute = delegate { sent++; ConfirmSystemPower(); check(!ActivationAllowed(), "ativacao bloqueada durante despacho"); };
                RequestSystemPower(SystemPowerAction.Suspend); ConfirmSystemPower();
                check(sent == at + 2, "reentrancia bloqueada durante despacho");

                // Historico ilegivel fica preservado sem escrita: nao bloqueia; gravavel e pendente continua bloqueando.
                string historyPath = Path.Combine(historyFolder, "history.json");
                Directory.CreateDirectory(historyFolder); File.WriteAllText(historyPath, "invalido");
                var readOnly = new GameHistory(historyPath); readOnly.Observe("fixture:power", DateTime.UtcNow);
                gameSessions = new GameSessions(readOnly); systemPowerPrepare = PrepareSystemPower; systemPowerExecute = delegate { sent++; };
                Show(); SetPanel(4); RequestSystemPower(SystemPowerAction.Shutdown); at = sent; ConfirmSystemPower();
                check(sent == at + 1 && !readOnly.Writable && File.ReadAllText(historyPath) == "invalido", "historico ilegivel preservado nao bloqueia energia");
                var pending = new GameHistory(Path.Combine(historyFolder, "ausente.json"), delegate { throw new IOException("historico bloqueado"); });
                pending.Observe("fixture:power", DateTime.UtcNow);
                gameSessions = new GameSessions(pending);
                Show(); SetPanel(4); RequestSystemPower(SystemPowerAction.Shutdown); ConfirmSystemPower();
                check(sent == at + 1 && pending.Pending && FeedbackText().Contains("histórico"), "historico gravavel pendente continua bloqueando");
                gameSessions = sessions; systemPowerPrepare = delegate { };

                // No desktop a barra so reabre pelo Win+G; a falha precisa reabrir a mesma barra com o erro.
                launchPreferences = new LaunchPreferenceState { DesktopWinG = true };
                activationRead = delegate { return false; }; ActivationAllowed();
                RequestActivation("Win+G"); check(window.IsVisible, "desktop: Win+G abre a barra para energia");
                SetPanel(4); RequestSystemPower(SystemPowerAction.Restart); at = sent;
                systemPowerExecute = delegate { sent++; throw new IOException("recusa no desktop"); };
                ConfirmSystemPower();
                check(sent == at + 1 && window.IsVisible && panel == 4 && !systemPowerBusy && FeedbackText().Contains("recusa no desktop"), "desktop: falha reabre a barra com o erro");
                Hide(); activationRead = activationReader; launchPreferences = preferences; ActivationAllowed();

                int launched = 0, suspended = 0; var nativeRequests = new List<string>();
                Func<ProcessStartInfo, int> launch = delegate(ProcessStartInfo request) {
                    launched++;
                    nativeRequests.Add(request.Arguments);
                    check(Path.IsPathRooted(request.FileName) && Path.GetFileName(request.FileName) == "shutdown.exe" &&
                        !request.UseShellExecute && request.CreateNoWindow && !request.Arguments.Contains("/f") &&
                        request.Arguments.Contains("/t 0") && !request.Arguments.Contains("/m"), "pedido nativo local sem fechamento forcado");
                    return 0;
                };
                SystemPower.Execute(SystemPowerAction.Shutdown, launch, delegate { suspended++; return true; });
                SystemPower.Execute(SystemPowerAction.Restart, launch, delegate { suspended++; return true; });
                SystemPower.Execute(SystemPowerAction.Suspend, launch, delegate { suspended++; return true; });
                check(launched == 2 && suspended == 1 && nativeRequests[0].StartsWith("/s ", StringComparison.Ordinal) &&
                    nativeRequests[1].StartsWith("/r ", StringComparison.Ordinal), "roteamento nativo correto para tres acoes com efeitos substituidos");
                bool failed = false;
                try { SystemPower.Execute(SystemPowerAction.Shutdown, delegate { return 5; }, delegate { return true; }); } catch (System.ComponentModel.Win32Exception) { failed = true; }
                check(failed, "recusa nativa de desligamento retorna erro");
                failed = false;
                try { SystemPower.Execute(SystemPowerAction.Suspend, launch, delegate { return false; }); } catch (IOException) { failed = true; }
                check(failed, "recusa nativa de suspensao retorna erro");
                failed = false;
                try { SystemPower.Execute((SystemPowerAction)99, launch, delegate { return true; }); } catch (ArgumentOutOfRangeException) { failed = true; }
                check(failed && launched == 2, "acao invalida falha antes de lancar processo");
                Show(); SetPanel(4); RequestSystemPower(SystemPowerAction.Shutdown); SettleMotion(); window.UpdateLayout(); SaveRender("energia-confirmacao-2.1.0"); Back();
                File.WriteAllLines(Path.Combine(Output, "energia-verificacao.txt"), checks, Encoding.UTF8);
            }
            finally
            {
                systemPowerExecute = execute; systemPowerPrepare = prepare; systemPowerBusy = false;
                appearanceWrite = appearanceWriter; appearanceDirty = false; gameSessions = sessions;
                if (activationRead != activationReader || launchPreferences != preferences)
                { Hide(); activationRead = activationReader; launchPreferences = preferences; ActivationAllowed(); }
                try { if (Directory.Exists(historyFolder)) Directory.Delete(historyFolder, true); } catch (IOException) { }
                powerConfirmation = null; steamInputPending = false; kbSuspended = false; gameBusy = null; SetPanel(0);
            }
        }
        void VerifyReleaseSurface()
        {
            var lines = new List<string>();
            foreach (int page in new[] { 0, 6, 3, 5, 4, StoresPanel })
            {
                SetPanel(page); window.UpdateLayout();
                string text = ProductText(body).ToLowerInvariant();
                foreach (string forbidden in new[] { "prévia", "previa", "protótipo", "prototipo", "simulação", "simulacao", "placeholder", "design v1", "ainda não são salvas" })
                    if (text.Contains(forbidden)) throw new Exception("Superficie de produto contem " + forbidden + " no painel " + page);
                lines.Add("OK: painel " + page + " sem rotulos de prototipo ou simulacao.");
            }
            SetPanel(5);
            if (!BodyHas("XGB_P · V.2.1.0") || BodyHas("Painel inicial · Início") || window.Title != "XGB_P") throw new Exception("Marca ou preferencias sem efeito na superficie final.");
            var version = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
            if (version.ProductVersion != "2.1.0" || version.FileVersion != "2.1.0.0") throw new Exception("Versao de produto divergente.");
            lines.Add("OK: marca, versao 2.1.0 e ausencia de preferencia sem efeito.");
            SettleMotion(); window.UpdateLayout(); SaveRender("configuracoes-2.1.0");
            ((ScrollViewer)window.FindName("BodyScroll")).ScrollToEnd(); window.UpdateLayout(); SaveRender("configuracoes-sobre-2.1.0");
            File.WriteAllLines(Path.Combine(Output, "produto-verificacao.txt"), lines, Encoding.UTF8); SetPanel(0);
        }
        static string ProductText(DependencyObject node)
        {
            var result = new StringBuilder();
            var text = node as TextBlock; if (text != null) result.Append(text.Text);
            var content = node as ContentControl; if (content != null && content.Content is string) result.Append(content.Content);
            var element = node as FrameworkElement; if (element != null) result.Append(element.ToolTip as string);
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) result.Append(' ').Append(ProductText(VisualTreeHelper.GetChild(node, i)));
            return result.ToString();
        }
        void VerifyAppearancePreferences()
        {
            string path = Path.Combine(Output, "appearance-" + Guid.NewGuid().ToString("N"), "appearance.json");
            var write = appearanceWrite; bool reduced = motionReduced; var lines = new List<string>();
            Action<bool, string> check = (ok, why) => { if (!ok) throw new Exception("Aparencia: " + why); lines.Add("OK: " + why); };
            try
            {
                RestoreAppearancePreferences(path); check(!motionReduced && !File.Exists(path), "ausencia usa default sem gravar");
                appearanceWrite = state => GameFiles.Write(path, state);
                ToggleReducedMotion(); check(ReadAppearancePreferences(path).ReducedMotion && !appearanceDirty, "alternar salva preferencia");
                motionReduced = false; RestoreAppearancePreferences(path); check(motionReduced, "nova leitura restaura preferencia");
                ToggleReducedMotion(); check(!ReadAppearancePreferences(path).ReducedMotion, "desligar tambem persiste");
                File.WriteAllText(path, "invalido"); RestoreAppearancePreferences(path);
                check(motionReduced && File.ReadAllText(path) == "invalido", "backup restaura sem sobrescrever arquivo invalido");
                appearanceWrite = delegate { throw new IOException("bloqueio de teste"); };
                ToggleReducedMotion(); check(appearanceDirty && LatestHistoryText().Contains("aparência"), "erro mantem pendencia e informa usuario");
                appearanceWrite = state => GameFiles.Write(path, state); FlushAppearancePreferences();
                check(!appearanceDirty && !ReadAppearancePreferences(path).ReducedMotion, "nova tentativa salva estado pendente");
                File.WriteAllLines(Path.Combine(Output, "aparencia-verificacao.txt"), lines, Encoding.UTF8);
            }
            finally { appearanceWrite = write; appearanceDirty = false; motionReduced = reduced; UpdateMotionSetting(); }
        }
    }
}

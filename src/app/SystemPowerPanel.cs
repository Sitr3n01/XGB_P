// Confirmacao de energia: nenhum comando no primeiro clique, no cancelamento ou em testes.
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using XGBP.Desktop;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        SystemPowerAction? powerConfirmation;
        bool systemPowerBusy;
        Action<SystemPowerAction> systemPowerExecute;
        Action systemPowerPrepare;

        void StartSystemPower()
        {
            systemPowerExecute = verify ? (Action<SystemPowerAction>)delegate { } : SystemPower.Execute;
            systemPowerPrepare = verify ? (Action)delegate { } : PrepareSystemPower;
        }
        static string PowerLabel(SystemPowerAction action)
        { return action == SystemPowerAction.Suspend ? "Suspender" : action == SystemPowerAction.Restart ? "Reiniciar" : "Desligar"; }
        void BuildPowerPanel()
        {
            Caption("DUALSENSE");
            SelectBattery(); batteryPanelText = Text(batteryText, 23, "TextPrimary"); body.Children.Add(batteryPanelText);
            Caption("ENERGIA");
            if (powerConfirmation.HasValue)
            {
                var selected = powerConfirmation.Value;
                body.Children.Add(Text(PowerLabel(selected) + " o computador?", 21, "TextPrimary"));
                body.Children.Add(Text(selected == SystemPowerAction.Suspend ?
                    "O Windows colocará o computador em suspensão. Jogos pausados pelo XGB_P serão retomados antes de continuar." :
                    "Salve seu trabalho antes de continuar. Jogos pausados pelo XGB_P serão retomados; o Windows poderá pedir para fechar aplicativos.", 15, "Muted"));
                Action("Cancelar", delegate { powerConfirmation = null; SetPanel(4); FocusFirstAction(); }).Tag = "system-power-cancel";
                Action("Confirmar · " + PowerLabel(selected), ConfirmSystemPower).Tag = "system-power-confirm";
                return;
            }
            foreach (SystemPowerAction action in new[] { SystemPowerAction.Suspend, SystemPowerAction.Restart, SystemPowerAction.Shutdown })
            {
                var selected = action;
                Action(PowerLabel(action), delegate { RequestSystemPower(selected); }).Tag = "system-power-" + action.ToString().ToLowerInvariant();
            }
            AddExitGamingMode();
        }
        void RequestSystemPower(SystemPowerAction action)
        {
            if (systemPowerBusy || disposed || panel != 4) return;
            powerConfirmation = action;
            RebuildPowerConfirmation();
            ActionSound("lista");
        }
        void RebuildPowerConfirmation()
        {
            // SetPanel invalida confirmacoes antigas; preservar somente a solicitacao atual deste painel.
            var selected = powerConfirmation; SetPanel(4); powerConfirmation = selected;
            body.Children.Clear(); actions.Clear(); BuildPowerPanel(); window.UpdateLayout(); FocusFirstAction();
        }
        bool SystemPowerBack()
        {
            if (panel != 4 || !powerConfirmation.HasValue) return false;
            powerConfirmation = null; SetPanel(4); FocusFirstAction(); return true;
        }
        string PowerBlockedReason()
        {
            if (gameBusy != null) return "Aguarde a operação do jogo terminar.";
            if (steamInputPending || kbSteamHeld || kbSteamGate) return "Aguarde a Steam liberar o controle antes de continuar.";
            if (kbVisible || kbSuspended) return "Feche o teclado virtual antes de continuar.";
            if (!verify && (File.Exists(steamInputMarker) || File.Exists(kbSteamMarker)))
                return "Há uma liberação de controle pendente. Feche e abra o XGB_P antes de continuar.";
            return null;
        }
        void ConfirmSystemPower()
        {
            if (systemPowerBusy || disposed || panel != 4 || !powerConfirmation.HasValue) return;
            string blocked = PowerBlockedReason();
            if (blocked != null) { Feedback(blocked, FeedbackKind.Warning); return; }
            // Estes writers podem atualizar feedback WPF em caso de falha: executar na thread da interface.
            FlushSoundPreferences(); FlushAppearancePreferences();
            if (soundPreferencesDirty || appearanceDirty)
            { Feedback("Não foi possível salvar todas as preferências. Confira o armazenamento antes de continuar.", FeedbackKind.Warning); return; }
            var selected = powerConfirmation.Value;
            powerConfirmation = null; systemPowerBusy = true;
            CancelSteamNavigation(); ClearHeld(); Hide(true);
            WriteLog("energia: confirmada pelo usuario | " + selected);
            var prepare = systemPowerPrepare; var execute = systemPowerExecute;
            ConnectionWork(delegate {
                if (prepare == null || execute == null) throw new InvalidOperationException("O serviço de energia não está disponível.");
                prepare();
                execute(selected);
            }, delegate(Exception error) {
                systemPowerBusy = false;
                if (error != null)
                {
                    WriteLog("energia: falha ou entrega incerta; sem repeticao | " + error.Message);
                    // No desktop, Show so reabre pelo Win+G: o erro volta na mesma barra que pediu a energia.
                    SetPanel(4); desktopShowByWinG = true;
                    try { Show(); } finally { desktopShowByWinG = false; }
                    Feedback("Não foi possível concluir o pedido. " + error.Message, FeedbackKind.Error);
                    return;
                }
                WriteLog("energia: pedido aceito pela API do Windows | " + selected);
                Feedback("Pedido de " + PowerLabel(selected).ToLowerInvariant() + " enviado ao Windows.", FeedbackKind.Info);
            });
        }
        void PrepareSystemPower()
        {
            gameOperations.Execute(delegate {
                RecoverPausedRegistry();
                if (pausedByPreview.Count != 0) throw new IOException("Não foi possível retomar todos os jogos pausados. Resolva a recuperação antes de continuar.");
                return null;
            });
            if (gameSessions != null && !gameSessions.FlushHistory()) throw new IOException("Não foi possível salvar o histórico de jogos. Confira o armazenamento antes de continuar.");
            if (!FlushLog()) throw new IOException("Não foi possível salvar o registro da operação.");
        }
    }
}

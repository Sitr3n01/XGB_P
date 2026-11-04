// Saida real do FSE exclusivamente por escolha do usuario; verificacoes usam substitutos.
using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        // Defaults seguros para verificacao; StartActivation configura o backend real no fluxo normal.
        Func<bool?> gamingModeCanExit = delegate { return true; };
        Action gamingModeExit = delegate { };
        Func<DateTime> gamingModeNow = delegate { return DateTime.UtcNow; };
        bool gamingModeExitPending, gamingModeExitDispatching;
        DateTime gamingModeExitDeadline, gamingModeNextCheck;
        FeedbackNotice gamingModeExitNotice;

        static bool? ReadGamingMode(Func<bool?> read) { try { return read(); } catch (Exception) { return null; } }
        void AddExitGamingMode()
        {
            bool? active = ReadGamingMode(activationRead), allowed = ReadGamingMode(gamingModeCanExit);
            string detail = gamingModeExitPending ? "Saindo…" : active == false ? "Já está no desktop" : active != true || allowed != true ? "Indisponível" : "Voltar ao desktop";
            var button = ConnectionButton(body, "Sair do modo Xbox", detail, "exit-xbox-mode", RequestExitGamingMode);
            button.IsEnabled = !gamingModeExitPending && active == true && allowed == true;
            button.ToolTip = "Volta ao desktop do Windows. A barra será ocultada e " + GuideName + "/Win+G deixarão de acioná-la.";
        }
        void EnableExitGamingModeButtons()
        {
            foreach (var control in actions)
                if (control.Tag as string == "exit-xbox-mode") control.IsEnabled = !gamingModeExitPending && ReadGamingMode(activationRead) == true && ReadGamingMode(gamingModeCanExit) == true;
        }
        void RequestExitGamingMode()
        {
            if (gamingModeExitPending) return;
            bool? active = ReadGamingMode(activationRead);
            if (active == false) { ActivationAllowed(); return; }
            if (active != true || ReadGamingMode(gamingModeCanExit) != true)
            { Feedback("O Windows não permite sair do modo Xbox neste momento.", FeedbackKind.Warning); EnableExitGamingModeButtons(); return; }
            gamingModeExitPending = gamingModeExitDispatching = true;
            gamingModeExitNotice = BeginFeedback("Saindo do modo Xbox…"); EnableExitGamingModeButtons();
            WriteLog("modo Xbox: saida solicitada pelo usuario");
            var exit = gamingModeExit;
            ConnectionWork(delegate { exit(); }, delegate(Exception error) {
                gamingModeExitDispatching = false;
                if (error != null)
                {
                    gamingModeExitPending = false;
                    FinishFeedback(gamingModeExitNotice, "Não foi possível sair do modo Xbox. " + error.Message, FeedbackKind.Error);
                    WriteLog("modo Xbox: saida falhou | " + error); EnableExitGamingModeButtons(); return;
                }
                gamingModeExitDeadline = gamingModeNow().AddSeconds(6); gamingModeNextCheck = DateTime.MinValue;
                CheckExitGamingMode();
            });
        }
        void CheckExitGamingMode()
        {
            if (!gamingModeExitPending || gamingModeExitDispatching || gamingModeNow() < gamingModeNextCheck) return;
            gamingModeNextCheck = gamingModeNow().AddMilliseconds(200);
            bool? active = ReadGamingMode(activationRead);
            if (active == false)
            {
                gamingModeExitPending = false;
                FinishFeedback(gamingModeExitNotice, "Modo Xbox desativado.", FeedbackKind.Success);
                WriteLog("modo Xbox: desktop confirmado apos pedido de saida"); ActivationAllowed(); return;
            }
            if (gamingModeNow() >= gamingModeExitDeadline)
            {
                gamingModeExitPending = false;
                FinishFeedback(gamingModeExitNotice, active == true ? "A saída foi solicitada, mas o Windows ainda está no modo Xbox." : "A saída foi solicitada, mas não foi possível confirmar o modo do Windows.", FeedbackKind.Warning);
                EnableExitGamingModeButtons();
            }
        }
#if XGB_VERIFY
        void VerifyExitGamingMode()
        {
            var read = activationRead; var canExit = gamingModeCanExit; var exit = gamingModeExit; var clock = gamingModeNow;
            DateTime now = DateTime.UtcNow; bool? active = true, allowed = true; int calls = 0, checks = 0;
            Action<bool, string> check = delegate(bool ok, string message) { checks++; if (!ok) throw new Exception("Saida FSE: " + message); };
            try
            {
                activationRead = delegate { return active; }; gamingModeCanExit = delegate { return allowed; }; gamingModeNow = delegate { return now; };
                gamingModeExit = delegate { calls++; };
                Show(); SetPanel(5); window.UpdateLayout();
                var button = actions.Find(c => (string)c.Tag == "exit-xbox-mode");
                check(button != null && button.IsEnabled, "botao habilitado em Configuracoes");
                button.Focus(); SaveRender("configuracoes-sair-modo-xbox");
                check(BodyHas("PS ou Win + G") && !BodyHas("Win + Shift + G") && !BodyHas("Gesto · microfone por 600 ms"), "abertura corresponde a regra atual");
                RequestExitGamingMode(); check(calls == 1 && gamingModeExitPending && !button.IsEnabled && window.IsVisible, "despacho nao inventa sucesso e bloqueia repeticao");
                RequestExitGamingMode(); check(calls == 1, "clique repetido nao despacha novamente");
                active = false; now = now.AddMilliseconds(200); CheckExitGamingMode();
                check(!gamingModeExitPending && !window.IsVisible, "estado desktop observado conclui e oculta");
                RequestExitGamingMode(); check(calls == 1, "ja no desktop nao altera modo");
                active = true; Show(); check(button.IsEnabled, "reentrada no FSE reabilita botao sem reconstruir painel");
                active = true; allowed = false; Show(); SetPanel(5);
                check(!actions.Find(c => (string)c.Tag == "exit-xbox-mode").IsEnabled, "sem permissao fica desabilitado");
                RequestExitGamingMode(); check(calls == 1, "sem permissao nao chama API");
                allowed = null; SetPanel(5); check(!actions.Find(c => (string)c.Tag == "exit-xbox-mode").IsEnabled, "permissao incerta nao habilita");
                allowed = true; gamingModeExit = delegate { calls++; throw new IOException("falha substituida"); }; SetPanel(5); RequestExitGamingMode();
                check(!gamingModeExitPending && window.IsVisible && FeedbackText().Contains("Não foi possível"), "erro preserva interface e explica falha");
                gamingModeExit = delegate { calls++; }; RequestExitGamingMode(); now = now.AddSeconds(6); CheckExitGamingMode();
                check(!gamingModeExitPending && window.IsVisible && FeedbackText().Contains("ainda está"), "timeout nao afirma que saiu");
                RequestExitGamingMode(); active = null; now = now.AddSeconds(6); CheckExitGamingMode();
                check(!gamingModeExitPending && FeedbackText().Contains("não foi possível confirmar"), "leitura incerta nao afirma sucesso");
                active = true; SetPanel(4); check(actions.Exists(c => (string)c.Tag == "exit-xbox-mode"), "Energia reutiliza a mesma acao real");
                File.WriteAllText(Path.Combine(Output, "sair-modo-xbox-verificacao.txt"), "OK: " + checks + " verificacoes. Botao em Configuracoes/Energia, permissoes, pedido duplicado, despacho/observacao, desktop, erro, timeout e API incerta.\r\nModo, permissao, comando e relogio substituidos; nenhuma alteracao real de FSE nos testes. Render WPF com dados de teste.\r\n", Encoding.UTF8);
            }
            finally
            {
                gamingModeExitPending = gamingModeExitDispatching = false;
                activationRead = read; gamingModeCanExit = canExit; gamingModeExit = exit; gamingModeNow = clock;
                ActivationAllowed(); SetPanel(0);
            }
        }
#endif
    }
}

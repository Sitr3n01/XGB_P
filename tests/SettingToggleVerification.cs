// Acessibilidade e confirmacao do interruptor, sem operar preferencias ou dispositivos reais.
using System;
using System.IO;
using System.Text;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifySettingToggles()
        {
            int checks = 0, sent = 0;
            Action<bool, string> check = (ok, why) => { if (!ok) throw new Exception("Interruptores: " + why); checks++; };
            Show(); SetPanel(5); window.UpdateLayout();
            foreach (string tag in new[] { "radio-wifi", "radio-bluetooth", "night-light", "launch-startup", "launch-admin", "launch-desktop-wing", "ui-sounds", "keyboard-auto", "keyboard-sounds", "reduce-motion" })
                check(actions.Find(c => (string)c.Tag == tag) is SettingToggle, "opcao comum: " + tag);
            SaveRender("configuracoes-interruptores");
            var row = ToggleSetting(body, "Teste", false, "toggle-fixture", delegate { sent++; });
            var peer = UIElementAutomationPeer.CreatePeerForElement(row);
            var provider = (IToggleProvider)peer.GetPattern(PatternInterface.Toggle);
            check(provider != null && provider.ToggleState == ToggleState.Off, "leitor de tela recebe desligado");
            provider.Toggle();
            check(sent == 1 && provider.ToggleState == ToggleState.Off, "comando segue o callback sem antecipar confirmacao");
            UpdateToggleSetting(row, "Teste", true);
            check(provider.ToggleState == ToggleState.On && AutomationProperties.GetName(row) == "Teste · Ativado", "estado confirmado e nome acessivel atualizados");
            row.IsEnabled = false;
            bool refused = false;
            try { provider.Toggle(); } catch (ElementNotEnabledException) { refused = true; }
            check(refused && sent == 1, "inativo nao envia comando");
            UpdateToggleSetting(row, "Teste", null);
            check(provider.ToggleState == ToggleState.Indeterminate, "indisponivel nao inventa estado desligado");
            SetPanel(0);
            File.WriteAllText(Path.Combine(Output, "interruptores-verificacao.txt"), "OK: " + checks + " verificacoes. Estados acessiveis, comando sem antecipar confirmacao e recusa quando inativo. Sem alterar o PC.\r\n", Encoding.UTF8);
        }
    }
}

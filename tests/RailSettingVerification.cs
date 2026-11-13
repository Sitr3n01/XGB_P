// ADR 0112: o trilho lateral como opcao das Configuracoes. Arquivos temporarios e escritor substituido; sem HID ou comandos reais.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyRailSetting()
        {
            var checks = new List<string>();
            Action<bool, string> check = delegate(bool ok, string why) {
                if (!ok) throw new Exception("Trilho lateral: " + why);
                checks.Add("OK: " + why);
            };
            var write = appearanceWrite; bool reduced = motionReduced, hidden = railHidden, testing = motionTesting; var icons = buttonIcons;
            var systemMotion = motionSystemOverride;
            string folder = Path.Combine(Output, "appearance-trilho-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(folder, "appearance.json");
            try
            {
                // Migracao: sem a escolha gravada vale o marcador da pasta, gravado uma vez; depois a escolha vence o marcador.
                Directory.CreateDirectory(folder);
                var writes = new List<AppearancePreferenceState>();
                appearanceWrite = delegate(AppearancePreferenceState s) { writes.Add(s); GameFiles.Write(path, s); };
                LoadAppearancePreferences(path, true);
            check(railHidden && writes.Count == 1 && ReadAppearancePreferences(path).RailLayout == RailHiddenLayout, "sem arquivo: marcador grava sem trilho");
                LoadAppearancePreferences(path, false);
                check(railHidden && writes.Count == 1, "escolha gravada vence a pasta sem marcador");
                File.Delete(path); File.Delete(path + ".bak");
                LoadAppearancePreferences(path, false);
            check(!railHidden && writes.Count == 2 && ReadAppearancePreferences(path).RailLayout == RailShownLayout, "sem arquivo e sem marcador: com trilho");
                LoadAppearancePreferences(path, true);
                check(!railHidden && writes.Count == 2, "escolha gravada vence o marcador");
                // Arquivo da 3.0.0, sem o campo: as outras preferencias sao preservadas na gravacao da migracao.
                File.WriteAllText(path, "{\"ButtonIcons\":2,\"ReducedMotion\":true,\"Version\":1}");
                LoadAppearancePreferences(path, true);
                var migrated = ReadAppearancePreferences(path);
                check(railHidden && motionReduced && buttonIcons == ButtonIconScheme.Xbox && migrated.RailLayout == RailHiddenLayout &&
                    migrated.ReducedMotion && migrated.ButtonIcons == 2, "arquivo antigo migra sem perder movimento e icones");
                File.WriteAllText(path, "{\"RailLayout\":7,\"ReducedMotion\":false,\"Version\":1}");
                LoadAppearancePreferences(path, false);
                check(!railHidden && ReadAppearancePreferences(path).RailLayout == RailShownLayout, "valor desconhecido volta ao marcador");
                // Arquivo principal ilegivel: usa o backup e o marcador nesta sessao, sem sobrescrever nada.
                int before = writes.Count; File.WriteAllText(path, "invalido");
                LoadAppearancePreferences(path, true);
                check(railHidden && writes.Count == before && File.ReadAllText(path) == "invalido", "arquivo invalido nao e regravado na abertura");
                appearanceWrite = delegate { throw new IOException("bloqueio de teste"); };
                File.Delete(path); File.Delete(path + ".bak");
                LoadAppearancePreferences(path, true);
                check(railHidden && !appearanceDirty, "falha ao gravar a migracao segue o marcador sem erro");

                // Configuracoes: interruptor em INTERFACE, aplicado na hora nos dois sentidos, com o foco na propria linha.
                writes.Clear(); appearanceWrite = delegate(AppearancePreferenceState s) { writes.Add(s); };
                railHidden = false; motionReduced = true; buttonIcons = ButtonIconScheme.PlayStation; SetSlidingTitleLayout(false);
                MotionFixtures(); window.Width = 1920; window.Height = 1080; window.Left = window.Top = -30000;
                returnToHome = false; Show(); SetPanel(5); window.UpdateLayout();
                var row = actions.Find(c => (string)c.Tag == "rail-layout");
            check(row != null && ((SettingToggle)row).State == true && ActionLabel("rail-layout") == "Trilho lateral · Ativado",
                "linha Trilho lateral ativada");
                check(MotionPart("RailFrame").IsVisible && Math.Abs(MotionPart("Sidebar").ActualWidth - 588) < .5, "trilho visivel com 92 + 16 + 480 DIP");
                SaveSceneRender("configuracoes-trilho-ligado", 1920, 1080);
                var scroll = (ScrollViewer)MotionPart("BodyScroll");
                scroll.ScrollToVerticalOffset(scroll.ViewportHeight - 40); window.UpdateLayout(); SaveSceneRender("configuracoes-ordem-meio", 1920, 1080);
                scroll.ScrollToEnd(); window.UpdateLayout(); SaveSceneRender("configuracoes-ordem-fim", 1920, 1080);
                scroll.ScrollToVerticalOffset(0); window.UpdateLayout();
                row.Focus(); Activate(); window.UpdateLayout();
                check(railHidden && SlidingTitleEnabled && writes.Count == 1 && writes[0].RailLayout == RailHiddenLayout, "desligar salva sem trilho");
                check(!MotionPart("RailFrame").IsVisible && Math.Abs(MotionPart("Sidebar").ActualWidth - 480) < .5 && titleHeader.IsVisible &&
                    MotionPart("PanelTitle").Parent == titleHeader, "desligar troca o layout na hora");
            check(panel == 5 && FocusedTag() == "rail-layout" && ActionLabel("rail-layout") == "Trilho lateral · Desativado", "foco fica na linha desligada");
                check(FeedbackText() == "Salvo. Os painéis trocam pela seta do título e por L1/R1.", "retorno ao desligar: " + FeedbackText());
                check(SceneBox(MotionPart("StatusArea")).Left >= SceneBox(MotionPart("Sidebar")).Right + 24, "faixa de status fora do painel sem trilho");
                SaveSceneRender("configuracoes-trilho-desligado", 1920, 1080);
                ChangePanel(1); check(panel == 4 && Keyboard.FocusedElement == titleNext, "R1 sem trilho segue o ciclo do titulo");
                ChangePanel(-1); check(panel == 5 && Keyboard.FocusedElement == titleNext, "L1 volta as Configuracoes");
                actions.Find(c => (string)c.Tag == "rail-layout").Focus(); Activate(); window.UpdateLayout();
                var title = (TextBlock)MotionPart("PanelTitle");
                check(!railHidden && !SlidingTitleEnabled && writes.Count == 2 && writes[1].RailLayout == RailShownLayout, "religar salva com trilho");
                check(MotionPart("RailFrame").IsVisible && Math.Abs(MotionPart("Sidebar").ActualWidth - 588) < .5 && !titleHeader.IsVisible &&
            title.Parent == MotionPart("PanelContent") && ((Grid)MotionPart("PanelContent")).Children.IndexOf(title) == 0,
                "religar devolve o trilho e o titulo");
                check(title.ReadLocalValue(TextBlock.TextWrappingProperty) == DependencyProperty.UnsetValue &&
                    title.ReadLocalValue(FrameworkElement.VerticalAlignmentProperty) == DependencyProperty.UnsetValue &&
                    title.TextWrapping == TextWrapping.Wrap && title.Margin == new Thickness(0, 0, 0, 20) && title.FontSize == 33,
                    "titulo religado igual ao do XAML (quebra pelo estilo, sem valores locais)");
                check(FocusedTag() == "rail-layout" && FeedbackText() == "Salvo. O trilho lateral voltou.", "foco e retorno ao religar");
                ChangePanel(1); check(panel == 4 && Keyboard.FocusedElement == rail[4], "R1 com trilho volta ao trilho");
                // Falha ao salvar: o layout muda, o aviso de nao salvo fica e nada diz Salvo.
                SetPanel(5); appearanceWrite = delegate { throw new IOException("bloqueio de teste"); };
                actions.Find(c => (string)c.Tag == "rail-layout").Focus(); Activate();
                check(railHidden && appearanceDirty && FeedbackText() == "O ajuste foi aplicado, mas não foi possível salvar a aparência neste PC.",
                    "falha ao salvar avisa sem desfazer");
                appearanceWrite = delegate(AppearancePreferenceState s) { writes.Add(s); }; FlushAppearancePreferences();
                check(!appearanceDirty && writes[writes.Count - 1].RailLayout == RailHiddenLayout, "nova tentativa salva a escolha pendente");
                // Com animacao: a troca termina com o trilho opaco e sem altura presa.
                motionReduced = false; motionTesting = true; motionSystemOverride = false;
                actions.Find(c => (string)c.Tag == "rail-layout").Focus(); Activate(); PumpMotion(400);
                check(!railHidden && MotionPart("RailFrame").Opacity == 1 && !heightPresenting && Math.Abs(MotionTranslation(MotionPart("RailFrame")).X) < .001,
                    "animacao de religar termina no lugar");
                File.WriteAllText(Path.Combine(Output, "trilho-opcao-verificacao.txt"),
                    "Trilho lateral (ADR 0112): " + checks.Count + " verificacoes aprovadas.\r\n" + string.Join("\r\n", checks) +
                    "\r\nArquivos temporarios e escritor substituido; sem HID ou comandos reais. Teste fisico pendente.\r\n", Encoding.UTF8);
            }
            finally
            {
                SettleMotion(); motionTesting = testing; motionSystemOverride = systemMotion;
                appearanceWrite = write; appearanceDirty = false; motionReduced = reduced; buttonIcons = icons;
                railHidden = hidden; SetSlidingTitleLayout(SlidingTitleEnabled);
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
        }
    }
}

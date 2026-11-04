// Regressao do painel AMD (ADR 0082). Driver substituido: nenhum ajuste real da GPU e lido ou alterado.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;
using XGBP.Amd;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyAmdTuning()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool ok, string reason) { checks++; if (!ok) throw new Exception("AMD ajustes: " + reason); };
            var oldRead = tuningRead; var oldSet = tuningSet; var oldInstall = tuningInstall; var oldOpen = amdOpen;
            // Estado lido na RX 9070 XT em 29/10 (sonda somente leitura): FSR 4 e Anti-Lag ligados, valores guardados no driver.
            var driver = new Dictionary<string, GraphicsFeature>();
            foreach (var key in AdlxGraphics.Keys) driver[key] = new GraphicsFeature { Key = key, Supported = true };
            driver["fsr"].Enabled = driver["antilag"].Enabled = true;
            driver["chill"].Value = 75; driver["chill"].Value2 = 140; driver["frtc"].Value = 60; driver["sharpening"].Value = 50; driver["rsr"].Value = 50; driver["boost"].Value = 84;
            var sets = new List<string>(); var opened = new List<string>();
            bool applies = true, readFails = false; Exception setFails = null; string coupled = null;
            try
            {
                tuningRead = delegate {
                    if (readFails) throw new InvalidOperationException("O driver da AMD não iniciou o ADLX (código 8).");
                    var s = new GraphicsState { Gpu = "AMD Radeon RX 9070 XT" };
                    foreach (var key in AdlxGraphics.Keys) { var d = driver[key]; s.Features.Add(new GraphicsFeature { Key = key, Supported = d.Supported, Enabled = d.Enabled, Value = d.Value, Value2 = d.Value2, Error = d.Error }); }
                    return s; };
                tuningSet = delegate(string key, bool on) {
                    sets.Add(key + ":" + on);
                    if (setFails != null) { var f = setFails; setFails = null; throw f; }
                    if (!applies) return;
                    driver[key].Enabled = on;
                    if (coupled != null) driver[coupled].Enabled = !on; // dependencia que o driver resolve sozinho
                };
                tuningInstall = delegate { return @"C:\Program Files\AMD\CNext\CNext"; };
                amdOpen = delegate(string target, bool maximize) { opened.Add(target); };
                Func<string, Control> find = delegate(string tag) { return actions.Find(c => (string)c.Tag == tag); };
                Action<string> press = delegate(string tag) { FocusHome(tag); Activate(); };
                Func<string> status = delegate { var line = body.Children.Count > 0 ? body.Children[0] as StackPanel : null; return line == null ? "" : ((TextBlock)line.Children[1]).Text; };

                amdTuning = null; amdTuningError = null; amdTuningPending = null; amdTuningFolder = null;
                Show(); SetPanel(AmdTuningPanel);
                check(status() == "Lendo o driver da AMD…" && find("amd-tune-fsr") == null && !find("amd-tuning-software").IsEnabled, "antes da leitura: lendo");
                ReadAmdTuning();
                check(status() == "Radeon RX 9070 XT · ajustes globais" && ActionLabel("amd-tune-fsr") == "FSR 4 (upgrade) · Ativado" &&
                    ActionLabel("amd-tune-framegen") == "Geração de quadros FSR (upgrade) · Desativado" && ActionLabel("amd-tune-chill") == "Radeon Chill · Desativado", "estado real com genero e estado");
                check(BodyHas("UPSCALING E QUADROS") && BodyHas("LATÊNCIA E DESEMPENHO") && BodyHas("IMAGEM") && !actions.Exists(c => ((string)c.Tag ?? "").Contains("metric")), "grupos do design e metricas fora da tela");
                rail[AmdTuningPanel].Focus(); window.UpdateLayout(); SaveRender("amd-ajustes");
                FocusHome("amd-tune-fsr"); Navigate("BAIXO"); check(FocusedTag() == "amd-tune-framegen", "navegacao linear pelas linhas");
                Navigate("ESQUERDA"); check(Keyboard.FocusedElement == rail[AmdTuningPanel], "esquerda volta ao trilho com o logotipo");

                // Ligar: aplica, rele e confirma pelo estado real; valor aparece so ligado.
                press("amd-tune-chill");
                check(sets[sets.Count - 1] == "chill:True" && amdTuningPending == null && driver["chill"].Enabled && LatestHistoryText() == "Radeon Chill: ligado." &&
                    ActionLabel("amd-tune-chill") == "Radeon Chill · Ativado · 75–140 FPS" && FocusedTag() == "amd-tune-chill", "liga, confirma e mostra a faixa");
                press("amd-tune-sharpening"); press("amd-tune-frtc");
                check(ActionLabel("amd-tune-sharpening") == "Nitidez da imagem · Ativado · 50%" && ActionLabel("amd-tune-frtc") == "Limite de FPS · Ativado · 60 FPS", "valores de nitidez e limite");
                window.UpdateLayout(); SaveRender("amd-ajustes-valores");
                press("amd-tune-chill"); check(sets[sets.Count - 1] == "chill:False" && LatestHistoryText() == "Radeon Chill: desligado." && ActionLabel("amd-tune-chill") == "Radeon Chill · Desativado", "desliga");

                // O driver muda outro recurso junto: a mensagem conta.
                coupled = "sharpening"; press("amd-tune-rsr");
                check(driver["rsr"].Enabled && !driver["sharpening"].Enabled && LatestHistoryText() == "Radeon Super Resolution: ligado. O driver também desligou Nitidez da imagem." &&
                    ActionLabel("amd-tune-sharpening") == "Nitidez da imagem · Desativado", "efeito colateral do driver informado e refletido");
                coupled = null;
                // O driver aceita sem aplicar (perfil, dependencia): aviso; recusa: erro; sem leitura: indisponivel.
                applies = false; press("amd-tune-afmf");
                check(!driver["afmf"].Enabled && LatestHistory().Type == FeedbackKind.Warning && LatestHistoryText().StartsWith("O driver da AMD não aplicou: Fluid Motion Frames"), "nao aplicado avisa");
                applies = true; setFails = new InvalidOperationException("O driver da AMD recusou a mudança (não suportado)."); press("amd-tune-boost");
                check(!driver["boost"].Enabled && LatestHistory().Type == FeedbackKind.Error && LatestHistoryText().Contains("recusou"), "recusa do driver e erro");
                amdTuningPending = "boost"; int before = sets.Count; ToggleAmdTuning("antilag");
                check(sets.Count == before && LatestHistoryText() == "Aguarde o ajuste anterior terminar.", "um ajuste por vez"); amdTuningPending = null;
                driver["enhancedsync"].Supported = false; driver["framegen"].Error = 6; ReadAmdTuning();
                check(ActionLabel("amd-tune-enhancedsync") == "Enhanced Sync · Não suportado" && !find("amd-tune-enhancedsync").IsEnabled &&
                    ActionLabel("amd-tune-framegen") == "Geração de quadros FSR (upgrade) · Indisponível" && !find("amd-tune-framegen").IsEnabled, "nao suportado e interface ausente desligados");
                ToggleAmdTuning("enhancedsync"); check(sets.Count == before, "nao suportado nao envia");
                readFails = true; amdTuning = null; ReadAmdTuning();
                check(status() == "Driver da AMD indisponível" && BodyHas("O driver da AMD não iniciou o ADLX (código 8).") && find("amd-tune-fsr") == null && find("amd-tuning-software").IsEnabled, "sem driver: aviso e AMD Software");
                readFails = false; press("amd-tuning-software");
                check(opened.Count == 1 && opened[0] == @"C:\Program Files\AMD\CNext\CNext\RadeonSoftware.exe" && !window.IsVisible, "abre o AMD Software");
                check(TuningSideEffects(null, null, "fsr") == "" && AdlxGraphics.Keys.Length == 10 && Array.IndexOf(AdlxGraphics.Keys, "afmf") >= 0, "contrato do backend");
                File.WriteAllText(Path.Combine(Output, "amd-ajustes-verificacao.txt"), "OK: " + checks + " verificacoes. Lendo, estado real com genero, grupos do design, metricas fora da tela, " +
                    "navegacao e trilho com logotipo, ligar/desligar com releitura e valores so ligados, efeito colateral do driver informado, nao aplicado, recusa, um ajuste por vez, " +
                    "nao suportado/interface ausente, driver indisponivel e abrir o AMD Software. Driver substituido; nenhum ajuste real lido ou alterado.\r\n", Encoding.UTF8);
            }
            finally
            {
                tuningRead = oldRead; tuningSet = oldSet; tuningInstall = oldInstall; amdOpen = oldOpen;
                amdTuning = null; amdTuningError = null; amdTuningPending = null; amdTuningFolder = null; amdTuningBusy = false;
                SetPanel(0);
            }
        }
    }
}

// Regressao do painel AMD (ADRs 0082 e 0100). Driver substituido: nenhum ajuste real da GPU e lido ou alterado.
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
            var oldRead = tuningRead; var oldSet = tuningSet; var oldValue = tuningValue; var oldReset = tuningReset; var oldInstall =
                tuningInstall; var oldOpen =
                amdOpen;
            // Estado lido na RX 9070 XT em 06/11 15:49 (sonda somente leitura); nivel da anisotropica ilustrativo.
            var driver = new Dictionary<string, GraphicsFeature>();
            foreach (var key in AdlxGraphics.Keys) driver[key] = new GraphicsFeature { Key = key, Supported = true };
            Action<string, string, int> val = delegate(string k, string f, int v) { driver[k].Values[f] = v; };
            Action<string, int, int, int> range = delegate(string k, int a, int b, int s) { driver[k].Min = a; driver[k].Max = b; driver[k].Step = s; };
            driver["fsr"].Enabled = driver["antilag"].Enabled = driver["vsync"].Enabled = driver["mlaa"].Enabled = driver["video"].Enabled = true;
            val("afmf", "search", 0); val("afmf", "performance", 0); val("afmf", "motion", 0);
            val("rsr", "sharpness", 50); range("rsr", 0, 100, 1); val("boost", "resolution", 84); range("boost", 50, 84, 17);
            val("chill", "min", 75); val("chill", "max", 140); range("chill", 30, 300, 1); val("frtc", "fps", 60); range("frtc", 15, 1000, 1);
        val("vsync", "mode", 2); val("sharpening", "sharpness", 50); range("sharpening", 10, 100, 10); val("video", "sharpness", 50); range("video", 1, 100, 1);
            val("aa", "mode", 0); val("aa", "level", 0); val("aa", "method", 0); val("aniso", "level", 16); val("tess", "mode", 0); val("tess", "level", 64);
            var sets = new List<string>(); var opened = new List<string>();
            bool applies = true, readFails = false; Exception setFails = null; string coupled = null; int resets = 0;
            try
            {
                tuningRead = delegate {
                    if (readFails) throw new InvalidOperationException("O driver da AMD não iniciou o ADLX (código 8).");
                    var s = new GraphicsState { Gpu = "AMD Radeon RX 9070 XT" };
                    foreach (var key in AdlxGraphics.Keys)
                    {
                        var d = driver[key];
                    var c = new GraphicsFeature { Key = key, Supported = d.Supported, Enabled = d.Enabled, Error = d.Error, Min = d.Min, Max =
                        d.Max, Step = d.Step };
                        foreach (var v in d.Values) c.Values[v.Key] = v.Value;
                        s.Features.Add(c);
                    }
                    return s; };
                Action fail = delegate { if (setFails != null) { var f = setFails; setFails = null; throw f; } };
                tuningSet = delegate(string key, bool on) {
                    sets.Add(key + ":" + on); fail();
                    if (!applies) return;
                    driver[key].Enabled = on;
                    if (coupled != null) driver[coupled].Enabled = !on; // dependencia que o driver resolve sozinho
                };
                tuningValue = delegate(string key, string field, int value) { sets.Add(key + "." + field + "=" + value); fail(); if (applies)
                    driver[key].Values[field] =
                    value; };
                tuningReset = delegate { sets.Add("reset"); fail(); resets++; };
                tuningInstall = delegate { return @"C:\Program Files\AMD\CNext\CNext"; };
                amdOpen = delegate(string target, bool maximize) { opened.Add(target); };
                Func<string, Control> find = delegate(string tag) { return actions.Find(c => (string)c.Tag == tag); };
                Action<string> press = delegate(string tag) { FocusHome(tag); Activate(); };
                Func<string, Slider> slider = delegate(string tag) { return find(tag) as Slider; };
                Func<string> status = delegate { var line = body.Children.Count > 0 ? body.Children[0] as StackPanel : null; return line == null ? "" :
                    ((TextBlock)line.Children[1]).Text; };

                amdTuning = null; amdTuningError = null; amdTuningPending = null; amdTuningFolder = null; amdSliderId = null;
                Show(); SetPanel(AmdTuningPanel);
            check(status() == "Lendo o driver da AMD…" && find("amd-tune-fsr") == null && !find("amd-tuning-software").IsEnabled, "antes da leitura: lendo");
                ReadAmdTuning();
                check(status() == "Radeon RX 9070 XT · ajustes globais" && ActionLabel("amd-tune-fsr") == "FSR 4 (upgrade) · Ativado" &&
                    ActionLabel("amd-tune-framegen") == "Geração de quadros FSR (upgrade) · Desativado" && ActionLabel("amd-tune-chill") ==
                        "Radeon Chill · Desativado",
                        "estado real com estado");
                check(BodyHas("UPSCALING E QUADROS") && BodyHas("LATÊNCIA E DESEMPENHO") && BodyHas("SINCRONIZAÇÃO") && BodyHas("IMAGEM") && BodyHas(
                    "ANTI-ALIASING E TEXTURAS") &&
                    BodyHas("MANUTENÇÃO") && !actions.Exists(c => ((string)c.Tag ?? "").Contains("metric")), "grupos do design e metricas fora da tela");
                check(ActionLabel("amd-tune-vsync.mode") == "Sincronização vertical · Ligada, salvo o jogo pedir" && ActionLabel("amd-tune-aa.mode") ==
                    "Anti-aliasing · Usar o do jogo" &&
                    ActionLabel("amd-tune-aa.method") == "Método de anti-aliasing · Multiamostragem" && ActionLabel("amd-tune-mlaa") ==
                        "Anti-aliasing morfológico · Ativado"
                        &&
                    ActionLabel("amd-tune-aniso") == "Filtragem anisotrópica · Desativado" && ActionLabel("amd-tune-tess.mode") == "Tesselação · AMD otimizado",
                        "opcoes novas com valores lidos");
                check(slider("amd-tune-video.sharpness") != null && slider("amd-tune-video.sharpness").Value == 50 && find("amd-tune-chill.min")
                    == null && find(
                    "amd-tune-aa.level") == null &&
                    find("amd-tune-tess.level") == null && find("amd-tune-afmf.search") == null && find("amd-tune-sharpdesktop") == null &&
                        find("amd-tune-aniso.level") ==
                        null, "subopcoes so com o recurso ligado");
                rail[AmdTuningPanel].Focus(); window.UpdateLayout(); SaveRender("amd-ajustes");
                FocusHome("amd-tune-fsr"); Navigate("BAIXO"); check(FocusedTag() == "amd-tune-framegen", "navegacao linear pelas linhas");
                Navigate("ESQUERDA"); check(Keyboard.FocusedElement == rail[AmdTuningPanel], "esquerda volta ao trilho com o logotipo");

                // Interruptor: aplica, rele e confirma pelo estado real; as subopcoes aparecem na mesma largura das linhas.
                press("amd-tune-chill");
                check(sets[sets.Count - 1] == "chill:True" && amdTuningPending == null && LatestHistoryText() == "Radeon Chill: ligado." &&
                    ActionLabel("amd-tune-chill")
                    == "Radeon Chill · Ativado" &&
                    FocusedTag() == "amd-tune-chill" && slider("amd-tune-chill.min").Value == 75 && slider("amd-tune-chill.max").Value == 140,
                        "liga e mostra os sliders do Chill");
                check(FeedbackText() == "", "ligar recurso confirmado pela linha, sem rodape rotineiro");
                check(slider("amd-tune-chill.min").Maximum == 140 && slider("amd-tune-chill.max").Minimum == 75, "minimo nunca passa do maximo");
                // Slider: anda pelo passo do driver e so aplica ao parar (um envio).
                int before = sets.Count; FocusHome("amd-tune-chill.min"); amdSliderLast = DateTime.MinValue; Navigate("DIREITA");
                check(slider("amd-tune-chill.min").Value == 76 && amdSliderId == "chill.min" && sets.Count == before, "slider espera parar antes de aplicar");
                FlushAmdSlider();
                check(sets.Count == before + 1 && sets[before] == "chill.min=76" && driver["chill"].Get("min") == 76 && LatestHistoryText() ==
                    "FPS mínimo do Chill: 76 FPS." &&
                    FocusedTag() == "amd-tune-chill.min" && amdSliderId == null, "slider aplica uma vez e mantem o foco");
                FocusHome("amd-tune-chill.max"); amdSliderLast = DateTime.MinValue; for (int i = 0; i < 20; i++) Navigate("ESQUERDA");
                check(slider("amd-tune-chill.max").Value == 76, "maximo limitado pelo minimo"); FlushAmdSlider();
                check(sets[sets.Count - 1] == "chill.max=76" && sets.Count == before + 2, "vinte passos viram um envio");
                press("amd-tune-frtc"); FocusHome("amd-tune-frtc.fps"); amdSliderLast = DateTime.MinValue; for (int i = 0; i < 20; i++) Navigate("DIREITA");
                check(slider("amd-tune-frtc.fps").Value == 184, "segurar acelera o FPS (1, 5 e 10 por passo)"); FlushAmdSlider();
                check(LatestHistoryText() == "Limite de FPS: 184 FPS." && driver["frtc"].Get("fps") == 184, "limite de FPS aplicado");
                FocusHome("amd-tune-video.sharpness"); amdSliderLast = DateTime.MinValue; Navigate("DIREITA"); FlushAmdSlider();
                check(LatestHistoryText() == "Nitidez do vídeo: 55%." && driver["video"].Get("sharpness") == 55, "porcentagem anda de 5 em 5");
                press("amd-tune-sharpening");
                check(slider("amd-tune-sharpening.sharpness").SmallChange == 10 && ActionLabel("amd-tune-sharpdesktop") ==
                    "Também na área de trabalho · Desativado",
                    "nitidez com passo do driver e area de trabalho");
                FocusHome("amd-tune-sharpening.sharpness"); amdSliderLast = DateTime.MinValue; Navigate("DIREITA"); FlushAmdSlider();
                check(LatestHistoryText() == "Intensidade da nitidez: 60%.", "intensidade de 10 em 10");
                press("amd-tune-sharpdesktop"); check(LatestHistoryText() == "Nitidez na área de trabalho: ligada." && driver["sharpdesktop"].Enabled,
                    "subopcao interruptor");
                window.UpdateLayout(); SaveRender("amd-ajustes-subopcoes");

                // Seletor: lista flutuante com foco preso; ○ fecha sem aplicar; escolher aplica uma vez.
                before = sets.Count; press("amd-tune-vsync.mode");
                check(amdTuningOpen == "vsync.mode" && flyout != null && FocusedTag() == "amd-tune-vsync.mode-opt-2" &&
                    ActionLabel("amd-tune-vsync.mode").EndsWith(
                    "Ligada, salvo o jogo pedir"), "abre na opcao atual");
                window.UpdateLayout(); SaveRender("amd-ajustes-seletor");
                Navigate("BAIXO"); check(FocusedTag() == "amd-tune-vsync.mode-opt-3", "desce na lista");
                Navigate("BAIXO"); check(FocusedTag() == "amd-tune-vsync.mode", "lista circula ate o seletor");
                Navigate("CIMA"); Navigate("ESQUERDA"); check(FocusedTag() == "amd-tune-vsync.mode-opt-3", "foco preso na lista");
            Back(); check(amdTuningOpen == null && flyout == null && FocusedTag() == "amd-tune-vsync.mode" && sets.Count == before, "voltar fecha sem aplicar");
                press("amd-tune-vsync.mode"); press("amd-tune-vsync.mode-opt-2"); check(sets.Count == before && amdTuningOpen == null, "mesma opcao nao envia");
                press("amd-tune-vsync.mode"); Navigate("BAIXO"); Activate();
                check(sets[sets.Count - 1] == "vsync.mode=3" && LatestHistoryText() == "Sincronização vertical: Sempre ligada." &&
                    ActionLabel("amd-tune-vsync.mode") ==
                    "Sincronização vertical · Sempre ligada" &&
                    FocusedTag() == "amd-tune-vsync.mode", "escolher aplica e confirma");
                press("amd-tune-aa.mode"); press("amd-tune-aa.mode-opt-2");
                check(LatestHistoryText() == "Anti-aliasing: Substituir o do jogo." && find("amd-tune-aa.level") != null && ActionLabel("amd-tune-aa.level") ==
                    "Nível · —", "nivel do AA so ao substituir");
                press("amd-tune-aa.level"); press("amd-tune-aa.level-opt-4"); check(LatestHistoryText() == "Nível do anti-aliasing: 4x." &&
                    driver["aa"].Get("level") ==
                    4, "nivel do AA");
                press("amd-tune-tess.mode"); press("amd-tune-tess.mode-opt-2"); check(ActionLabel("amd-tune-tess.level") == "Nível · 64x",
                    "nivel da tesselacao so ao substituir");
                press("amd-tune-aniso"); check(ActionLabel("amd-tune-aniso.level") == "Nível · 16x", "nivel da anisotropica com o recurso ligado");
                press("amd-tune-boost"); press("amd-tune-boost.resolution");
                check(find("amd-tune-boost.resolution-opt-50") != null && find("amd-tune-boost.resolution-opt-67") != null && FocusedTag() ==
                    "amd-tune-boost.resolution-opt-84", "resolucao do Boost pela faixa do driver");
                Activate(); press("amd-tune-boost.resolution"); press("amd-tune-boost.resolution-opt-67"); check(LatestHistoryText() ==
                    "Resolução mínima do Boost: 67%.",
                    "resolucao do Boost");
                press("amd-tune-afmf");
                check(ActionLabel("amd-tune-afmf.search") == "Busca · Automática" && ActionLabel("amd-tune-afmf.performance") == "Desempenho · Automático" &&
                    ActionLabel("amd-tune-afmf.motion") == "Movimento rápido · Repetir quadros", "opcoes do AFMF 2 com o recurso ligado");
                press("amd-tune-afmf.search"); driver["afmf"].Enabled = false; driver["afmf"].Values.Remove("motion"); ReadAmdTuning();
                check(amdTuningOpen == null && find("amd-tune-afmf.search") == null, "lista fecha quando o driver desliga o recurso");
                driver["afmf"].Enabled = true; ReadAmdTuning(); check(find("amd-tune-afmf.search") != null && find("amd-tune-afmf.motion") == null,
                    "opcao sem valor do driver some");

                // O driver muda outro recurso junto: a mensagem conta.
                coupled = "sharpening"; press("amd-tune-rsr");
                check(driver["rsr"].Enabled && !driver["sharpening"].Enabled && LatestHistoryText() ==
                    "Radeon Super Resolution: ligado. O driver também desligou Nitidez da imagem." &&
                    ActionLabel("amd-tune-sharpening") == "Nitidez da imagem · Desativado" && slider("amd-tune-rsr.sharpness") != null,
                        "efeito colateral do driver informado e refletido");
                check(LatestHistory().Type == FeedbackKind.Warning && FeedbackText() == LatestHistoryText(), "efeito colateral permanece visivel como aviso");
                coupled = null;
                // Aceito sem aplicar: aviso; recusa: erro; um ajuste por vez.
                applies = false; press("amd-tune-mlaa");
                check(driver["mlaa"].Enabled && LatestHistory().Type == FeedbackKind.Warning && LatestHistoryText().StartsWith(
                    "O driver da AMD não aplicou: Anti-aliasing morfológico"), "interruptor nao aplicado avisa");
                press("amd-tune-aa.method"); press("amd-tune-aa.method-opt-2");
                check(driver["aa"].Get("method") == 0 && LatestHistoryText().StartsWith("O driver da AMD não aplicou: Método de anti-aliasing"),
                    "valor nao aplicado avisa");
                applies = true; setFails = new InvalidOperationException("O driver da AMD recusou o valor (valor inválido).");
                    press("amd-tune-tess.level"); press(
                    "amd-tune-tess.level-opt-16");
                check(driver["tess"].Get("level") == 64 && LatestHistory().Type == FeedbackKind.Error && LatestHistoryText().Contains("recusou"),
                    "recusa do driver e erro");
                check(FeedbackText() == LatestHistoryText(), "recusa do driver aparece no rodape");
                readFails = true; ToggleAmdTuning("enhancedsync");
                check(LatestHistory().Type == FeedbackKind.Warning && FeedbackText().Contains("não foi possível confirmar") &&
                BodyHas("Não foi possível atualizar os ajustes da AMD; os valores abaixo são da última leitura. O driver da A"
                    +"MD não iniciou o ADLX (código 8)."),
                    "falha da releitura avisa incerteza e identifica valores antigos");
                readFails = false; ReadAmdTuning();
                amdTuningPending = "boost"; before = sets.Count; ToggleAmdTuning("antilag"); ToggleAmdChoice("vsync.mode");
                FocusHome("amd-tune-frtc.fps"); amdSliderLast = DateTime.MinValue; Navigate("DIREITA"); FlushAmdSlider();
                check(sets.Count == before && amdTuningOpen == null && amdSliderId == "frtc.fps" && LatestHistoryText() ==
                    "Aguarde o ajuste anterior terminar.",
                    "um ajuste por vez, slider espera");
                string signature = amdTuningSignature; driver["fsr"].Enabled = false; ReadAmdTuning();
                check(amdTuningSignature == signature && ActionLabel("amd-tune-fsr") == "FSR 4 (upgrade) · Ativado", "releitura espera o slider parar");
                amdTuningPending = null; FlushAmdSlider();
                check(sets[sets.Count - 1] == "frtc.fps=185" && ActionLabel("amd-tune-fsr") == "FSR 4 (upgrade) · Desativado",
                    "slider aplica depois e a releitura volta"
                    );

                // Redefinir cache: confirmacao, cancelar e voltar nao executam.
                press("amd-reset"); check(FocusedTag() == "amd-reset-cancel" && BodyHas("Redefinir o cache do sombreador?") && resets == 0,
                    "pede confirmacao com foco em cancelar");
                window.UpdateLayout(); SaveRender("amd-ajustes-redefinir");
                Back(); check(FocusedTag() == "amd-reset" && resets == 0 && !amdResetConfirm, "voltar cancela");
                press("amd-reset"); press("amd-reset-cancel"); check(FocusedTag() == "amd-reset" && resets == 0, "cancelar nao executa");
                press("amd-reset"); press("amd-reset-confirm");
                check(resets == 1 && LatestHistoryText() == "Cache do sombreador redefinido. Os jogos recompilam os shaders na próxima abertura."
                    && FocusedTag() ==
                    "amd-reset", "confirmar redefine");
                check(FeedbackText() == LatestHistoryText(), "confirmacao relevante do cache continua visivel");
            setFails = new InvalidOperationException("Este driver não permite redefinir o cache do sombreador."); press("amd-reset");
                press("amd-reset-confirm");
                check(resets == 1 && LatestHistory().Type == FeedbackKind.Error, "falha ao redefinir e erro");

                // Sem suporte ou interface ausente: linha desligada; sem leitura: aviso e AMD Software.
                before = sets.Count; driver["enhancedsync"].Supported = false; driver["framegen"].Error = 6; driver["vsync"].Error = 6;
                    driver["video"].Error = 6; driver[
                    "resetcache"].Supported = false; ReadAmdTuning();
                check(ActionLabel("amd-tune-enhancedsync") == "Enhanced Sync · Não suportado" && !find("amd-tune-enhancedsync").IsEnabled &&
                    ActionLabel("amd-tune-framegen") == "Geração de quadros FSR (upgrade) · Indisponível" && !find("amd-tune-framegen").IsEnabled &&
                    ActionLabel("amd-tune-vsync.mode") == "Sincronização vertical · Indisponível" && !find("amd-tune-vsync.mode").IsEnabled &&
                    find("amd-tune-video.sharpness") == null && !find("amd-reset").IsEnabled, "nao suportado e interface ausente desligados");
                ToggleAmdTuning("enhancedsync"); ToggleAmdChoice("vsync.mode"); check(sets.Count == before && amdTuningOpen == null, "nao suportado nao envia");
                readFails = true; amdTuning = null; ReadAmdTuning();
                check(status() == "Driver da AMD indisponível" && BodyHas("O driver da AMD não iniciou o ADLX (código 8).") &&
                    find("amd-tune-fsr") == null && find(
                    "amd-tuning-software").IsEnabled, "sem driver: aviso e AMD Software");
                readFails = false; press("amd-tuning-software");
                check(opened.Count == 1 && opened[0] == @"C:\Program Files\AMD\CNext\CNext\RadeonSoftware.exe" && !window.IsVisible, "abre o AMD Software");
                var toggles = new List<string>(AdlxGraphics.Toggles);
                check(TuningSideEffects(null, null, "fsr") == "" && AdlxGraphics.Keys.Length == 18 && toggles.Contains("mlaa") && toggles.Contains("video") &&
                    toggles.Contains("sharpdesktop") &&
!toggles.Contains("vsync") && !toggles.Contains("aa") && !toggles.Contains("tess") && !toggles.Contains("resetcache"), "contrato do backend");
                File.WriteAllText(Path.Combine(Output, "amd-ajustes-verificacao.txt"), "OK: " + checks +
                    " verificacoes. Lendo, estado real, seis grupos, opcoes novas com valores lidos, " +
                    "subopcoes so com o recurso ligado ou no modo que as usa, navegacao e trilho, interruptores com relei" +
                        "tura, sliders pelo passo do driver com aceleracao no FPS, " +
                    "limites do Chill, um envio ao parar, seletores com lista flutuante e foco preso, voltar sem aplicar," +
                        " mesma opcao sem envio, resolucao do Boost pela faixa, " +
                    "opcoes do AFMF 2, lista fechada quando o recurso some, efeito colateral do driver, nao aplicado, rec" +
                        "usa, um ajuste por vez, releitura adiada pelo slider, " +
                    "redefinir cache com confirmacao/cancelar/voltar/falha, nao suportado/interface ausente, driver indisponivel e abrir o AMD Software. " +
                    "Driver substituido; nenhum ajuste real lido ou alterado.\r\n", Encoding.UTF8);
            }
            finally
            {
                tuningRead = oldRead; tuningSet = oldSet; tuningValue = oldValue; tuningReset = oldReset; tuningInstall = oldInstall; amdOpen = oldOpen;
                amdTuning = null; amdTuningError = null; amdTuningPending = null; amdTuningFolder = null; amdTuningBusy = false;
                amdTuningOpen = null; amdResetConfirm = false; amdSliderId = null; amdSliderTimer.Stop();
                SetPanel(0);
            }
            VerifyAmdFeedback();
        }
    }
}

// Textos e opcoes do painel AMD (ADR 0100): nomes curtos, valores dos enums do ADLX (ADLXDefines.h), unidades e subopcoes.
// Linha "recurso" e interruptor; "recurso.campo" e seletor (com opcoes) ou slider (com faixa do driver).
using System;
using System.Collections.Generic;
using XGBP.Amd;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        static readonly string[][] AmdTuningGroups = {
            new[] { "UPSCALING E QUADROS", "fsr", "framegen", "afmf", "rsr" },
            new[] { "LATÊNCIA E DESEMPENHO", "antilag", "boost", "chill", "frtc" },
            new[] { "SINCRONIZAÇÃO", "vsync.mode", "enhancedsync" },
            new[] { "IMAGEM", "sharpening", "video" },
            new[] { "ANTI-ALIASING E TEXTURAS", "aa.mode", "aa.method", "mlaa", "aniso", "tess.mode" } };
        static readonly Dictionary<string, KeyValuePair<int[], string[]>> TuningChoices = new Dictionary<string, KeyValuePair<int[], string[]>> {
            { "vsync.mode", Choice(new[] { 0, 1, 2, 3 }, "Sempre desligada", "Desligada, salvo o jogo pedir", "Ligada, salvo o jogo pedir", "Sempre ligada") },
            { "aa.mode", Choice(new[] { 0, 1, 2 }, "Usar o do jogo", "Aprimorar o do jogo", "Substituir o do jogo") },
            { "aa.method", Choice(new[] { 0, 1, 2 }, "Multiamostragem", "Multiamostragem adaptativa", "Supersampling") },
            { "aa.level", Choice(new[] { 2, 3, 4, 5, 8, 9 }, "2x", "2x EQ", "4x", "4x EQ", "8x", "8x EQ") },
            { "aniso.level", Choice(new[] { 2, 4, 8, 16 }, "2x", "4x", "8x", "16x") },
            { "tess.mode", Choice(new[] { 0, 1, 2 }, "AMD otimizado", "Usar a do jogo", "Substituir a do jogo") },
            { "tess.level", Choice(new[] { 1, 2, 4, 6, 8, 16, 32, 64 }, "Desligada", "2x", "4x", "6x", "8x", "16x", "32x", "64x") },
            { "afmf.search", Choice(new[] { 0, 1, 2 }, "Automática", "Padrão", "Alta") },
            { "afmf.performance", Choice(new[] { 0, 1, 2 }, "Automático", "Qualidade", "Desempenho") },
            { "afmf.motion", Choice(new[] { 0, 1 }, "Repetir quadros", "Mesclar quadros") } };
        static KeyValuePair<int[], string[]> Choice(int[] values, params string[] labels) { return new KeyValuePair<int[], string[]>(values, labels); }

        static string TuningName(string key)
        {
            switch (key)
            {
                case "fsr": return "FSR 4 (upgrade)";
                case "framegen": return "Geração de quadros FSR (upgrade)";
                case "afmf": return "Fluid Motion Frames";
                case "rsr": return "Radeon Super Resolution";
                case "antilag": return "Anti-Lag";
                case "boost": return "Radeon Boost";
                case "chill": return "Radeon Chill";
                case "frtc": return "Limite de FPS";
                case "enhancedsync": return "Enhanced Sync";
                case "sharpening": return "Nitidez da imagem";
                case "sharpdesktop": return "Também na área de trabalho";
                case "video": return "Upscale de vídeo";
                case "mlaa": return "Anti-aliasing morfológico";
                case "aniso": return "Filtragem anisotrópica";
            }
            return key;
        }
        // Nome nas mensagens e no historico, fora do contexto da linha.
        static string TuningFull(string key) { return key == "sharpdesktop" ? "Nitidez na área de trabalho" : TuningName(key); }
        static bool TuningFeminine(string key) { return key == "framegen" || key == "sharpening" || key == "sharpdesktop" || key == "aniso"; }
        static string TuningOn(string key, bool on) { return on ? (TuningFeminine(key) ? "ligada" : "ligado") : (TuningFeminine(key) ?
            "desligada" : "desligado");
            }
        static string TuningFieldTitle(string id)
        {
            switch (id)
            {
                case "vsync.mode": return "Sincronização vertical";
                case "aa.mode": return "Anti-aliasing";
                case "aa.method": return "Método de anti-aliasing";
                case "tess.mode": return "Tesselação";
                case "aa.level": case "aniso.level": case "tess.level": return "Nível";
                case "afmf.search": return "Busca";
                case "afmf.performance": return "Desempenho";
                case "afmf.motion": return "Movimento rápido";
                case "boost.resolution": return "Resolução mínima";
                case "chill.min": return "FPS mínimo";
                case "chill.max": return "FPS máximo";
                case "frtc.fps": return "Limite";
                case "sharpening.sharpness": return "Intensidade";
                case "rsr.sharpness": case "video.sharpness": return "Nitidez";
            }
            return id;
        }
        static string TuningFieldFull(string id)
        {
            switch (id)
            {
                case "aa.level": return "Nível do anti-aliasing";
                case "aniso.level": return "Nível da filtragem anisotrópica";
                case "tess.level": return "Nível da tesselação";
                case "afmf.search": return "Busca do Fluid Motion Frames";
                case "afmf.performance": return "Desempenho do Fluid Motion Frames";
                case "afmf.motion": return "Movimento rápido do Fluid Motion Frames";
                case "boost.resolution": return "Resolução mínima do Boost";
                case "chill.min": return "FPS mínimo do Chill";
                case "chill.max": return "FPS máximo do Chill";
                case "frtc.fps": return "Limite de FPS";
                case "sharpening.sharpness": return "Intensidade da nitidez";
                case "rsr.sharpness": return "Nitidez do RSR";
                case "video.sharpness": return "Nitidez do vídeo";
            }
            return TuningFieldTitle(id);
        }
        static string TuningUnit(string id) { return id.StartsWith("chill.", StringComparison.Ordinal) || id == "frtc.fps" ? " FPS" : "%"; }
        static string TuningKey(string id) { int dot = id.IndexOf('.'); return dot < 0 ? id : id.Substring(0, dot); }
        static string TuningField(string id) { int dot = id.IndexOf('.'); return dot < 0 ? null : id.Substring(dot + 1); }

        // Opcoes do seletor; a resolucao do Boost vem da faixa do driver (50/67/84%). Null: e um slider.
        static KeyValuePair<int[], string[]>? TuningOptions(string id, GraphicsFeature f)
        {
            KeyValuePair<int[], string[]> fixedChoice;
            if (TuningChoices.TryGetValue(id, out fixedChoice)) return fixedChoice;
            if (id != "boost.resolution" || f == null || f.Max < f.Min || f.Step <= 0) return null;
            var values = new List<int>(); var labels = new List<string>();
            for (int v = f.Min; v <= f.Max && values.Count < 12; v += f.Step) { values.Add(v); labels.Add(v + "%"); }
            return Choice(values.ToArray(), labels.ToArray());
        }
        static string TuningValueText(string id, int value, GraphicsFeature f)
        {
            var options = TuningOptions(id, f);
            if (options == null) return value + TuningUnit(id);
            int i = Array.IndexOf(options.Value.Key, value);
            return i >= 0 ? options.Value.Value[i] : "—";
        }
        // Subopcoes: so com o recurso ligado (ou no modo que as usa) e quando o driver entregou o valor.
        static List<string> TuningChildren(string id, GraphicsFeature f)
        {
            var list = new List<string>();
            if (f == null || f.Error != 0 || !f.Supported) return list;
            string[] candidates = null;
            switch (id)
            {
                case "afmf": candidates = new[] { "afmf.search", "afmf.performance", "afmf.motion" }; break;
                case "rsr": candidates = new[] { "rsr.sharpness" }; break;
                case "boost": candidates = new[] { "boost.resolution" }; break;
                case "chill": candidates = new[] { "chill.min", "chill.max" }; break;
                case "frtc": candidates = new[] { "frtc.fps" }; break;
                case "sharpening": candidates = new[] { "sharpening.sharpness", "sharpdesktop" }; break;
                case "video": candidates = new[] { "video.sharpness" }; break;
                case "aniso": candidates = new[] { "aniso.level" }; break;
                case "aa.mode": if (f.Get("mode") > 0) candidates = new[] { "aa.level" }; break;
                case "tess.mode": if (f.Get("mode") == 2) candidates = new[] { "tess.level" }; break;
            }
            if (candidates == null || TuningField(id) == null && !f.Enabled) return list;
            foreach (var child in candidates)
            {
                string field = TuningField(child);
                if (field == null) { list.Add(child); continue; } // outro recurso; a linha confere o suporte
                if (f.Get(field) < 0) continue;
                if (TuningOptions(child, f) == null && f.Max <= f.Min) continue; // slider sem faixa valida
                list.Add(child);
            }
            return list;
        }
    }
}

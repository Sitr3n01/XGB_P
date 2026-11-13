// Fone na faixa de status: icone de fone e o nivel, so com o fone conectado. Duas fontes, so de leitura e nada enviado ao fone:
// o nivel que o fone Bluetooth informou ao Windows (ADR 0094) e o que o G HUB conhece dos fones Logitech sem fio (ADR 0106).
// Leitura a cada 30 s com a barra visivel e a cada abertura; uma fonte que falha nao apaga a outra.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using XGBP.Network;
using Shapes = System.Windows.Shapes;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        sealed class HeadsetReading
        {
            public List<HeadsetBattery> Found = new List<HeadsetBattery>();
            public List<string> Notes = new List<string>();
        }
        // Default seguro: a verificacao nunca le dispositivos reais; StartHeadsets liga o leitor no fluxo normal.
        Func<HeadsetReading> headsetRead = delegate { return new HeadsetReading(); };
        List<HeadsetBattery> headsets = new List<HeadsetBattery>();
        StackPanel headsetSlot;
        Shapes.Rectangle headsetFill;
        Shapes.Path headsetBolt;
        TextBlock headsetMark;
        bool headsetBusy, headsetWasVisible;
        DateTime headsetNextRead = DateTime.MinValue;
        string headsetLogged, headsetNoted, headsetDrawn;

        void StartHeadsets(StackPanel icons)
        {
            headsetSlot = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
            icons.Children.Add(headsetSlot);
            if (!verify) headsetRead = ReadHeadsets;
            DrawHeadset();
        }
        // G HUB primeiro (tempo limitado, GHubBattery.BudgetMs); o Bluetooth le em seguida, mesmo com o G HUB fechado.
        static HeadsetReading ReadHeadsets()
        {
            var r = new HeadsetReading();
            try { var g = GHubBattery.Read(); r.Found.AddRange(g.Headsets); if (g.Note != null) r.Notes.Add("G HUB: " + g.Note); }
            catch (Exception ex) { r.Notes.Add("G HUB: leitura falhou | " + ex.Message); }
            try { r.Found.AddRange(BluetoothBattery.Headsets()); }
            catch (Exception ex) { r.Notes.Add("Bluetooth: leitura falhou | " + ex.Message); }
            return r;
        }
        void HeadsetTick()
        {
            bool visible = window.IsVisible;
            if (visible && !headsetWasVisible) headsetNextRead = DateTime.MinValue; // cada abertura le de novo
            headsetWasVisible = visible;
            if (verify || disposed || headsetBusy || headsetSlot == null || !visible || DateTime.UtcNow < headsetNextRead) return;
            headsetBusy = true;
            var read = headsetRead;
            Task.Factory.StartNew(delegate {
                HeadsetReading found = null; string error = null;
                try { found = read(); } catch (Exception ex) { error = ex.Message; }
                DispatchPresentation(delegate {
                    headsetBusy = false; headsetNextRead = DateTime.UtcNow.AddSeconds(30);
                    // Leitura falha: o fone some da faixa, em vez de um nivel antigo parecer atual.
                    if (found == null) { found = new HeadsetReading(); found.Notes.Add("leitura falhou | " + error); }
                    string notes = string.Join("; ", found.Notes.ToArray());
                    if (notes != headsetNoted) { headsetNoted = notes; if (notes.Length > 0) WriteLog("fones: " + notes); }
                    SetHeadsets(found.Found);
                });
            });
        }
        void SetHeadsets(List<HeadsetBattery> found)
        {
            headsets = found ?? new List<HeadsetBattery>();
            OrderHeadsets(headsets);
            var parts = headsets.ConvertAll(h => h.Name + " " + h.Percent + "% " + (h.Charging ? "carregando " : "") + (h.Connected ? "conectado"
                : "desconectado") +
                " (" + (h.FromGHub ? "G HUB, " : "") + h.Link + ")");
            string text = parts.Count == 0 ? "nenhum com bateria" : string.Join("; ", parts.ToArray());
            if (text != headsetLogged) { headsetLogged = text; WriteLog("fones: " + text); }
            DrawHeadset();
            RenderSettings(); // Conexoes aberta: so reconstroi se o fone do G HUB mudou (assinatura)
        }
        // Conectados primeiro; entre eles, o do G HUB (informa carga) antes do Bluetooth; depois pelo nome.
        static void OrderHeadsets(List<HeadsetBattery> list)
        {
            list.Sort(delegate(HeadsetBattery a, HeadsetBattery b) {
                int c = b.Connected.CompareTo(a.Connected); if (c != 0) return c;
                c = b.FromGHub.CompareTo(a.FromGHub); if (c != 0) return c;
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
        }
        // O Windows guarda o ultimo nivel depois de desconectar: so o conectado aparece.
        HeadsetBattery ShownHeadset() { return headsets.Find(h => h.Connected && h.Percent >= 0 && h.Percent <= 100); }
        // Nome, nivel e, do G HUB, carga ou tempo restante estimado.
        static string HeadsetLabel(HeadsetBattery h)
        {
            return h.Name + " · " + h.Percent + "%" + (h.Charging ? " · carregando" : h.HoursLeft >= 0 ? " · cerca de " + h.HoursLeft + " h" : "");
        }
        void DrawHeadset()
        {
            if (headsetSlot == null) return;
            var h = ShownHeadset();
            string key = h == null ? "-" : h.Container + ":" + h.Link + ":" + HeadsetLabel(h);
            if (key == headsetDrawn) return;
            bool first = headsetDrawn == null; headsetDrawn = key;
            headsetSlot.Children.Clear(); headsetFill = null; headsetMark = null; headsetBolt = null;
            if (h == null) { headsetSlot.ToolTip = null; AutomationProperties.SetName(headsetSlot, "Nenhum fone com bateria"); return; }
            // Fone: arco e duas conchas, no mesmo traco dos outros icones; o pe fica na linha de base.
            var glyph = new Canvas { Width = 22, Height = 20, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Bottom };
            glyph.Children.Add(IconStroke("M 3.5,14 V 11 A 7.5,7.5 0 0 1 18.5,11 V 14", "TextPrimary", 1.7));
            glyph.Children.Add(IconFill("M 2,12.5 H 6 V 19.5 H 2 Q 1,19.5 1,18.5 V 13.5 Q 1,12.5 2,12.5 Z", "TextPrimary"));
            glyph.Children.Add(IconFill("M 16,12.5 H 20 Q 21,12.5 21,13.5 V 18.5 Q 21,19.5 20,19.5 H 16 Z", "TextPrimary"));
            headsetSlot.Children.Add(glyph);
            // Raio so com carga informada pelo G HUB; o Hands-Free do Bluetooth nao informa carga.
            var battery = BatteryGlyph(h.Percent, h.Charging, out headsetFill, out headsetBolt, out headsetMark);
            battery.Margin = new Thickness(5, 0, 0, 0);
            headsetSlot.Children.Add(battery);
            string name = HeadsetLabel(h);
            headsetSlot.ToolTip = name; AutomationProperties.SetName(headsetSlot, "Fone " + h.Link + ": " + name);
            if (!first) MotionReveal(headsetSlot, MotionFast);
        }
        // Conexoes: fone sem fio lido pelo G HUB, logo abaixo dos dispositivos Bluetooth. Linha so informativa, fora do foco.
        string GHubHeadsetSignature()
        {
            var text = new StringBuilder();
            foreach (var h in headsets) if (h.FromGHub && h.Connected) text.Append("|" + h.Name + h.Link + HeadsetDetail(h));
            return text.ToString();
        }
        void AddGHubHeadsetRows()
        {
            foreach (var h in headsets)
            {
                if (!h.FromGHub || !h.Connected) continue;
                var content = new Grid { VerticalAlignment = VerticalAlignment.Center };
                content.ColumnDefinitions.Add(new ColumnDefinition()); content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var left = Text(h.Name, 16, "TextPrimary"); left.TextWrapping = TextWrapping.NoWrap; left.TextTrimming = TextTrimming.CharacterEllipsis;
                var right = Text(HeadsetDetail(h), 14, h.Percent <= 15 && !h.Charging ? "Critical" : "Muted");
                right.TextWrapping = TextWrapping.NoWrap; right.Margin = new Thickness(12, 0, 0, 0); right.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(right, 1); content.Children.Add(left); content.Children.Add(right);
                // Mesmas medidas da linha de botao (borda 2, cantos 12, padding 10,7), sem foco nem clique.
                var row = new Border { Background = Brush("SurfaceCard"), BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 0, 0, 5), MinHeight = 40, Child = content, Tag = "fone-ghub" };
                string label = h.Name + " · " + HeadsetDetail(h);
                row.ToolTip = label; AutomationProperties.SetName(row, label);
                body.Children.Add(row);
            }
        }
        static string HeadsetDetail(HeadsetBattery h)
        {
            return h.Link + " · " + (h.Charging ? "carregando · " + h.Percent + "%" : h.Percent + "%" + (h.HoursLeft >= 0 ? " · cerca de " +
                h.HoursLeft + " h" : ""
                ));
        }
        static string BluetoothDetail(BluetoothThing d)
        {
            return (d.Connected ? "Conectado" + (d.Battery >= 0 ? " · " + d.Battery + "%" : "") : "Desconectado") + " · " + d.Kind;
        }

    }
}

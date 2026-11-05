// Fone Bluetooth na faixa de status: icone de fone e o nivel que ele informou ao Windows, so com o fone conectado (ADR 0094).
// Leitura pela propriedade do dispositivo, a cada 30 s com a barra visivel e a cada abertura; nada e enviado ao fone.
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
        // Default seguro: a verificacao nunca le dispositivos reais; StartHeadsets liga o leitor no fluxo normal.
        Func<List<HeadsetBattery>> headsetRead = delegate { return new List<HeadsetBattery>(); };
        List<HeadsetBattery> headsets = new List<HeadsetBattery>();
        StackPanel headsetSlot;
        Shapes.Rectangle headsetFill;
        TextBlock headsetMark;
        bool headsetBusy, headsetWasVisible;
        DateTime headsetNextRead = DateTime.MinValue;
        string headsetLogged, headsetDrawn;

        void StartHeadsets(StackPanel icons)
        {
            headsetSlot = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
            icons.Children.Add(headsetSlot);
            if (!verify) headsetRead = BluetoothBattery.Headsets;
            DrawHeadset();
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
                List<HeadsetBattery> found = null; string error = null;
                try { found = read(); } catch (Exception ex) { error = ex.Message; }
                DispatchPresentation(delegate {
                    headsetBusy = false; headsetNextRead = DateTime.UtcNow.AddSeconds(30);
                    // Leitura falha: o fone some da faixa, em vez de um nivel antigo parecer atual.
                    if (found == null) { found = new List<HeadsetBattery>(); if (error != headsetLogged) { headsetLogged = error;
                        WriteLog("fones bluetooth: leitura falhou | " + error); } }
                    SetHeadsets(found);
                });
            });
        }
        void SetHeadsets(List<HeadsetBattery> found)
        {
            headsets = found ?? new List<HeadsetBattery>();
            var parts = headsets.ConvertAll(h => h.Name + " " + h.Percent + "% " + (h.Connected ? "conectado" : "desconectado"));
            string text = parts.Count == 0 ? "nenhum com bateria" : string.Join("; ", parts.ToArray());
            if (text != headsetLogged) { headsetLogged = text; WriteLog("fones bluetooth: " + text); }
            DrawHeadset();
        }
        // O Windows guarda o ultimo nivel depois de desconectar: so o conectado aparece.
        HeadsetBattery ShownHeadset() { return headsets.Find(h => h.Connected && h.Percent >= 0 && h.Percent <= 100); }
        void DrawHeadset()
        {
            if (headsetSlot == null) return;
            var h = ShownHeadset();
            string key = h == null ? "-" : h.Container + ":" + h.Percent + ":" + h.Name;
            if (key == headsetDrawn) return;
            bool first = headsetDrawn == null; headsetDrawn = key;
            headsetSlot.Children.Clear(); headsetFill = null; headsetMark = null;
            if (h == null) { headsetSlot.ToolTip = null; AutomationProperties.SetName(headsetSlot, "Nenhum fone Bluetooth com bateria"); return; }
            // Fone: arco e duas conchas, no mesmo traco dos outros icones; o pe fica na linha de base.
            var glyph = new Canvas { Width = 22, Height = 20, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Bottom };
            glyph.Children.Add(IconStroke("M 3.5,14 V 11 A 7.5,7.5 0 0 1 18.5,11 V 14", "TextPrimary", 1.7));
            glyph.Children.Add(IconFill("M 2,12.5 H 6 V 19.5 H 2 Q 1,19.5 1,18.5 V 13.5 Q 1,12.5 2,12.5 Z", "TextPrimary"));
            glyph.Children.Add(IconFill("M 16,12.5 H 20 Q 21,12.5 21,13.5 V 18.5 Q 21,19.5 20,19.5 H 16 Z", "TextPrimary"));
            headsetSlot.Children.Add(glyph);
            Shapes.Path bolt; // o Hands-Free nao informa carga; nunca ha raio
            var battery = BatteryGlyph(h.Percent, false, out headsetFill, out bolt, out headsetMark);
            battery.Margin = new Thickness(5, 0, 0, 0);
            headsetSlot.Children.Add(battery);
            string name = h.Name + " · " + h.Percent + "%";
            headsetSlot.ToolTip = name; AutomationProperties.SetName(headsetSlot, "Fone Bluetooth: " + name);
            if (!first) MotionReveal(headsetSlot, MotionFast);
        }
        static string BluetoothDetail(BluetoothThing d)
        {
            return (d.Connected ? "Conectado" + (d.Battery >= 0 ? " · " + d.Battery + "%" : "") : "Desconectado") + " · " + d.Kind;
        }
    }
}

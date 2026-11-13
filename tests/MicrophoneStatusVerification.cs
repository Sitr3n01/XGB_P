// Microfone mudo na faixa de status (ADR 0107): icone vermelho so com o microfone padrao no mudo, venha o mudo do DSX
// (botao do controle), do Windows ou da propria barra. Le pelos avisos do Windows (MicrophoneWatch) e, de reserva, ao abrir
// e a cada 5 s com a barra visivel. Cada aviso tambem antecipa a leitura dos paineis Som e AMD. Somente leitura.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using XGBP.Audio;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyMicrophoneStatus()
        {
            var checks = new List<string>();
            Action<bool, string> check = delegate(bool ok, string why) { if (!ok) throw new Exception("Microfone mudo: " + why); checks.Add("OK: " + why); };
            var savedRead = micStatusRead; var savedMic = amdMic; var savedRelive = amdRelive; var savedMicRead = micRead;
            int reads = 0; MicrophoneState next = null; Exception fail = null;
            try
            {
                micStatusRead = delegate { reads++; if (fail != null) throw fail; return next; };
                SetPanel(0); Show(); window.UpdateLayout();
                var g522 = new MicrophoneState { Id = "mic-g522", Name = "Microfone (G522 LIGHTSPEED - Wireless Mode)", Volume = 100, Muted = true };
                next = g522; MicrophoneChanged();
                check(reads == 1 && micSlot.Children.Count == 1 && (string)micSlot.ToolTip == "Microfone mudo · Microfone (G522 LIGHTSPEED - Wireless Mode)",
                    "aviso do Windows le na hora e mostra o microfone mudo");
            check(AutomationProperties.GetName(micSlot) == "Microfone mudo · Microfone (G522 LIGHTSPEED - Wireless Mode)", "nome acessivel do microfone mudo");
                var icons = (Panel)window.FindName("StatusIcons");
                check(icons.Children.IndexOf(micSlot) == 0 && icons.Children.IndexOf(networkIcon) == 1, "icone antes da rede");
                window.UpdateLayout();
                var scene = (UIElement)window.FindName("Scene");
                double networkBottom = networkIcon.TranslatePoint(new Point(0, networkIcon.ActualHeight), scene).Y;
                var glyph = (FrameworkElement)micSlot.Children[0];
                check(Math.Abs(glyph.TranslatePoint(new Point(0, glyph.ActualHeight), scene).Y - networkBottom) <= 1, "pe do icone na mesma linha da rede");
                SaveRender("faixa-status-microfone-mudo");
                next = new MicrophoneState { Id = "mic-g522", Name = g522.Name, Volume = 100, Muted = false }; MicrophoneChanged();
                check(micSlot.Children.Count == 0 && micSlot.ToolTip == null, "sem mudo o icone some");
                next = null; MicrophoneChanged(); check(micSlot.Children.Count == 0, "sem microfone ativo, sem icone");
                next = g522; MicrophoneChanged(); fail = new InvalidOperationException("teste"); MicrophoneChanged();
                check(micSlot.Children.Count == 0 && micStatus == null, "leitura falha remove o icone em vez de mostrar estado antigo");
                fail = null;

                // Rajada de avisos com uma leitura em andamento: uma leitura extra no fim, nao uma por aviso.
                reads = 0; micReadBusy = true; MicrophoneChanged(); MicrophoneChanged(); MicrophoneChanged();
                check(reads == 0 && micReadAgain, "avisos em rajada esperam a leitura em andamento");
                micReadBusy = false; micReadAgain = false; MicrophoneChanged(); check(reads == 1 && micSlot.Children.Count == 1,
                    "leitura seguinte mostra o estado final"
                    );

                int before = reads; micNextRead = DateTime.MinValue; MicStatusTick();
                check(reads == before, "verificacao nao le o microfone real pelo tick");

                // Paineis abertos: o aviso adianta a leitura do Som e le a AMD na hora.
                SetPanel(6); audioNextRead = DateTime.UtcNow.AddSeconds(30); MicrophoneChanged();
                check(audioNextRead == DateTime.MinValue, "no painel Som o aviso adianta a leitura do mixer");
            int amdReads = 0; micRead = delegate { amdReads++; return new MicrophoneState { Id = "mic-g522", Name = g522.Name, Volume = 100, Muted = true }; };
                amdRelive = new XGBP.Amd.ReliveState { Installed = true, Running = true, Enabled = true, Microphone = true };
                SetPanel(AmdPanel); amdReads = 0; amdNextRead = DateTime.UtcNow.AddSeconds(30); MicrophoneChanged();
                check(amdReads == 1 && amdMic != null && amdMic.Muted, "no painel AMD o aviso le o microfone na hora");
                Func<FrameworkElement> warning = delegate {
                    foreach (UIElement child in body.Children) { var f = child as FrameworkElement; if (f != null && (string)f.Tag ==
                        "amd-mic-warning") return f; } return
                        null; };
                var warn = warning();
                check(warn != null && ((TextBlock)warn).Text == "O microfone do Windows está mudo: a gravação sai sem a sua voz." &&
                    ((TextBlock)warn).Foreground == Brush("Critical"), "AMD avisa gravacao sem voz com o microfone mudo");
                window.UpdateLayout(); SaveRender("amd-microfone-mudo");
                micRead = delegate { return new MicrophoneState { Id = "mic-g522", Name = g522.Name, Volume = 100, Muted = false }; };
                amdNextRead = DateTime.MinValue; MicrophoneChanged(); check(warning() == null, "microfone ativo: sem aviso");
                amdRelive = new XGBP.Amd.ReliveState { Installed = true, Running = true, Enabled = true, Microphone = false };
                micRead = delegate { return new MicrophoneState { Id = "mic-g522", Name = g522.Name, Volume = 100, Muted = true }; };
                amdNextRead = DateTime.MinValue; MicrophoneChanged(); check(warning() == null, "microfone fora da gravacao: sem aviso mesmo no mudo");
            }
            finally
            {
                micStatusRead = savedRead; micRead = savedMicRead; amdMic = savedMic; amdRelive = savedRelive;
                micReadBusy = micReadAgain = false; SetMicStatus(null, null); micLogged = null; Hide(); SetPanel(0);
            }
            checks.Insert(0, "OK: " + checks.Count + " verificacoes do microfone mudo (faixa de status, avisos, rajada, falha, Som e AMD).");
            checks.Add("Leitura e avisos substituidos; nenhum microfone real lido ou alterado.");
            File.WriteAllLines(Path.Combine(Output, "microfone-mudo-verificacao.txt"), checks, Encoding.UTF8);
        }
    }
}

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
        // Default seguro: a verificacao nunca le o microfone real; StartMicrophoneStatus liga o leitor no fluxo normal.
        Func<MicrophoneState> micStatusRead = delegate { return null; };
        MicrophoneWatch micWatch;
        MicrophoneState micStatus;
        StackPanel micSlot;
        bool micReadBusy, micReadAgain, micWasVisible;
        DateTime micNextRead = DateTime.MinValue;
        string micLogged, micDrawn;

        void StartMicrophoneStatus(StackPanel icons)
        {
            // Antes da rede: e um alerta, como o indicador de microfone da bandeja do Windows.
            micSlot = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, Background = Brushes.Transparent };
            icons.Children.Insert(0, micSlot);
            DrawMicStatus();
            if (verify) return;
            micStatusRead = CoreAudio.ReadMicrophone;
            var watch = new MicrophoneWatch(); micWatch = watch;
            watch.Changed += delegate { Dispatch(delegate { MicrophoneChanged(); }); };
            Task.Factory.StartNew(delegate {
                string error = null;
                try { watch.Start(); } catch (Exception ex) { error = ex.Message; }
                Dispatch(delegate {
                    WriteLog(error == null ? "microfone: avisos do Windows registrados" + (watch.BoundId == null ? " (sem microfone padrao agora)" : "")
                        : "microfone: avisos indisponiveis, leitura ao abrir e a cada 5 s | " + error);
                    ReadMicStatus();
                });
            });
        }
        void StopMicrophoneWatch()
        {
            var watch = micWatch; micWatch = null;
            if (watch != null) try { watch.Dispose(); } catch (Exception ex) { WriteLog("microfone ao encerrar: " + ex.Message); }
        }

        // Aviso do Windows (mudo, volume, troca do padrao ou de aparelho): le agora e adianta os paineis abertos.
        void MicrophoneChanged()
        {
            if (disposed) return;
            ReadMicStatus();
            if (panel == 6) audioNextRead = DateTime.MinValue;                    // Som: o ciclo de 150 ms le o mixer em seguida
            if (panel == AmdPanel) { amdNextRead = DateTime.MinValue; AmdTick(); } // AMD: le microfone e captura agora
        }
        // Reserva para um aviso perdido ou indisponivel.
        void MicStatusTick()
        {
            bool visible = window.IsVisible;
            if (visible && !micWasVisible) micNextRead = DateTime.MinValue; // cada abertura le de novo
            micWasVisible = visible;
            if (verify || disposed || !visible || DateTime.UtcNow < micNextRead) return;
            ReadMicStatus();
        }
        void ReadMicStatus()
        {
            if (disposed) return;
            if (micReadBusy) { micReadAgain = true; return; } // avisos em rajada (arrastar o volume): uma leitura por vez
            micReadBusy = true; micNextRead = DateTime.UtcNow.AddSeconds(5);
            var read = micStatusRead;
            Action<Action> run = verify ? (Action<Action>)delegate(Action a) { a(); } : delegate(Action a) { Task.Factory.StartNew(a); };
            run(delegate {
                MicrophoneState found = null; string error = null;
                try { found = read(); } catch (Exception ex) { error = ex.Message; }
                Action apply = delegate {
                    micReadBusy = false;
                    SetMicStatus(found, error);
                    if (micReadAgain) { micReadAgain = false; ReadMicStatus(); }
                };
                if (verify) apply(); else Dispatch(apply);
            });
        }
        void SetMicStatus(MicrophoneState found, string error)
        {
            // Leitura falha: sem icone, em vez de um estado antigo parecer atual.
            micStatus = error == null ? found : null;
            string text = error != null ? "leitura falhou | " + error : found == null ? "nenhum microfone ativo" : found.Name + " " +
                (found.Muted ? "mudo" : "ativo"
                );
            if (text != micLogged) { micLogged = text; WriteLog("microfone: " + text); }
            DrawMicStatus();
        }
        void DrawMicStatus()
        {
            if (micSlot == null) return;
            var m = micStatus; bool show = m != null && m.Muted;
            string key = show ? m.Id + "|" + m.Name : "-";
            if (key == micDrawn) return;
            bool first = micDrawn == null; micDrawn = key;
            micSlot.Children.Clear();
            if (!show) { micSlot.ToolTip = null; AutomationProperties.SetName(micSlot, "Microfone sem mudo"); return; }
            var icon = RecordingIcon("microphone-muted", 22);
            icon.Margin = new Thickness(0, 0, 14, 0); icon.VerticalAlignment = VerticalAlignment.Bottom;
            micSlot.Children.Add(icon);
            string label = "Microfone mudo · " + m.Name;
            micSlot.ToolTip = label; AutomationProperties.SetName(micSlot, label);
            if (!first) MotionReveal(micSlot, MotionFast);
        }
        // AMD: a gravacao com "Microfone na gravacao" ligado sai sem voz se o Windows estiver no mudo.
        bool AmdMicSilenced { get { return AmdOperational && amdRelive.Microphone && amdMic != null && amdMic.Muted; } }

    }
}

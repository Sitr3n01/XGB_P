using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using XGBP.Desktop;
using XGBP.Games;
using XGBP.Input;

namespace XGBP.Design
{
    sealed partial class Preview
    {
#if XGB_VERIFY
        // Diagnostico somente leitura: le o campo em foco e a integridade do app da frente; nenhuma tecla e enviada.
        public void VerifyKeyboardLive()
        {
            var info = TextFocusWatcher.Query(KeyboardExcludedPath);
            int own = ProcessIntegrity.Current();
            int? front = null;
            if (info.WindowProcess != 0) front = ProcessIntegrity.Level(info.WindowProcess);
            var prefs = KeyboardSettings.Load();
            string report = "Campo em foco: " + info.Describe() + ".\r\nIntegridade: barra=" + ProcessIntegrity.Name(own) + "; app da frente=" +
                ProcessIntegrity.Name(front) +
                ".\r\nPreferencias: abrir sozinho=" + prefs.AutoOpen + "; posicao=" + prefs.Position + "; sons=" + prefs.KeySounds +
                ".\r\nPlano de Ctrl+C: " + TextInput.Describe(TextInput.Plan(KeyStroke.Key(0x43, KeyModifiers.Ctrl))) +
                ".\r\nDiagnostico somente leitura: sem ganchos, sem SendInput, sem abrir o teclado.\r\n";
            File.WriteAllText(Path.Combine(Output, "teclado-real.txt"), report, Encoding.UTF8);
        }
#endif
    }
}

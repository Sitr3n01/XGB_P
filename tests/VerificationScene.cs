// Cena das verificacoes com tamanho fixo, sem depender da tela de quem roda (inclusive o runner da CI).
// O Windows limita a janela ao monitor: um pedido de 1920 x 1080 numa tela menor volta como tela + 20 px, a cena
// encolhe e os paineis passam a rolar. Na build de testes a janela aceita qualquer tamanho e comeca em 1920 x 1080.
// O produto continua cobrindo o monitor principal (PlaceWindow); este arquivo so entra em XGB_P.Tests.exe.
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        const int VerificationSceneWidth = 1920, VerificationSceneHeight = 1080;
        const int WmGetMinMaxInfo = 0x0024, MaxTrackSizeOffset = 32, VerificationMaxTrack = 16384;

        // Chamado na criacao do HWND: o gancho precisa existir antes do proximo WM_GETMINMAXINFO.
        void FixVerificationScene()
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            if (source == null) throw new InvalidOperationException("Verificacao: janela sem HwndSource.");
            source.AddHook(VerificationMinMax);
            window.Width = VerificationSceneWidth; window.Height = VerificationSceneHeight;
            FitScene(new Size(VerificationSceneWidth, VerificationSceneHeight));
        }

        // Eleva ptMaxTrackSize do MINMAXINFO. O gancho do WPF roda depois deste e le o valor ja elevado.
        static IntPtr VerificationMinMax(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message != WmGetMinMaxInfo) return IntPtr.Zero;
            Marshal.WriteInt32(lParam, MaxTrackSizeOffset, VerificationMaxTrack);
            Marshal.WriteInt32(lParam, MaxTrackSizeOffset + 4, VerificationMaxTrack);
            return IntPtr.Zero;
        }
    }
}

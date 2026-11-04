// Regressao: GDI com pixels assimetricos e apresentacao de fotos com efeitos substituidos.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using XGBP.Amd;
using XGBP.Desktop;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyCaptureCorrections()
        {
            var checks = new List<string>();
            Action<bool, string> check = (ok, why) => { if (!ok) throw new Exception("Correcao de capturas: " + why); checks.Add("OK: " + why); };
            CheckThumbnailPixels(check);
            var oldRead = capturePhotoRead; var oldApply = capturePhotoApply; var oldNow = capturePhotoNow;
            var oldMode = activationRead; var oldOpen = amdOpen; int oldEpoch = activationEpoch;
            DateTime now = new DateTime(2026, 10, 29, 20, 0, 0, DateTimeKind.Utc);
            bool? mode = false; bool maximize = false; WindowObservation front = null;
            var calls = new List<bool>(); string opened = null; bool accepts = true;
            const string file = @"C:\Capturas\foto.png";
            Func<long, WindowObservation> photo = created => new WindowObservation {
                Window = new DesktopWindow { Handle = new IntPtr(100), ProcessId = 50, ProcessCreated = created,
                    ExecutablePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"WindowsApps\Microsoft.Windows.Photos_2026.11090.22001.0_x64__8wekyb3d8bbwe\Photos.exe"),
                    AppUserModelId = "Microsoft.Windows.Photos_8wekyb3d8bbwe!App", Title = "foto.png", ClassName = "WinUIDesktopWin32WindowClass" },
                Visible = true, HasMonitor = true, Width = 800, Height = 600, MonitorWidth = 1920, MonitorHeight = 1080 };
            Action<bool?> open = active => { mode = active; front = null; calls.Clear(); accepts = true; OpenAmdTarget(file, "foto de teste"); };
            try
            {
                activationRead = delegate { return mode; }; capturePhotoNow = delegate { return now; };
                capturePhotoRead = delegate { return front; };
                capturePhotoApply = delegate(DesktopWindow target, string path, bool fit) { calls.Add(fit); return accepts; };
                amdOpen = delegate(string target, bool max) { opened = target; maximize = max; };
                open(false);
                check(opened == file && !maximize && capturePhotoPending == null && calls.Count == 0, "desktop usa abertura normal sem ajustar a janela");
                open(null); check(!maximize && capturePhotoPending == null, "API incerta nao arma apresentacao");
                mode = true; OpenAmdTarget(@"C:\Capturas\video.mp4", "video");
                check(!maximize && capturePhotoPending == null, "video nao recebe politica de foto");
                OpenAmdTarget(@"C:\AMD\RadeonSoftware.exe", "AMD");
                check(!maximize && capturePhotoPending == null, "AMD Software nao recebe politica de foto");
                check(AmdOpenInfo(file, true).WindowStyle == ProcessWindowStyle.Maximized && AmdOpenInfo(file, false).WindowStyle == ProcessWindowStyle.Normal,
                    "pedido inicial de maximizacao so na abertura elegivel");

                open(true); front = photo(1); CheckCapturePhoto();
                check(maximize && capturePhotoPending != null && calls.Count == 1 && !calls[0] && capturePhotoPending.Notice.Kind == FeedbackKind.Pending,
                    "FSE solicita maximizar sem antecipar sucesso");
                now = now.AddMilliseconds(500); CheckCapturePhoto(); check(calls.Count == 1, "espera observacao apos o pedido");
                now = now.AddMilliseconds(300); CheckCapturePhoto();
                check(calls.Count == 2 && calls[1], "shell que ignora maximizar recebe uma unica tentativa de ocupar o monitor");
                front = photo(1); front.Width = 1920; front.Height = 1080; CheckCapturePhoto();
                check(capturePhotoPending == null && LatestHistoryText() == "Foto aberta em tela cheia.", "somente geometria posterior confirma sucesso");

                open(true); front = photo(1); front.Left = front.MonitorLeft = -1920; front.Width = 1920; front.Height = 1080; CheckCapturePhoto();
                check(capturePhotoPending == null && calls.Count == 0, "foto que ja ocupa monitor secundario nao muda geometria");
                open(true); front = photo(1); CheckCapturePhoto(); front = photo(2); now = now.AddSeconds(1); CheckCapturePhoto();
                check(capturePhotoPending == null && calls.Count == 1, "PID ou janela reciclada nao recebe segunda tentativa");
                open(true); front = photo(1); CheckCapturePhoto(); front = photo(1); front.Window.Title = "outra.png"; CheckCapturePhoto();
                check(capturePhotoPending == null && calls.Count == 1, "troca de foto cancela imediatamente");
                open(true); front = photo(1); CheckCapturePhoto(); front = photo(1); front.Window.AppUserModelId = "OutroApp!App"; CheckCapturePhoto();
                check(capturePhotoPending == null && calls.Count == 1, "escolha de outro app e respeitada");
                open(true); front = photo(1); CheckCapturePhoto(); mode = false; now = now.AddSeconds(1); CheckCapturePhoto();
                check(capturePhotoPending == null && calls.Count == 1, "saida do FSE cancela sem alterar desktop");
                open(true); front = photo(1); CheckCapturePhoto(); mode = null; CheckCapturePhoto();
                check(capturePhotoPending == null && calls.Count == 1, "perda da confirmacao do modo interrompe ajuste");
                open(true); activationEpoch++; front = photo(1); CheckCapturePhoto();
                check(capturePhotoPending == null && calls.Count == 0, "geracao de ativacao antiga nao ajusta janela nova");
                open(true); front = photo(1); CheckCapturePhoto(); Show();
                check(capturePhotoPending == null && calls.Count == 1, "reabrir a barra cancela o ajuste pendente");
                Hide(); open(true); now = now.AddSeconds(4); CheckCapturePhoto();
                check(capturePhotoPending == null && calls.Count == 0 && LatestHistory().Type == FeedbackKind.Warning, "prazo sem janela termina com aviso");
                open(true); front = photo(1); accepts = false; CheckCapturePhoto(); now = now.AddSeconds(1); CheckCapturePhoto();
                now = now.AddSeconds(1); CheckCapturePhoto(); check(calls.Count == 2 && capturePhotoPending != null, "recusa nao dispara ajuste indefinido nem finge conclusao");
                now = now.AddSeconds(3); CheckCapturePhoto(); check(capturePhotoPending == null && LatestHistory().Type == FeedbackKind.Warning, "geometria nao confirmada encerra a operacao");

                var wrong = photo(1); wrong.Window.AppUserModelId = "OutroApp!App";
                check(!CapturePhotoPolicy.Matches(wrong, file), "titulo igual em outro aplicativo nao e aceito");
                wrong = photo(1); wrong.Window.ExecutablePath = @"C:\Outro\Photos.exe";
                check(!CapturePhotoPolicy.Matches(wrong, file), "nome e appId sem o executavel do pacote nao sao aceitos");
                wrong = photo(0); check(!CapturePhotoPolicy.Matches(wrong, file), "criacao de processo desconhecida nao e aceita");
                wrong = photo(1); wrong.Window.Minimized = true; check(!CapturePhotoPolicy.Matches(wrong, file), "janela minimizada nao e restaurada pela politica");
                wrong = photo(1); wrong.Window.Title = "outra.png"; check(!CapturePhotoPolicy.Matches(wrong, file), "janela de outra foto nao e aceita");
                check(CapturePhotoPolicy.IsPhoto("A.PNG") && !CapturePhotoPolicy.IsPhoto("A.mp4") && !CapturePhotoPolicy.IsPhoto(null), "tipos de foto separados de video e pasta");
            }
            finally
            {
                CancelCapturePhoto(); capturePhotoRead = oldRead; capturePhotoApply = oldApply; capturePhotoNow = oldNow;
                activationRead = oldMode; amdOpen = oldOpen; activationEpoch = oldEpoch; SetPanel(0);
            }
            checks.Insert(0, "OK: " + checks.Count + " verificacoes. GDI real apenas em bitmaps criados pelo teste; janelas/FSE/lancamentos substituidos.");
            File.WriteAllLines(Path.Combine(Output, "capturas-correcao-verificacao.txt"), checks, Encoding.UTF8);
        }
        static void CheckThumbnailPixels(Action<bool, string> check)
        {
            Color[] colors = { Color.Red, Color.Lime, Color.Blue, Color.Yellow, Color.Magenta, Color.Cyan };
            foreach (var format in new[] { PixelFormat.Format24bppRgb, PixelFormat.Format32bppArgb })
            using (var source = new Bitmap(3, 2, format))
            {
                for (int y = 0; y < 2; y++) for (int x = 0; x < 3; x++) source.SetPixel(x, y, colors[y * 3 + x]);
                IntPtr native = source.GetHbitmap();
                try
                {
                    var png = CaptureThumbnails.EncodeBitmap(native);
                    check(png != null, "HBITMAP " + format + " convertido");
                    using (var stream = new MemoryStream(png)) using (var result = new Bitmap(stream))
                    {
                        bool same = result.Width == 3 && result.Height == 2;
                        for (int y = 0; y < 2; y++) for (int x = 0; x < 3; x++) same &= result.GetPixel(x, y).ToArgb() == colors[y * 3 + x].ToArgb();
                        check(same, "cores e orientacao vertical preservadas em " + format + ", inclusive opacidade");
                    }
                }
                finally { CaptureDeleteBitmap(native); }
            }
            check(CaptureThumbnails.EncodeBitmap(IntPtr.Zero) == null && CaptureThumbnails.Png(null, 256, 144) == null, "miniatura ausente nao derruba a galeria");
        }
        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")] static extern bool CaptureDeleteBitmap(IntPtr bitmap);
    }
}

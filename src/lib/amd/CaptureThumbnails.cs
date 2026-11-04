// Miniatura de foto ou video pelo cache de miniaturas do shell (a mesma do Explorador). Somente leitura.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace XGBP.Amd
{
    public static class CaptureThumbnails
    {
        // PNG da miniatura, ou null quando o shell nao tem uma (arquivo sendo gravado, formato sem codec).
        public static byte[] Png(string path, int width, int height)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            IShellItemImageFactory factory = null; IntPtr bitmap = IntPtr.Zero;
            try
            {
                Guid iid = typeof(IShellItemImageFactory).GUID;
                // BIGGERSIZEOK | THUMBNAILONLY: nunca o icone do tipo de arquivo no lugar da imagem.
                if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factory) != 0 || factory.GetImage(new Size(width, height), 0x1 | 0x8, out bitmap) != 0) return null;
                return EncodeBitmap(bitmap);
            }
            catch (COMException) { return null; }
            catch (ArgumentException) { return null; }
            finally
            {
                if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                if (factory != null) Marshal.ReleaseComObject(factory);
            }
        }

        // GetDIBits normaliza a orientacao do HBITMAP. O sinal do cabecalho interno do shell nao
        // descreve com seguranca a ordem observada ao acessar bmBits diretamente (ADR 0084).
        internal static byte[] EncodeBitmap(IntPtr bitmap)
        {
            BITMAP bm;
            if (bitmap == IntPtr.Zero || GetObject(bitmap, Marshal.SizeOf(typeof(BITMAP)), out bm) == 0 ||
                bm.Width <= 0 || bm.Height <= 0 || bm.Width > 4096 || bm.Height > 4096) return null;
            int stride = checked(bm.Width * 4);
            var pixels = new byte[checked(stride * bm.Height)];
            var header = new BITMAPINFOHEADER { Size = Marshal.SizeOf(typeof(BITMAPINFOHEADER)), Width = bm.Width,
                Height = -bm.Height, Planes = 1, BitCount = 32 }; // BI_RGB, top-down explicito.
            IntPtr dc = GetDC(IntPtr.Zero);
            if (dc == IntPtr.Zero) return null;
            try { if (GetDIBits(dc, bitmap, 0, (uint)bm.Height, pixels, ref header, 0) != bm.Height) return null; }
            finally { ReleaseDC(IntPtr.Zero, dc); }
            // HBITMAPs opacos podem deixar o byte reservado em zero. Preservar alpha quando existir.
            bool alpha = false;
            for (int i = 3; i < pixels.Length && !alpha; i += 4) alpha = pixels[i] != 0;
            if (!alpha) for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
            using (var raw = new Bitmap(bm.Width, bm.Height, PixelFormat.Format32bppPArgb))
            using (var copy = new Bitmap(bm.Width, bm.Height, PixelFormat.Format32bppArgb))
            using (var stream = new MemoryStream())
            {
                var bits = raw.LockBits(new Rectangle(0, 0, bm.Width, bm.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
                try { for (int y = 0; y < bm.Height; y++) Marshal.Copy(pixels, y * stride, IntPtr.Add(bits.Scan0, y * bits.Stride), stride); }
                finally { raw.UnlockBits(bits); }
                using (var g = Graphics.FromImage(copy)) g.DrawImage(raw, 0, 0, bm.Width, bm.Height);
                copy.Save(stream, ImageFormat.Png); return stream.ToArray();
            }
        }

        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItemImageFactory { [PreserveSig] int GetImage(Size size, int flags, out IntPtr bitmap); }
        [StructLayout(LayoutKind.Sequential)] struct BITMAP { public int Type, Width, Height, WidthBytes; public ushort Planes, BitsPixel; public IntPtr Bits; }
        [StructLayout(LayoutKind.Sequential)] struct BITMAPINFOHEADER { public int Size, Width, Height; public ushort Planes, BitCount; public int Compression, SizeImage, XPels, YPels, ClrUsed, ClrImportant; }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory item);
        [DllImport("gdi32.dll")] static extern int GetObject(IntPtr handle, int size, out BITMAP bitmap);
        [DllImport("gdi32.dll")] static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint first, uint count, [Out] byte[] pixels, ref BITMAPINFOHEADER header, uint usage);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr handle);
    }
}

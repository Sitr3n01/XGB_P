// Sons curtos da interface pelo waveOut do Windows (winmm): somente um efeito toca por vez, sem fila.
// O gesto novo substitui o anterior. WAV PCM de 16 bits carregado na memoria, com o silencio do inicio cortado
// para a resposta ser imediata. Sem NuGet, sem arquivos temporarios; arquivo ausente ou invalido fica mudo.
// C# 5 / .NET Framework, x64. Use sempre da mesma thread.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace XGBP.Audio
{
    public sealed class UiSounds : IDisposable
    {
        // Source: amostras cortadas, sem volume; Data: o buffer tocado, refeito quando o volume muda.
        sealed class Voice { public IntPtr Device, Header, Data; public int Bytes; public double Seconds, Lead; public short[] Source; }
        readonly Dictionary<string, Voice> voices = new Dictionary<string, Voice>(StringComparer.Ordinal);
        public readonly List<string> Missing = new List<string>(), Errors = new List<string>();
        // Algum canal falhou ao tocar (aparelho de audio removido, uso exclusivo): o dono recarrega.
        public bool Broken { get; private set; }

        // files: nome do som -> arquivo na pasta. volume de 0 a 1, aplicado nas amostras.
        public UiSounds(string folder, IDictionary<string, string> files, double volume)
        {
            Volume = Math.Max(0, Math.Min(1, volume));
            foreach (var pair in files)
            {
                string path = Path.Combine(folder, pair.Value);
                if (!File.Exists(path)) { Missing.Add(pair.Key); continue; }
                try { voices[pair.Key] = Open(path, Volume, pair.Key == "selecionar" ? .160 : 0); }
                catch (Exception ex) { Errors.Add(pair.Key + ": " + ex.Message); }
            }
        }
        public bool Has(string name) { return voices.ContainsKey(name); }
        public int Count { get { return voices.Count; } }
        public double Volume { get; private set; }
        public double Duration(string name) { Voice v; return voices.TryGetValue(name, out v) ? v.Seconds : 0; }
        public int PlayingCount { get { int count = 0; foreach (var name in voices.Keys) if (!Done(name)) count++; return count; } }

        // Novo volume de 0 a 1: para os canais e refaz os buffers a partir das amostras originais (sem reabrir arquivos).
        public void SetVolume(double volume)
        {
            Volume = Math.Max(0, Math.Min(1, volume));
            foreach (var v in voices.Values) { waveOutReset(v.Device); Fill(v, Volume); }
        }
        // Maior amplitude do buffer tocado (0 a 32767), para os testes conferirem o volume aplicado.
        public int Peak(string name)
        {
            Voice v; if (!voices.TryGetValue(name, out v)) return 0;
            var data = new byte[v.Bytes]; Marshal.Copy(v.Data, data, 0, data.Length); int peak = 0;
            for (int i = 0; i + 1 < data.Length; i += 2) peak = Math.Max(peak, Math.Abs((int)(short)(data[i] | data[i + 1] << 8)));
            return peak;
        }

        public void Play(string name)
        {
            Voice v; if (!voices.TryGetValue(name, out v)) return;
            StopAll();
            // Reiniciar devolve o buffer preparado; tocar de novo reaproveita o mesmo cabecalho.
            waveOutReset(v.Device);
            if (waveOutWrite(v.Device, v.Header, HeaderSize) != 0) Broken = true;
        }
        public void Stop(string name) { Voice v; if (voices.TryGetValue(name, out v)) waveOutReset(v.Device); }
        public void StopAll() { foreach (var v in voices.Values) waveOutReset(v.Device); }
        // Terminou de tocar (ou nunca tocou): para os testes esperarem o fim sem som audivel.
        public bool Done(string name)
        {
            Voice v; if (!voices.TryGetValue(name, out v)) return true;
            int flags = Marshal.ReadInt32(v.Header, FlagsOffset);
            return (flags & 1) != 0 || (flags & 0x10) == 0;   // WHDR_DONE ou fora da fila (WHDR_INQUEUE)
        }
        public string Describe()
        {
            var text = new StringBuilder();
            foreach (var pair in voices) text.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0}={1:0.000}s (corte {2:0.000}s); ", pair.Key, pair.Value.Seconds, pair.Value.Lead);
            if (Missing.Count > 0) text.Append("ausentes: " + string.Join(", ", Missing.ToArray()) + "; ");
            if (Errors.Count > 0) text.Append("erros: " + string.Join(" | ", Errors.ToArray()));
            return text.ToString().Trim();
        }
        public void Dispose()
        {
            foreach (var v in voices.Values)
            {
                waveOutReset(v.Device); waveOutUnprepareHeader(v.Device, v.Header, HeaderSize); waveOutClose(v.Device);
                Marshal.FreeHGlobal(v.Header); Marshal.FreeHGlobal(v.Data);
            }
            voices.Clear();
        }

        static Voice Open(string path, double volume, double maximum)
        {
            short[] samples; int channels, rate;
            Read(path, out samples, out channels, out rate);
            // Corte: do primeiro trecho audivel (com 4 ms antes, para nao perder o ataque) ate 30 ms depois do ultimo.
            const int Threshold = 300;
            int first = -1, last = -1;
            for (int i = 0; i < samples.Length; i++) if (Math.Abs((int)samples[i]) > Threshold) { if (first < 0) first = i; last = i; }
            if (first < 0) throw new InvalidDataException("arquivo sem som");
            int frame = channels, pre = rate * 4 / 1000 * frame, post = rate * 30 / 1000 * frame;
            int start = Math.Max(0, first / frame * frame - pre), end = Math.Min(samples.Length, (last / frame + 1) * frame + post);
            var source = new short[end - start]; Array.Copy(samples, start, source, 0, source.Length);
            // Confirmacao frequente: ataque original, no maximo 160 ms e saida suave de 20 ms.
            // Derivacao so em memoria; o WAV local original nunca e alterado.
            int limit = (int)(maximum * rate) * frame;
            if (limit > 0 && source.Length > limit)
            {
                Array.Resize(ref source, limit);
                int fade = Math.Min(rate * 20 / 1000, limit / frame);
                for (int f = 0; f < fade; f++)
                    for (int c = 0; c < frame; c++)
                    {
                        int at = limit - (fade - f) * frame + c;
                        source[at] = (short)(source[at] * (fade - 1 - f) / Math.Max(1, fade - 1));
                    }
            }
            var format = new WaveFormat { Tag = 1, Channels = (ushort)channels, Rate = (uint)rate, BytesPerSecond = (uint)(rate * channels * 2), BlockAlign = (ushort)(channels * 2), Bits = 16 };
            var v = new Voice { Bytes = source.Length * 2, Source = source, Seconds = (double)source.Length / frame / rate, Lead = (double)start / frame / rate };
            int status = waveOutOpen(out v.Device, WaveMapper, ref format, IntPtr.Zero, IntPtr.Zero, 0);
            if (status != 0) throw new InvalidOperationException("waveOutOpen " + status);
            v.Data = Marshal.AllocHGlobal(v.Bytes); Fill(v, volume);
            v.Header = Marshal.AllocHGlobal(HeaderSize);
            Marshal.StructureToPtr(new WaveHeader { Data = v.Data, Length = (uint)v.Bytes }, v.Header, false);
            status = waveOutPrepareHeader(v.Device, v.Header, HeaderSize);
            if (status != 0) { waveOutClose(v.Device); Marshal.FreeHGlobal(v.Header); Marshal.FreeHGlobal(v.Data); throw new InvalidOperationException("waveOutPrepareHeader " + status); }
            return v;
        }
        // Copia as amostras originais com o ganho para o buffer tocado (o canal precisa estar parado).
        static void Fill(Voice v, double gain)
        {
            var data = new byte[v.Bytes];
            for (int i = 0; i < v.Source.Length; i++) { short s = (short)Math.Round(v.Source[i] * gain); data[i * 2] = (byte)s; data[i * 2 + 1] = (byte)(s >> 8); }
            Marshal.Copy(data, 0, v.Data, data.Length);
        }
        // RIFF/WAVE com PCM de 16 bits (tag 1, ou extensivel com subtipo PCM); outros formatos sao recusados.
        public static void Read(string path, out short[] samples, out int channels, out int rate)
        {
            byte[] b = File.ReadAllBytes(path);
            if (b.Length < 44 || b.Length > 16 * 1024 * 1024 || Encoding.ASCII.GetString(b, 0, 4) != "RIFF" || Encoding.ASCII.GetString(b, 8, 4) != "WAVE")
                throw new InvalidDataException("nao e um WAV");
            channels = 0; rate = 0; int bits = 0, dataAt = -1, dataSize = 0;
            for (int p = 12; p + 8 <= b.Length; )
            {
                string id = Encoding.ASCII.GetString(b, p, 4); int size = BitConverter.ToInt32(b, p + 4);
                if (size < 0 || p + 8 + (long)size > b.Length) size = b.Length - p - 8;
                if (id == "fmt " && size >= 16)
                {
                    int tag = BitConverter.ToUInt16(b, p + 8);
                    if (tag != 1 && tag != 0xFFFE) throw new InvalidDataException("formato " + tag + " nao suportado");
                    channels = BitConverter.ToUInt16(b, p + 10); rate = BitConverter.ToInt32(b, p + 12); bits = BitConverter.ToUInt16(b, p + 22);
                }
                else if (id == "data") { dataAt = p + 8; dataSize = size; }
                p += 8 + size + (size & 1);
            }
            if (bits != 16 || channels < 1 || channels > 2 || rate < 8000 || rate > 192000 || dataAt < 0) throw new InvalidDataException("WAV PCM de 16 bits esperado");
            samples = new short[dataSize / 2];
            Buffer.BlockCopy(b, dataAt, samples, 0, samples.Length * 2);
        }

        const uint WaveMapper = 0xFFFFFFFF;
        static readonly int HeaderSize = Marshal.SizeOf(typeof(WaveHeader));
        static readonly int FlagsOffset = (int)Marshal.OffsetOf(typeof(WaveHeader), "Flags");
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        struct WaveFormat { public ushort Tag, Channels; public uint Rate, BytesPerSecond; public ushort BlockAlign, Bits, Extra; }
        [StructLayout(LayoutKind.Sequential)]
        struct WaveHeader { public IntPtr Data; public uint Length, Recorded; public IntPtr User; public uint Flags, Loops; public IntPtr Next, Reserved; }
        [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr device, uint id, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr device, IntPtr header, int size);
        [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr device, IntPtr header, int size);
        [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr device, IntPtr header, int size);
        [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr device);
        [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr device);
    }
}

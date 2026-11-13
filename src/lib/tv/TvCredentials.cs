// Leitura explicita do envelope v2 original e DPAPI do mesmo usuario, sem entropia. Sem leitura automatica na fase 3.
using System;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace XGBP.Tv
{
    [DataContract] public sealed class TvCredentialEnvelope
    {
        [DataMember] public int SchemaVersion;
        [DataMember] public string Ciphertext, CertificateSha256, Mac, Duid, ObtainedAt;
    }
    public sealed class TvCredentials
    {
        readonly object gate = new object();
        readonly string output;
        readonly TvCoreConfig target;
        string token, certificate;
        public TvCredentials(string output, TvCoreConfig target) { target.Validate(); this.output = output; this.target = target.Copy(); }
        public string Token { get { lock (gate) return token; } }
        public string Certificate { get { lock (gate) return certificate; } }
        public bool Matches(TvCoreConfig config)
        { return string.Equals(target.Mac, config.Mac, StringComparison.OrdinalIgnoreCase) && string.Equals(target.Duid, config.Duid,
            StringComparison.OrdinalIgnoreCase); }
        public static bool ValidToken(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 1024) return false;
        foreach (char c in text) if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-' || c == '_' || c == '.')) return false;
            return true;
        }
        public static bool ValidPin(string pin)
        {
            if (pin == null || pin.Length != 64) return false;
            foreach (char c in pin) if (!Uri.IsHexDigit(c)) return false;
            return true;
        }
        // A origem nunca e regravada; formato legado sem pin exige novo pareamento explicito.
        public bool Import(string source)
        {
            if (string.Equals(System.IO.Path.GetFullPath(source), System.IO.Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Importação exige uma origem diferente do destino.");
            return ReadEnvelope(source, true);
        }
        public bool Load() { return ReadEnvelope(output, false); }
        bool ReadEnvelope(string source, bool copy)
        {
            var value = TvJson.Read<TvCredentialEnvelope>(source);
            if (value == null || value.SchemaVersion != 2 || !ValidPin(value.CertificateSha256) ||
                !string.Equals(value.Mac, target.Mac, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(value.Duid, target.Duid, StringComparison.OrdinalIgnoreCase)) return false;
            byte[] clear = null;
            try
            {
                clear = TvProtection.Transform(Convert.FromBase64String(value.Ciphertext ?? ""), false);
                string imported = new UTF8Encoding(false, true).GetString(clear);
                if (!ValidToken(imported)) return false;
                if (copy) Save(imported, value.CertificateSha256);
                else lock (gate) { token = imported; certificate = value.CertificateSha256.ToUpperInvariant(); }
                return true;
            }
            catch (FormatException) { return false; }
            catch (CryptographicException) { return false; }
            catch (DecoderFallbackException) { return false; }
            finally { if (clear != null) Array.Clear(clear, 0, clear.Length); }
        }
        public void Save(string value, string pin)
        {
            if (!ValidToken(value) || !ValidPin(pin)) throw new ArgumentException("Credencial da TV inválida.");
            byte[] clear = Encoding.UTF8.GetBytes(value);
            try
            {
                var envelope = new TvCredentialEnvelope { SchemaVersion = 2, Ciphertext = Convert.ToBase64String(TvProtection.Transform(clear, true)),
                    CertificateSha256 = pin.ToUpperInvariant(), Mac = target.Mac, Duid = target.Duid, ObtainedAt = DateTimeOffset.UtcNow.ToString("o") };
                lock (gate)
                {
                    // Nenhum backup de token legado em texto e criado pela migracao.
                    TvJson.Write(output, envelope, System.Threading.CancellationToken.None, false);
                    token = value; certificate = envelope.CertificateSha256;
                }
            }
            finally { Array.Clear(clear, 0, clear.Length); }
        }
        public static bool PinMatches(byte[] raw, string expected)
        {
            if (raw == null || !ValidPin(expected)) return false;
            byte[] actual; using (var sha = SHA256.Create()) actual = sha.ComputeHash(raw);
            int different = 0;
            for (int i = 0; i < actual.Length; i++) different |= actual[i] ^ Convert.ToByte(expected.Substring(i * 2, 2), 16);
            return different == 0;
        }
    }
    public static class TvProtection
    {
        [StructLayout(LayoutKind.Sequential)] struct Blob { public int Length; public IntPtr Data; }
        [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CryptProtectData(ref Blob input, string description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
        [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
        [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr memory);
        public static byte[] Transform(byte[] value, bool protect)
        {
            if (value == null || value.Length < 1 || value.Length > 16384) throw new CryptographicException("Tamanho de credencial inválido.");
            var input = new Blob { Length = value.Length, Data = Marshal.AllocHGlobal(value.Length) };
            var output = new Blob();
            try
            {
                Marshal.Copy(value, 0, input.Data, value.Length);
                bool ok = protect ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output) :
                    CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
                if (!ok) throw new CryptographicException(Marshal.GetLastWin32Error());
                if (output.Length < 1 || output.Length > 32768) throw new CryptographicException("Resposta DPAPI inválida.");
                var result = new byte[output.Length]; Marshal.Copy(output.Data, result, 0, result.Length); return result;
            }
            finally
            {
                Marshal.Copy(new byte[input.Length], 0, input.Data, input.Length); Marshal.FreeHGlobal(input.Data);
                if (output.Data != IntPtr.Zero)
        { if (output.Length > 0 && output.Length <= 32768) Marshal.Copy(new byte[output.Length], 0, output.Data, output.Length); LocalFree(output.Data); }
            }
        }
    }
}

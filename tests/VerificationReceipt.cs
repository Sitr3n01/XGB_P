// Recibo por execucao: scripts conferem ID, modo, hash do proprio binario e conclusao real.
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;

namespace XGBP.Design
{
    [DataContract]
    sealed class VerificationReceipt
    {
        [DataMember] public int Version = 1;
        [DataMember] public string RunId, Mode, ExecutableSha256, StartedUtc, CompletedUtc;
        [DataMember] public bool Success;
        readonly string path;
        public VerificationReceipt(string[] args)
        {
            int idAt = Array.IndexOf(args, "--run-id");
            Guid id;
            if (idAt >= 0 && (idAt + 1 >= args.Length || !Guid.TryParse(args[idAt + 1], out id)))
                throw new ArgumentException("ID de verificacao invalido.");
            RunId = idAt >= 0 ? Guid.Parse(args[idAt + 1]).ToString("N") : Guid.NewGuid().ToString("N");
            foreach (string arg in args) if (arg.StartsWith("--verify", StringComparison.Ordinal))
            {
                if (Mode != null) throw new ArgumentException("Execute uma verificacao por processo.");
                Mode = arg;
            }
            if (Mode == null) Mode = "--inspect-motion";
            StartedUtc = DateTime.UtcNow.ToString("o");
            ExecutableSha256 = Hash(System.Reflection.Assembly.GetExecutingAssembly().Location);
            Directory.CreateDirectory(Preview.ReportFolder);
            path = Path.Combine(Preview.ReportFolder, "execucao-" + RunId + ".json");
            if (File.Exists(path)) throw new IOException("ID de verificacao ja utilizado.");
        }
        public static string Hash(string file)
        {
            using (var stream = File.OpenRead(file)) using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
        public void Complete(bool success)
        {
            Success = success; CompletedUtc = DateTime.UtcNow.ToString("o");
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                new DataContractJsonSerializer(typeof(VerificationReceipt)).WriteObject(stream, this);
        }
    }
}

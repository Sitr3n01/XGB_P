// Diario local de recuperacao: identidades completas; arquivo invalido preservado e pausa bloqueada.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace XGBP.Games
{
    [DataContract]
    public sealed class PausedProcessRecord
    {
        [DataMember] public string Game;
        [DataMember] public uint Pid;
        [DataMember] public long Created;
    }
    [DataContract]
    public sealed class PauseRecoveryData
    {
        [DataMember] public int Version = 1;
        [DataMember] public List<PausedProcessRecord> Processes = new List<PausedProcessRecord>();
    }
    public sealed class PauseRecoveryStore
    {
        readonly string path;
        readonly object gate = new object();
        bool valid = true;
        public PauseRecoveryStore(string path) { this.path = path; }
        public void Load(PausedGameRegistry registry)
        {
            lock (gate)
            {
                try
                {
                    var data = GameFiles.Read<PauseRecoveryData>(path);
                    if (data == null || data.Version != 1 || data.Processes == null || data.Processes.Count > 4096)
                        throw new InvalidDataException("Diario de pausas invalido.");
                    foreach (var process in data.Processes)
                    {
                        if (process == null || string.IsNullOrEmpty(process.Game) || process.Pid == 0 || process.Created <= 0)
                            throw new InvalidDataException("Identidade de pausa invalida.");
                    }
                    foreach (var process in data.Processes)
                    {
                        PausedGame paused;
                        if (!registry.TryGetValue(process.Game, out paused))
                            registry[process.Game] = paused = new PausedGame { Pid = process.Pid };
                        paused.Members[process.Pid] = process.Created;
                    }
                }
                catch { valid = false; throw; }
            }
        }
        public void Save(PausedGameRegistry registry)
        {
            lock (gate)
            {
                if (!valid) throw new InvalidDataException("Diario anterior preservado; nao e seguro iniciar novas pausas.");
                var data = new PauseRecoveryData();
                foreach (var game in registry)
                    foreach (var process in game.Value.Members)
                        data.Processes.Add(new PausedProcessRecord { Game = game.Key, Pid = process.Key, Created = process.Value });
                if (data.Processes.Count > 4096) throw new InvalidDataException("Limite de processos pausados excedido.");
                if (data.Processes.Count == 0 && !File.Exists(path)) return;
                GameFiles.Write(path, data);
            }
        }
    }
}

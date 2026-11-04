// Protocolo unico para producao e testes: preparar diario -> aplicar -> registrar resultado -> recuperar pendencias.
using System;
using System.Collections.Generic;
using System.IO;

namespace XGBP.Games
{
    public sealed class GameProcessService
    {
        readonly PausedGameRegistry registry;
        readonly Func<GameActivity, List<ProcessIdentity>> tree;
        readonly Func<IList<ProcessIdentity>, ProcessAction, ProcessActionResult> apply;
        readonly Func<bool> persist;

        public GameProcessService(PausedGameRegistry registry, Func<GameActivity, List<ProcessIdentity>> tree,
            Func<IList<ProcessIdentity>, ProcessAction, ProcessActionResult> apply, Func<bool> persist)
        {
            this.registry = registry; this.tree = tree; this.apply = apply; this.persist = persist;
        }
        public string Execute(string key, GameActivity activity, ProcessAction action)
        {
            var targets = tree(activity);
            PausedGame registered;
            if (action == ProcessAction.Resume && registry.TryGetValue(key, out registered))
                foreach (var pair in registered.Members)
                    if (!targets.Exists(p => p.Pid == pair.Key)) targets.Add(new ProcessIdentity(pair.Key, pair.Value));
            if (action == ProcessAction.Suspend)
            {
                if (registry.TryGetValue(key, out registered) && registered.Members.Count > 0)
                    throw new InvalidOperationException("Existe uma pausa anterior pendente de recuperacao. Retome o jogo antes de pausar novamente.");
                var intent = new Dictionary<uint, long>();
                foreach (var target in targets)
                {
                    if (!target.Known) throw new InvalidOperationException("Identidade de pausa desconhecida.");
                    intent[target.Pid] = target.Created;
                }
                registry[key] = new PausedGame { Pid = activity.Pid, Members = intent };
                if (!persist())
                {
                    registry.Remove(key);
                    throw new IOException("Nao foi possivel preparar a recuperacao da pausa. Nenhum processo foi suspenso.");
                }
            }
            ProcessActionResult outcome;
            try { outcome = apply(targets, action); }
            catch
            {
                // O backend pode falhar depois de aplicar parte da arvore. A intencao permite compensar.
                if (action == ProcessAction.Suspend && registry.TryGetValue(key, out registered))
                    try { ResumeRegistered(key, registered); } catch (Exception) { }
                throw;
            }
            bool saved = true;
            if (action == ProcessAction.Suspend)
            {
                var members = new Dictionary<uint, long>();
                foreach (var applied in outcome.Applied) members[applied.Pid] = applied.Created;
                if (members.Count > 0) registry[key] = new PausedGame { Pid = activity.Pid, Members = members };
                else registry.Remove(key);
                saved = persist();
                outcome.RequireRoot();
            }
            else if (action == ProcessAction.Resume) saved = RetainPending(key, outcome);
            else outcome.RequireRoot();
            return !saved ? "Acao aplicada; diario de recuperacao anterior preservado por falha de gravacao." : outcome.Warning;
        }
        public bool RetainPending(string key, ProcessActionResult result)
        {
            if (result.Pending.Count == 0) registry.Remove(key);
            else
            {
                var members = new Dictionary<uint, long>();
                foreach (var pending in result.Pending) members[pending.Pid] = pending.Created;
                registry[key] = new PausedGame { Pid = result.Pending[0].Pid, Members = members };
            }
            return persist();
        }
        public string ResumeRegistered(string key, PausedGame registered)
        {
            var targets = new List<ProcessIdentity>();
            foreach (var pair in registered.Members) targets.Add(new ProcessIdentity(pair.Key, pair.Value));
            var result = apply(targets, ProcessAction.Resume);
            return RetainPending(key, result) ? result.Warning : "Retomada aplicada; diario anterior preservado por falha de gravacao.";
        }
    }
}

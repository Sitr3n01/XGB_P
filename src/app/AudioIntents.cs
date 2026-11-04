// Intencoes de volume/mute por alvo nativo, incluindo comandos em voo. Somente na thread da UI.
using System;
using System.Collections.Generic;
using XGBP.Audio;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void CoalesceAudio(AudioChange next)
        {
            foreach (var old in new List<AudioChange>(audioPending.Values))
            {
                if (old.DeviceId != next.DeviceId || old.Kind != next.Kind) continue;
                if (next.SessionIds == null) { audioPending.Remove(old.Key); continue; }
                var keep = new List<string>(old.SessionIds ?? new string[0]);
                keep.RemoveAll(id => Array.IndexOf(next.SessionIds, id) >= 0);
                if (keep.Count == 0) audioPending.Remove(old.Key); else old.SessionIds = keep.ToArray();
            }
        }
        void CompleteAudioRead(bool fresh, long revision, List<AudioChange> failed)
        {
            foreach (var change in new List<AudioChange>(audioInFlight.Values))
                if (fresh && change.Revision <= revision || failed.Contains(change) || (DateTime.UtcNow - change.QueuedAt).TotalSeconds >= 6)
                    audioInFlight.Remove(change.Key);
        }
        void EffectiveAudio(string device, string session, ref float level, ref bool muted)
        {
            long levelRevision = -1, muteRevision = -1;
            foreach (var changes in new[] { audioInFlight, audioPending }) foreach (var change in changes.Values)
            {
                if (change.DeviceId != device || (session == null ? change.SessionIds != null :
                    change.SessionIds == null || Array.IndexOf(change.SessionIds, session) < 0)) continue;
                if (change.Kind.EndsWith("-volume", StringComparison.Ordinal) && change.Revision > levelRevision)
                { level = change.Level; levelRevision = change.Revision; }
                if (change.Kind.EndsWith("-mute", StringComparison.Ordinal) && change.Revision > muteRevision)
                { muted = change.Muted; muteRevision = change.Revision; }
            }
        }
        bool BindingAudioState(AudioBinding binding, out float level, out bool muted)
        {
            level = 0; muted = true;
            if (audioSnapshot == null) return false;
            if (binding.SessionIds == null)
            {
                var device = audioSnapshot.Devices.Find(d => d.Id == binding.DeviceId);
                if (device == null || !device.Available) return false;
                level = device.Level; muted = device.Muted; EffectiveAudio(device.Id, null, ref level, ref muted); return true;
            }
            int count = 0;
            foreach (var id in binding.SessionIds)
            {
                var session = audioSnapshot.Sessions.Find(s => s.Id == id); if (session == null) continue;
                float value = session.Level; bool silence = session.Muted;
                EffectiveAudio(session.DeviceId, session.Id, ref value, ref silence);
                level += value; muted &= silence; count++;
            }
            if (count == 0) return false;
            level /= count; return true;
        }
    }
}

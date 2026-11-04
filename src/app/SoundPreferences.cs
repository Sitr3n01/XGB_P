// Preferencias locais dos sons: JSON separado dos WAVs, troca atomica e backup da versao anterior.
// Verificacoes usam apenas uma pasta isolada em bin; nunca leem nem gravam as preferencias do usuario.
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Windows.Threading;
using XGBP.Games;

namespace XGBP.Design
{
    [DataContract]
    sealed class SoundPreferenceState
    {
        [DataMember(IsRequired = true)] public int Version = 1;
        [DataMember(IsRequired = true)] public bool Enabled = true;
        [DataMember(IsRequired = true)] public int Volume = 70;
    }

    sealed partial class Preview
    {
        static readonly string SoundPreferencesFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XGB_P", "settings", "ui-sounds.json");
        readonly DispatcherTimer soundPreferencesTimer = new DispatcherTimer();
        Action<SoundPreferenceState> soundPreferencesWrite;
        bool soundPreferencesDirty, soundPreferencesError;

        static SoundPreferenceState ReadSoundPreferences(string path)
        {
            if (!File.Exists(path)) return new SoundPreferenceState();
            if (new FileInfo(path).Length > 4096) throw new InvalidDataException("Preferencias de sons grandes demais.");
            var state = GameFiles.Read<SoundPreferenceState>(path);
            if (state == null || state.Version != 1 || state.Volume < 0 || state.Volume > 100)
                throw new InvalidDataException("Preferencias de sons invalidas.");
            return state;
        }
        void RestoreSoundPreferences(string path)
        {
            SoundPreferenceState state;
            try { state = ReadSoundPreferences(path); }
            catch (Exception ex)
            {
                WriteLog("sons: preferencias invalidas; tentando backup | " + ex.Message);
                try { state = ReadSoundPreferences(path + ".bak"); }
                catch (Exception) { state = new SoundPreferenceState(); }
            }
            soundsOn = state.Enabled; soundVolume = state.Volume / 100.0;
        }
        void StartSoundPreferences()
        {
            // A bateria de testes nao herda o volume/mute real nem altera seu arquivo.
            if (verify) { soundPreferencesWrite = delegate { }; return; }
            RestoreSoundPreferences(SoundPreferencesFile); // antes de carregar canais e tocar a abertura
            soundPreferencesWrite = delegate(SoundPreferenceState state) { GameFiles.Write(SoundPreferencesFile, state); };
            soundPreferencesTimer.Interval = TimeSpan.FromMilliseconds(350);
            soundPreferencesTimer.Tick += delegate { FlushSoundPreferences(); };
            WriteLog("sons: preferencias restauradas | ligados=" + soundsOn + " | volume=" + Math.Round(soundVolume * 100));
        }
        void QueueSoundPreferences(bool immediate)
        {
            soundPreferencesDirty = true;
            soundPreferencesTimer.Stop();
            if (immediate) FlushSoundPreferences();
            else if (!verify) soundPreferencesTimer.Start();
        }
        void FlushSoundPreferences()
        {
            soundPreferencesTimer.Stop();
            if (!soundPreferencesDirty || soundPreferencesWrite == null) return;
            try
            {
                soundPreferencesWrite(new SoundPreferenceState { Enabled = soundsOn, Volume = (int)Math.Round(soundVolume * 100) });
                soundPreferencesDirty = false; soundPreferencesError = false;
            }
            catch (Exception ex)
            {
                WriteLog("sons: nao foi possivel salvar preferencias | " + ex.Message);
                if (!soundPreferencesError && !disposed) Feedback("O ajuste foi aplicado, mas não foi possível salvar os sons neste PC.", FeedbackKind.Warning);
                soundPreferencesError = true; // outra mudanca ou o encerramento tenta novamente
            }
        }

#if XGB_VERIFY
        void VerifySoundPreferences()
        {
            string folder = Path.Combine(Output, "sound-preferences-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(folder, "ui-sounds.json");
            var writer = soundPreferencesWrite; bool enabled = soundsOn; double volume = soundVolume;
            try
            {
                RestoreSoundPreferences(path);
                SoundAssert(soundsOn && soundVolume == .7 && !File.Exists(path), "sem arquivo usa defaults sem gravar");
                soundPreferencesWrite = delegate(SoundPreferenceState state) { GameFiles.Write(path, state); };
                SetPanel(5); window.UpdateLayout(); FocusHome("ui-sounds-volume");
                SetSoundVolume(.35); SoundAssert(!File.Exists(path) && soundPreferencesDirty, "arraste agrupa gravacoes");
                FlushSoundPreferences(); SoundAssert(ReadSoundPreferences(path).Volume == 35, "slider grava volume no disco");
                FocusHome("ui-sounds"); Activate();
                SoundAssert(!ReadSoundPreferences(path).Enabled && !soundPreferencesDirty, "botao salva desligado imediatamente");
                soundsOn = true; soundVolume = .7; RestoreSoundPreferences(path);
                SoundAssert(!soundsOn && soundVolume == .35, "nova leitura restaura mute e volume");
                UpdateSoundSetting(); SoundAssert(!soundVolumeSlider.IsEnabled && soundVolumeSlider.Value == 35, "UI reflete estado restaurado");
                FocusHome("ui-sounds"); Activate();
                SoundAssert(ReadSoundPreferences(path).Enabled && ReadSoundPreferences(path).Volume == 35, "religar preserva volume salvo");
                foreach (double edge in new[] { 0.0, 1.0 })
                {
                    SetSoundVolume(edge); FlushSoundPreferences(); RestoreSoundPreferences(path);
                    SoundAssert(soundVolume == edge, "limite do volume persiste: " + edge);
                }
                // Backup valido, arquivo principal danificado: restaura sem sobrescrever a evidencia.
                GameFiles.Write(path + ".bak", new SoundPreferenceState { Enabled = false, Volume = 25 });
                File.WriteAllText(path, "{quebrado"); RestoreSoundPreferences(path);
                SoundAssert(!soundsOn && soundVolume == .25 && File.ReadAllText(path) == "{quebrado", "backup recupera arquivo corrompido sem sobrescrever");
                File.Delete(path + ".bak");
                foreach (string invalid in new[] { "{}", "{\"Version\":1,\"Enabled\":true,\"Volume\":101}", "{\"Version\":2,\"Enabled\":false,\"Volume\":40}", "null" })
                {
                    File.WriteAllText(path, invalid); RestoreSoundPreferences(path);
                    SoundAssert(soundsOn && soundVolume == .7, "arquivo invalido usa defaults");
                }
                GameFiles.Write(path, new SoundPreferenceState { Volume = 40 });
                using (var locked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    SetSoundVolume(.45); FlushSoundPreferences();
                    SoundAssert(soundPreferencesDirty && soundPreferencesError && soundVolume == .45 && ReadSoundPreferences(path).Volume == 40, "falha preserva arquivo e ajuste da sessao");
                }
                FlushSoundPreferences();
                SoundAssert(!soundPreferencesDirty && !soundPreferencesError && ReadSoundPreferences(path).Volume == 45, "nova tentativa salva depois da falha");
                SetSoundVolume(.55); FlushSoundPreferences(); // mesmo flush usado no encerramento
                SoundAssert(ReadSoundPreferences(path).Volume == 55, "flush final salva ajuste pendente");
                File.WriteAllText(Path.Combine(Output, "sons-preferencias-verificacao.txt"),
                    "OK: defaults sem arquivo, slider grava e agrupa mudancas, liga/desliga salva imediatamente, releitura restaura ambos, UI restaurada, limites 0/100, " +
                    "backup de JSON corrompido, campos ausentes/versao/volume invalidos, falha de gravacao preserva arquivo e estado da sessao, nova tentativa e flush final. " +
                    "Arquivos isolados em bin; preferencias do usuario nao lidas nem alteradas.\r\n", Encoding.UTF8);
            }
            finally
            {
                soundPreferencesTimer.Stop(); soundPreferencesWrite = writer; soundPreferencesDirty = soundPreferencesError = false;
                soundsOn = enabled; soundVolume = volume; SetPanel(0);
                foreach (var file in new[] { path, path + ".tmp", path + ".bak", path + ".bak.tmp", path + ".bak.bak" }) if (File.Exists(file)) File.Delete(file);
                if (Directory.Exists(folder)) Directory.Delete(folder); // pasta propria vazia, sem exclusao recursiva
            }
        }
#endif
    }
}

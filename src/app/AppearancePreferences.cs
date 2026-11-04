// Preferencia de acessibilidade persistida sem alterar as animacoes do Windows.
using System;
using System.IO;
using System.Runtime.Serialization;
using XGBP.Games;

namespace XGBP.Design
{
    [DataContract]
    sealed class AppearancePreferenceState
    {
        [DataMember(IsRequired = true)] public int Version = 1;
        [DataMember(IsRequired = true)] public bool ReducedMotion;
        [DataMember] public int ButtonIcons; // ButtonIconScheme; ausente em arquivos antigos = Automatico
    }
    sealed partial class Preview
    {
        static readonly string AppearancePreferencesFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XGB_P", "settings", "appearance.json");
        Action<AppearancePreferenceState> appearanceWrite;
        bool appearanceDirty;

        static AppearancePreferenceState ReadAppearancePreferences(string path)
        {
            if (!File.Exists(path)) return new AppearancePreferenceState();
            if (new FileInfo(path).Length > 4096) throw new InvalidDataException("Preferências de aparência grandes demais.");
            var state = GameFiles.Read<AppearancePreferenceState>(path);
            if (state == null || state.Version != 1) throw new InvalidDataException("Preferências de aparência inválidas.");
            return state;
        }
        void RestoreAppearancePreferences(string path)
        {
            AppearancePreferenceState state;
            try { state = ReadAppearancePreferences(path); }
            catch (Exception ex)
            {
                WriteLog("aparencia: arquivo invalido; tentando backup | " + ex.Message);
                try { state = ReadAppearancePreferences(path + ".bak"); }
                catch (Exception) { state = new AppearancePreferenceState(); }
            }
            motionReduced = state.ReducedMotion;
            buttonIcons = state.ButtonIcons >= 0 && state.ButtonIcons <= (int)ButtonIconScheme.Xbox ? (ButtonIconScheme)state.ButtonIcons : ButtonIconScheme.Auto;
        }
        void StartAppearancePreferences()
        {
            if (verify) { appearanceWrite = delegate { }; return; }
            RestoreAppearancePreferences(AppearancePreferencesFile);
            appearanceWrite = state => GameFiles.Write(AppearancePreferencesFile, state);
        }
        void ToggleReducedMotion()
        {
            motionReduced = !motionReduced; SettleMotion(); UpdateMotionSetting();
            appearanceDirty = true; FlushAppearancePreferences();
        }
        void FlushAppearancePreferences()
        {
            if (!appearanceDirty || appearanceWrite == null) return;
            try { appearanceWrite(new AppearancePreferenceState { ReducedMotion = motionReduced, ButtonIcons = (int)buttonIcons }); appearanceDirty = false; }
            catch (Exception ex)
            {
                WriteLog("aparencia: nao foi possivel salvar | " + ex.Message);
                if (!disposed) Feedback("O ajuste foi aplicado, mas não foi possível salvar a aparência neste PC.", FeedbackKind.Warning);
            }
        }
    }
}

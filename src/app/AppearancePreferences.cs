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
        [DataMember] public int RailLayout; // ADR 0112: 1 com trilho, 2 sem trilho; ausente (0) segue o marcador da pasta
    }
    sealed partial class Preview
    {
        static readonly string AppearancePreferencesFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XGB_P",
            "settings", "appearance.json");
        const int RailShownLayout = 1, RailHiddenLayout = 2;
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
        // Sem a escolha gravada (arquivos anteriores a ADR 0112), vale o marcador da pasta, como na 3.0.0.
        static bool RailHiddenFor(AppearancePreferenceState state, bool marker, out bool migrate)
        {
            migrate = state.RailLayout != RailShownLayout && state.RailLayout != RailHiddenLayout;
            return migrate ? marker : state.RailLayout == RailHiddenLayout;
        }
        // Devolve true quando a escolha do trilho veio do marcador e pode ser gravada: so com o arquivo principal legivel ou ausente.
        bool RestoreAppearancePreferences(string path, bool marker = false)
        {
            AppearancePreferenceState state; bool primary = true;
            try { state = ReadAppearancePreferences(path); }
            catch (Exception ex)
            {
                primary = false;
                WriteLog("aparencia: arquivo invalido; tentando backup | " + ex.Message);
                try { state = ReadAppearancePreferences(path + ".bak"); }
                catch (Exception) { state = new AppearancePreferenceState(); }
            }
            motionReduced = state.ReducedMotion;
        buttonIcons = state.ButtonIcons >= 0 && state.ButtonIcons <= (int)ButtonIconScheme.Xbox ? (ButtonIconScheme)state.ButtonIcons : ButtonIconScheme.Auto;
            bool migrate; railHidden = RailHiddenFor(state, marker, out migrate);
            return migrate && primary;
        }
        void StartAppearancePreferences()
        {
            // Testes usam o layout de referencia; a inspecao manual ainda aceita o marcador da pasta.
            if (verify) { appearanceWrite = delegate { }; railHidden = railMarker && InspectionWindow; return; }
            appearanceWrite = state => GameFiles.Write(AppearancePreferencesFile, state);
            LoadAppearancePreferences(AppearancePreferencesFile, railMarker);
        }
        // A escolha que veio do marcador e gravada uma vez; depois disso o marcador pode sair da pasta sem mudar o layout.
        void LoadAppearancePreferences(string path, bool marker)
        {
            if (!RestoreAppearancePreferences(path, marker)) return;
            try { appearanceWrite(AppearanceSnapshot()); WriteLog("aparencia: trilho lateral gravado a partir do marcador | " + (railHidden ? "sem trilho" :
                "com trilho")); }
            catch (Exception ex) { WriteLog("aparencia: escolha do trilho nao gravada; segue o marcador | " + ex.Message); }
        }
        void ToggleReducedMotion()
        {
            motionReduced = !motionReduced; SettleMotion(); UpdateMotionSetting();
            appearanceDirty = true; FlushAppearancePreferences();
        }
        AppearancePreferenceState AppearanceSnapshot()
        {
            return new AppearancePreferenceState { ReducedMotion = motionReduced, ButtonIcons = (int)buttonIcons,
                RailLayout = railHidden ? RailHiddenLayout : RailShownLayout };
        }
        void FlushAppearancePreferences()
        {
            if (!appearanceDirty || appearanceWrite == null) return;
            try { appearanceWrite(AppearanceSnapshot()); appearanceDirty = false; }
            catch (Exception ex)
            {
                WriteLog("aparencia: nao foi possivel salvar | " + ex.Message);
                if (!disposed) Feedback("O ajuste foi aplicado, mas não foi possível salvar a aparência neste PC.", FeedbackKind.Warning);
            }
        }
    }
}

// Icones dos botoes na interface, PlayStation ou Xbox, e o Guia lido pelo XInput (ADR 0080).
// A barra continua lendo o DualSense fisico pelo HID. Com o DSX em emulacao Xbox, o Guia do Xbox 360 virtual
// e o que a Steam ve: entra como o PS do virtual DualSense, para o ciclo da Steam continuar valendo.
using System;
using XGBP.Input;

namespace XGBP.Design
{
    // Valores persistidos em appearance.json: novos esquemas entram no fim.
    enum ButtonIconScheme { Auto = 0, PlayStation = 1, Xbox = 2 }
    sealed partial class Preview
    {
        ButtonIconScheme buttonIcons;
        bool xboxPadPresent;
        XInputGuideMonitor xinputGuide;

        // Automatico: Xbox quando ha controle XInput (na emulacao DualSense do DSX nao existe nenhum).
        bool XboxIcons { get { return buttonIcons == ButtonIconScheme.Xbox || buttonIcons == ButtonIconScheme.Auto && xboxPadPresent; } }
        // O codigo e os logs usam os nomes da PlayStation; a interface mostra o equivalente do esquema atual.
        string ButtonLabel(string playStation)
        {
            if (playStation == null || !XboxIcons) return playStation;
            switch (playStation)
            {
                case "✕": return "A";
                case "○": return "B";
                case "□": return "X";
                case "△": return "Y";
                case "L1": return "LB";
                case "R1": return "RB";
                case "L2": return "LT";
                case "R2": return "RT";
                case "L3": return "LS";
                case "R3": return "RS";
            }
            return playStation; // Options e Menu tem o mesmo desenho; microfone e touchpad nao existem no Xbox
        }
        string GuideName { get { return XboxIcons ? "Botão Xbox" : "PS"; } }

        void StartXInputGuide()
        {
            xinputGuide = new XInputGuideMonitor();
            xinputGuide.Log += delegate(string message) { WriteLog(message); };
            xinputGuide.ButtonChanged += delegate(ButtonEvent e) { Dispatch(delegate { OnController(e); }); };
            xinputGuide.DeviceLost += delegate(string device) { Dispatch(delegate { GuideDeviceLost(device); }); };
            xinputGuide.PresenceChanged += delegate(bool present) { Dispatch(delegate { XboxPadPresence(present); }); };
            xinputGuide.Start();
        }
        void StopXInputGuide()
        {
            try { if (xinputGuide != null) xinputGuide.Stop(); } catch (Exception ex) { WriteLog("xinput ao encerrar: " + ex.Message); }
        }
        void XboxPadPresence(bool present)
        {
            if (present == xboxPadPresent) return;
            xboxPadPresent = present;
            WriteLog("controle: XInput " + (present ? "presente" : "ausente") + "; icones " + (XboxIcons ? "Xbox" : "PlayStation"));
            ButtonIconsChanged();
        }

        void AddButtonIconsSetting()
        {
            string scheme = buttonIcons == ButtonIconScheme.Xbox ? "Xbox" : buttonIcons == ButtonIconScheme.PlayStation ? "PlayStation" :
                "Automático (" + (XboxIcons ? "Xbox" : "PlayStation") + ")";
            var row = ConnectionButton(body, "Ícones dos botões", scheme, "button-icons", CycleButtonIcons);
            row.ToolTip = "Automático usa Xbox quando o DSX emula um controle Xbox. Só muda os desenhos; o DualSense continua sendo lido do mesmo jeito.";
        }
        // Automatico, PlayStation, Xbox e de volta; salvo junto da aparencia.
        void CycleButtonIcons()
        {
            buttonIcons = buttonIcons == ButtonIconScheme.Auto ? ButtonIconScheme.PlayStation : buttonIcons == ButtonIconScheme.PlayStation ? ButtonIconScheme.Xbox : ButtonIconScheme.Auto;
            WriteLog("controle: icones -> " + buttonIcons + " (" + (XboxIcons ? "Xbox" : "PlayStation") + ")");
            appearanceDirty = true; FlushAppearancePreferences();
            ButtonIconsChanged();
        }
        void ButtonIconsChanged()
        {
            // Textos com nomes de botoes (Abertura, dicas do Som) e o teclado visivel acompanham o esquema.
            if (panel == 5 && settingsPage == "main" && window.IsVisible) RebuildSettings("button-icons");
            if (kbVisible) BuildKeyboardLayer();
        }
    }
}

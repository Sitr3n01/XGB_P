// Sobre as telas do Big Picture, que ja mostram a hora no canto, a barra esconde o proprio relogio e leva rede e
// bateria para o lado do titulo do painel (ADR 0093).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using XGBP.Desktop;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        const double SteamStatusGap = 24;
        bool steamStatusActive;
        Thickness statusIconsMargin;
        Func<DesktopWindow, bool> steamUiWindow = DesktopWindows.IsSteamBigPicture;
        // A verificacao nunca depende da janela real da frente.
        void StartSteamStatus() { if (verify) steamUiWindow = delegate { return false; }; }

        // Na abertura: decide pela janela que estava na frente (qualquer tela do Big Picture).
        void UpdateStatusPlacement()
        {
            bool steam = false;
            try { steam = previousWindow != null && previousWindow.Window != null && steamUiWindow(previousWindow.Window); }
            catch (Exception) { steam = false; } // leitura incerta: faixa padrao com a hora
            ApplyStatusPlacement(steam);
        }
        void ApplyStatusPlacement(bool steam)
        {
            var icons = (StackPanel)window.FindName("StatusIcons");
            var area = (StackPanel)window.FindName("StatusArea");
            var anchor = (Canvas)window.FindName("SteamStatusAnchor");
            if (steam != steamStatusActive)
            {
                ((Panel)icons.Parent).Children.Remove(icons);
                if (steam) { statusIconsMargin = icons.Margin; icons.Margin = new Thickness(0); anchor.Children.Add(icons); }
                else { icons.Margin = statusIconsMargin; area.Children.Insert(0, icons); }
                area.Visibility = steam ? Visibility.Collapsed : Visibility.Visible;
                steamStatusActive = steam;
            }
            if (steam) PlaceSteamStatus();
        }
        // Centro dos icones na altura do centro do titulo do painel, a SteamStatusGap da borda direita do painel.
        void PlaceSteamStatus()
        {
            var icons = (StackPanel)window.FindName("StatusIcons");
            var anchor = (Canvas)window.FindName("SteamStatusAnchor");
            var title = (FrameworkElement)window.FindName("PanelTitle");
            window.UpdateLayout();
            // DesiredSize ja inclui a escala de 1,15 da LayoutTransform dos icones.
            double top = title.TranslatePoint(new Point(), anchor).Y + (title.ActualHeight - icons.DesiredSize.Height) / 2;
            Canvas.SetLeft(icons, SteamStatusGap); Canvas.SetTop(icons, Math.Round(top));
        }
    }
}

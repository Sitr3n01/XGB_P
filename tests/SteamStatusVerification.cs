// Verificacao do status nas telas Steam (ADR 0093): janela da frente substituida, renders com dados de teste.
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
        void VerifySteamStatus()
        {
            var checks = new List<string>();
            Action<bool, string> check = delegate(bool ok, string why) { if (!ok) throw new Exception("Status Steam: " + why); checks.Add("OK: " + why); };
            var oldPrevious = previousWindow; var oldSteam = steamUiWindow;
            try
            {
                MotionFixtures(); SetPanel(0); Show(); window.UpdateLayout();
                // Previa com o conjunto real: rede e um DualSense Bluetooth (dados de teste).
                batteries.Clear(); batteryDevice = null;
                SetBattery("fisico-bt", StatusSample(5, 55, 0, "descarregando", XGBP.Input.BatterySource.PhysicalBluetooth)); UpdateStatus();
                SetHeadsets(new List<XGBP.Network.HeadsetBattery> { new XGBP.Network.HeadsetBattery { Name = "G435", Percent = 80, Connected = true } });
                steamUiWindow = delegate { return true; }; previousWindow = new WindowObservation { Window = new DesktopWindow { Handle = new IntPtr(5),
                    ProcessId = 5 } };
                UpdateStatusPlacement();
                check(steamStatusActive && MotionPart("StatusArea").Visibility == Visibility.Collapsed,
                    "Big Picture na frente ao abrir: sem a hora da barra, rede e bateria ao lado do titulo");
                steamUiWindow = delegate { return false; }; UpdateStatusPlacement();
                check(!steamStatusActive && MotionPart("StatusArea").Visibility == Visibility.Visible, "outra janela na frente: faixa padrao com a hora");
                steamUiWindow = delegate { return true; }; UpdateStatusPlacement(); previousWindow = null; UpdateStatusPlacement();
                check(!steamStatusActive && MotionPart("StatusArea").Visibility == Visibility.Visible, "sem janela na frente volta a faixa padrao");
                steamUiWindow = delegate { throw new InvalidOperationException("fixture: leitura incerta"); };
                previousWindow = new WindowObservation { Window = new DesktopWindow { Handle = new IntPtr(5), ProcessId = 5 } };
                UpdateStatusPlacement(); check(!steamStatusActive, "leitura incerta da janela mantem a faixa padrao");
                var cases = new[] { new[] { 1920, 1080 }, new[] { 3840, 2160 } };
                foreach (var c in cases)
                {
                    string label = c[0] + "x" + c[1];
                    FitScene(new Size(c[0], c[1])); window.UpdateLayout();
                    SaveSceneRender("status-padrao-" + label, c[0], c[1]);
                    ApplyStatusPlacement(true); window.UpdateLayout();
                    var icons = MotionPart("StatusIcons"); var panelBox = SceneBox(MotionPart("PanelFrame")); var titleBox = SceneBox(MotionPart("PanelTitle"));
                    var box = SceneBox(icons); var scene = MotionPart("Scene");
                    check(MotionPart("StatusArea").Visibility == Visibility.Collapsed, "hora da barra oculta sobre a Steam em " + label);
                    check(box.Left >= panelBox.Right + SteamStatusGap - .5 && box.Left <= panelBox.Right + SteamStatusGap + .5,
                        "rede e bateria a " + SteamStatusGap + " DIP da borda do painel em " + label);
                    check(Math.Abs((box.Top + box.Bottom) / 2 - (titleBox.Top + titleBox.Bottom) / 2) <= 1, "icones centrados na linha do titulo em " + label);
                    check(box.Right <= scene.Width - sceneEdge && box.Width > 0 && box.Height > 0, "icones dentro da area segura em " + label);
                    SaveSceneRender("status-steam-" + label, c[0], c[1]);
                    SetPanel(6); window.UpdateLayout(); PlaceSteamStatus();
                    box = SceneBox(icons); titleBox = SceneBox(MotionPart("PanelTitle"));
                    check(Math.Abs((box.Top + box.Bottom) / 2 - (titleBox.Top + titleBox.Bottom) / 2) <= 1,
                        "outro painel com altura diferente mantem o alinhamento em " + label);
                    SaveSceneRender("status-steam-som-" + label, c[0], c[1]);
                    SetPanel(0); ApplyStatusPlacement(false); window.UpdateLayout();
                    check(MotionPart("StatusArea").Visibility == Visibility.Visible && icons.Parent == MotionPart("StatusArea"),
                        "fora da Steam a faixa volta ao canto com a hora em " + label);
                }
            }
            finally
            {
                ApplyStatusPlacement(false); previousWindow = oldPrevious; steamUiWindow = oldSteam;
                batteries.Clear(); batteryDevice = null; statusDrawn = null; SetHeadsets(null); headsetLogged = null; UpdateStatus();
                FitScene(new Size(window.Width, window.Height)); window.UpdateLayout(); Hide(); SetPanel(0);
            }
            checks.Insert(0, "OK: " + checks.Count + " verificacoes do status nas telas Steam.");
            checks.Add("Renders com dados de teste: status-padrao-*, status-steam-* e status-steam-som-*. Janela da frente substituida.");
            File.WriteAllLines(Path.Combine(Output, "status-steam-verificacao.txt"), checks, Encoding.UTF8);
        }
    }
}

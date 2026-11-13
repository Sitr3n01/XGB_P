// XGB_P: interface residente WPF, acoes do usuario e verificacoes isoladas. HID somente leitura.
// C# 5 / WPF do .NET Framework, sem instalacao automatica de pacotes.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using XGBP.Input;
using Forms = System.Windows.Forms;

namespace XGBP.Design
{

    sealed partial class Preview
    {
#if XGB_VERIFY
        public void Verify()
        {
            // Renderiza a mesma arvore WPF e verifica transicoes sem pressionar teclas do usuario.
            window.Width = 1920; window.Height = 1080; window.Left = -30000; window.Top = -30000;
            window.Show(); window.UpdateLayout();
            foreach (int index in new[] { 0, 3, 4, 5, 6 })
            {
                SetPanel(index);
                if (index == 0) actions[0].Focus(); else rail[index].Focus();
                window.UpdateLayout();
                SaveRender("painel-" + index);
            }
            SetPanel(3); actions[0].Focus(); Back();
            if (Keyboard.FocusedElement != rail[3]) throw new Exception("Falha: voltar ao trilho.");
            Back(); if (window.IsVisible) throw new Exception("Falha: ocultar pelo trilho.");
            RequestActivation("Win+G");
            if (!window.IsVisible) throw new Exception("Falha: abrir por ativacao Win+G.");
            RequestActivation("Win+G");
            if (window.IsVisible) throw new Exception("Falha: ocultar por ativacao Win+G.");
            // Biblioteca recebe foco no ciclo, sem despacho externo nem painel proprio.
            Show(); SetPanel(0); rail[0].Focus(); ChangePanel(1);
            if (panel != 0 || Keyboard.FocusedElement != rail[1]) throw new Exception("Falha: ancorar na Biblioteca.");
            ChangePanel(1); if (panel != 6) throw new Exception("Falha: proximo painel.");
            ChangePanel(-1); if (panel != 6 || Keyboard.FocusedElement != rail[1]) throw new Exception("Falha: voltar a Biblioteca.");
            ChangePanel(-1); if (panel != 0) throw new Exception("Falha: painel anterior.");
            ChangePanel(-1); if (panel != 4) throw new Exception("Falha: ciclo do painel anterior.");
            Show(); window.UpdateLayout();
            rail[0].Focus(); Navigate("BAIXO"); Navigate("BAIXO");
            if (Keyboard.FocusedElement != rail[6]) throw new Exception("Falha: trilho sem Jogo atual.");
            Navigate("DIREITA");
            if ((string)((Control)Keyboard.FocusedElement).Tag != "output") throw new Exception("Falha: entrada do Som pela saida de som.");
            VerifyAudioPanel();
            VerifySystemPower();
            VerifyReleaseSurface();
            VerifyAppearancePreferences();
            VerifyHome();
            VerifyGames();
            VerifyHomeGamesLayout();
            VerifyOpenApps();
            VerifyXboxOpenApps();
            VerifyAppSwitchDelivery();
            VerifyGameTransitions();
            VerifySteamSessionRelease();
            VerifyMedia();
            VerifyMediaConflicts();
            VerifyPerf();
            VerifyBattery(); VerifyStatus(); VerifyHeadsetStatus(); VerifyMicrophoneStatus();
            VerifySteamActions();
            VerifyWindowFocus();
            VerifySteamInput();
            VerifySettingsActions();
            VerifyConnections();
            VerifySettingsExtras();
            VerifyStores(); VerifyPrograms();
            VerifyRailFocus();
            VerifySounds();
            VerifyLaunchSettings();
            VerifyDescriptions();
            VerifyOperationFeedback();
            VerifyActionHistory();
            VerifyButtonIcons();
            VerifyAmd();
            VerifyAmdTuning();
            VerifyCaptureCorrections();
            VerifySettingToggles();
            VerifyController();
            VerifyActivation();
            VerifyDesktopWinG();
            VerifyGamePersistence();
            VerifyLauncherChildren();
            VerifyExitGamingMode();
            VerifyTaskbarAutoHide(); VerifyAppVolume(); VerifySteamStatus();
            VerifyKeyboard(); VerifyKeyboardCompose(); VerifyWake(); VerifyTv(); VerifyTvCore(); VerifyTvIntegration();
            VerifyAccentTheme();
            VerifyScene(); VerifyRailSetting(); // por ultimo: liga e desliga o layout real (ADR 0112)
            File.WriteAllText(Path.Combine(Output, "verificacao.txt"),
                "OK: 5 paineis renderizados (sem Jogo atual); camada sem titulo, selo, dicas ou botao de encerrar; ce" +
                "na responsiva em 8 telas (escala do Windows, area minima, margem segura, barra e status nos cantos, " +
                "Home com cartoes proporcionais e rolagem quando necessaria); foco e atalho; ciclo e trilho sem Jogo " +
                "atual; Som com player no topo, entrada pela saida de som, mute compacto e pelo slider, listas de dis" +
                "positivos flutuantes sem deslocar controles e com foco preso, apps no painel principal, agrupamento " +
                "por identidade/GUID, alvos em multiplas saidas, scroll/foco durante tracking e estado vazio; energia" +
                " real com confirmacao e efeitos substituidos nos testes; Home com Voltar ao jogo logo abaixo do Inic" +
                "io e Acesso rapido abaixo dele, Biblioteca/Programas e Som do sistema/Gravacao em quatro retangulos " +
                "somente com icones, seis quadrados incluindo AMD e nome do foco no titulo da secao; alternador de ja" +
                "nelas, segurar 2 s mostra o X de fechar ao lado do icone (toque curto abre, Esc/foco recolhe, fechar" +
                " pela janela, erro e janela que continua aberta), faixa com setas sobrepostas/degrade, paginacao sem" +
                " ativar app, foco fora do esfumacado, scroll/foco, janela fechada, erros e confirmacao de foreground" +
                "; player de midia com reproduzir/pausar, anterior/proxima, aleatorio, ciclo de repetir, posicao, vol" +
                "ume/mute pelo mixer, troca de fonte, controles inativos pulados, erro e fonte encerrada; Desempenho " +
                "com valores formatados sem rolagem, lista flutuante de modos de energia com foco preso, aplicacao so" +
                " pela escolha, erro, estado sem HWiNFO, fora do plano Equilibrado e mapeamento de rotulos AMD/Intel/" +
                "NVIDIA e dos rotulos reais AM5/RDNA4 (SVI3, TBP sem o limite, hotspot sem o maximo, FCLK/UCLK, DIMM," +
                " PresentMon fora); faixa de status com hora grande, rede (Ethernet, Wi-Fi por barras, sem internet, " +
                "sem conexao, ilegivel) e DualSense com bateria (normal, baixa, carregando, erro, aguardando, troca e" +
                " perda de dispositivo); Home e retorno; handlers Steam e erros; Configuracoes do Windows/Steam com d" +
                "espacho, ocultacao e erros; Conexoes (Wi-Fi e Bluetooth, redes e dispositivos conhecidos) com comand" +
                "os substituidos, ver conexoes-verificacao.txt; Luz noturna, DSX e contorno vermelho dos botoes de fe" +
                "char, ver configuracoes-extras-verificacao.txt; Lojas pelo acesso rapido, ver lojas-verificacao.txt." +
                "\r\nAudio, janelas, midia e lancamentos externos substituidos na verificacao; nao confirma tecla fis" +
                "ica, controle real ou telas externas. Ver verificacao de hardware de audio separada.\r\n", Encoding.UTF8);
            File.AppendAllText(Path.Combine(Output, "verificacao.txt"),
                "Jogos: VDF textual/binario; catalogo multipla biblioteca, atalhos unsigned e wrappers; historico ato" +
                "mico/protegido; sessoes por executavel/PID+criacao, foco, minimizacao e recriacao; recentes reais co" +
                "m imagens locais em cache e simbolo de reserva, estados vazios, confirmacao de troca, despacho subst" +
                "ituido, erro e timeout. Ver jogos-verificacao.txt.\r\n", Encoding.UTF8);
            WriteLog("verificacao OK"); window.Hide();
        }
#endif

#if XGB_VERIFY
        void SaveRender(string name)
        {
            var scene = (FrameworkElement)window.FindName("Scene");
            var bitmap = new RenderTargetBitmap((int)Math.Round(scene.ActualWidth), (int)Math.Round(scene.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(scene);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(Output, name + ".png"))) encoder.Save(file);
        }
#endif

    }
}

// Lojas: painel aberto pelo Acesso rapido da Home, com as lojas e launchers de jogos instalados.
// Leitura numa thread STA ao abrir o painel (os icones vem do shell); abrir uma loja so por escolha do usuario.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XGBP.Games;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        const int StoresPanel = 7;
        List<GameStore> stores;
        bool storesBusy;
        string storesSignature;
        // Substituiveis: a verificacao nunca abre lojas reais.
        Func<bool, List<GameStore>> storesRead = GameStores.Read;
        Action<GameStore> storeOpen = GameStores.Open;

        // Lojas fica sob a Home: no trilho, o destaque e o foco ficam no Inicio.
        static int RailIndex(int index) { return index == StoresPanel ? 0 : index; }

        void StartStores()
        {
#if XGB_VERIFY
            if (verify) { storesRead = delegate { return ExampleStores(); }; storeOpen = delegate { }; }
#endif
        }
#if XGB_VERIFY
        static List<GameStore> ExampleStores()
        {
            byte[] icon;
            using (var bitmap = new System.Drawing.Bitmap(32, 32))
            using (var stream = new System.IO.MemoryStream())
            {
                using (var g = System.Drawing.Graphics.FromImage(bitmap)) g.FillEllipse(System.Drawing.Brushes.SeaGreen, 2, 2, 28, 28);
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png); icon = stream.ToArray();
            }
            return new List<GameStore> {
                new GameStore { Key = "xbox", Name = "Xbox", Detail = "App Xbox · Game Pass", Target = @"shell:AppsFolder\Exemplo!App", IconPng = icon },
                new GameStore { Key = "steam", Name = "Steam", Detail = "Loja da Steam", Target = "steam://url/StoreFrontPage" },
                new GameStore { Key = "epic", Name = "Epic Games", Detail = "Loja da Epic", Target = "com.epicgames.launcher://store" },
                new GameStore { Key = "ea", Name = "EA app", Detail = "Abre o app", Target = @"C:\Exemplo\EALauncher.exe" } };
        }
#endif

        void BuildStores()
        {
            storesSignature = StoresSignature();
            Caption("LOJAS INSTALADAS" + (stores != null ? " · " + stores.Count : ""));
            if (stores == null) { ConnectionNote("Procurando lojas…"); return; }
            if (stores.Count == 0) ConnectionNote("Nenhuma loja de jogos encontrada neste PC.");
            foreach (var entry in stores)
            {
                var store = entry;
                var content = new Grid();
                content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) }); content.ColumnDefinitions.Add(new ColumnDefinition());
                FrameworkElement icon = PngImage(store.IconPng, 32) ?? (FrameworkElement)Symbol("", 22);
                content.Children.Add(new Border { Background = Brush("SurfaceHover"), CornerRadius = new CornerRadius(6), Height = 44, Child = icon });
                var labels = new StackPanel { Margin = new Thickness(13, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                var name = Text(store.Name, 20, "TextPrimary"); name.TextWrapping = TextWrapping.NoWrap; name.TextTrimming = TextTrimming.CharacterEllipsis;
                var detail = Text(store.Detail, 14, "Muted"); detail.TextWrapping = TextWrapping.NoWrap; detail.TextTrimming = TextTrimming.CharacterEllipsis;
                labels.Children.Add(name); labels.Children.Add(detail); Grid.SetColumn(labels, 1); content.Children.Add(labels);
                var button = HomeButton(content, delegate { OpenStore(store); });
                button.Tag = "store-" + store.Key; button.ToolTip = store.Name + " · " + store.Detail;
                System.Windows.Automation.AutomationProperties.SetName(button, store.Name + " · " + store.Detail);
                button.Padding = new Thickness(10, 6, 10, 6); button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                body.Children.Add(button);
            }
        }
        string StoresSignature()
        {
            if (stores == null) return "lendo";
            var text = new System.Text.StringBuilder();
            foreach (var s in stores) text.Append(s.Key + "|" + s.Detail + "|" + s.Target + "|" + (s.IconPng == null ? 0 : s.IconPng.Length) + ";");
            return text.ToString();
        }

        // A cada abertura do painel: a lista anterior aparece na hora e a leitura nova so redesenha se algo mudou.
        void ReadStores()
        {
            if (storesBusy) return;
            storesBusy = true;
            var read = storesRead; bool xbox = xboxMode() == true;
            ThreadStart work = delegate {
                List<GameStore> found = null; string error = null;
                try { found = read(xbox); } catch (Exception ex) { error = ex.Message; }
                Action done = delegate {
                    storesBusy = false;
                    if (error != null) WriteLog("lojas: leitura falhou | " + error);
                    if (found == null) { if (stores == null) stores = new List<GameStore>(); }
                    else { stores = found; WriteLog("lojas: " + found.Count + " encontradas | " + string.Join(", ", found.ConvertAll(s => s.Key).ToArray())); }
                    if (panel == StoresPanel && StoresSignature() != storesSignature) RebuildStores();
                };
                if (verify) done(); else Dispatch(done);
            };
            if (verify) { work(); return; }
            var thread = new Thread(work) { IsBackground = true, Name = "XGBP lojas" };
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
        }
        void RebuildStores()
        {
            var focused = Keyboard.FocusedElement as Control;
            string tag = focused != null && actions.Contains(focused) ? focused.Tag as string : null;
            bool onRail = rail.Contains(focused as Button);
            double height = ReleaseSidebarHeight();
            CancelElementMotion(body); body.Children.Clear(); actions.Clear(); flyout = null; flyoutSelector = null; panelFill = null;
            BuildStores(); window.UpdateLayout();
            // Fora do trilho, o foco fica na mesma loja ou vai para a primeira (a lista pode chegar depois de abrir o painel).
            if (!onRail) { var target = actions.Find(c => (string)c.Tag == tag && c.IsEnabled) ?? actions.Find(c => c.IsEnabled); if (target != null) target.Focus(); }
            MotionHeight(height);
        }
        void OpenStore(GameStore store)
        {
            try
            {
                storeOpen(store);
                WriteLog("lojas: " + store.Key + " aberta | " + store.Target);
                // O shell aceitou o pedido; isso nao confirma a janela da loja na tela.
                Hide();
            }
            catch (Exception ex)
            {
                Feedback("Não foi possível abrir " + store.Name + ".", FeedbackKind.Error);
                WriteLog("lojas: falha ao abrir " + store.Key + " | " + ex.Message);
            }
        }

        string StoreRows()
        {
            var rows = new List<string>();
            foreach (var c in actions) { string tag = c.Tag as string ?? ""; if (tag.StartsWith("store-", StringComparison.Ordinal)) rows.Add(tag.Substring(6)); }
            return string.Join("|", rows);
        }
        static void StoresAssert(bool condition, string message) { if (!condition) throw new Exception("Lojas: " + message); }

#if XGB_VERIFY
        // Acesso rapido -> Lojas -> abrir, voltar, trilho, L1/R1, modo Xbox, erro e listas vazias. Nenhuma loja real e aberta.
        void VerifyStores()
        {
            var read = storesRead; var open = storeOpen; var mode = xboxMode; var opened = new List<string>(); bool xbox = false;
            try
            {
                stores = null;
                storesRead = delegate(bool active) { var list = ExampleStores(); if (active) list[1].Detail = "Loja no Big Picture"; return list; };
                storeOpen = delegate(GameStore s) { opened.Add(s.Key + "|" + s.Target); };
                xboxMode = delegate { return xbox; };
                returnToHome = false; SetPanel(0); Show(); window.UpdateLayout();
                StoresAssert(homeUtilities.Count == 7 && (string)homeUtilities[1].Tag == "utility-stores" && (string)homeUtilities[1].ToolTip == "Lojas" &&
                    ((TextBlock)((Button)homeUtilities[1]).Content).Text == "", "Lojas logo depois da Biblioteca no acesso rapido");
                // As pontas nao tem margem externa; os atalhos do meio tem a mesma largura e o ultimo termina dentro do painel.
                StoresAssert(homeUtilities.TrueForAll(c => c.ActualWidth > 40) && Math.Abs(homeUtilities[1].ActualWidth - homeUtilities[4].ActualWidth) < .5 &&
                    homeUtilities[6].TranslatePoint(new Point(homeUtilities[6].ActualWidth, 0), body).X <= body.ActualWidth + .5, "sete atalhos com a mesma largura dentro do painel");
                window.UpdateLayout(); SaveRender("home-acesso-lojas");
                FocusHome("utility-stores"); StoresAssert(utilitiesHint.Text == "Lojas", "nome do atalho no titulo da secao");
                Activate();
                StoresAssert(panel == StoresPanel && ((TextBlock)window.FindName("PanelTitle")).Text == "Lojas" && returnToHome && FocusedTag() == "store-xbox", "abrir Lojas pela Home");
                StoresAssert(StoreRows() == "xbox|steam|epic|ea" && BodyHas("LOJAS INSTALADAS · 4") && rail[0].Background == Brush("SurfaceSelected"), "lojas em ordem e Inicio destacado no trilho");
                var xboxRow = (Button)actions.Find(c => (string)c.Tag == "store-xbox"); var steamRow = (Button)actions.Find(c => (string)c.Tag == "store-steam");
                StoresAssert(((Border)((Grid)xboxRow.Content).Children[0]).Child is Image && ((Border)((Grid)steamRow.Content).Children[0]).Child is TextBlock, "icone da loja e simbolo de reserva");
                window.UpdateLayout(); SaveRender("lojas");
                Navigate("BAIXO"); StoresAssert(FocusedTag() == "store-steam", "navegar nas lojas");
                Activate(); StoresAssert(opened.Count == 1 && opened[0] == "steam|steam://url/StoreFrontPage" && !window.IsVisible, "abrir a loja da Steam esconde a barra");
                Show(); StoresAssert(panel == StoresPanel && rail[0].IsKeyboardFocused, "reabrir a barra continua em Lojas, com o Inicio em foco no trilho");
                Navigate("DIREITA"); StoresAssert(FocusedTag() == "store-xbox", "do trilho para a primeira loja");
                Navigate("ESQUERDA"); StoresAssert(panel == StoresPanel && rail[0].IsKeyboardFocused, "esquerda vai ao Inicio no trilho sem sair de Lojas");
                Back(); StoresAssert(panel == 0 && !returnToHome && FocusedTag() == "utility-stores", "Esc volta para a Home no atalho Lojas");
                // Erro do shell: aviso e a barra continua aberta.
                storeOpen = delegate { throw new InvalidOperationException("falha de despacho simulada"); };
                Activate(); FocusHome("store-epic"); Activate();
                StoresAssert(window.IsVisible && FeedbackText() == "Não foi possível abrir Epic Games." && FocusedTag() == "store-epic", "erro ao abrir uma loja");
                // Modo Xbox: a Steam abre a loja do Big Picture.
                xbox = true; Back(); FocusHome("utility-stores"); Activate();
                StoresAssert(ActionLabel("store-steam") == "Steam · Loja no Big Picture", "loja da Steam no modo Xbox");
                // L1/R1 pode parar na Biblioteca sem abrir a Steam nem perder Lojas.
                ChangePanel(1); StoresAssert(panel == StoresPanel && Keyboard.FocusedElement == rail[1], "R1 a partir de Lojas ancora na Biblioteca");
                ChangePanel(1); StoresAssert(panel == 6 && !returnToHome, "R1 continua da Biblioteca ao Som");
                // Leitura que falha mantem a lista anterior; sem lista, mostra que nada foi encontrado.
                storesRead = delegate { throw new InvalidOperationException("leitura simulada"); };
                SetPanel(StoresPanel); StoresAssert(StoreRows() == "xbox|steam|epic|ea", "leitura falha mantem a lista anterior");
                stores = null; SetPanel(StoresPanel); StoresAssert(StoreRows() == "" && BodyHas("Nenhuma loja de jogos encontrada neste PC."), "sem lojas");
                storesRead = delegate { return new List<GameStore>(); }; stores = null; SetPanel(StoresPanel);
                StoresAssert(StoreRows() == "" && BodyHas("LOJAS INSTALADAS · 0"), "lista vazia");
                System.IO.File.WriteAllText(System.IO.Path.Combine(Output, "lojas-verificacao.txt"),
                    "OK: Lojas no acesso rapido logo depois da Biblioteca (icone, nome no titulo da secao, seis atalhos dentro do painel, os do meio com a mesma largura); painel Lojas pela Home com Inicio destacado no trilho, " +
                    "lojas em ordem, icone e simbolo de reserva; abrir pelo shell esconde a barra, reabrir continua em Lojas, trilho sem sair do painel, Esc volta ao atalho; erro do shell avisa; " +
                    "Steam abre a loja do Big Picture no modo Xbox; R1 a partir de Lojas; leitura que falha mantem a lista, sem lista e lista vazia. Lojas substituidas; nada real aberto.\r\n", System.Text.Encoding.UTF8);
            }
            finally
            {
                storesRead = read; storeOpen = open; xboxMode = mode; stores = null; returnToHome = false; SetPanel(0);
            }
        }
#endif

#if XGB_VERIFY
        // Diagnostico real so de leitura: lojas detectadas e icones; nada e aberto. Caminhos e a tela ficam so em bin.
        public void VerifyStoresLive()
        {
            var found = GameStores.Read(xboxMode() == true);
            var lines = new List<string> { "lojas encontradas: " + found.Count };
            foreach (var s in found)
                lines.Add("  " + s.Key + " | " + (s.Target.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase) ? "pacote" : s.Target.Contains("://") ? "protocolo" : "executavel") +
                    " | icone " + (s.IconPng == null ? "ausente (simbolo de reserva)" : s.IconPng.Length + " bytes"));
            lines.Add("Nenhuma loja aberta.");
            storesRead = delegate { return found; };
            window.Width = 1920; window.Height = 1080; window.Left = -30000; window.Top = -30000; window.Show();
            stores = found; SetPanel(StoresPanel); window.UpdateLayout(); SaveRender("lojas-reais");
            SetPanel(0); window.Hide();
            System.IO.File.WriteAllLines(System.IO.Path.Combine(Output, "lojas-reais.txt"), lines.ToArray(), System.Text.Encoding.UTF8);
            WriteLog("lojas: leitura real concluida, nada aberto");
        }
#endif
    }
}

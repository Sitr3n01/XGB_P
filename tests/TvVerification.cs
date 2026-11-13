// TV Samsung (ADR 0110, fase 2) com leituras substituidas: nenhuma tela, rede, arquivo do Orchestrator ou processo real
// e consultado. O unico Probe chamado usa um IP publico e precisa parar antes de qualquer conexao.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Controls;
using XGBP.Tv;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyTv()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool ok, string why) { checks++; if (!ok) throw new Exception("TV Samsung: " + why); };
            const string mac = "AA:BB:CC:DD:EE:FF", duid = "uuid:11111111-2222-3333-4444-555555555555", ip = "192.168.55.40";
            const string fragment = "DISPLAY#SAM1234#7&TESTE&0&UID101";
            const string tvPath = @"\\?\DISPLAY#SAM1235#7&TESTE&0&UID101#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
            const string aocPath = @"\\?\DISPLAY#AOC2503#5&1a2b3c4d&0&UID256#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";

            // Identificacao pela instancia do conector: o codigo de produto do EDID muda, o conector nao.
            check(TvDisplay.DecodeEdidVendor(0x2D4C) == "SAM" && TvDisplay.DecodeEdidVendor(0) == "", "fabricante do EDID");
            check(TvDisplay.Matches(fragment, tvPath) && TvDisplay.Matches(fragment, @"DISPLAY\SAM1235\7&TESTE&0&UID101"),
                "1234 configurado reconhece 1235 no mesmo conector");
            check(!TvDisplay.Matches(fragment, @"\\?\DISPLAY#SAM1235#7&TESTE&0&UID102#{x}") && !TvDisplay.Matches(fragment, aocPath) &&
                !TvDisplay.Matches("", tvPath) && !TvDisplay.Matches(fragment, null), "outro conector, outro fabricante ou vazio");
            Func<string, string, string, uint, uint, bool, TvDisplayTarget> target = delegate(string path, string vendor, string tech, uint id,
                uint source, bool
                active) {
                return new TvDisplayTarget { DevicePath = path, EdidVendor = vendor, EdidProductCode = 0x1235, OutputTechnology = tech,
                    AdapterLow = 7, TargetId = id,
                    SourceId = source, Active = active, Available = true, Width = 3840, Height = 2160, RefreshHz = 60 }; };
            var tvOnly = TvDisplay.Classify(new List<TvDisplayTarget> { target(tvPath, "SAM", "HDMI", 268, 0, true), target(aocPath, "AOC",
                "DisplayPort", 256, 1,
                false) }, fragment);
            check(tvOnly.Topology == TvTopology.ExternalOnly && tvOnly.SamsungActive && !tvOnly.MatchedByEdid && tvOnly.ActiveCount == 1 && tvOnly.Valid,
                "so a TV ativa");
            var extend = TvDisplay.Classify(new List<TvDisplayTarget> { target(tvPath, "SAM", "HDMI", 268, 0, true), target(aocPath, "AOC",
                "DisplayPort", 256, 1,
                true) }, fragment);
            var clone = TvDisplay.Classify(new List<TvDisplayTarget> { target(tvPath, "SAM", "HDMI", 268, 0, true), target(aocPath, "AOC",
                "DisplayPort", 256, 0,
                true) }, fragment);
            check(extend.Topology == TvTopology.Extend && clone.Topology == TvTopology.Clone, "estender e duplicar pela origem");
            var pcOnly = TvDisplay.Classify(new List<TvDisplayTarget> { target(tvPath, "SAM", "HDMI", 268, 0, false), target(aocPath, "AOC",
                "DisplayPort", 256, 1,
                true) }, fragment);
            check(pcOnly.Topology == TvTopology.PcScreenOnly && pcOnly.SamsungFound && !pcOnly.SamsungActive, "TV inativa com o monitor ativo");
            var none = TvDisplay.Classify(new List<TvDisplayTarget> { target(tvPath, "SAM", "HDMI", 268, 0, false) }, fragment);
            check(none.Topology == TvTopology.NoActiveDisplay, "nenhuma tela ativa");
            var otherPort = TvDisplay.Classify(new List<TvDisplayTarget> { target(@"\\?\DISPLAY#SAM1235#7&TESTE&0&UID102#{x}", "SAM", "HDMI", 270, 0, true),
                target(aocPath, "AOC", "DisplayPort", 256, 1, true) }, fragment);
            check(!otherPort.SamsungFound && otherPort.Topology == TvTopology.PcScreenOnly, "Samsung em outro conector nao e a TV configurada");
            var paths = TvDisplay.Classify(new List<TvDisplayTarget> { target(tvPath, "SAM", "HDMI", 268, 1, false), target(tvPath, "SAM",
                "HDMI", 268, 0, true) },
                fragment);
            check(paths.SamsungActive && paths.Samsung.SourceId == 0 && paths.Topology == TvTopology.ExternalOnly,
                "varios caminhos do mesmo alvo contam uma vez, o ativo");
            var byEdid = TvDisplay.Classify(new List<TvDisplayTarget> { target(tvPath, "SAM", "HDMI", 268, 0, true), target(aocPath, "AOC",
                "DisplayPort", 256, 1,
                true) }, "");
            var ambiguous = TvDisplay.Classify(new List<TvDisplayTarget> { target(tvPath, "SAM", "HDMI", 268, 0, true), target(aocPath, "SAM",
                "HDMI", 256, 1, true)
                }, "");
            check(byEdid.MatchedByEdid && byEdid.Topology == TvTopology.Extend && ambiguous.Ambiguous && !ambiguous.Valid,
                "sem conector: uma Samsung HDMI; duas e ambiguo");
            check(!tvOnly.Describe().Contains("UID101") && tvOnly.Describe().Contains("SAM1235"), "descricao sem caminho do dispositivo");

            // Configuracao importada (v2 e formato de 08/09) e formato da credencial, sem decifrar nada.
            string config = "{\"SchemaVersion\":2,\"Enabled\":false,\"Tv\":{\"Model\":\"ModeloTeste\",\"Mac\":\"" + mac + "\",\"LastKnownIp\":\"" + ip +
                "\",\"Transport\":\"WiFi\"," +
                "\"MonitorDevicePathFragment\":\"" + fragment + "\",\"Duid\":\"" + duid + "\",\"PowerOffKey\":\"KEY_POWER\",\"PowerOffKeyValidated\":false}," +
            "\"Behavior\":{\"Extend\":1,\"External\":1,\"Clone\":1,\"PcOnly\":0,\"Suspend\":2,\"Resume\":0,\"Dual"
                +"Sense\":1},\"DualSenseDisplayBehavior\":0," +
                "\"Reliability\":{\"ProbeBackoffSeconds\":[2,4,7],\"WakeRetryEnabled\":true},\"Display\":{\"KeepSamsungHdrOff\":true}}";
            string certificate = new string('A', 64);
            string credential = "{\"SchemaVersion\":2,\"Ciphertext\":\"AQAAANCMnd8B\",\"CertificateSha256\":\"" + certificate + "\",\"Mac\":\"" + mac +
                "\",\"Duid\":\"" + duid +
                "\",\"ObtainedAt\":\"2026-10-12T16:29:58.2268901+00:00\"}";
            var setup = TvSetup.Parse(config, credential);
            check(setup.Found && setup.Schema == 2 && setup.Enabled == false && setup.Model == "ModeloTeste" && setup.HasTarget && setup.Ip == ip
                && setup.Fragment ==
                fragment &&
                setup.KeepHdrOff && setup.Extend == 1 && setup.PcOnly == 0 && setup.Suspend == 2 && setup.DualSense == 1 && setup.Credential ==
                    TvCredential.Protected,
                    "configuracao v2 completa");
            var old = TvSetup.Parse("{\"Tv\":{\"Model\":\"ModeloTeste\",\"Mac\":\"" + mac + "\",\"Duid\":\"" + duid +
                "\"},\"Display\":{\"KeepSamsungHdrOff\":true}}",
                "{\"Token\":\"12345678\",\"ObtainedAt\":\"2026-09-08T20:16:10-03:00\"}");
            check(old.Found && old.Schema == 0 && old.Enabled == null && old.Credential == TvCredential.Legacy && old.Extend == -1,
                "formato de 08/09 e token antigo"
                );
            check(TvSetup.Parse(config, credential.Replace(mac, "11:22:33:44:55:66")).Credential == TvCredential.OtherTv &&
                TvSetup.Parse(config, "{").Credential == TvCredential.Invalid && TvSetup.Parse(config, null).Credential == TvCredential.Missing &&
                TvSetup.Parse(config, credential.Replace(certificate, "ABC")).Credential == TvCredential.Invalid,
                    "credencial de outra TV, ilegivel, ausente ou sem certificado");
            check(!TvSetup.Parse(null, null).Found && TvSetup.Parse(null, null).Error == "ausente" && TvSetup.Parse("{x", null).Error == "ilegivel" &&
                TvSetup.Parse("{\"SchemaVersion\":2}", null).Error == "ilegivel", "configuracao ausente, ilegivel ou sem a TV");
            string described = setup.Describe();
            check(!described.Contains(mac) && !described.Contains(duid) && !described.Contains(ip) && !described.Contains(certificate),
                "descricao sem MAC, DUID, IP ou certificado");

            // Rede: so IP privado; a identidade precisa ter o mesmo MAC e DUID; nada afirma a energia.
check(TvProbe.IsPrivateIPv4(ip) && TvProbe.IsPrivateIPv4("10.0.0.1") && TvProbe.IsPrivateIPv4("172.20.1.1") && !TvProbe.IsPrivateIPv4("172.32.0.1") &&
                !TvProbe.IsPrivateIPv4("8.8.8.8") && !TvProbe.IsPrivateIPv4("fe80::1") && !TvProbe.IsPrivateIPv4("") && !TvProbe.IsPrivateIPv4(null),
                    "somente IPv4 privado");
            check(TvProbe.ParseMac("aa-bb-cc-dd-ee-ff") != null && TvProbe.ParseMac("AA:BB:CC:DD:EE") == null &&
                TvProbe.ParseMac("GG:BB:CC:DD:EE:FF") == null, "MAC"
                );
            string api = "{\"device\":{\"FrameTVSupport\":\"false\",\"duid\":\"" + duid + "\",\"modelName\":\"ModeloTeste\",\"model\":\"MODELO_TESTE\"," +
                "\"networkType\":\"wireless\",\"wifiMac\":\"aa:bb:cc:dd:ee:ff\",\"name\":\"[TV] Samsung\"},\"id\":\"" + duid + "\",\"version\":\"2.0.25\"}";
            var reach = TvProbe.Evaluate(true, api, mac, duid);
            check(reach.Status == TvNetwork.Reachable && reach.ModelName == "ModeloTeste" && reach.NetworkType == "wireless" && reach.ReportedPower == null,
                "TV identificada, sem energia informada");
            check(TvProbe.Evaluate(true, api.Replace("\"networkType\"", "\"PowerState\":\"on\",\"networkType\""), mac, duid).ReportedPower == "on",
                "modelo que informa a energia");
            check(TvProbe.Evaluate(false, api, mac, duid).Status == TvNetwork.Unreachable && TvProbe.Evaluate(true, null, mac, duid).Status ==
                TvNetwork.QueryFailed
                &&
                TvProbe.Evaluate(true, "{\"device\":{}}", mac, duid).Status == TvNetwork.QueryFailed, "sem porta ou sem identidade");
            check(TvProbe.Evaluate(true, api, "11:22:33:44:55:66", duid).Status == TvNetwork.IdentityMismatch &&
                TvProbe.Evaluate(true, api, mac, "uuid:outro").Status == TvNetwork.IdentityMismatch && TvProbe.Evaluate(true, api, mac, "").Status ==
                    TvNetwork.IdentityMismatch,
                "MAC ou DUID diferentes, ou DUID ausente, nao correspondem");
            var outside = TvProbe.Probe("8.8.8.8", mac, duid, 1000, 2000);
            check(outside.Status == TvNetwork.NotConfigured && outside.PortMs < 0, "IP publico nunca e contatado");

            // Configuracoes: TELA -> TV Samsung, leitura em worker substituida.
            var oldRead = tvRead; var oldState = tvState; var oldAt = tvReadAt;
            Func<TvStatus> make = delegate {
                var s = new TvStatus { Setup = TvSetup.Parse(config, credential), Display = tvOnly, Network = reach, OrchestratorRegistered =
                    true, OrchestratorStartup =
                    false };
                s.Display.HdrSupported = true; s.Display.HdrEnabled = false; return s; };
            var fixture = make(); int reads = 0;
            try
            {
                tvRead = delegate { reads++; return fixture; };
                tvState = null; tvReadAt = DateTime.MinValue;
                Show(); SetPanel(5);
                check(reads == 1 && ActionLabel("page-tv") == "TV Samsung ModeloTeste · Na rede", "Configuracoes leem ao abrir: " + ActionLabel("page-tv"));
                check(!BodyHas("Energia não confirmada: a rede responde igual com a tela acesa ou em standby."), "pagina principal so com a linha");
                FocusHome("page-tv"); Activate();
                check(settingsPage == "tv" && ((TextBlock)window.FindName("PanelTitle")).Text == "TV Samsung" && reads == 1 &&
                    ActionLabel("tv-read") == "TV Samsung ModeloTeste · Na rede", "pagina da TV sem nova leitura");
                check(BodyHas("Tela: única tela ativa (HDMI, 3840×2160 a 60 Hz) · HDR desligado.") &&
                    BodyHas("Energia não confirmada: a rede responde igual com a tela acesa ou em standby.") &&
                    BodyHas("Samsung TV Orchestrator parado, com a inicialização desativada. " +
                        "O Orchestrator mantinha o HDR da Samsung desligado."), "tela, energia nao confirmada e Orchestrator");
                window.UpdateLayout(); SaveRender("configuracoes-tv");
                Back(); check(settingsPage == "main" && FocusedTag() == "page-tv", "voltar para a linha da TV");
                SetPanel(5); check(reads == 1, "leitura recente nao repete");
                FocusHome("page-tv"); Activate();
                fixture = make(); fixture.Network = TvProbe.Evaluate(false, null, mac, duid);
                FocusHome("tv-read"); Activate();
                check(reads == 2 && ActionLabel("tv-read") == "TV Samsung ModeloTeste · Fora da rede" && FocusedTag() == "tv-read" &&
            BodyHas("Sem resposta na rede: a TV pode estar em sono profundo (apagada há mais de 2 min) ou desconectada."),
                "apertar le de novo e mantem o foco");
                var rows = new Dictionary<string, TvNetworkState> {
                { "Outro aparelho no endereço", TvProbe.Evaluate(true, api, mac, "uuid:outro") }, { "Sem identidade", TvProbe.Evaluate(true, null, mac, duid) },
                    { "Sem endereço", new TvNetworkState { Status = TvNetwork.NotConfigured } } };
                foreach (var r in rows)
                {
                    fixture = make(); fixture.Network = r.Value; FocusHome("tv-read"); Activate();
                    check(ActionLabel("tv-read") == "TV Samsung ModeloTeste · " + r.Key, "estado da rede " + r.Key);
                }
                check(BodyHas("A configuração não tem o endereço da TV."), "sem endereco explicado");
                fixture = make(); fixture.Network = TvProbe.Evaluate(true, api.Replace("\"networkType\"", "\"PowerState\":\"off\",\"networkType\""), mac, duid);
                FocusHome("tv-read"); Activate();
                check(BodyHas("Energia não confirmada: a rede responde igual com a tela acesa ou em standby. A TV informa que está apagada."),
                    "energia informada pela TV"
                    );
                fixture = make(); fixture.Display = extend; fixture.OrchestratorRunning = true; fixture.Setup.Credential = TvCredential.Legacy;
                FocusHome("tv-read"); Activate();
                check(BodyHas("Tela: estendendo outra tela (HDMI, 3840×2160 a 60 Hz).") && BodyHas(
                    "Samsung TV Orchestrator em execução: ele controla a TV. Encerre-o para usar os comandos da barra." +
                    " A autorização salva está no formato antigo: será preciso autorizar de novo na TV. O Orchestrator mantinha o HDR da Samsung desligado."),
                    "estendida, Orchestrator em execucao e token antigo");
                var displays = new Dictionary<string, TvDisplayState> {
                    { "Tela: conectada, mas inativa (só a tela do PC).", pcOnly }, { "Tela: a Samsung não aparece nas saídas de vídeo.", otherPort },
                    { "Tela: mais de uma Samsung corresponde; a TV não é identificada.", ambiguous },
                    { "Tela: não foi possível ler as saídas de vídeo (CCD consulta rc=31).", new TvDisplayState { Error = "CCD consulta rc=31" } } };
                foreach (var d in displays)
                {
                    fixture = make(); fixture.Display = d.Value; FocusHome("tv-read"); Activate();
                    check(BodyHas(d.Key), "tela: " + d.Key);
                }
                fixture = new TvStatus { Setup = TvSetup.Parse(null, null) }; FocusHome("tv-read"); Activate();
                check(ActionLabel("tv-read") == "TV Samsung · Não configurada" &&
                    BodyHas("Nenhuma configuração do Samsung TV Orchestrator neste usuário. Sem ela, a barra não tem o endereço n" +"em a identidade da TV."),
                        "sem configuracao");
                fixture = new TvStatus { Setup = TvSetup.Parse("{x", null) }; FocusHome("tv-read"); Activate();
                check(ActionLabel("tv-read") == "TV Samsung · Configuração ilegível", "configuracao ilegivel");
                fixture = new TvStatus { Error = "falha de teste" }; FocusHome("tv-read"); Activate();
            check(ActionLabel("tv-read") == "TV Samsung · Indisponível" && BodyHas("Não foi possível ler a TV: falha de teste."), "erro de leitura visivel");
                fixture = make();
                string text = fixture.Describe();
                check(!text.Contains(mac) && !text.Contains(duid) && !text.Contains(ip) && !text.Contains("UID101"), "registro sem MAC, DUID, IP ou conector");
                File.WriteAllText(Path.Combine(Output, "tv-verificacao.txt"), "OK: " + checks + " verificacoes da TV Samsung (ADR 0110, fase 2). " +
"Fabricante do EDID; 1234 reconhece 1235 no mesmo conector; outro conector/fabricante; so a TV, esten" +"der, duplicar, TV inativa, nenhuma tela, " +
"caminhos repetidos, sem conector (uma Samsung HDMI e ambiguidade); configuracao v2 e de 08/09; crede" +"ncial protegida, antiga, de outra TV, " +
"ilegivel, ausente e sem certificado; IPv4 privado, MAC, identidade com MAC/DUID, energia informada, " +"IP publico nao contatado; Configuracoes " +
                    "mostram uma linha, abrem a pagina da TV, voltam para a linha, nao repetem leitura recente, releem pe" +
                        "la linha mantendo o foco, estados de rede e tela, Orchestrator, erro; descricoes sem " +
                    "MAC/DUID/IP. Leituras substituidas; nada consultado ou enviado.\r\n", Encoding.UTF8);
            }
            finally
            {
                tvRead = oldRead; tvState = oldState; tvReadAt = oldAt; settingsPage = "main";
                SetPanel(0);
                if (window.IsVisible) Hide();
            }
        }
    }
}

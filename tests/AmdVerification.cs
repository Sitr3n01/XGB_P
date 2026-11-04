// Regressao da aba AMD (ADR 0081). ReLive, pasta, microfone, relogio e abertura substituidos: nenhum comando real e enviado.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;
using XGBP.Amd;
using XGBP.Audio;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyAmd()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool ok, string reason) { checks++; if (!ok) throw new Exception("AMD: " + reason); };
            var oldRead = amdRead; var oldSend = amdSend; var oldRecent = amdRecent; var oldThumb = amdThumb; var oldMic = micRead; var oldMicSet = micSet;
            var oldOpen = amdOpen; var oldElevated = amdElevated; var oldNow = amdNow;
            DateTime now = new DateTime(2026, 10, 29, 14, 0, 0);
            var st = new ReliveState { Installed = true, Running = true, Enabled = true, InstantReplay = true, ReplaySeconds = 225, Desktop = true, Microphone = true,
                OutputFolder = @"C:\Videos\Radeon ReLive", InstallFolder = @"C:\Program Files\AMD\CNext\CNext", Version = "26.20.15.07" };
            var files = new List<ReliveCapture> {
                new ReliveCapture { Path = @"C:\Videos\Radeon ReLive\Flight\Flight_replay_1.mp4", Game = "Microsoft Flight Simulator 2024", Video = true, Replay = true, Modified = now.AddDays(-7), Bytes = 500, Length = TimeSpan.FromSeconds(225) },
                new ReliveCapture { Path = @"C:\Videos\Radeon ReLive\Waves\Waves_1.png", Game = "Wuthering Waves", Modified = now.AddDays(-40), Bytes = 9 } };
            var mic = new MicrophoneState { Id = "mic-1", Name = "Microfone (Realtek USB Audio)", Volume = 72 };
            var sent = new List<string>(); var opened = new List<string>(); var micCalls = new List<string>();
            bool elevated = true, micPresent = true; Exception failNext = null;
            try
            {
                amdNow = delegate { return now; }; amdElevated = delegate { return elevated; }; amdThumb = delegate { return null; };
                amdRead = delegate { return new ReliveState { Installed = st.Installed, Running = st.Running, Enabled = st.Enabled, InstantReplay = st.InstantReplay, ReplaySeconds = st.ReplaySeconds,
                    Desktop = st.Desktop, Microphone = st.Microphone, Camera = st.Camera, InstantGif = st.InstantGif, InGameReplay = st.InGameReplay, OutputFolder = st.OutputFolder, InstallFolder = st.InstallFolder }; };
                amdRecent = delegate(string folder, int count) {
                    var list = files.ConvertAll(f => new ReliveCapture { Path = f.Path, Game = f.Game, Video = f.Video, Replay = f.Replay, Modified = f.Modified, Created = f.Created, Bytes = f.Bytes, Length = f.Length });
                    list.Sort((a, b) => b.Modified.CompareTo(a.Modified)); return list.GetRange(0, Math.Min(count, list.Count)); };
                micRead = delegate { return micPresent ? new MicrophoneState { Id = mic.Id, Name = mic.Name, Volume = mic.Volume, Muted = mic.Muted } : null; };
                micSet = delegate(string id, int? volume, bool? muted) { micCalls.Add(id + ":" + volume + ":" + muted); if (volume.HasValue) mic.Volume = volume.Value; if (muted.HasValue) mic.Muted = muted.Value; };
                amdSend = delegate(string command) { if (failNext != null) { var f = failNext; failNext = null; throw f; } sent.Add(command); };
                amdOpen = delegate(string target, bool maximize) { opened.Add(target); };
                Action read = delegate { amdNextRead = DateTime.MinValue; AmdTick(); };
                Func<string, Control> find = delegate(string tag) { return actions.Find(c => (string)c.Tag == tag); };
                Action<string> press = delegate(string tag) { FocusHome(tag); Activate(); };
                Action reopen = delegate { Show(); SetPanel(AmdPanel); };

                // Contrato do backend: lista de comandos e leitura do MP4 sem tocar no AMD Software.
                check(RadeonRelive.IsAllowed("save_ir") && RadeonRelive.IsAllowed("toggle_rs_perf_rec") && !RadeonRelive.IsAllowed("exit") &&
                    !RadeonRelive.IsAllowed("set_file_output_path") && !RadeonRelive.IsAllowed("start_streaming") && !RadeonRelive.IsAllowed("crashdump"), "so comandos de acao permitidos");
                check(Mp4(0, 1000, 225000) == TimeSpan.FromSeconds(225) && Mp4(1, 600, 600 * 90) == TimeSpan.FromSeconds(90), "duracao do mvhd v0 e v1");
                check(RadeonRelive.Mp4Duration(new MemoryStream(Box("ftyp", new byte[8]))) == null && RadeonRelive.Mp4Duration(new MemoryStream(new byte[5])) == null, "sem moov ou truncado: sem duracao");
                VerifyAmdFolder(check, now);
                check(AmdGameName("steamappid2147483647") == "Jogo da Steam" && AmdGameName("Wuthering Waves") == "Wuthering Waves", "pasta steamappid sem catalogo vira Jogo da Steam");

                // Antes da primeira leitura: estado lendo e captura desligada.
                amdRelive = null; amdMic = null; amdCaptures = new List<ReliveCapture>(); amdRecording = false; amdCapture = amdToggle = null;
                reopen();
                check(amdStatusText.Text == "Lendo o AMD Software…" && !find("amd-shot").IsEnabled && !find("amd-record").IsEnabled, "lendo: captura desligada");
                read();
                check(amdRelive != null && amdStatusText.Text == "Repetição instantânea ativa · últimos 3:45" && find("amd-replay").IsEnabled && amdCaptures.Count == 2 && amdMic != null, "leitura preenche o painel");
                check(!actions.Exists(c => ((string)c.Tag ?? "").Contains("metric")) && find("amd-gif") == null && find("amd-ingame") == null, "metricas da AMD, GIF e repeticao no jogo fora da tela");
                window.UpdateLayout(); SaveRender("amd-painel");

                // Navegacao por linhas.
                FocusHome("amd-replay"); Navigate("DIREITA"); check(FocusedTag() == "amd-shot", "direita dentro da linha");
                Navigate("BAIXO"); check(FocusedTag() == "amd-toggle-replay", "baixo para a proxima linha");
                FocusHome("amd-replay"); Navigate("ESQUERDA"); check(Keyboard.FocusedElement == rail[AmdPanel], "esquerda da primeira coluna volta ao trilho");

                // Foto: barra oculta antes do comando; confirmada pelo arquivo novo.
                reopen(); press("amd-shot");
                check(!window.IsVisible && sent.Count == 1 && sent[0] == "still_cap" && amdCapture != null && amdCapture.Notice.Kind == FeedbackKind.Pending, "foto oculta a barra e envia still_cap");
                files.Add(new ReliveCapture { Path = @"C:\Videos\Radeon ReLive\Waves\Waves_2.png", Game = "Wuthering Waves", Modified = now, Bytes = 10 }); read();
                check(amdCapture == null && LatestHistoryText() == "Captura de tela salva · Wuthering Waves.", "arquivo novo confirma a foto");
                reopen(); press("amd-shot"); press("amd-shot");
                check(sent.Count == 2, "segunda captura com a primeira pendente nao envia");

                // Repeticao sem arquivo: aviso apos o prazo; sem elevacao, a causa provavel.
                now = now.AddSeconds(11); read(); amdCapture = null;
                reopen(); press("amd-replay"); check(sent[sent.Count - 1] == "save_ir", "salvar repeticao envia save_ir");
                now = now.AddSeconds(26); read();
                check(amdCapture == null && LatestHistory().Type == FeedbackKind.Warning && LatestHistoryText().StartsWith("O AMD Software não confirmou a captura"), "sem arquivo novo: aviso de verificacao");
                elevated = false; reopen(); press("amd-replay"); now = now.AddSeconds(26); read();
                check(LatestHistory().Type == FeedbackKind.Attention && LatestHistoryText().Contains("Sempre executar como administrador"), "sem elevacao: pede administrador");
                elevated = true;
                files.Add(new ReliveCapture { Path = @"C:\Videos\Radeon ReLive\Flight\Flight_replay_2.mp4", Game = "Microsoft Flight Simulator 2024", Video = true, Replay = true, Modified = now, Bytes = 400, Length = TimeSpan.FromSeconds(225) });
                reopen(); press("amd-replay"); files[files.Count - 1].Modified = now.AddSeconds(1); files.Add(new ReliveCapture { Path = @"C:\Videos\Radeon ReLive\Flight\Flight_replay_3.mp4",
                    Game = "Microsoft Flight Simulator 2024", Video = true, Replay = true, Modified = now.AddSeconds(2), Bytes = 300 });
                now = now.AddSeconds(3); read();
                check(LatestHistoryText() == "Repetição salva · Microsoft Flight Simulator 2024." && !amdRecording, "repeticao salva confirmada e nao vira gravacao");

                // Gravacao pela barra: inicia, confirma pelo video crescendo, cronometro, para e confirma o fim.
                now = now.AddSeconds(10); reopen(); press("amd-record");
                check(sent[sent.Count - 1] == "capture_to_file_start" && amdRecording, "gravar envia o inicio e assume gravacao");
                var rec = new ReliveCapture { Path = @"C:\Videos\Radeon ReLive\Flight\Flight_1.mp4", Game = "Microsoft Flight Simulator 2024", Video = true, Modified = now, Created = now, Bytes = 1000 };
                files.Add(rec); now = now.AddSeconds(1); read(); rec.Bytes = 5000; rec.Modified = now; now = now.AddSeconds(1); read();
                check(amdCapture == null && amdRecording && LatestHistoryText() == "Gravando · Microsoft Flight Simulator 2024.", "video crescendo confirma a gravacao");
                now = now.AddSeconds(129); reopen();
                check(ActionLabel("amd-record") == "Parar" && amdStatusText.Text == "Gravando · 2:11", "botao de parar acessivel e cronometro na linha de estado");
                window.UpdateLayout(); SaveRender("amd-gravando");
                rec.Bytes = 9000; rec.Modified = now; read(); press("amd-record");
                check(sent[sent.Count - 1] == "capture_to_file_stop" && !amdRecording, "parar envia o fim");
                now = now.AddSeconds(3); read();
                check(amdCapture == null && !amdRecording && LatestHistoryText() == "Gravação salva · Microsoft Flight Simulator 2024.", "video parado confirma o fim");

                // Gravacao iniciada fora da barra (atalho do Adrenalin) e terminada fora dela.
                var outside = new ReliveCapture { Path = @"C:\Videos\Radeon ReLive\Waves\Waves_1.mp4", Game = "Wuthering Waves", Video = true, Modified = now, Created = now, Bytes = 10 };
                reopen(); files.Add(outside); now = now.AddSeconds(1); read(); outside.Bytes = 99; outside.Modified = now; now = now.AddSeconds(1); read();
                check(amdRecording, "video crescendo de fora vira gravacao");
                now = now.AddSeconds(5); read(); check(!amdRecording, "parou de crescer: gravacao terminou");

                // Interruptores: confirmados pelo registro; sem mudanca, aviso.
                reopen(); press("amd-toggle-replay");
                check(sent[sent.Count - 1] == "toggle_ir" && ActionLabel("amd-toggle-replay").EndsWith("Aplicando…") && !find("amd-toggle-desktop").IsEnabled, "interruptor envia e mostra aplicando");
                st.InstantReplay = false; read();
                check(amdToggle == null && LatestHistoryText() == "Repetição instantânea: desligada." && ActionLabel("amd-toggle-replay").EndsWith("Desativado") && !find("amd-replay").IsEnabled,
                    "registro confirma e a repeticao sai da captura");
                press("amd-toggle-desktop"); now = now.AddSeconds(5); read();
                check(amdToggle == null && LatestHistoryText() == "O AMD Software não confirmou: Gravar a área de trabalho.", "interruptor sem efeito avisa");
                st.InstantReplay = true; read();

                // Falhas do comando.
                failNext = new InvalidOperationException("O Radeon ReLive não está ativo. Ative Gravar e transmitir no AMD Software."); reopen(); press("amd-shot");
                check(amdCapture == null && LatestHistory().Type == FeedbackKind.Attention, "ReLive desligado no envio pede acao");
                failNext = new TimeoutException("O AMD Software não respondeu ao comando."); reopen(); press("amd-shot");
                check(amdCapture == null && LatestHistory().Type == FeedbackKind.Error, "falha do comando e erro");
                st.Running = false; reopen(); read(); int before = sent.Count; AmdCaptureAction("amd-shot");
                check(!find("amd-shot").IsEnabled && sent.Count == before && LatestHistory().Type == FeedbackKind.Attention && BodyHas("Ative Gravar e transmitir no AMD Software para usar a captura."), "ReLive desligado: nada enviado");
                st.Running = true; st.Installed = false; read();
                check(BodyHas("O AMD Software não foi encontrado neste PC. O microfone do Windows continua disponível.") && find("amd-shot") == null && find("amd-mic-mute") != null, "sem AMD Software: so o microfone");
                st.Installed = true;

                // GIF e repeticao no jogo so quando ativados.
                st.InstantGif = true; st.InGameReplay = true; read();
                check(find("amd-gif") != null && find("amd-ingame") != null, "GIF e repeticao no jogo aparecem ativados");
                window.UpdateLayout(); SaveRender("amd-captura-completa");
                press("amd-gif"); files.Add(new ReliveCapture { Path = @"C:\Videos\Radeon ReLive\Waves\Waves_1.gif", Game = "Wuthering Waves", Modified = now.AddSeconds(1), Bytes = 7 });
                now = now.AddSeconds(2); read(); check(sent[sent.Count - 1] == "save_instant_gif" && LatestHistoryText() == "GIF salvo · Wuthering Waves.", "GIF confirmado pelo arquivo");
                reopen(); press("amd-ingame"); check(sent[sent.Count - 1] == "save_ingame_replay" && amdCapture == null && LatestHistory().Type == FeedbackKind.Info, "repeticao no jogo termina na entrega");
                st.InstantGif = st.InGameReplay = false;

                // Microfone do Windows.
                reopen(); read(); press("amd-mic-mute");
                check(micCalls[micCalls.Count - 1] == "mic-1::True" && amdMic.Muted, "mudo pelo botao");
                read(); var slider = (Slider)find("amd-mic-volume"); slider.Value = 40;
                check(micCalls[micCalls.Count - 1] == "mic-1:40:" && mic.Volume == 40, "volume pelo controle deslizante");
                micPresent = false; read(); check(BodyHas("Nenhum microfone ativo no Windows."), "sem microfone avisa");
                micPresent = true;

                // Abrir capturas, pasta e AMD Software ocultam a barra.
                reopen(); read(); press("amd-capture-0"); check(opened.Count == 1 && opened[0] == files.Find(f => f.Path.EndsWith(".gif")).Path && !window.IsVisible, "abrir captura");
                reopen(); press("amd-folder"); check(opened[1] == st.OutputFolder && !window.IsVisible, "abrir pasta");
                reopen(); press("amd-software"); check(opened[2] == st.InstallFolder + @"\RadeonSoftware.exe", "abrir o AMD Software");

                // Metricas: so logica, comandos existentes.
                reopen(); SendAmdMetrics(true); SendAmdMetrics(false);
                check(sent[sent.Count - 2] == "toggle_in_content_rs_perf_from_rsx" && sent[sent.Count - 1] == "toggle_rs_perf_rec", "metricas e registro de desempenho pela logica");

                File.WriteAllText(Path.Combine(Output, "amd-verificacao.txt"), "OK: " + checks + " verificacoes. Comandos permitidos, duracao MP4 (v0/v1, sem moov, truncado), pasta por jogo, " +
                    "estado lendo, painel preenchido, navegacao por linhas, foto/repeticao/GIF com a barra oculta e confirmacao pelo arquivo, aviso sem arquivo e sem elevacao, " +
                    "gravacao iniciada/confirmada/cronometro/parada, gravacao de fora detectada e encerrada, interruptores confirmados pelo registro e sem efeito, falhas, ReLive desligado, " +
                    "sem AMD Software, microfone mudo/volume/ausente, abrir captura/pasta/AMD Software e metricas so na logica. ReLive, pasta, microfone e relogio substituidos; nada real.\r\n", Encoding.UTF8);
            }
            finally
            {
                amdRead = oldRead; amdSend = oldSend; amdRecent = oldRecent; amdThumb = oldThumb; micRead = oldMic; micSet = oldMicSet; amdOpen = oldOpen; amdElevated = oldElevated; amdNow = oldNow;
                amdRelive = null; amdMic = null; amdCaptures = new List<ReliveCapture>(); amdRecording = false; amdCapture = amdToggle = null; amdGrowPath = null; amdGrowSeen = amdRecordingSince = DateTime.MinValue;
                SetPanel(0);
            }
        }
        // Pasta real em miniatura dentro da saida da verificacao: ordem, jogo pela subpasta, raiz como area de trabalho e limite.
        void VerifyAmdFolder(Action<bool, string> check, DateTime now)
        {
            string root = Path.Combine(Output, "amd-fixture");
            if (Directory.Exists(root)) Directory.Delete(root, true);
            Directory.CreateDirectory(Path.Combine(root, "Jogo A")); Directory.CreateDirectory(Path.Combine(root, "Jogo B"));
            Action<string, int> write = delegate(string name, int minutes) { string p = Path.Combine(root, name); File.WriteAllBytes(p, new byte[] { 1, 2, 3 }); File.SetLastWriteTime(p, now.AddMinutes(minutes)); };
            write(@"Jogo A\Jogo A_replay_1.mp4", -30); write(@"Jogo B\Jogo B_1.png", -10); write(@"Jogo B\notas.txt", 0); write("Desktop_1.mp4", -20);
            var list = RadeonRelive.Recent(root, 2);
            check(list.Count == 2 && list[0].Game == "Jogo B" && !list[0].Video && list[1].Game == "Área de trabalho" && list[1].Video && !list[1].Replay, "pasta: mais recentes, jogo pela subpasta e limite");
            check(RadeonRelive.Recent(root, 9).Exists(c => c.Replay && c.Game == "Jogo A") && !RadeonRelive.Recent(root, 9).Exists(c => c.Path.EndsWith(".txt")), "repeticao marcada; outros arquivos ignorados");
            check(RadeonRelive.Recent(Path.Combine(root, "ausente"), 3).Count == 0, "pasta ausente: lista vazia");
        }
        static TimeSpan? Mp4(int version, uint scale, ulong duration)
        {
            var mvhd = new List<byte> { (byte)version, 0, 0, 0 };
            mvhd.AddRange(new byte[version == 1 ? 16 : 8]); mvhd.AddRange(BigEndian(scale, 4)); mvhd.AddRange(BigEndian(duration, version == 1 ? 8 : 4)); mvhd.AddRange(new byte[80]);
            var file = new List<byte>(Box("ftyp", new byte[8])); file.AddRange(Box("mdat", new byte[32])); file.AddRange(Box("moov", Box("mvhd", mvhd.ToArray())));
            return RadeonRelive.Mp4Duration(new MemoryStream(file.ToArray()));
        }
        static byte[] Box(string type, byte[] payload)
        {
            var box = new List<byte>(BigEndian((ulong)(payload.Length + 8), 4)); box.AddRange(Encoding.ASCII.GetBytes(type)); box.AddRange(payload); return box.ToArray();
        }
        static byte[] BigEndian(ulong value, int count) { var b = new byte[count]; for (int i = count - 1; i >= 0; i--) { b[i] = (byte)value; value >>= 8; } return b; }
    }
}

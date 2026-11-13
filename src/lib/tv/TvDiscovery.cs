// Descoberta local apenas para recuperar IP; cada candidato ainda exige MAC e DUID no transporte.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace XGBP.Tv
{
    public static class TvDiscovery
    {
        public static async Task<TvNetworkState> CheckAsync(string ip, string mac, string duid, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (!TvProbe.IsPrivateIPv4(ip)) return new TvNetworkState { Status = TvNetwork.NotConfigured };
            bool open = false;
            using (var tcp = new TcpClient(AddressFamily.InterNetwork))
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            using (timeout.Token.Register(tcp.Close))
            {
                timeout.CancelAfter(1000);
                try { await tcp.ConnectAsync(IPAddress.Parse(ip), 8001).ConfigureAwait(false); open = true; }
                catch (SocketException) { ct.ThrowIfCancellationRequested(); }
                catch (ObjectDisposedException) { ct.ThrowIfCancellationRequested(); }
            }
            if (!open) return TvProbe.Evaluate(false, null, mac, duid);
            var request = (HttpWebRequest)WebRequest.Create("http://" + ip + ":8001/api/v2/");
            request.Proxy = null; request.AllowAutoRedirect = false; request.KeepAlive = false; request.Method = "GET";
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            using (timeout.Token.Register(request.Abort))
            {
                timeout.CancelAfter(2000);
                try
                {
                    using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                    using (var stream = response.GetResponseStream())
                    {
                        if (response.StatusCode != HttpStatusCode.OK || response.ContentLength > 16384) return TvProbe.Evaluate(true, null, mac, duid);
                        byte[] data = new byte[16385]; int count = 0, n;
                    while (count < data.Length && (n = await stream.ReadAsync(data, count, data.Length - count,
                        timeout.Token).ConfigureAwait(false)) > 0) count += n;
                        ct.ThrowIfCancellationRequested();
                        return TvProbe.Evaluate(true, count > 16384 ? null : Encoding.UTF8.GetString(data, 0, count), mac, duid);
                    }
                }
                catch (WebException) { ct.ThrowIfCancellationRequested(); return TvProbe.Evaluate(true, null, mac, duid); }
                catch (IOException) { ct.ThrowIfCancellationRequested(); return TvProbe.Evaluate(true, null, mac, duid); }
            }
        }
        public static List<string> NeighborCandidates(string output, string mac)
        {
            var result = new HashSet<string>(); var expected = TvWake.ParseMac(mac);
            if (expected == null || output == null || output.Length > 65536) return result.ToList();
            foreach (string line in output.Split('\n'))
            {
                var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || !TvProbe.IsPrivateIPv4(parts[0])) continue;
                var actual = TvWake.ParseMac(parts[1]);
                if (actual != null && actual.SequenceEqual(expected)) result.Add(parts[0]);
                if (result.Count == 32) break;
            }
            return result.ToList();
        }
        public static async Task<List<string>> CandidatesAsync(string mac, CancellationToken ct)
        {
            var result = new HashSet<string>();
            using (var window = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                window.CancelAfter(2000);
                var tasks = new List<Task>();
                foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    foreach (var address in nic.GetIPProperties().UnicastAddresses)
                    {
                        var local = address.Address;
                        if (!TvProbe.IsPrivateIPv4(local.ToString())) continue;
                        tasks.Add(SsdpAsync(local, result, window.Token));
                    }
                }
                await Task.WhenAll(tasks).ConfigureAwait(false); ct.ThrowIfCancellationRequested();
            }
            // Cache de vizinhos e somente leitura; o helper e encerrado se exceder o prazo.
            using (var process = new Process { StartInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "arp.exe"), "-a")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } })
            {
                if (process.Start())
                {
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    using (timeout.Token.Register(delegate { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } }))
                    {
                        timeout.CancelAfter(2000);
                        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                        await Task.WhenAll(output, error).ConfigureAwait(false); ct.ThrowIfCancellationRequested();
                        foreach (string candidate in NeighborCandidates(output.Result, mac)) if (result.Count < 32) result.Add(candidate);
                    }
                }
            }
            return result.Take(32).ToList();
        }
        static async Task SsdpAsync(IPAddress local, HashSet<string> result, CancellationToken ct)
        {
            using (var udp = new UdpClient(new IPEndPoint(local, 0)))
            using (ct.Register(udp.Close))
            {
                var request = Encoding.ASCII.GetBytes("M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 1\r\nST: urn:dial" +
                    "-multiscreen-org:service:dial:1\r\n\r\n");
                try
                {
                    await udp.SendAsync(request, request.Length, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900)).ConfigureAwait(false);
                    while (!ct.IsCancellationRequested)
                    {
                        var response = await udp.ReceiveAsync().ConfigureAwait(false);
                        if (response.Buffer.Length > 16384) continue;
                        string text = Encoding.ASCII.GetString(response.Buffer);
                        if (text.IndexOf("Samsung", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        var match = Regex.Match(text, @"(?im)^LOCATION:\s*http://([0-9.]+):", RegexOptions.None, TimeSpan.FromMilliseconds(100));
                        string ip = match.Success ? match.Groups[1].Value : response.RemoteEndPoint.Address.ToString();
                        if (TvProbe.IsPrivateIPv4(ip) && TvWake.Choose(IPAddress.Parse(ip)) != null)
                            lock (result) { if (result.Count < 32) result.Add(ip); }
                    }
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
            }
        }
    }
}

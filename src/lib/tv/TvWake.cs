// WoL pela sub-rede, nunca pela rota preferencial; pontos de substituicao impedem UDP real nos testes.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace XGBP.Tv
{
    public sealed class TvEgress
    {
        public readonly IPAddress Local, Broadcast;
        public TvEgress(IPAddress local, IPAddress broadcast) { Local = local; Broadcast = broadcast; }
    }
    public static class TvWake
    {
        public static byte[] ParseMac(string mac)
        {
            var bytes = TvProbe.ParseMac(mac == null ? null : mac.Replace(".", ""));
            return bytes == null || bytes.All(b => b == 0) || bytes.All(b => b == 255) || (bytes[0] & 1) != 0 ? null : bytes;
        }
        public static byte[] Magic(byte[] mac)
        {
            if (mac == null || mac.Length != 6) throw new ArgumentException("MAC exige 6 bytes.");
            var bytes = new byte[102]; for (int i = 0; i < 6; i++) bytes[i] = 255;
            for (int i = 0; i < 16; i++) Buffer.BlockCopy(mac, 0, bytes, 6 + i * 6, 6);
            return bytes;
        }
        public static bool SameSubnet(IPAddress local, IPAddress target, IPAddress mask)
        {
            if (local == null || target == null || mask == null || local.AddressFamily != AddressFamily.InterNetwork ||
                target.AddressFamily != AddressFamily.InterNetwork || mask.AddressFamily != AddressFamily.InterNetwork) return false;
            var a = local.GetAddressBytes(); var b = target.GetAddressBytes(); var m = mask.GetAddressBytes();
            if (m.All(x => x == 0)) return false;
            for (int i = 0; i < 4; i++) if ((a[i] & m[i]) != (b[i] & m[i])) return false;
            return true;
        }
        public static TvEgress Choose(IPAddress target)
        {
            if (target == null || !TvProbe.IsPrivateIPv4(target.ToString())) return null;
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces().OrderBy(n => n.Id, StringComparer.Ordinal))
                {
                    if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        nic.NetworkInterfaceType ==
                        NetworkInterfaceType.Tunnel) continue;
                    foreach (var u in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (!SameSubnet(u.Address, target, u.IPv4Mask)) continue;
                        var ip = u.Address.GetAddressBytes(); var mask = u.IPv4Mask.GetAddressBytes();
                        return new TvEgress(u.Address, new IPAddress(Enumerable.Range(0, 4).Select(i => (byte)(ip[i] | ~mask[i])).ToArray()));
                    }
                }
            }
            catch (NetworkInformationException) { }
            return null;
        }
        static async Task SendAsync(TvEgress egress, IPEndPoint target, byte[] packet, CancellationToken ct)
        {
            using (var udp = new UdpClient(new IPEndPoint(egress.Local, 0)))
            using (ct.Register(udp.Close))
            { udp.EnableBroadcast = true; await udp.SendAsync(packet, packet.Length, target).ConfigureAwait(false); }
        }
        public static async Task<WakeResult> BurstAsync(TvCoreConfig config, ITvClock clock, CancellationToken ct,
            Func<IPAddress, TvEgress> choose = null, Func<TvEgress, IPEndPoint, byte[], CancellationToken, Task> send = null)
        {
            config.Validate(); ct.ThrowIfCancellationRequested();
            IPAddress target;
            if (!IPAddress.TryParse(config.Ip, out target) || !TvProbe.IsPrivateIPv4(config.Ip)) return new WakeResult(0, false,
                "Sem endereço privado para selecionar a sub-rede.");
            choose = choose ?? Choose; send = send ?? SendAsync;
            var deadline = new TvDeadline(clock, TimeSpan.FromSeconds(10));
            TvEgress egress;
            while ((egress = choose(target)) == null && !deadline.Expired)
                await clock.Delay(deadline.UpTo(0.5), ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (egress == null) return new WakeResult(0, false, "Sem interface na sub-rede da TV; nenhum pacote enviado.");
            var targets = new HashSet<IPAddress> { egress.Broadcast, IPAddress.Broadcast, target };
            byte[] packet = Magic(ParseMac(config.Mac)); int packets = 0;
            foreach (int delay in config.BurstDelaysMs)
            {
                await clock.Delay(TimeSpan.FromMilliseconds(delay), ct).ConfigureAwait(false);
                foreach (var destination in targets) foreach (int port in new[] { 9, 7 })
                {
                    ct.ThrowIfCancellationRequested();
                    try { await send(egress, new IPEndPoint(destination, port), packet, ct).ConfigureAwait(false); packets++; }
                    catch (SocketException) { ct.ThrowIfCancellationRequested(); }
                }
            }
            return new WakeResult(packets, true, "WoL: " + packets + " pacotes pela interface da sub-rede; energia não confirmada.");
        }
    }
}

// Estado da rede para a faixa de status: tipo da conexao, acesso a internet e barras do sinal.
// So leitura, pela API publica Windows.Networking.Connectivity. O SSID nao e lido: no Windows 11 24H2
// a leitura dele passou a exigir a permissao de localizacao.
using System;
using Windows.Networking.Connectivity;

namespace XGBP.Network
{
    public sealed class NetworkState
    {
        public string Kind = "none";   // "ethernet", "wifi", "celular", "none" ou "error"
        public bool Internet;          // NetworkConnectivityLevel.InternetAccess
        public int Bars = -1;          // 0..5 no Wi-Fi e no celular; -1 sem informacao
        public uint InterfaceType;     // IANA: 6 Ethernet, 71 Wi-Fi
        public string Error;

        public string Describe()
        {
            if (Kind == "error") return "rede ilegivel: " + Error;
            if (Kind == "none") return "sem conexao";
            return Kind + (Internet ? " com internet" : " sem internet") + (Bars >= 0 ? " | sinal " + Bars + "/5" : "") + " | IANA " + InterfaceType;
        }
    }

    public static class NetworkStatus
    {
        public static NetworkState Read()
        {
            var state = new NetworkState();
            try
            {
                // O perfil de internet e o que o Windows usa na bandeja; sem ele, vale uma rede so local.
                var profile = NetworkInformation.GetInternetConnectionProfile();
                if (profile == null || profile.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.None) profile = LocalProfile();
                if (profile == null) return state;
                state.Internet = profile.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.InternetAccess;
                state.Kind = profile.IsWlanConnectionProfile ? "wifi" : profile.IsWwanConnectionProfile ? "celular" : "ethernet";
                if (profile.NetworkAdapter != null) state.InterfaceType = profile.NetworkAdapter.IanaInterfaceType;
                if (state.Kind != "ethernet")
                {
                    byte? bars = profile.GetSignalBars();
                    if (bars.HasValue) state.Bars = bars.Value;
                }
            }
            catch (Exception ex) { state.Kind = "error"; state.Error = ex.Message; }
            return state;
        }

        static ConnectionProfile LocalProfile()
        {
            foreach (var profile in NetworkInformation.GetConnectionProfiles())
                if (profile.GetNetworkConnectivityLevel() != NetworkConnectivityLevel.None) return profile;
            return null;
        }
    }
}

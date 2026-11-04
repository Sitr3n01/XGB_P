// Radios de Wi-Fi e Bluetooth pela API publica Windows.Devices.Radios: o mesmo liga/desliga das Configuracoes rapidas.
// Leitura sempre; mudanca so por escolha do usuario.
using System;
using System.Collections.Generic;
using System.Threading;
using Windows.Devices.Radios;
using Windows.Foundation;

namespace XGBP.Network
{
    public sealed class RadioInfo
    {
        public bool Present, On, Disabled;   // Disabled: chave fisica, politica ou adaptador desativado
        public string Name = "";
        public string Describe() { return !Present ? "ausente" : Disabled ? "desativado no sistema" : On ? "ligado" : "desligado"; }
    }

    public static class Radios
    {
        public static RadioInfo Read(string kind)
        {
            var info = new RadioInfo();
            foreach (var radio in Wait(Radio.GetRadiosAsync(), "consultar os rádios"))
            {
                if (radio.Kind != Kind(kind)) continue;
                info.Present = true; info.Name = radio.Name ?? "";
                info.Disabled = radio.State == RadioState.Disabled;
                info.On = radio.State == RadioState.On;
                if (info.On) break;
            }
            return info;
        }
        // Permissao de controlar radios (privacidade do Windows). So consulta; nao muda o estado.
        public static string Access()
        {
            var status = Wait(Radio.RequestAccessAsync(), "consultar a permissão de rádios");
            return status == RadioAccessStatus.Allowed ? "permitido" : status == RadioAccessStatus.DeniedByUser ? "negado pelo usuário" :
                status == RadioAccessStatus.DeniedBySystem ? "negado pelo sistema" : "indefinido";
        }
        public static void Set(string kind, bool on)
        {
            if (Wait(Radio.RequestAccessAsync(), "pedir acesso aos rádios") != RadioAccessStatus.Allowed)
                throw new InvalidOperationException("O Windows não permite que apps controlem os rádios. Ative em Privacidade > Rádios.");
            bool found = false;
            foreach (var radio in Wait(Radio.GetRadiosAsync(), "consultar os rádios"))
            {
                if (radio.Kind != Kind(kind)) continue;
                found = true;
                if (radio.State == RadioState.Disabled) throw new InvalidOperationException(Label(kind) + " está desativado no sistema ou pela chave do aparelho.");
                var result = Wait(radio.SetStateAsync(on ? RadioState.On : RadioState.Off), "mudar o rádio");
                if (result != RadioAccessStatus.Allowed) throw new InvalidOperationException("O Windows recusou " + (on ? "ligar " : "desligar ") + Label(kind) + ".");
            }
            if (!found) throw new InvalidOperationException("Nenhum adaptador de " + Label(kind) + " encontrado.");
        }
        public static string Label(string kind) { return kind == "bluetooth" ? "o Bluetooth" : "o Wi-Fi"; }
        static RadioKind Kind(string kind) { return kind == "bluetooth" ? RadioKind.Bluetooth : RadioKind.WiFi; }

        // Espera de operacao WinRT sem System.Runtime.WindowsRuntime (mesmo padrao do player de midia).
        internal static T Wait<T>(IAsyncOperation<T> operation, string what)
        {
            var done = new ManualResetEvent(false);
            operation.Completed = delegate { try { done.Set(); } catch (ObjectDisposedException) { } };
            try
            {
                if (!done.WaitOne(5000)) { operation.Cancel(); throw new TimeoutException("O Windows não respondeu ao " + what + "."); }
                if (operation.Status != AsyncStatus.Completed)
                    throw new InvalidOperationException("Não foi possível " + what + (operation.ErrorCode == null ? "." : ": " + operation.ErrorCode.Message));
                return operation.GetResults();
            }
            finally { done.Close(); }
        }
    }
}

// Entrega assincrona do primeiro plano e foco de entrada (ADR 0115), com substitutos: nenhuma janela real muda.
using System;
using XGBP.Desktop;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        void VerifyInputFocus(Action<bool, string> check)
        {
            IntPtr target = new IntPtr(123), front = new IntPtr(456);
            int requests = 0, connections = 0, waits = 0;
            Func<bool> accepted = delegate { requests++; return true; };
            Func<string> connect = delegate { connections++; return null; };
            string report = DesktopWindows.TransferAppForeground(target, delegate { return front; }, accepted, connect, delegate { }, delegate { },
                delegate { if (++waits == 3) front = target; });
            check(requests == 1 && connections == 0 && waits == 3 && report.Contains("espera=15 ms") && !report.Contains("comFila"),
                "pedido aceito e entregue logo depois nao liga a fila");
            requests = connections = waits = 0; front = new IntPtr(456);
            report = DesktopWindows.TransferAppForeground(target, delegate { return front; }, delegate { requests++; return false; }, connect,
                delegate { }, delegate { }, delegate { waits++; });
            check(waits == 0 && requests == 2 && connections == 1 && report.Contains("direto=False"), "pedido recusado nao espera e segue para a fila");
            requests = connections = waits = 0;
            DesktopWindows.TransferAppForeground(target, delegate { return front; }, accepted, connect, delegate { }, delegate { }, delegate { waits++; });
            check(waits == 30 && connections == 1, "espera limitada a 150 ms antes da fila");

            var oldRead = inputFocusRead; var oldRestore = inputFocusRestore; var oldFront = foregroundRead;
            try
            {
                if (window.IsVisible) Hide();
                var item = new DesktopWindow { Handle = target, ProcessId = 9, Title = "Jogo", AppName = "Jogo" };
                int restored = 0; bool? ready = true; front = target;
                foregroundRead = delegate { return front; };
                inputFocusRead = delegate { return ready; };
                inputFocusRestore = delegate { restored++; ready = true; return "devolvido"; };
                WatchInputFocus(item, "teste"); CheckInputFocus(); check(restored == 0, "app com foco de entrada nao e tocado");
                ready = false; WatchInputFocus(item, "teste"); CheckInputFocus(); check(restored == 1, "app na frente sem foco de entrada o recebe de volta");
                ready = false; CheckInputFocus(); check(restored == 1, "uma conferencia por entrega");
                ready = null; WatchInputFocus(item, "teste"); CheckInputFocus(); check(restored == 1, "leitura indisponivel nao altera nada");
                ready = false; front = new IntPtr(789); WatchInputFocus(item, "teste"); CheckInputFocus();
                check(restored == 1, "outra janela na frente e respeitada");
                front = target; window.Show(); WatchInputFocus(item, "teste"); CheckInputFocus(); check(restored == 1, "barra reaberta cancela a conferencia");
            }
            finally { window.Hide(); inputFocusRead = oldRead; inputFocusRestore = oldRestore; foregroundRead = oldFront; inputFocusTarget = null; }
        }
    }
}

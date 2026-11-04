// Luz noturna do Windows (filtro amarelo). Nao ha API publica: o estado fica no CloudStore do usuario (HKCU),
// num blob que o Windows observa. Leitura sempre; escrita so por escolha do usuario e so com o formato conhecido.
using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace XGBP.Display
{
    public static class NightLight
    {
        const string StateKey = @"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\" +
            @"default$windows.data.bluelightreduction.bluelightreductionstate\windows.data.bluelightreduction.bluelightreductionstate";
        // Blob observado no Windows 10.0.26300 (43 bytes ligada, 41 desligada): cabecalho, carimbo em LEB128 (5 bytes,
        // segundos Unix) em 10, 2A 2B 0E, tamanho do conteudo em 18 (0x15 ligada, 0x13 desligada), conteudo a partir
        // de 19 (43 42 01 00 e, so com a luz ligada, 10 00 em 23) e tres zeros no fim.
        static readonly byte[] Head = { 0x43, 0x42, 0x01, 0x00, 0x0A, 0x02, 0x01, 0x00, 0x2A, 0x06 };
        static readonly byte[] Middle = { 0x2A, 0x2B, 0x0E }, Inner = { 0x43, 0x42, 0x01, 0x00 };

        // null: indisponivel (chave ausente, Luz noturna nunca configurada) ou formato desconhecido.
        public static bool? Read() { return Parse(ReadData()); }
        public static void Set(bool on)
        {
            byte[] data = ReadData();
            if (Parse(data) == null) throw new InvalidOperationException("A Luz noturna não pode ser alterada por aqui neste PC.");
            byte[] changed = Change(data, on, (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
            if (changed == data) return;
            using (var key = Registry.CurrentUser.OpenSubKey(StateKey, true))
            {
                if (key == null) throw new InvalidOperationException("A Luz noturna não está disponível neste PC.");
                key.SetValue("Data", changed, RegistryValueKind.Binary);
            }
        }

        public static bool? Parse(byte[] data)
        {
            if (data == null || data.Length < 27 || !Match(data, 0, Head) || !Match(data, 15, Middle) || !Match(data, 19, Inner)) return null;
            for (int i = 10; i < 15; i++) if (((data[i] & 0x80) != 0) != (i < 14)) return null;
            if (data[18] != data.Length - 22 || data[data.Length - 3] != 0 || data[data.Length - 2] != 0 || data[data.Length - 1] != 0) return null;
            bool flag = data[23] == 0x10 && data[24] == 0x00;
            if (data[18] == 0x15 && flag) return true;
            if (data[18] == 0x13 && !flag) return false;
            return null;
        }
        // Liga ou desliga no blob: insere ou remove 10 00, ajusta o tamanho e grava um carimbo mais novo que o anterior.
        public static byte[] Change(byte[] data, bool on, long unixSeconds)
        {
            bool? current = Parse(data);
            if (current == null) throw new InvalidOperationException("Formato da Luz noturna desconhecido.");
            if (current.Value == on) return data;
            var result = new List<byte>(data);
            if (on) result.InsertRange(23, new byte[] { 0x10, 0x00 }); else result.RemoveRange(23, 2);
            result[18] = (byte)(on ? 0x15 : 0x13);
            long stamp = Math.Max(unixSeconds, Stamp(data) + 1);
            for (int i = 0; i < 5; i++) { result[10 + i] = (byte)((int)(stamp & 0x7F) | (i < 4 ? 0x80 : 0)); stamp >>= 7; }
            return result.ToArray();
        }
        public static long Stamp(byte[] data)
        {
            long value = 0;
            for (int i = 14; i >= 10; i--) value = value * 128 + (data[i] & 0x7F);
            return value;
        }

        public static byte[] ReadData()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(StateKey)) return key == null ? null : key.GetValue("Data") as byte[];
        }
        static bool Match(byte[] data, int at, byte[] expected)
        {
            for (int i = 0; i < expected.Length; i++) if (data[at + i] != expected[i]) return false;
            return true;
        }
    }
}

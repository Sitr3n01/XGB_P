// Observacao limitada do contexto escolhido pela Steam. Nao altera perfil/configuracao.
using System;

namespace XGBP.Games
{
    public static class SteamGameContext
    {
        public static string AppId(string gameKey)
        {
            if (string.IsNullOrEmpty(gameKey)) return null;
            string[] parts = gameKey.Split(':');
            if (parts.Length != 3 || parts[0] != "steam" && parts[0] != "shortcut") return null;
            uint id;
            return uint.TryParse(parts[2], out id) && id > 0 ? id.ToString() : null;
        }
        // Aceita game/window/URL forcing; ignora o Guia momentaneo. IDs de atalhos podem ser signed.
        public static string Last(string text)
        {
            if (text == null) return null;
            int complete = text.LastIndexOf('\n');
            if (complete < 0) return null;
            string found = null;
            foreach (string line in text.Substring(0, complete).Split('\n'))
            {
                int at = line.IndexOf("OnFocusWindowChanged ", StringComparison.Ordinal);
                if (at < 0) continue;
                int app = line.IndexOf("AppID ", at, StringComparison.Ordinal);
                string value;
                if (app >= 0) value = line.Substring(app + 6).Trim();
                else
                {
                    if (line.IndexOf("URL Forcing to window type:", at, StringComparison.Ordinal) < 0) continue;
                    int comma = line.LastIndexOf(',');
                    if (comma < at) continue;
                    value = line.Substring(comma + 1).Trim();
                }
                int end = value.StartsWith("-", StringComparison.Ordinal) ? 1 : 0;
                while (end < value.Length && char.IsDigit(value[end])) end++;
                long id;
                if (!long.TryParse(value.Substring(0, end), out id) || id < int.MinValue || id > uint.MaxValue) continue;
                string normalized = unchecked((uint)id).ToString();
                if (normalized != SteamInput.GuideAppId) found = normalized;
            }
            return found;
        }
    }
}

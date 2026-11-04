// Identidade do foco acompanha o jogo quando ele muda de posicao no grupo Voltar ao jogo.
using System;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        static string GameHomeFocusTag(string tag, string before, string current)
        {
            if (tag == null || before == current) return tag;
            if (tag == "resume" && before != null) return "recent-" + before;
            if (current != null && tag == "recent-" + current) return "resume";
            foreach (string action in new[] { "pause-", "close-" })
            {
                if (before != null && tag == action + before) return "recent-" + tag;
                if (current != null && tag == "recent-" + action + current) return action + current;
            }
            return tag;
        }
    }
}

// TV Samsung nas Configuracoes (ADR 0110, fase 2, porte do Samsung TV Orchestrator): estado da tela, da rede e do
// Orchestrator, somente leitura. Nenhum comando vai a TV nesta fase. A leitura (lib/tv) roda em worker e vale por 30 s;
// apertar a linha le de novo.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using XGBP.Tv;

namespace XGBP.Design
{
    sealed partial class Preview
    {
        public void VerifyTvLive()
        {
            var state = SafeTvRead(TvStatusReader.Read);
            var text = new StringBuilder("TV Samsung (ADR 0110, fase 2): " + state.Describe() + ".\r\n");
            text.Append("Linha nas Configuracoes: " + TvRowText(state) + ".\r\n");
            foreach (var note in TvNotes(state)) text.Append("Nota: " + note + "\r\n");
            text.Append("Leitura em " + state.ElapsedMs + " ms.\r\n");
            // A identidade de pacote nao revela a virtualizacao (07/11: GetCurrentPackageFullName sem pacote e, mesmo assim, a copia de 08/09).
            text.Append("Configuracao " + (state.Setup.Found ? (state.Setup.Schema == 0 ? "sem SchemaVersion (gravada antes da v2)" : "v" +
                state.Setup.Schema) :
                "ausente") +
". Iniciado por um app empacotado (agente), %LOCALAPPDATA% pode ser a copia virtualizada; a leitura r" +"eal vale com o XGB_P aberto pelo usuario.\r\n");
            text.Append("Diagnostico somente leitura: CCD, configuracao do Orchestrator, uma conexao TCP e um GET /api/v2/ no" +
                " IP salvo; nenhum comando a TV, nada escrito.\r\n");
            File.WriteAllText(Path.Combine(Output, "tv-real.txt"), text.ToString(), Encoding.UTF8);
        }
    }
}

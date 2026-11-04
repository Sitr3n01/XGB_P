@AGENTS.md

## Notas específicas do Claude Code

- **Instruções desta máquina** (caminhos locais, estado do PC, onde ficam os backups e o arquivo privado) vão num `CLAUDE.local.md`, ignorado pelo Git. Nunca as escreva aqui.
- **Tarefas agendadas como SYSTEM**, certificados e HidHide são alterações de segurança: entregue o comando ao usuário, mesmo que ele autorize o agente.
- **Fim de linha:** no Git Bash, `grep -c $'\r$'` conta todas as linhas mesmo sem CR. Meça CRLF pelos bytes (Python ou `[IO.File]::ReadAllBytes`).
- **Antes de pedir um teste físico ao usuário,** deixe as ferramentas de leitura (`bin\DualSenseReader.exe`, `bin\ModoXboxMonitor.exe`, compiladas por `scripts\compilar-ferramentas.ps1`) rodando em segundo plano e leia os logs depois.

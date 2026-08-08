# ADR-0005 — Ratificação da stack de referência
Data: 2026-08-08 · Status: **aceito** · Autor: Arquiteto de Software Principal (decisão delegada por Frederico em 2026-08-08)

## Contexto

A Seção 3 do prompt mestre define uma **stack de referência**, com a observação de que qualquer
alteração exige ADR. Até aqui ela nunca foi formalmente ratificada — era proposta. Este ADR a ratifica
ou a corrige, item a item, antes que `ARQUITETURA.md` a assuma como dada.

## Decisão

**A stack de referência é ratificada integralmente**, com um gatilho de reversão registrado para o
launcher.

| Camada | Tecnologia ratificada | Observação |
|--------|----------------------|------------|
| Launcher | C# · .NET 10 · **WinUI 3 / Windows App SDK** · MSIX · SQLite · Windows Credential Manager | Ver §gatilho abaixo |
| Control Plane | ASP.NET Core (REST + SignalR) · EF Core · **PostgreSQL** | SignalR entra de fato na V2, com o Agent |
| Agent | .NET Windows Service · WebSocket outbound | V2 |
| Painel Admin | Blazor | MVP-1 |
| Infra | Windows Server 2025 RDS · RD Gateway (V2) · FSLogix · AppLocker/WDAC · GPO | Topologia em ADR-0002 |

### Gatilho de reversão do launcher (a parte que importa)

WinUI 3 é a escolha moderna e alinhada ao MSIX, mas o launcher precisa de três coisas que
historicamente são atrito nessa pilha: **ícone na área de notificação** (RF-033), **ativação por
protocolo** `appbridge://` (RF-029) e **criação de atalhos no Desktop** a partir de aplicativo
empacotado (RF-030).

Fica decidido: se, durante a implementação do MVP-0, essas três funções custarem em conjunto **mais de
5 dias de trabalho** ou exigirem dependência de terceiros não mantida, o launcher **migra para WPF em
.NET 10**, via novo ADR que substitui este. WPF é maduro, tem suporte de primeira classe a ícone de
notificação e continua compatível com MSIX, SQLite e Credential Manager. A interface do launcher é
simples (lista de aplicativos, estado de conexão, indicador de latência) — não há nada nela que
justifique pagar caro por WinUI 3.

Registrar o gatilho agora evita a armadilha clássica: descobrir o atrito na semana 6 e insistir na
escolha por já ter investido nela.

## Alternativas consideradas

| Item | Alternativa | Por que não |
|------|-------------|-------------|
| Launcher | **WPF desde já** | Menor risco, mas abre mão do alinhamento com MSIX e Windows App SDK sem necessidade comprovada. Fica como plano B formalizado, não como escolha inicial. |
| Launcher | Avalonia / MAUI / Electron | Multiplataforma que não precisamos (NO-06), com custo de empacotamento e integração Windows maior. Electron ainda traria peso injustificável para um catálogo de aplicativos. |
| Banco | **SQL Server** | Integraria melhor com o ecossistema Windows do cliente, mas adiciona custo de licença por servidor — direto contra a meta PRE-05 e contra o Caminho A, em que o cliente paga a infraestrutura. |
| Banco | SQLite no servidor | Insuficiente para concorrência do Control Plane a partir do piloto. |
| Painel | React/Angular com API separada | Ecossistema mais rico, mas obriga a manter duas linguagens e dois processos de build para um painel administrativo interno. Blazor mantém tudo em C#, o que importa muito num projeto de uma pessoa (R-006). |
| Control Plane | Minimal APIs vs. controllers | Detalhe de implementação, não de arquitetura. Fica a critério da implementação. |

## Consequências

**Positivas**
- Uma linguagem (C#) e um ecossistema (.NET 10) dos quatro componentes ponta a ponta — decisivo para
  produtividade de um desenvolvedor solo (R-006).
- PostgreSQL sem custo de licença preserva a margem do Caminho B (PRE-05) e não impõe licença ao
  cliente do Caminho A.
- EF Core viabiliza o filtro global de tenant decidido em ADR-0004, que é o mecanismo de isolamento.

**Negativas**
- PostgreSQL em ambiente Windows Server é operado por equipes que normalmente conhecem SQL Server;
  backup, monitoramento e ajuste ficam por conta do provedor (RNF-033).
- WinUI 3 tem base de conhecimento pública menor que WPF; problemas de nicho custam mais tempo.
- Blazor no painel implica escolher entre Server (dependência de conexão contínua) e WebAssembly
  (carga inicial maior) — decisão adiada para o MVP-1, quando o painel existir.

**Riscos**
- **Risco de calendário concentrado no launcher.** O gatilho de reversão acima é a mitigação.
- `PREMISSA:` (PRE-17) MSIX instalado por usuário, sem privilégio administrativo, consegue registrar o
  protocolo `appbridge://` e criar atalhos no Desktop. O registro de protocolo por manifesto é
  suportado; a criação de atalho no Desktop precisa de validação prática antes de ser dada como certa.
  Se falhar, RF-030 muda de forma e o plano de implantação muda junto.

## Requisitos relacionados

RF-005, RF-014, RF-029, RF-030, RF-033, RF-034, RF-035, RF-043 · RNF-035, RNF-036, RNF-044, RNF-051,
RNF-052 · Origem: §3 prompt, RP-07, R-006

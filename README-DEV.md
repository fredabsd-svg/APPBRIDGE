# 🚀 AppBridge — Implementação do MVP-0a

**Status:** 🔨 Desenvolvimento iniciado (E-00 · Setup)  
**Fase:** MVP-0a (meados de out/2026)  
**Repositório:** https://github.com/fredabsd-svg/APPBRIDGE

---

## O que é AppBridge?

Plataforma de distribuição de aplicativos Windows remotos construída sobre **Windows Server RDS/RemoteApp**. Aplicativos instalados em servidores aparecem no PC do usuário como se fossem locais — atalhos no Desktop, Menu Iniciar, autenticação central, permissões e auditoria — **sem entregar o desktop do servidor**.

**Lema:** *"Instale uma vez. Publique para todos."*

---

## 📋 Componentes

| Componente | Tecnologia | Status |
|-----------|-----------|--------|
| **Launcher** (cliente) | C# · .NET 10 · WinUI 3 | Planejado |
| **Control Plane** (API) | ASP.NET Core · PostgreSQL | Em desenvolvimento (E-02) |
| **Agent** (service) | .NET Windows Service | Planejado |
| **Painel Admin** (web) | Blazor | Planejado (E-05) |

---

## 📚 Documentação

### Começar Aqui
1. **[SETUP-DEV.md](docs/SETUP-DEV.md)** — Configure seu ambiente local
2. **[BACKLOG_MVP0A_PRIORIZADO.md](docs/BACKLOG_MVP0A_PRIORIZADO.md)** — Épicos e tarefas priorizadas
3. **[ANALISE_BUGS_E_MELHORIAS.md](docs/ANALISE_BUGS_E_MELHORIAS.md)** — Riscos e gaps técnicos

### Arquitetura
- **[ARQUITETURA.md](docs/ARQUITETURA.md)** — Modelo C4, diagramas de sequência
- **[MODELO-DE-DADOS.md](docs/MODELO-DE-DADOS.md)** — Entidades e relacionamentos
- **[API.md](docs/API.md)** — Contrato de endpoints

### Visão de Produto
- **[VISAO.md](docs/VISAO.md)** — Problema, público, proposta de valor
- **[REQUISITOS.md](docs/REQUISITOS.md)** — RFs e RNFs priorizados
- **[SEGURANCA.md](docs/SEGURANCA.md)** — Ameaças (STRIDE) e controles

### Decisões Técnicas
- **[docs/adr/](docs/adr/)** — 14 Architecture Decision Records aceitos

---

## 🎯 Épicos (MVP-0a)

| Épico | Status | Pontos | Deadline |
|-------|--------|--------|----------|
| **E-00** · Setup + Decisões | 🔨 Em progresso | 0 | 15/ago |
| **E-01a** · Medições | 🟡 Planned | 8 | 22/ago–05/set |
| **E-02** · Fundação (DB, auth, trilha) | 🟡 Planned | 21 | 22/ago–26/set |
| **E-03** · Closures (gaps arquiteturais) | 🟡 Planned | 12 | 26/set–17/out |
| **E-04** · Features (catálogo, RDP, launch) | 🟡 Planned | 18 | 17/out–05/dez |
| **E-05** · Launch (atalhos, UI, testes) | 🟡 Planned | 20 | 05/dez–31/dez |

**Total:** ~96 pontos (~12 semanas solo @ 8 pts/semana)

---

## 🚀 Começar a Codificar

### 1. Clone e Configure
```bash
git clone https://github.com/fredabsd-svg/APPBRIDGE.git
cd APPBRIDGE
cat docs/SETUP-DEV.md
```

### 2. Veja as Issues
- [Issues de E-00 (Setup)](#3–7)
- [Todas as issues](https://github.com/fredabsd-svg/APPBRIDGE/issues?q=label%3Aepic-E)

### 3. Comece uma Tarefa
```bash
# Exemplo: começar T-02.1 (Setup DB)
git checkout -b feat/T-02.1-setup-db main
# ... desenvolva ...
git push -u origin feat/T-02.1-setup-db
```

### 4. Abra um Pull Request
Descreva a mudança. Referencie a issue.

---

## 🔐 Segurança

- 🚫 **Nunca** commit secrets (`.pfx`, `.env`, senhas)
- ✅ Use `dotnet user-secrets` para desenvolvimento
- ✅ Detecta-secrets rodará em cada PR
- ✅ `.gitignore` bloqueia arquivos sensíveis

Ver: **[docs/SEGURANCA.md](docs/SEGURANCA.md)**

---

## 📊 Status Atual (2026-08-09)

**Fase:** Design concluído → **Implementação iniciada**

- ✅ 7 entregáveis de design (VISAO, REQUISITOS, ARQUITETURA, etc.)
- ✅ 14 ADRs aceitos
- ✅ 32 issues priorizadas (E-00 a E-05)
- 🔨 Setup do repositório em progresso (E-00)
- 🟡 Próxima: Reunião com Frederico (decisões R-020, B-006, B-007)

Ver: **[docs/STATUS.md](docs/STATUS.md)**

---

## 🤝 Contribuir

### Regras (RP-01 a RP-09)
- **Idioma:** Português em docs/comentários, Inglês em código
- **Commits:** Conventional Commits (`feat:`, `fix:`, `test:`, etc.)
- **Versionamento:** SemVer independente por componente
- **Testes:** 80%+ cobertura no Control Plane + Agent

Ver: **[CLAUDE.md](CLAUDE.md)** (regras completas)

### Branch Strategy
- `main` → sempre com código pronto para produção
- `feat/T-XX.X-descricao` → feature branch para cada tarefa
- `fix/RX-descricao` → hotfix de risco crítico

---

## 📞 Suporte

- 📖 Documentação: [/docs](docs/)
- 🐛 Issues: https://github.com/fredabsd-svg/APPBRIDGE/issues
- 📋 Backlog: [BACKLOG_MVP0A_PRIORIZADO.md](docs/BACKLOG_MVP0A_PRIORIZADO.md)

---

## 📄 Licença

Privado · Frederico Assessoria Contábil

---

**Last updated:** 2026-08-09  
**MVP-0a ETA:** meados de out/2026
